using AmberBackend.Movement;
using AmberBackend.Zones;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace AmberBackend.Database
{
    /// <summary>
    /// Loads zone definitions, NPCs, portals, and respawn points from the database.
    /// </summary>
    public class ZoneRepository
    {
        private readonly string _connectionString;

        public ZoneRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        // ============================================================
        // ZONES
        // ============================================================

        public List<ZoneDefinition> LoadAllZones()
        {
            var zones = new List<ZoneDefinition>();

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand(
                "SELECT zone_id, display_name, tilemap_path FROM zones",
                conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                zones.Add(new ZoneDefinition
                {
                    ZoneId = reader.GetString(0),
                    Name = reader.GetString(1),
                    TilemapPath = reader.GetString(2)
                });
            }

            Console.WriteLine($"[ZoneRepository] Loaded {zones.Count} zones");
            return zones;
        }

        // ============================================================
        // NPCs
        // ============================================================

        public List<NpcSpawnPoint> LoadNpcsForZone(string zoneId)
        {
            var npcs = new List<NpcSpawnPoint>();

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand(
                "SELECT spawn_id, npc_id, role, x, y, patrol_path, speed FROM zone_npc_spawns WHERE zone_id = @zoneId",
                conn);
            cmd.Parameters.AddWithValue("zoneId", zoneId);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var roleStr = reader.GetString(2);
                var role = Enum.TryParse<NpcRole>(roleStr, out var parsedRole)
                    ? parsedRole
                    : NpcRole.Neutral;

                var patrolJson = reader.IsDBNull(5) ? "[]" : reader.GetString(5);

                npcs.Add(new NpcSpawnPoint
                {
                    SpawnId = reader.GetString(0),
                    NpcId = reader.GetString(1),
                    Role = role,
                    SpawnPosition = new TilePosition(reader.GetInt32(3), reader.GetInt32(4)),
                    PatrolPath = ParseTilePositions(patrolJson),
                    Speed = reader.GetFloat(6)
                });
            }

            Console.WriteLine($"[ZoneRepository] Loaded {npcs.Count} NPCs for zone {zoneId}");
            return npcs;
        }

        // ============================================================
        // PORTALS
        // ============================================================

        public List<ZonePortal> LoadPortalsForZone(string zoneId)
        {
            var portals = new List<ZonePortal>();

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand(
                "SELECT portal_id, zone_id, x, y, dest_zone_id, dest_x, dest_y FROM zone_portals WHERE zone_id = @zoneId",
                conn);
            cmd.Parameters.AddWithValue("zoneId", zoneId);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                portals.Add(new ZonePortal
                {
                    PortalId = reader.GetString(0),
                    SourceZoneId = reader.GetString(1),
                    TriggerPosition = new TilePosition(reader.GetInt32(2), reader.GetInt32(3)),
                    DestinationZoneId = reader.GetString(4),
                    DestinationPosition = new TilePosition(reader.GetInt32(5), reader.GetInt32(6))
                });
            }

            Console.WriteLine($"[ZoneRepository] Loaded {portals.Count} portals for zone {zoneId}");
            return portals;
        }

        // ============================================================
        // RESPAWN POINTS
        // ============================================================

        public List<TilePosition> LoadRespawnPointsForZone(string zoneId)
        {
            var points = new List<TilePosition>();

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand(
                "SELECT x, y FROM zone_respawn_points WHERE zone_id = @zoneId ORDER BY id",
                conn);
            cmd.Parameters.AddWithValue("zoneId", zoneId);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                points.Add(new TilePosition(reader.GetInt32(0), reader.GetInt32(1)));
            }

            Console.WriteLine($"[ZoneRepository] Loaded {points.Count} respawn points for zone {zoneId}");
            return points;
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private List<TilePosition> ParseTilePositions(string json)
        {
            var list = new List<TilePosition>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    int x = element.GetProperty("x").GetInt32();
                    int y = element.GetProperty("y").GetInt32();
                    list.Add(new TilePosition(x, y));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ZoneRepository] Failed to parse tile positions: {ex.Message}");
            }
            return list;
        }
    }
}