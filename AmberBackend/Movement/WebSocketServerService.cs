using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

[Serializable]
public class TileClickMessage
{
    public string type { get; set; }
    public int x { get; set; }
    public int y { get; set; }
    public string playerId { get; set; }
    public bool walkable { get; set; } // server → client
}

[Serializable]
public class PathMessage
{
    public string type { get; set; } = "path";
    public string playerId { get; set; }
    public List<TilePosition> path { get; set; } = new();
}

public class WebSocketServerService
{
    private readonly HttpListener _httpListener = new HttpListener();
    private readonly TilemapRepository tilemaps;
    private readonly ConcurrentDictionary<string, Player> players = new();
    private GridAStarPathfinder _pathFinder;

    // Dictionary: message type → handler
    private readonly Dictionary<string, Func<WebSocket, string, Task>> _messageHandlers;

    public WebSocketServerService()
    {
        _httpListener.Prefixes.Add("http://localhost:5000/ws/");
        tilemaps = new TilemapRepository("Resources/Tilemaps");
        _pathFinder = new GridAStarPathfinder(tilemaps);

        _messageHandlers = new Dictionary<string, Func<WebSocket, string, Task>>
        {
            { "register_player", async (ws, msg) => await RegisterPlayer(ws) },
            { "tile_click", HandleTileClickMessage }
            // Add more message types here
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _httpListener.Start();
        Console.WriteLine("WebSocket server started on ws://localhost:5000/ws/");

        while (!cancellationToken.IsCancellationRequested)
        {
            var context = await _httpListener.GetContextAsync();
            if (context.Request.IsWebSocketRequest)
            {
                var wsContext = await context.AcceptWebSocketAsync(null);
                _ = HandleClientAsync(wsContext.WebSocket);
            }
            else
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
            }
        }
    }

    private async Task HandleClientAsync(WebSocket ws)
    {
        var buffer = new byte[4096];

        while (ws.State == WebSocketState.Open)
        {
            try
            {
                var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType != WebSocketMessageType.Text)
                    continue;

                string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var baseMsg = JsonConvert.DeserializeObject<TileClickMessage>(msg);

                if (baseMsg == null || string.IsNullOrEmpty(baseMsg.type))
                {
                    Console.WriteLine($"Received invalid message: {msg}");
                    continue;
                }

                if (_messageHandlers.TryGetValue(baseMsg.type, out var handler))
                {
                    await handler(ws, msg); // invoke handler
                }
                else
                {
                    Console.WriteLine($"Unknown message type: {baseMsg.type}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebSocket error: {ex}");
                break;
            }
        }
    }

    private async Task HandleTileClickMessage(WebSocket ws, string message)
    {
        try
        {
            var click = JsonConvert.DeserializeObject<TileClickMessage>(message);
            if (click == null || !players.TryGetValue(click.playerId, out var player))
                return;

            // Pathfinding starts from player's current position
            var path = _pathFinder.FindPath(player.Position, new TilePosition { X = click.x, Y = click.y });

            if (path.Count > 0)
                player.Position = path.Last(); // update last tile for server authority

            var response = new PathMessage
            {
                playerId = click.playerId,
                path = path
            };

            await SendJsonAsync(ws, response);
            Console.WriteLine($"Server: Sent path for player {click.playerId} with {path.Count} nodes.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to handle tile click for pathfinding: {ex}");
        }
    }

    private async Task<string> RegisterPlayer(WebSocket ws)
    {
        string playerId = Guid.NewGuid().ToString();
        var spawnTile = new TilePosition { X = 5, Y = -5 };

        var player = new Player
        {
            Id = playerId,
            Position = spawnTile
        };

        players[playerId] = player;

        var msg = new TileClickMessage
        {
            type = "player_registered",
            playerId = player.Id,
            x = spawnTile.X,
            y = spawnTile.Y,
            walkable = true
        };

        await SendJsonAsync(ws, msg);

        Console.WriteLine($"Player {playerId} registered at ({spawnTile.X},{spawnTile.Y})");
        return playerId;
    }

    private async Task SendJsonAsync(WebSocket ws, object obj)
    {
        string json = JsonConvert.SerializeObject(obj);
        byte[] buffer = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
    }
}
