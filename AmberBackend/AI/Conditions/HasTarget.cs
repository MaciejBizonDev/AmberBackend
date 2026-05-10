namespace AmberBackend.AI.Conditions
{
    /// <summary>
    /// Checks if AI has a valid target.
    /// </summary>
    public class HasTarget : BehaviorNode
    {
        public override NodeStatus Execute(AIContext context)
        {
            return !string.IsNullOrEmpty(context.TargetPlayerId) && context.TargetPosition != null
                ? NodeStatus.Success
                : NodeStatus.Failure;
        }
    }
}