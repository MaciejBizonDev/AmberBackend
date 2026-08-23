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
        public List<NpcSpawnPoint> NpcSpawns { get; set; } = new List<NpcSpawnPoint>();

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
            NpcSpawns = new List<NpcSpawnPoint>
            {
                // Guard (friendly, patrols)
                new NpcSpawnPoint
                {
                    SpawnId = "spawn_guard_1",
                    NpcId = "npc_guard_1",
                    SpawnPosition = new TilePosition(0, 0),
                    Role = NpcRole.Guard,
                    PatrolPath = new List<TilePosition>
                    {
                        new TilePosition(0, 0),
                        new TilePosition(5, 0)
                    },
                    Speed = 2f
                },
                // Quest giver (stationary)
                new NpcSpawnPoint
                {
                    SpawnId = "spawn_quest_giver",
                    NpcId = "npc_quest_giver_1",
                    SpawnPosition = new TilePosition(10, -3),
                    Role = NpcRole.QuestGiver,
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
            NpcSpawns = new List<NpcSpawnPoint>
            {
                new NpcSpawnPoint
                {
                    SpawnId = "spawn_merchant_1",
                    NpcId = "npc_merchant_1",
                    SpawnPosition = new TilePosition(20, 20),
                    Role = NpcRole.Merchant,
                    Speed = 0f
                },
                new NpcSpawnPoint
                {
                    SpawnId = "spawn_guard_town_1",
                    NpcId = "npc_guard_town_1",
                    SpawnPosition = new TilePosition(18, 18),
                    Role = NpcRole.Guard,
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