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
        private readonly ZoneRepository _zoneRepository;
        public const int ZONE_SIZE = 100;
        public enum GridDirection { North, South, East, West }
        private readonly Dictionary<(int x, int y), string> _worldGrid = new();
        private readonly Dictionary<string, (int x, int y)> _zoneGridPositions = new();


        public ZoneManager(TilemapRepository tilemaps, GridAStarPathfinder pathfinder, EnemyRepository enemyRepository, ZoneRepository zoneRepository)
        {
            _tilemaps = tilemaps;
            _pathfinder = pathfinder;
            _enemyRepository = enemyRepository;
            _zoneRepository = zoneRepository;
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

            var npcs = _zoneRepository.LoadNpcsForZone(definition.ZoneId);
            zone.LoadNpcSpawns(npcs);

            var portals = _zoneRepository.LoadPortalsForZone(definition.ZoneId);
            foreach (var portal in portals)
            {
                zone.AddPortal(portal);
            }

            _zones[definition.ZoneId] = zone;
            zone.Start();
            Console.WriteLine($"[ZoneManager] Created zone: {definition.ZoneId}");
            if (definition.ZoneType == "world" && definition.WorldGridX.HasValue && definition.WorldGridY.HasValue)
            {
                var pos = (definition.WorldGridX.Value, definition.WorldGridY.Value);
                _worldGrid[pos] = definition.ZoneId;
                _zoneGridPositions[definition.ZoneId] = pos;
                Console.WriteLine($"[ZoneManager] Registered {definition.ZoneId} at world grid ({pos.Item1},{pos.Item2})");
            }
            return zone;
        }

        /// <summary>
        /// Get the zone adjacent to the given zone in a direction (null if none / not a world zone).
        /// </summary>
        public string GetNeighbor(string zoneId, GridDirection direction)
        {
            if (!_zoneGridPositions.TryGetValue(zoneId, out var pos))
                return null; // not a world zone or unknown

            var neighborPos = direction switch
            {
                GridDirection.North => (pos.x, pos.y + 1),
                GridDirection.South => (pos.x, pos.y - 1),
                GridDirection.East => (pos.x + 1, pos.y),
                GridDirection.West => (pos.x - 1, pos.y),
                _ => pos
            };

            return _worldGrid.TryGetValue(neighborPos, out var neighborId) ? neighborId : null;
        }

        /// <summary>
        /// Get a world zone's grid position (null if not a world zone).
        /// </summary>
        public (int x, int y)? GetGridPosition(string zoneId)
        {
            return _zoneGridPositions.TryGetValue(zoneId, out var pos) ? pos : null;
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