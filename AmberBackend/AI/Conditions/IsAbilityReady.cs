namespace AmberBackend.AI.Conditions
{
    /// <summary>
    /// Check if an ability is off cooldown.
    /// </summary>
    public class IsAbilityReady : BehaviorNode
    {
        private readonly string _abilityId;

        public IsAbilityReady(string abilityId)
        {
            _abilityId = abilityId;
        }

        public override NodeStatus Execute(AIContext context)
        {
            return context.IsAbilityReady(_abilityId)
                ? NodeStatus.Success
                : NodeStatus.Failure;
        }
    }
}