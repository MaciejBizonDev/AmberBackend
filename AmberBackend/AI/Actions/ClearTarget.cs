namespace AmberBackend.AI.Actions
{
    /// <summary>
    /// Clear current target (give up chase).
    /// </summary>
    public class ClearTarget : BehaviorNode
    {
        public override NodeStatus Execute(AIContext context)
        {
            context.TargetPlayerId = null;
            context.TargetPosition = null;

            System.Console.WriteLine($"[AI:{context.EntityId}] Cleared target");
            return NodeStatus.Success;
        }
    }
}