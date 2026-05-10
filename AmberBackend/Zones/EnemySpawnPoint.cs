using AmberBackend.AI;
using AmberBackend.Movement;

namespace AmberBackend.Zones
{
    /// <summary>
    /// Defines where and when an enemy respawns.
    /// </summary>
    public class EnemySpawnPoint
    {
        public string SpawnId { get; set; }
        public string EnemyId { get; set; }  // Current instance ID (e.g., "npc_guard_1")
        public TilePosition SpawnPosition { get; set; }
        public float RespawnTime { get; set; }  // Seconds until respawn
        public bool IsAlive { get; set; } = true;
        public DateTime DeathTime { get; set; }

        // Optional: For enemies that patrol
        public List<TilePosition> PatrolPath { get; set; } = new List<TilePosition>();
        public float Speed { get; set; } = 2f;
        public AIBehaviorType AIBehavior { get; set; } = AIBehaviorType.Passive;
    }

}