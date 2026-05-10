using System;
using System.Collections.Generic;

namespace AmberBackend.Zones
{
    /// <summary>
    /// Manages all active zones.
    /// Creates, destroys, and routes to zones.
    /// </summary>
    public class ZoneManager
    {
        private readonly Dictionary<string, Zone> _zones = new Dictionary<string, Zone>();
        private readonly TilemapRepository _tilemaps;
        private readonly GridAStarPathfinder _pathfinder;
        private WebSocketServer _webSocketServer;

        public ZoneManager(TilemapRepository tilemaps, GridAStarPathfinder pathfinder)
        {
            _tilemaps = tilemaps;
            _pathfinder = pathfinder;
        }

        // NEW: Set the WebSocketServer after construction
        public void SetWebSocketServer(WebSocketServer webSocketServer)
        {
            _webSocketServer = webSocketServer;
        }

        public Zone CreateZone(ZoneDefinition definition)
        {
            if (_zones.ContainsKey(definition.ZoneId))
            {
                Console.WriteLine($"[ZoneManager] Zone {definition.ZoneId} already exists");
                return _zones[definition.ZoneId];
            }

            if (_webSocketServer == null)
            {
                throw new InvalidOperationException("WebSocketServer must be set before creating zones");
            }

            var zone = new Zone(definition, _tilemaps, _pathfinder);
            zone.SetBroadcaster(_webSocketServer); // Set broadcaster after construction
            _zones[definition.ZoneId] = zone;
            zone.Start();

            Console.WriteLine($"[ZoneManager] Created zone: {definition.ZoneId}");
            return zone;
        }


        /// <summary>
        /// Get a zone by ID.
        /// </summary>
        public Zone GetZone(string zoneId)
        {
            return _zones.TryGetValue(zoneId, out var zone) ? zone : null;
        }

        /// <summary>
        /// Get all active zones.
        /// </summary>
        public IEnumerable<Zone> GetAllZones()
        {
            return _zones.Values;
        }

        /// <summary>
        /// Destroy a zone (for dynamic instances).
        /// </summary>
        public void DestroyZone(string zoneId)
        {
            if (!_zones.TryGetValue(zoneId, out var zone))
            {
                Console.WriteLine($"[ZoneManager] Zone {zoneId} doesn't exist");
                return;
            }

            zone.Stop();
            _zones.Remove(zoneId);

            Console.WriteLine($"[ZoneManager] Destroyed zone: {zoneId}");
        }

        public void StopAll()
        {
            foreach (var zone in _zones.Values)
            {
                zone.Stop();
            }
            _zones.Clear();
        }
    }
}