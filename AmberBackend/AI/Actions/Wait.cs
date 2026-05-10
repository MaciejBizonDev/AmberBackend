namespace AmberBackend.AI.Actions
{
    /// <summary>
    /// Do nothing (idle/patrol handled elsewhere).
    /// </summary>
    public class Wait : BehaviorNode
    {
        public override NodeStatus Execute(AIContext context)
        {
            // Just idle - NPCService patrol handles movement
            return NodeStatus.Success;
        }
    }
}