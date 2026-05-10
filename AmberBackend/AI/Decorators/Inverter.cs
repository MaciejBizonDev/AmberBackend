namespace AmberBackend.AI.Decorators
{
    /// <summary>
    /// Inverts the result of child node.
    /// Success -> Failure, Failure -> Success
    /// </summary>
    public class Inverter : BehaviorNode
    {
        private readonly BehaviorNode _child;

        public Inverter(BehaviorNode child)
        {
            _child = child;
        }

        public override NodeStatus Execute(AIContext context)
        {
            var result = _child.Execute(context);

            if (result == NodeStatus.Success)
                return NodeStatus.Failure;

            if (result == NodeStatus.Failure)
                return NodeStatus.Success;

            return NodeStatus.Running;
        }

        public override void Reset()
        {
            _child.Reset();
        }
    }
}