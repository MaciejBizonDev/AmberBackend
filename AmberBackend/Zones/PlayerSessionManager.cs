using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;

namespace AmberBackend.Zones
{
    /// <summary>
    /// Central registry of all connected players.
    /// Tracks which zone each player is in.
    /// </summary>
    public class PlayerSessionManager
    {
        private readonly ConcurrentDictionary<string, PlayerSession> _sessions = new ConcurrentDictionary<string, PlayerSession>();

        /// <summary>
        /// Register a new player session.
        /// </summary>
        public void RegisterSession(string playerId, WebSocket webSocket, string username)
        {
            var session = new PlayerSession(playerId, webSocket)
            {
                Username = username
            };

            _sessions[playerId] = session;
            System.Console.WriteLine($"[SessionManager] Registered session: {playerId} ({username})");
        }

        /// <summary>
        /// Remove a player session (on disconnect).
        /// </summary>
        public PlayerSession RemoveSession(string playerId)
        {
            if (_sessions.TryRemove(playerId, out var session))
            {
                System.Console.WriteLine($"[SessionManager] Removed session: {playerId}");
                return session;
            }
            return null;
        }

        /// <summary>
        /// Get a player's session.
        /// </summary>
        public PlayerSession GetSession(string playerId)
        {
            return _sessions.TryGetValue(playerId, out var session) ? session : null;
        }

        /// <summary>
        /// Set which zone a player is in.
        /// </summary>
        public void SetPlayerZone(string playerId, string zoneId)
        {
            if (_sessions.TryGetValue(playerId, out var session))
            {
                var oldZone = session.CurrentZoneId;
                session.CurrentZoneId = zoneId;
                System.Console.WriteLine($"[SessionManager] Player {playerId} moved: {oldZone ?? "null"} -> {zoneId}");
            }
        }

        /// <summary>
        /// Get all players in a specific zone.
        /// </summary>
        public List<PlayerSession> GetPlayersInZone(string zoneId)
        {
            return _sessions.Values
                .Where(s => s.CurrentZoneId == zoneId)
                .ToList();
        }

        /// <summary>
        /// Get all WebSockets for players in a zone (for broadcasting).
        /// </summary>
        public List<WebSocket> GetZoneWebSockets(string zoneId)
        {
            return _sessions.Values
                .Where(s => s.CurrentZoneId == zoneId)
                .Select(s => s.WebSocket)
                .Where(ws => ws.State == WebSocketState.Open)
                .ToList();
        }

        public int TotalPlayers => _sessions.Count;
    }
}