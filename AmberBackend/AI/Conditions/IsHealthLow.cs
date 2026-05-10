namespace AmberBackend.AI.Conditions
{
    /// <summary>
    /// Checks if entity health is below threshold.
    /// </summary>
    public class IsHealthLow : BehaviorNode
    {
        private readonly float _threshold;

        public IsHealthLow(float threshold = 0.2f)
        {
            _threshold = threshold;
        }

        public override NodeStatus Execute(AIContext context)
        {
            if (context.Stats == null)
                return NodeStatus.Failure;

            float healthPercent = (float)context.Stats.Hp / context.Stats.MaxHp;
            return healthPercent <= _threshold ? NodeStatus.Success : NodeStatus.Failure;
        }
    }
}