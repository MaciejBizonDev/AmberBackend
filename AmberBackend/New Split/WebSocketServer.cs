using AmberBackend.Movement;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class WebSocketServer
{
    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();
    private readonly MessageHandlerService _messageHandler;
    private readonly MovementService _movementService;

    public WebSocketServer(MessageHandlerService messageHandler, MovementService movementService)
    {
        _messageHandler = messageHandler;
        _movementService = movementService;

        // Subscribe to movement events
        _movementService.OnEntityMove += BroadcastEntityMovement;
        _movementService.OnPositionCorrected += SendPositionCorrection;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:5000/");
        listener.Start();

        Console.WriteLine("[WebSocketServer] Listening on http://localhost:5000/");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync();

                if (context.Request.IsWebSocketRequest)
                {
                    var wsContext = await context.AcceptWebSocketAsync(null);
                    var ws = wsContext.WebSocket;

                    Console.WriteLine("[WebSocketServer] Client connected");

                    // Handle client in background
                    _ = Task.Run(() => HandleClientAsync(ws), cancellationToken);
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebSocketServer] Error: {ex.Message}");
        }
        finally
        {
            listener.Stop();
            Console.WriteLine("[WebSocketServer] Stopped");
        }
    }

    public async Task HandleClientAsync(WebSocket ws)
    {
        string playerId = null;

        try
        {
            var buffer = new byte[4096];

            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine($"[WebSocketServer] Client {playerId ?? "unknown"} requested close");
                    break;
                }

                if (result.MessageType != WebSocketMessageType.Text) continue;

                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var baseMsg = JsonConvert.DeserializeObject<BaseMessage>(message);

                if (baseMsg != null && !string.IsNullOrEmpty(baseMsg.type))
                {
                    playerId = await _messageHandler.HandleMessageAsync(ws, baseMsg.type, message, playerId);

                    // Register client socket after player registration
                    if (baseMsg.type == "register_player" && !string.IsNullOrEmpty(playerId))
                    {
                        _clients[playerId] = ws;
                        Console.WriteLine($"[WebSocketServer] Registered client socket for {playerId}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebSocketServer] Error handling client {playerId}: {ex.Message}");
        }
        finally
        {
            // ✅ Clean up when client disconnects
            if (!string.IsNullOrEmpty(playerId))
            {
                _clients.TryRemove(playerId, out _);

                // ✅ NEW: Remove from MovementService
                _movementService.RemoveEntity(playerId);

                Console.WriteLine($"[WebSocketServer] Removed and cleaned up client {playerId}");
            }

            if (ws.State == WebSocketState.Open)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// Broadcast entity movement to all other clients (not the entity that moved).
    /// </summary>
    private async void BroadcastEntityMovement(string entityId, TilePosition from, TilePosition to, float duration)
    {
        var msg = new
        {
            type = "move_command",
            playerId = entityId,
            fromX = from.X,
            fromY = from.Y,
            toX = to.X,
            toY = to.Y,
            duration = duration
        };

        var json = JsonConvert.SerializeObject(msg);
        var buffer = Encoding.UTF8.GetBytes(json);

        // Broadcast to all clients EXCEPT the entity that moved
        var tasks = _clients
            .Where(kvp => kvp.Key != entityId)
            .Select(kvp => SafeSendAsync(kvp.Value, buffer));

        await Task.WhenAll(tasks);

        Console.WriteLine($"[WebSocketServer] Broadcast movement: {entityId} moved {from} -> {to}");
    }

    /// <summary>
    /// Send position correction to a specific client.
    /// </summary>
    private async void SendPositionCorrection(string playerId, TilePosition correctedPosition, string reason)
    {
        if (!_clients.TryGetValue(playerId, out var ws))
        {
            Console.WriteLine($"[WebSocketServer] Can't send correction to {playerId}, client not found");
            return;
        }

        var msg = new PositionCorrectionMessage
        {
            type = "position_correction",
            playerId = playerId,
            x = correctedPosition.X,
            y = correctedPosition.Y,
            reason = reason
        };

        var json = JsonConvert.SerializeObject(msg);
        var buffer = Encoding.UTF8.GetBytes(json);

        await SafeSendAsync(ws, buffer);

        Console.WriteLine($"[WebSocketServer] Sent position correction to {playerId}: {correctedPosition} (reason: {reason})");
    }

    private async Task SafeSendAsync(WebSocket ws, byte[] buffer)
    {
        try
        {
            if (ws.State == WebSocketState.Open)
            {
                await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebSocketServer] Error sending message: {ex.Message}");
        }
    }
}