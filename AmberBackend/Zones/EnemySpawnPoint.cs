using AmberBackend.Movement;
using System.Collections.Generic;

namespace AmberBackend.Zones
{
    /// <summary>
    /// Defines WHERE an enemy appears. Stats come from the referenced template.
    /// </summary>
    public class EnemySpawnPoint
    {
        public string SpawnId { get; set; }
        public string ZoneId { get; set; }
        public string TemplateId { get; set; }
        public TilePosition SpawnPosition { get; set; }
        public List<TilePosition> PatrolPath { get; set; } = new List<TilePosition>();

        // Runtime state
        public string EnemyId { get; set; }  // Instance ID, generated at spawn
        public bool IsAlive { get; set; } = true;
        public System.DateTime DeathTime { get; set; }

        // The resolved template (set when loaded)
        public EnemyTemplate Template { get; set; }
    }
}