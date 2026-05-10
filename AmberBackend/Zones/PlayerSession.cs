using System.Net.WebSockets;

namespace AmberBackend.Zones
{
    /// <summary>
    /// Tracks a connected player's session.
    /// Knows which zone they're in, their WebSocket, etc.
    /// </summary>
    public class PlayerSession
    {
        public string PlayerId { get; set; }
        public string CurrentZoneId { get; set; }
        public WebSocket WebSocket { get; set; }
        public string Username { get; set; }

        public PlayerSession(string playerId, WebSocket webSocket)
        {
            PlayerId = playerId;
            WebSocket = webSocket;
        }
    }
}