using AmberBackend.AI;
using AmberBackend.Movement;
using AmberBackend.Zones;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace AmberBackend.Database
{
    /// <summary>
    /// Loads enemy templates and spawn points from the database.
    /// </summary>
    public class EnemyRepository
    {
        private readonly string _connectionString;

        public EnemyRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public Dictionary<string, EnemyTemplate> LoadTemplates()
        {
            var templates = new Dictionary<string, EnemyTemplate>();

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand(
                "SELECT template_id, display_name, max_hp, attack_power, speed, ai_behavior, respawn_time, aggro_range, model_id FROM enemy_templates",
                conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var template = new EnemyTemplate
                {
                    TemplateId = reader.GetString(0),
                    DisplayName = reader.GetString(1),
                    MaxHp = reader.GetInt32(2),
                    AttackPower = reader.GetInt32(3),
                    Speed = reader.GetFloat(4),
                    AIBehavior = Enum.Parse<AIBehaviorType>(reader.GetString(5)),
                    RespawnTime = reader.GetFloat(6),
                    AggroRange = reader.GetInt32(7),
                    ModelId = reader.IsDBNull(8) ? null : reader.GetString(8)
                };
                templates[template.TemplateId] = template;
            }

            Console.WriteLine($"[EnemyRepository] Loaded {templates.Count} enemy templates");
            return templates;
        }

        public List<EnemySpawnPoint> LoadSpawnsForZone(string zoneId, Dictionary<string, EnemyTemplate> templates)
        {
            var spawns = new List<EnemySpawnPoint>();

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand(
                "SELECT spawn_id, zone_id, template_id, x, y, patrol_path FROM zone_enemy_spawns WHERE zone_id = @zoneId",
                conn);
            cmd.Parameters.AddWithValue("zoneId", zoneId);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var templateId = reader.GetString(2);
                if (!templates.TryGetValue(templateId, out var template))
                {
                    Console.WriteLine($"[EnemyRepository] WARNING: spawn references unknown template '{templateId}', skipping");
                    continue;
                }

                var patrolJson = reader.IsDBNull(5) ? "[]" : reader.GetString(5);
                var patrolPath = ParsePatrolPath(patrolJson);

                var spawn = new EnemySpawnPoint
                {
                    SpawnId = reader.GetString(0),
                    ZoneId = reader.GetString(1),
                    TemplateId = templateId,
                    SpawnPosition = new TilePosition(reader.GetInt32(3), reader.GetInt32(4)),
                    PatrolPath = patrolPath,
                    Template = template,
                    EnemyId = reader.GetString(0) //$"npc_{reader.GetString(0)}"  // e.g., npc_spawn_critter_1
                };

                spawns.Add(spawn);
            }

            Console.WriteLine($"[EnemyRepository] Loaded {spawns.Count} enemy spawns for zone {zoneId}");
            return spawns;
        }

        private List<TilePosition> ParsePatrolPath(string json)
        {
            var path = new List<TilePosition>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    int x = element.GetProperty("x").GetInt32();
                    int y = element.GetProperty("y").GetInt32();
                    path.Add(new TilePosition(x, y));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EnemyRepository] Failed to parse patrol path: {ex.Message}");
            }
            return path;
        }
    }
}