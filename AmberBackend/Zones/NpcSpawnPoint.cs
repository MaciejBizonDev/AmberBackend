using AmberBackend.AI;
using AmberBackend.Movement;
using System.Collections.Generic;

namespace AmberBackend.Zones
{
    /// <summary>
    /// Defines a friendly NPC that doesn't die or respawn (merchants, quest givers, guards).
    /// </summary>
    public class NpcSpawnPoint
    {
        public string SpawnId { get; set; }
        public string NpcId { get; set; }  // e.g., "npc_merchant_1"
        public TilePosition SpawnPosition { get; set; }
        public NpcRole Role { get; set; } = NpcRole.Neutral;
        public List<TilePosition> PatrolPath { get; set; } = new List<TilePosition>();
        public float Speed { get; set; } = 0f;
    }

    public enum NpcRole
    {
        Neutral,       // Just stands around
        Merchant,      // Has inventory to sell
        QuestGiver,    // Gives quests
        Guard          // Patrols, defends area but doesn't attack first
    }
}