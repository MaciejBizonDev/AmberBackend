namespace AmberBackend.AI.Actions
{
    /// <summary>
    /// Use a specific ability on the current target.
    /// </summary>
    public class UseAbility : BehaviorNode
    {
        private readonly string _abilityId;

        public UseAbility(string abilityId)
        {
            _abilityId = abilityId;
        }

        public override NodeStatus Execute(AIContext context)
        {
            if (string.IsNullOrEmpty(context.TargetPlayerId))
                return NodeStatus.Failure;

            var ability = context.GetAbility(_abilityId);
            if (ability == null)
            {
                System.Console.WriteLine($"[AI:{context.EntityId}] Ability {_abilityId} not found");
                return NodeStatus.Failure;
            }

            if (!ability.IsReady(context.CurrentTime))
            {
                // Still on cooldown
                return NodeStatus.Failure;
            }

            // Use ability
            context.CombatService.UseAbility(
                context.EntityId,
                _abilityId,
                context.TargetPlayerId,
                context.CurrentPosition,
                context.TargetPosition
            );

            ability.MarkUsed(context.CurrentTime);

            System.Console.WriteLine($"[AI:{context.EntityId}] Used {_abilityId} on {context.TargetPlayerId}");
            return NodeStatus.Success;
        }
    }
}