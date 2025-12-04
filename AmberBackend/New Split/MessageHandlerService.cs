using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class BaseMessage { public string type; }

public class TileClickMessage : BaseMessage
{
    public string playerId;
    public int x;
    public int y;
}

// NEW: Move complete message from client
public class MoveCompleteMessage : BaseMessage
{
    public string playerId;
    public int x;
    public int y;
}

public class StateSnapshotMessage
{
    public string type { get; set; } = "state_snapshot";
    public List<EntityStateDto> entities { get; set; }
}

public class MessageHandlerService
{
    private readonly PlayerService _playerService;
    private readonly MovementService _movementService;

    private readonly Dictionary<string, Func<WebSocket, string, string, Task>> _handlers;
    private readonly Dictionary<string, Func<WebSocket, string, Task<string>>> _registrationHandlers;
    private readonly MovementWebSocketHandler _movementWsHandler;

    public MessageHandlerService(
          PlayerService playerService,
          MovementService movementService,
          MovementWebSocketHandler movementWsHandler)
    {
        _playerService = playerService;
        _movementService = movementService;
        _movementWsHandler = movementWsHandler;

        _registrationHandlers = new Dictionary<string, Func<WebSocket, string, Task<string>>>
        {
            { "register_player", HandleRegisterPlayer }
        };

        _handlers = new Dictionary<string, Func<WebSocket, string, string, Task>>
        {
            { "tile_click", HandleTileClickWrapper },
            { "state_request", HandleStateRequest },
            { "move_complete", HandleMoveComplete }  // NEW!
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

    private async Task HandleTileClickWrapper(WebSocket ws, string message, string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return;

        var click = JsonConvert.DeserializeObject<TileClickMessage>(message);
        if (click == null) return;

        var target = new TilePosition { X = click.x, Y = click.y };

        await _movementWsHandler.HandleTileClick(ws, playerId, target);
    }

    // NEW: Handle movement completion acknowledgment
    private async Task HandleMoveComplete(WebSocket ws, string message, string playerId)
    {
        if (string.IsNullOrEmpty(playerId))
        {
            Console.WriteLine("[MessageHandler] move_complete without playerId");
            return;
        }

        var msg = JsonConvert.DeserializeObject<MoveCompleteMessage>(message);
        if (msg == null) return;

        var completedCell = new TilePosition { X = msg.x, Y = msg.y };

        Console.WriteLine($"[MessageHandler] {playerId} completed move to ({msg.x}, {msg.y})");

        // Notify movement service
        _movementService.OnClientMovementComplete(playerId, completedCell);

        await Task.CompletedTask;
    }

    private async Task<string> HandleRegisterPlayer(WebSocket ws, string message)
    {
        var id = _playerService.RegisterPlayer();
        _movementService.RegisterEntity(id, new TilePosition { X = 5, Y = -5 }, speed: 4f);

        var response = new { type = "player_registered", playerId = id, x = 5, y = -5 };
        var json = JsonConvert.SerializeObject(response);
        var buf = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(buf, WebSocketMessageType.Text, true, CancellationToken.None);
        return id;
    }
}