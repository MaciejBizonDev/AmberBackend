using System;

namespace AmberBackend.AI.Conditions
{
    /// <summary>
    /// Checks if a player is within range of this AI.
    /// </summary>
    public class IsPlayerInRange : BehaviorNode
    {
        private readonly int _range;

        public IsPlayerInRange(int range)
        {
            _range = range;
        }

        public override NodeStatus Execute(AIContext context)
        {
            if (string.IsNullOrEmpty(context.TargetPlayerId) || context.TargetPosition == null)
            {
                return NodeStatus.Failure;
            }

            int distance = Math.Max(
                Math.Abs(context.TargetPosition.X - context.CurrentPosition.X),
                Math.Abs(context.TargetPosition.Y - context.CurrentPosition.Y)
            );

            return distance <= _range ? NodeStatus.Success : NodeStatus.Failure;
        }
    }
}