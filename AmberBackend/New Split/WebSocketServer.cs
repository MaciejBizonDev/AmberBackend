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
    private readonly HttpListener _listener = new();
    private readonly MessageHandlerService _handlers;
    private readonly MovementService _movementService;

    // Track player WebSocket connections
    private readonly ConcurrentDictionary<string, WebSocket> _playerSockets = new();

    public WebSocketServer(MessageHandlerService handlers, MovementService movementService)
    {
        _handlers = handlers;
        _movementService = movementService;
        _listener.Prefixes.Add("http://localhost:5000/ws/");
    }

    public async Task StartAsync(CancellationToken ct)
    {
        Console.WriteLine("[WebSocketServer] Starting listener...");
        try
        {
            _listener.Start();
            Console.WriteLine($"[WebSocketServer] Listening on: {string.Join(", ", _listener.Prefixes)}");
        }
        catch (HttpListenerException ex)
        {
            Console.WriteLine($"[WebSocketServer] Failed to start listener: {ex.Message} (ErrorCode: {ex.ErrorCode})");
            Console.WriteLine("If you see ErrorCode 5 (Access denied), run (as Administrator):");
            Console.WriteLine("  netsh http add urlacl url=http://localhost:5000/ws/ user=%USERNAME%");
            throw;
        }

        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException ex) when (ct.IsCancellationRequested)
            {
                Console.WriteLine("[WebSocketServer] Listener stopped (cancellation requested).");
                break;
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine("[WebSocketServer] Listener disposed; exiting accept loop.");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WebSocketServer] Error accepting connection: " + ex);
                continue;
            }

            Console.WriteLine($"[WebSocketServer] Incoming connection from {ctx.Request.RemoteEndPoint}");

            HttpListenerWebSocketContext wsContext;
            try
            {
                wsContext = await ctx.AcceptWebSocketAsync(subProtocol: null).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WebSocketServer] Failed to accept WebSocket: " + ex);
                ctx.Response.StatusCode = 500;
                ctx.Response.Close();
                continue;
            }

            _ = Task.Run(async () => await HandleClientAsync(wsContext.WebSocket), ct);
        }

        Console.WriteLine("[WebSocketServer] Accept loop exited.");
    }

    private async Task HandleClientAsync(WebSocket ws)
    {
        var buffer = new byte[4096];
        string currentPlayerId = null;

        try
        {
            while (ws.State == WebSocketState.Open)
            {
                try
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", CancellationToken.None);
                        break;
                    }

                    if (result.MessageType != WebSocketMessageType.Text)
                        continue;

                    string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var baseMsg = JsonConvert.DeserializeObject<BaseMessage>(msg);
                    if (baseMsg == null || string.IsNullOrEmpty(baseMsg.type))
                        continue;

                    currentPlayerId = await _handlers.HandleMessageAsync(ws, baseMsg.type, msg, currentPlayerId);

                    // Track this player's WebSocket
                    if (!string.IsNullOrEmpty(currentPlayerId) && !_playerSockets.ContainsKey(currentPlayerId))
                    {
                        _playerSockets[currentPlayerId] = ws;
                        Console.WriteLine($"[WebSocketServer] Registered socket for {currentPlayerId}");

                        // Send initial time sync message
                        await SendTimeSyncMessage(ws);
                    }
                }
                catch (WebSocketException)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in WebSocket handler: {ex.Message}");
        }
        finally
        {
            // Cleanup
            if (currentPlayerId != null)
            {
                _playerSockets.TryRemove(currentPlayerId, out _);
                Console.WriteLine($"[WebSocketServer] Removed socket for {currentPlayerId}");
            }

            if (ws.State != WebSocketState.Closed)
            {
                try
                {
                    await ws.CloseAsync(WebSocketCloseStatus.InternalServerError, "Connection closed", CancellationToken.None);
                }
                catch { /* Ignore */ }
            }
        }
    }

    /// <summary>
    /// Send time synchronization message to help client calculate offset.
    /// Client can use this to align timestamps.
    /// </summary>
    private async Task SendTimeSyncMessage(WebSocket ws)
    {
        var msg = new
        {
            type = "time_sync",
            serverUptime = _movementService.GetServerUptime()
        };

        try
        {
            var json = JsonConvert.SerializeObject(msg);
            var buffer = Encoding.UTF8.GetBytes(json);
            await ws.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
            Console.WriteLine($"[WebSocketServer] Sent time_sync: {msg.serverUptime:F3}s");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebSocketServer] Error sending time_sync: {ex.Message}");
        }
    }

    /// <summary>
    /// Send timestamped move command to player(s).
    /// - NPCs: Broadcast to ALL clients
    /// - Players: Send only to that specific player
    /// </summary>
    public async Task SendMoveCommandToPlayer(string playerId, TilePosition fromCell, TilePosition toCell, float duration, double timestamp)
    {
        // NPCs: broadcast to everyone
        if (playerId.StartsWith("npc_"))
        {
            await BroadcastMoveCommand(playerId, fromCell, toCell, duration, timestamp);
            return;
        }

        // Players: send only to that player
        if (!_playerSockets.TryGetValue(playerId, out var ws))
        {
            Console.WriteLine($"[WebSocketServer] No socket for {playerId}");
            return;
        }

        if (ws.State != WebSocketState.Open)
        {
            Console.WriteLine($"[WebSocketServer] Socket closed for {playerId}");
            return;
        }

        var command = new
        {
            type = "move_command",
            playerId,
            fromX = fromCell.X,
            fromY = fromCell.Y,
            toX = toCell.X,
            toY = toCell.Y,
            duration,
            timestamp // ✅ NEW: Server timestamp
        };

        try
        {
            var json = JsonConvert.SerializeObject(command);
            var buffer = Encoding.UTF8.GetBytes(json);
            await ws.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);

            Console.WriteLine($"[Server→Client] move_command to {playerId}: ({fromCell.X},{fromCell.Y}) → ({toCell.X},{toCell.Y}) | duration:{duration:F3}s | timestamp:{timestamp:F3}s");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebSocketServer] Error sending to {playerId}: {ex.Message}");
        }
    }

    /// <summary>
    /// Broadcast a timestamped move command to ALL connected clients (used for NPCs).
    /// </summary>
    private async Task BroadcastMoveCommand(string playerId, TilePosition fromCell, TilePosition toCell, float duration, double timestamp)
    {
        var command = new
        {
            type = "move_command",
            playerId,
            fromX = fromCell.X,
            fromY = fromCell.Y,
            toX = toCell.X,
            toY = toCell.Y,
            duration,
            timestamp // ✅ NEW: Server timestamp
        };

        var json = JsonConvert.SerializeObject(command);
        var buffer = Encoding.UTF8.GetBytes(json);

        int successCount = 0;
        int failCount = 0;

        // Broadcast to ALL connected clients
        foreach (var kvp in _playerSockets)
        {
            var socket = kvp.Value;
            if (socket.State == WebSocketState.Open)
            {
                try
                {
                    await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                    successCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WebSocketServer] Error broadcasting to {kvp.Key}: {ex.Message}");
                    failCount++;
                }
            }
        }

        Console.WriteLine($"[Server→ALL] move_command for {playerId}: ({fromCell.X},{fromCell.Y}) → ({toCell.X},{toCell.Y}) | duration:{duration:F3}s | timestamp:{timestamp:F3}s | sent to {successCount} clients");
    }
}