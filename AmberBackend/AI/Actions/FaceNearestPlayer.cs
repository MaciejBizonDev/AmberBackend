namespace AmberBackend.AI.Actions
{
    /// <summary>
    /// Turn to face the nearest player (cosmetic).
    /// </summary>
    public class FaceNearestPlayer : BehaviorNode
    {
        public override NodeStatus Execute(AIContext context)
        {
            if (string.IsNullOrEmpty(context.TargetPlayerId) || context.TargetPosition == null)
                return NodeStatus.Failure;

            // Calculate direction to player
            int dx = context.TargetPosition.X - context.CurrentPosition.X;
            int dy = context.TargetPosition.Y - context.CurrentPosition.Y;

            // Store facing direction in context for future use
            // This could be used for sprite direction on client
            // For now, just log it
            System.Console.WriteLine($"[AI:{context.EntityId}] Facing direction: ({dx}, {dy})");

            return NodeStatus.Success;
        }
    }
}