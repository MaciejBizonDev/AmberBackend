using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class MessageHandlerService
{
    private readonly PlayerService _playerService;
    private readonly MovementService _movementService;
    private readonly MovementWebSocketHandler _movementWsHandler;

    // Registration-only handlers: (ws, payloadJson) -> returns new playerId (or null)
    private readonly Dictionary<string, Func<WebSocket, string, Task<string>>> _registrationHandlers;

    // Normal handlers: (ws, payloadJson, currentPlayerId) -> Task
    private readonly Dictionary<string, Func<WebSocket, string, string, Task>> _handlers;

    public MessageHandlerService(PlayerService playerService,
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
            { "tile_click", HandleTileClickWrapper }
            // add more message types here
        };
    }

    // Entry point used by your WebSocketServer loop
    public async Task<string> HandleMessageAsync(WebSocket ws, string type, string message, string currentPlayerId)
    {
        if (_registrationHandlers.TryGetValue(type, out var regHandler))
        {
            var newId = await regHandler(ws, message);
            return newId ?? currentPlayerId;
        }

        if (_handlers.TryGetValue(type, out var handler))
        {
            await handler(ws, message, currentPlayerId);
            return currentPlayerId;
        }

        Console.WriteLine($"Unknown message type: {type}");
        return currentPlayerId;
    }

    // Wrapper: parse incoming JSON, route to movement WebSocket handler
    private async Task HandleTileClickWrapper(WebSocket ws, string message, string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return;

        var click = JsonConvert.DeserializeObject<TileClickMessage>(message);
        if (click == null) return;

        var target = new TilePosition { X = click.x, Y = click.y };
        await _movementWsHandler.HandleTileClick(ws, playerId, target);
    }

    // Registration: create id, register movement state, reply to client
    private async Task<string> HandleRegisterPlayer(WebSocket ws, string message)
    {
        string newPlayerId = _playerService.RegisterPlayer();

        var spawn = new TilePosition { X = 5, Y = -5 };
        _movementService.RegisterEntity(newPlayerId, spawn, speed: 1f);

        var response = new
        {
            type = "player_registered",
            playerId = newPlayerId,
            x = spawn.X,
            y = spawn.Y
        };

        var json = JsonConvert.SerializeObject(response);
        var buffer = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(buffer, WebSocketMessageType.Text, true, System.Threading.CancellationToken.None);

        return newPlayerId;
    }

    // Optional utilities if your server loop uses TryGet*
    public bool TryGetHandler(string type, out Func<WebSocket, string, string, Task> handler)
        => _handlers.TryGetValue(type, out handler);

    public bool TryGetRegistrationHandler(string type, out Func<WebSocket, string, Task<string>> handler)
        => _registrationHandlers.TryGetValue(type, out handler);
}

