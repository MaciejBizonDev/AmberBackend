using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class WebSocketServer
{
    private readonly HttpListener _listener = new HttpListener();
    private readonly PlayerService _playerService;
    private readonly MovementService _movementService;
    private readonly MessageHandlerService _messageHandlers;
    private readonly GridAStarPathfinder _pathfinder;
    private readonly MovementWebSocketHandler movementWsHandler;
    private readonly ConcurrentDictionary<string, Player> _players = new();
    private readonly HttpListener _httpListener = new HttpListener();

    public WebSocketServer(GridAStarPathfinder pathfinder)
    {
        _pathfinder = pathfinder;
        _playerService = new PlayerService();
        _movementService = new MovementService();
        movementWsHandler = new MovementWebSocketHandler(_movementService);
        _messageHandlers = new MessageHandlerService(_playerService, _movementService, movementWsHandler);
        _listener.Prefixes.Add("http://localhost:5000/ws/");
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _listener.Start();
        while (!cancellationToken.IsCancellationRequested)
        {
            var context = await _listener.GetContextAsync();
            if (context.Request.IsWebSocketRequest)
            {
                var wsContext = await context.AcceptWebSocketAsync(null);
                _ = HandleClientAsync(wsContext.WebSocket);
            }
        }
    }

    private async Task HandleClientAsync(WebSocket ws)
    {
        var buffer = new byte[4096];
        string playerId = null;

        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType != WebSocketMessageType.Text) continue;

            string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
            var baseMsg = JsonConvert.DeserializeObject<BaseMessage>(msg);
            if (baseMsg == null || string.IsNullOrEmpty(baseMsg.type)) continue;

            // Delegate message handling to MessageHandlerService
            playerId = await _messageHandlers.HandleMessageAsync(ws, baseMsg.type, msg, playerId);
        }
    }


    public static async Task SendJson(WebSocket ws, object obj)
    {
        string json = JsonConvert.SerializeObject(obj);
        byte[] buffer = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
    }
}
