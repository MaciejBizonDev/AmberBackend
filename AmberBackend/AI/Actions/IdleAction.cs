namespace AmberBackend.AI.Actions
{
    /// <summary>
    /// Do nothing. Always succeeds.
    /// </summary>
    public class IdleAction : BehaviorNode
    {
        public override NodeStatus Execute(AIContext context)
        {
            // Just chill
            return NodeStatus.Success;
        }
    }
}