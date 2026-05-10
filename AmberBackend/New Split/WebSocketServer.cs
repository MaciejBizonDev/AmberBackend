using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using AmberBackend.Combat;
using AmberBackend.Zones;

public class WebSocketServer
{
    private readonly MessageHandlerService _messageHandler;
    private readonly PlayerService _playerService;
    private readonly ZoneManager _zoneManager;
    private readonly PlayerSessionManager _sessionManager;
    private string url = "http://+:8080/ws/";

    public WebSocketServer(
        MessageHandlerService messageHandler,
        PlayerService playerService,
        ZoneManager zoneManager,
        PlayerSessionManager sessionManager)
    {
        _messageHandler = messageHandler;
        _playerService = playerService;
        _zoneManager = zoneManager;
        _sessionManager = sessionManager;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var listener = new HttpListener();
            Console.WriteLine("[WebSocketServer] Adding prefix...");
            listener.Prefixes.Add(url);

            Console.WriteLine("[WebSocketServer] Starting listener...");
            listener.Start();

            Console.WriteLine("[WebSocketServer] Listener started successfully!");
            Console.WriteLine($"[WebSocketServer] Listening on {url}");

            while (!cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine("[WebSocketServer] Waiting for connection...");
                var context = await listener.GetContextAsync();
                Console.WriteLine("[WebSocketServer] Connection received!");

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

            listener.Stop();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebSocketServer] FATAL ERROR: {ex.Message}");
            Console.WriteLine($"[WebSocketServer] Stack trace: {ex.StackTrace}");
            throw;
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
                    string newPlayerId = await _messageHandler.HandleMessageAsync(ws, baseMsg.type, message, playerId);

                    if (!string.IsNullOrEmpty(newPlayerId) && string.IsNullOrEmpty(playerId))
                    {
                        playerId = newPlayerId;
                        Console.WriteLine($"[WebSocketServer] Registered client: {playerId}");
                    }
                    else if (!string.IsNullOrEmpty(newPlayerId))
                    {
                        playerId = newPlayerId;
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
            if (!string.IsNullOrEmpty(playerId))
            {
                var session = _sessionManager.GetSession(playerId);
                if (session != null && !string.IsNullOrEmpty(session.CurrentZoneId))
                {
                    var zone = _zoneManager.GetZone(session.CurrentZoneId);
                    if (zone != null)
                    {
                        // Save position AND zone before removing
                        var position = zone.MovementService.GetEntityPosition(playerId);
                        if (position != null)
                        {
                            _playerService.UpdatePlayerPosition(playerId, position, session.CurrentZoneId);
                            Console.WriteLine($"[WebSocketServer] Saved {playerId} position: {position} in zone {session.CurrentZoneId}");
                        }

                        zone.RemovePlayer(playerId);

                        BroadcastToZone(session.CurrentZoneId, new
                        {
                            type = "entity_removed",
                            playerId = playerId
                        });
                    }
                }

                _sessionManager.RemoveSession(playerId);
                Console.WriteLine($"[WebSocketServer] Cleaned up client {playerId}");
            }

            if (ws.State == WebSocketState.Open)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// Broadcast a message to all players in a specific zone.
    /// </summary>
    public async void BroadcastToZone(string zoneId, object message)
    {
        var json = JsonConvert.SerializeObject(message);
        var buffer = Encoding.UTF8.GetBytes(json);

        var sockets = _sessionManager.GetZoneWebSockets(zoneId);
        var tasks = sockets.Select(ws => SafeSendAsync(ws, buffer));

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Send message to all players in a zone except one.
    /// </summary>
    public async void BroadcastToZoneExcept(string zoneId, string excludePlayerId, object message)
    {
        var json = JsonConvert.SerializeObject(message);
        var buffer = Encoding.UTF8.GetBytes(json);

        var sessions = _sessionManager.GetPlayersInZone(zoneId)
            .Where(s => s.PlayerId != excludePlayerId);

        var tasks = sessions
            .Select(s => s.WebSocket)
            .Where(ws => ws.State == WebSocketState.Open)
            .Select(ws => SafeSendAsync(ws, buffer));

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Send message to a specific player.
    /// </summary>
    public async Task SendToPlayer(string playerId, object message)
    {
        var session = _sessionManager.GetSession(playerId);
        if (session == null || session.WebSocket.State != WebSocketState.Open)
            return;

        var json = JsonConvert.SerializeObject(message);
        var buffer = Encoding.UTF8.GetBytes(json);

        await SafeSendAsync(session.WebSocket, buffer);
    }

    private async Task SafeSendAsync(WebSocket ws, byte[] buffer)
    {
        try
        {
            if (ws.State == WebSocketState.Open)
            {
                await ws.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebSocketServer] Error sending message: {ex.Message}");
        }
    }

    public int SaveAllPlayerPositions()
    {
        int savedCount = 0;

        foreach (var zone in _zoneManager.GetAllZones())
        {
            var players = _sessionManager.GetPlayersInZone(zone.ZoneId);

            foreach (var session in players)
            {
                var position = zone.MovementService.GetEntityPosition(session.PlayerId);
                if (position != null)
                {
                    try
                    {
                        _playerService.UpdatePlayerPosition(session.PlayerId, position, zone.ZoneId);
                        savedCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[WebSocketServer] Error saving {session.PlayerId}: {ex.Message}");
                    }
                }
            }
        }

        return savedCount;
    }
}