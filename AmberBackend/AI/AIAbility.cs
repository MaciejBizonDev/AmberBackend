using AmberBackend.Combat;

namespace AmberBackend.AI
{
    /// <summary>
    /// Represents an ability an AI can use, with cooldown tracking.
    /// </summary>
    public class AIAbility
    {
        public string AbilityId { get; set; }
        public AbilityDefinition Definition { get; set; }
        public float LastUsedTime { get; set; } = -999f; // Time since last use

        public AIAbility(string abilityId, AbilityDefinition definition)
        {
            AbilityId = abilityId;
            Definition = definition;
        }

        /// <summary>
        /// Check if ability is ready (cooldown expired).
        /// </summary>
        public bool IsReady(float currentTime)
        {
            return currentTime >= LastUsedTime + Definition.Cooldown;
        }

        /// <summary>
        /// Mark ability as used.
        /// </summary>
        public void MarkUsed(float currentTime)
        {
            LastUsedTime = currentTime;
        }
    }
}