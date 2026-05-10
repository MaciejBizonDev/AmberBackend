using AmberBackend.Combat;
using AmberBackend.Movement;
using System.Collections.Generic;
using System.Linq;

namespace AmberBackend.AI
{
    public class AIContext
    {
        // Entity info
        public string EntityId { get; set; }
        public TilePosition CurrentPosition { get; set; }
        public PlayerStats Stats { get; set; }

        // Target info
        public string TargetPlayerId { get; set; }
        public TilePosition TargetPosition { get; set; }

        // Services
        public MovementService MovementService { get; set; }
        public CombatService CombatService { get; set; }
        public GridAStarPathfinder Pathfinder { get; set; }
        public NPCStateManager StateManager { get; set; } // NEW
        public WebSocketServer WebSocketServer { get; set; } // NEW
        public string ZoneId { get; set; } // NEW

        // AI config
        public TilePosition SpawnPosition { get; set; }
        public int AggroRange { get; set; } = 5;
        public int AttackRange { get; set; } = 1;
        public float FleeHealthThreshold { get; set; } = 0.2f;
        public List<TilePosition> PatrolPath { get; set; } // NEW

        // Abilities
        public List<AIAbility> Abilities { get; set; } = new List<AIAbility>();

        // Runtime state
        public float CurrentTime { get; set; } = 0f;

        // Helper methods
        public AIAbility GetAbility(string abilityId)
        {
            return Abilities.FirstOrDefault(a => a.AbilityId == abilityId);
        }

        public bool IsAbilityReady(string abilityId)
        {
            var ability = GetAbility(abilityId);
            return ability != null && ability.IsReady(CurrentTime);
        }
    }
}