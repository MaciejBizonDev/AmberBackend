using AmberBackend.Movement;
using System.Collections.Generic;

namespace AmberBackend.Combat
{
    /// <summary>
    /// Context passed through ability execution steps.
    /// Each step can read/modify this data.
    /// </summary>
    public class AbilityContext
    {
        public string SourceId { get; set; }
        public string TargetId { get; set; }
        public TilePosition SourcePosition { get; set; }
        public TilePosition TargetPosition { get; set; }
        public List<string> AffectedEntities { get; set; } = new List<string>();
        public int DamageAmount { get; set; }
        public int HealAmount { get; set; }
        public TilePosition ImpactPoint { get; set; }
        public PlayerStats SourceStats { get; set; }

        // For chaining
        public Dictionary<string, object> CustomData { get; set; } = new Dictionary<string, object>();
    }
}