using System;

namespace AmberBackend.AI.Conditions
{
    /// <summary>
    /// Check if any player is within range.
    /// </summary>
    public class IsPlayerNearby : BehaviorNode
    {
        private readonly int _range;

        public IsPlayerNearby(int range)
        {
            _range = range;
        }

        public override NodeStatus Execute(AIContext context)
        {
            // Check if we have a target within range
            if (!string.IsNullOrEmpty(context.TargetPlayerId) && context.TargetPosition != null)
            {
                int distance = Math.Abs(context.TargetPosition.X - context.CurrentPosition.X) +
                              Math.Abs(context.TargetPosition.Y - context.CurrentPosition.Y);

                if (distance <= _range)
                    return NodeStatus.Success;
            }

            return NodeStatus.Failure;
        }
    }
}