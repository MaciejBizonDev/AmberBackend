using System;
using System.Collections.Generic;

namespace AmberBackend.Combat
{
    // Client → Server
    public class UseAbilityMessage
    {
        public string type = "use_ability";
        public string abilityId;
        public string targetId; // EntityId of target (or null for self/ground)
        public int? targetX;    // For ground-targeted abilities
        public int? targetY;
    }

    // Server → Client
    public class AbilityResultMessage
    {
        public string type = "ability_result";
        public string sourceId;
        public string targetId;
        public string abilityId;
        public int damage;           // 0 if not damage ability
        public int healing;          // 0 if not heal
        public int newTargetHp;
        public int newTargetMaxHp;
        public bool wasKilled;
        public string resultType;    // "hit", "miss", "dodged", "blocked"
    }

    public class StatsUpdateMessage
    {
        public string type = "stats_update";
        public string playerId;
        public int hp;
        public int maxHp;
        public int mana;
        public int maxMana;
        public int level;
    }

    public class CooldownMessage
    {
        public string type = "cooldown_start";
        public string playerId;
        public string abilityId;
        public float duration;
    }
}