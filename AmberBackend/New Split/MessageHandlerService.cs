using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using AmberBackend.Movement;
using Newtonsoft.Json;

public class BaseMessage { public string type; }

public class PositionUpdateMessage : BaseMessage
{
    public string playerId;
    public int x;
    public int y;
}

public class PathRequestMessage : BaseMessage
{
    public string playerId;
    public int targetX;
    public int targetY;
}

public class StateSnapshotMessage
{
    public string type { get; set; } = "state_snapshot";
    public List<EntityStateDto> entities { get; set; }
}

public class PositionCorrectionMessage
{
    public string type { get; set; } = "position_correction";
    public string playerId { get; set; }
    public int x { get; set; }
    public int y { get; set; }
    public string reason { get; set; }
}

public class MessageHandlerService
{
    private readonly PlayerService _playerService;
    private readonly MovementService _movementService;

    private readonly Dictionary<string, Func<WebSocket, string, string, Task>> _handlers;
    private readonly Dictionary<string, Func<WebSocket, string, Task<string>>> _registrationHandlers;

    public MessageHandlerService(
          PlayerService playerService,
          MovementService movementService)
    {
        _playerService = playerService;
        _movementService = movementService;

        _registrationHandlers = new Dictionary<string, Func<WebSocket, string, Task<string>>>
        {
            { "register_player", HandleRegisterPlayer }
        };

        _handlers = new Dictionary<string, Func<WebSocket, string, string, Task>>
        {
            { "position_update", HandlePositionUpdate },
            { "path_request", HandlePathRequest },
            { "state_request", HandleStateRequest }
        };
    }

    public async Task<string> HandleMessageAsync(WebSocket ws, string type, string message, string currentPlayerId)
    {
        if (_registrationHandlers.TryGetValue(type, out var reg))
            return await reg(ws, message);

        if (_handlers.TryGetValue(type, out var handler))
        {
            await handler(ws, message, currentPlayerId);
            return currentPlayerId;
        }

        Console.WriteLine($"Unknown message type: {type}");
        return currentPlayerId;
    }

    private async Task HandleStateRequest(WebSocket ws, string message, string playerId)
    {
        var snap = _movementService.GetAllEntitiesSnapshot();

        var response = new StateSnapshotMessage
        {
            type = "state_snapshot",
            entities = snap
        };

        var json = JsonConvert.SerializeObject(response);
        var buffer = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(buffer, WebSocketMessageType.Text, true, System.Threading.CancellationToken.None);
    }

    private async Task HandlePositionUpdate(WebSocket ws, string message, string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return;

        var update = JsonConvert.DeserializeObject<PositionUpdateMessage>(message);
        if (update == null) return;

        var newPosition = new TilePosition { X = update.x, Y = update.y };

        // Validate and update position
        _movementService.OnPositionUpdate(playerId, newPosition);

        await Task.CompletedTask;
    }

    private async Task HandlePathRequest(WebSocket ws, string message, string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return;

        var request = JsonConvert.DeserializeObject<PathRequestMessage>(message);
        if (request == null) return;

        var target = new TilePosition { X = request.targetX, Y = request.targetY };

        // For now, just acknowledge - client handles pathfinding
        _movementService.RequestPath(playerId, target);

        await Task.CompletedTask;
    }

    private async Task<string> HandleRegisterPlayer(WebSocket ws, string message)
    {
        var id = _playerService.RegisterPlayer();
        _movementService.RegisterEntity(id, new TilePosition { X = 5, Y = -5 }, speed: 4f);

        var response = new { type = "player_registered", playerId = id, x = 5, y = -5 };
        var json = JsonConvert.SerializeObject(response);
        var buf = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(buf, WebSocketMessageType.Text, true, System.Threading.CancellationToken.None);
        return id;
    }
}