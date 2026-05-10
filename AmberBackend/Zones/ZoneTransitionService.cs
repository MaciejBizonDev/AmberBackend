using AmberBackend.Movement;
using Newtonsoft.Json;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace AmberBackend.Zones
{
    /// <summary>
    /// Handles player transitions between zones.
    /// </summary>
    public class ZoneTransitionService
    {
        private readonly ZoneManager _zoneManager;
        private readonly PlayerSessionManager _sessionManager;
        private readonly PlayerService _playerService;

        public ZoneTransitionService(ZoneManager zoneManager, PlayerSessionManager sessionManager, PlayerService playerService)
        {
            _zoneManager = zoneManager;
            _sessionManager = sessionManager;
            _playerService = playerService;
        }

        /// <summary>
        /// Transition a player from one zone to another.
        /// </summary>
        public void TransitionPlayer(string playerId, ZonePortal portal)
        {
            var session = _sessionManager.GetSession(playerId);
            if (session == null)
            {
                Console.WriteLine($"[ZoneTransition] Session not found for {playerId}");
                return;
            }

            var sourceZone = _zoneManager.GetZone(portal.SourceZoneId);
            var destZone = _zoneManager.GetZone(portal.DestinationZoneId);

            if (sourceZone == null || destZone == null)
            {
                Console.WriteLine($"[ZoneTransition] Invalid zones: {portal.SourceZoneId} -> {portal.DestinationZoneId}");
                return;
            }

            Console.WriteLine($"[ZoneTransition] Transitioning {playerId}: {portal.SourceZoneId} -> {portal.DestinationZoneId}");

            // Save to database with new zone
            _playerService.UpdatePlayerPosition(playerId, portal.DestinationPosition, portal.DestinationZoneId);

            // Remove from source zone
            sourceZone.RemovePlayer(playerId);

            // Notify other players in source zone
            if (session.WebSocket.State == WebSocketState.Open)
            {
                var removeMsg = new { type = "entity_removed", playerId = playerId };
                BroadcastToZoneExcept(portal.SourceZoneId, playerId, removeMsg);
            }

            // Add to destination zone
            destZone.AddPlayer(playerId, portal.DestinationPosition);
            _sessionManager.SetPlayerZone(playerId, portal.DestinationZoneId);

            // Send zone change message to client
            SendZoneChangeMessage(session.WebSocket, portal.DestinationZoneId, portal.DestinationPosition);

            Console.WriteLine($"[ZoneTransition] {playerId} entered {portal.DestinationZoneId} at ({portal.DestinationPosition.X}, {portal.DestinationPosition.Y})");
        }

        private void SendZoneChangeMessage(WebSocket ws, string newZoneId, TilePosition spawnPos)
        {
            var msg = new
            {
                type = "zone_change",
                zoneId = newZoneId,
                x = spawnPos.X,
                y = spawnPos.Y
            };

            var json = JsonConvert.SerializeObject(msg);
            var buffer = Encoding.UTF8.GetBytes(json);

            try
            {
                ws.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ZoneTransition] Error sending zone change: {ex.Message}");
            }
        }

        private void BroadcastToZoneExcept(string zoneId, string excludePlayerId, object message)
        {
            var json = JsonConvert.SerializeObject(message);
            var buffer = Encoding.UTF8.GetBytes(json);

            var sessions = _sessionManager.GetPlayersInZone(zoneId);
            foreach (var session in sessions)
            {
                if (session.PlayerId == excludePlayerId)
                    continue;

                if (session.WebSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        session.WebSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None).Wait();
                    }
                    catch { }
                }
            }
        }
    }
}