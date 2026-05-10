using AmberBackend.AI;
using AmberBackend.Movement;
using System.Collections.Generic;

namespace AmberBackend.Zones
{
    public class ZoneDefinition
    {
        public string ZoneId { get; set; }
        public string Name { get; set; }
        public string TilemapPath { get; set; }
        public List<TilePosition> SpawnPoints { get; set; } = new List<TilePosition>();
        public List<EnemySpawnPoint> EnemySpawns { get; set; } = new List<EnemySpawnPoint>();

        public static ZoneDefinition TestZone => new ZoneDefinition
        {
            ZoneId = "test_zone",
            Name = "Test Zone",
            TilemapPath = "Resources/Tilemaps",
            SpawnPoints = new List<TilePosition>
            {
                new TilePosition(5, -5),
                new TilePosition(6, -5),
                new TilePosition(7, -5)
            },
            EnemySpawns = new List<EnemySpawnPoint>
            {
                // Aggressive guard with patrol
                new EnemySpawnPoint
                {
                    SpawnId = "spawn_guard_1",
                    EnemyId = "npc_guard_1",
                    SpawnPosition = new TilePosition(0, 0),
                    RespawnTime = 5f,
                    AIBehavior = AIBehaviorType.MeleeAggressive,
                    PatrolPath = new List<TilePosition>
                    {
                        new TilePosition(0, 0),
                        new TilePosition(5, 0)
                    },
                    Speed = 2f
                },
        
                // Quest giver (non-combat)
                new EnemySpawnPoint
                {
                    SpawnId = "spawn_quest_giver",
                    EnemyId = "npc_quest_giver_1",
                    SpawnPosition = new TilePosition(10, -3),
                    RespawnTime = 999f, // Don't respawn
                    AIBehavior = AIBehaviorType.QuestGiver,
                    PatrolPath = new List<TilePosition>(),
                    Speed = 0f
                },
        
                // Wandering critter
                new EnemySpawnPoint
                {
                    SpawnId = "spawn_critter_1",
                    EnemyId = "npc_critter_1",
                    SpawnPosition = new TilePosition(15, -5),
                    RespawnTime = 10f,
                    AIBehavior = AIBehaviorType.Critter,
                    PatrolPath = new List<TilePosition>(),
                    Speed = 1f
                },
        
                // Training dummy (aggressive, stationary)
                new EnemySpawnPoint
                {
                    SpawnId = "spawn_dummy_1",
                    EnemyId = "npc_dummy_1",
                    SpawnPosition = new TilePosition(8, -5),
                    RespawnTime = 3f,
                    AIBehavior = AIBehaviorType.MeleeAggressive,
                    PatrolPath = new List<TilePosition>(),
                    Speed = 0f
                }
            }
        };

        public static ZoneDefinition TownZone => new ZoneDefinition
        {
            ZoneId = "town_1",
            Name = "Starter Town",
            TilemapPath = "Resources/Tilemaps",
            SpawnPoints = new List<TilePosition>
            {
                new TilePosition(3, -2),
                new TilePosition(4, -2),
                new TilePosition(5, -2)
            },
            EnemySpawns = new List<EnemySpawnPoint>
            {
                new EnemySpawnPoint
                {
                    SpawnId = "spawn_merchant_1",
                    EnemyId = "npc_merchant_1",
                    SpawnPosition = new TilePosition(20, 20),
                    RespawnTime = 10f,
                    PatrolPath = new List<TilePosition>(),
                    Speed = 0f
                },
                new EnemySpawnPoint
                {
                    SpawnId = "spawn_guard_town_1",
                    EnemyId = "npc_guard_town_1",
                    SpawnPosition = new TilePosition(18, 18),
                    RespawnTime = 5f,
                    PatrolPath = new List<TilePosition>
                    {
                        new TilePosition(18, 18),
                        new TilePosition(22, 18)
                    },
                    Speed = 2f
                }
            }
        };
    }
}