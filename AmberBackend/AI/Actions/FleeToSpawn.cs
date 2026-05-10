using AmberBackend.Movement;
using System;

namespace AmberBackend.AI.Actions
{
    /// <summary>
    /// Flee back to spawn position.
    /// </summary>
    public class FleeToSpawn : BehaviorNode
    {
        private System.Collections.Generic.List<TilePosition> _fleePath;

        public override NodeStatus Execute(AIContext context)
        {
            // Already at spawn?
            if (context.CurrentPosition.X == context.SpawnPosition.X &&
                context.CurrentPosition.Y == context.SpawnPosition.Y)
            {
                context.TargetPlayerId = null;
                context.TargetPosition = null;
                Console.WriteLine($"[AI:{context.EntityId}] Reached spawn, resetting");
                return NodeStatus.Success;
            }

            // Calculate path if needed
            if (_fleePath == null || _fleePath.Count == 0)
            {
                _fleePath = context.Pathfinder.FindPath(
                    context.CurrentPosition,
                    context.SpawnPosition
                );

                if (_fleePath == null || _fleePath.Count == 0)
                {
                    Console.WriteLine($"[AI:{context.EntityId}] Cannot find path to spawn!");
                    return NodeStatus.Failure;
                }

                // Remove first cell if it's our current position
                if (_fleePath.Count > 0 &&
                    _fleePath[0].X == context.CurrentPosition.X &&
                    _fleePath[0].Y == context.CurrentPosition.Y)
                {
                    _fleePath.RemoveAt(0);
                }

                if (_fleePath.Count == 0)
                {
                    return NodeStatus.Success;
                }
            }

            // Move to next cell
            var nextCell = _fleePath[0];

            if (context.CurrentPosition.X == nextCell.X && context.CurrentPosition.Y == nextCell.Y)
            {
                _fleePath.RemoveAt(0);

                if (_fleePath.Count == 0)
                {
                    return NodeStatus.Success;
                }

                return NodeStatus.Running;
            }

            // Trigger movement
            TriggerMove(context, context.CurrentPosition, nextCell);

            return NodeStatus.Running;
        }

        private void TriggerMove(AIContext context, TilePosition from, TilePosition to)
        {
            float speed = 3f;
            float distance = Math.Abs(to.X - from.X) + Math.Abs(to.Y - from.Y);
            float duration = distance / speed;

            context.MovementService.BroadcastNpcMovement(
                context.EntityId,
                from,
                to,
                duration
            );

            // IMPORTANT: Update server-side position immediately
            context.MovementService.OnPositionUpdate(context.EntityId, to);
            context.CurrentPosition = to;

            Console.WriteLine($"[AI:{context.EntityId}] Fleeing to spawn: ({from.X},{from.Y}) -> ({to.X},{to.Y})");
        }

        public override void Reset()
        {
            _fleePath = null;
        }
    }
}