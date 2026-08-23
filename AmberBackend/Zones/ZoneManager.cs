using System;
using System.Collections.Generic;
using AmberBackend.Database;

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
        private readonly EnemyRepository _enemyRepository;
        private readonly Dictionary<string, EnemyTemplate> _enemyTemplates;
        private WebSocketServer _webSocketServer;

        public ZoneManager(TilemapRepository tilemaps, GridAStarPathfinder pathfinder, EnemyRepository enemyRepository)
        {
            _tilemaps = tilemaps;
            _pathfinder = pathfinder;
            _enemyRepository = enemyRepository;
            _enemyTemplates = enemyRepository.LoadTemplates(); // Load templates once at startup
        }

        // Set the WebSocketServer after construction
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
            zone.SetBroadcaster(_webSocketServer);

            // Load enemy spawns from DB for this zone
            var enemySpawns = _enemyRepository.LoadSpawnsForZone(definition.ZoneId, _enemyTemplates);
            zone.LoadEnemySpawns(enemySpawns);

            _zones[definition.ZoneId] = zone;
            zone.Start();
            Console.WriteLine($"[ZoneManager] Created zone: {definition.ZoneId}");
            return zone;
        }

        public Zone GetZone(string zoneId)
        {
            return _zones.TryGetValue(zoneId, out var zone) ? zone : null;
        }

        public IEnumerable<Zone> GetAllZones()
        {
            return _zones.Values;
        }

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