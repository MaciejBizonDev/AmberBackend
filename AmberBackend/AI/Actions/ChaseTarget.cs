using AmberBackend.Movement;
using System;
using System.Linq;

namespace AmberBackend.AI.Actions
{
    public class ChaseTarget : BehaviorNode
    {
        private TilePosition _lastTargetPosition;
        private System.Collections.Generic.List<TilePosition> _currentPath;

        public override NodeStatus Execute(AIContext context)
        {
            if (string.IsNullOrEmpty(context.TargetPlayerId) || context.TargetPosition == null)
                return NodeStatus.Failure;

            // Check if already adjacent to target
            int distance = Math.Abs(context.TargetPosition.X - context.CurrentPosition.X) +
                          Math.Abs(context.TargetPosition.Y - context.CurrentPosition.Y);

            if (distance <= 1)
            {
                // Close enough to attack
                return NodeStatus.Success;
            }

            // Re-path if target moved significantly or no path exists
            bool needsRepath = _currentPath == null ||
                              _currentPath.Count == 0 ||
                              _lastTargetPosition == null ||
                              Math.Abs(_lastTargetPosition.X - context.TargetPosition.X) +
                              Math.Abs(_lastTargetPosition.Y - context.TargetPosition.Y) > 2;

            if (needsRepath)
            {
                _currentPath = context.Pathfinder.FindPath(
                    context.CurrentPosition,
                    context.TargetPosition
                );

                _lastTargetPosition = new TilePosition(context.TargetPosition.X, context.TargetPosition.Y);

                if (_currentPath == null || _currentPath.Count == 0)
                {
                    Console.WriteLine($"[AI:{context.EntityId}] Cannot find path to {context.TargetPlayerId}");
                    return NodeStatus.Failure;
                }

                // Remove first cell if it's our current position
                if (_currentPath.Count > 0 &&
                    _currentPath[0].X == context.CurrentPosition.X &&
                    _currentPath[0].Y == context.CurrentPosition.Y)
                {
                    _currentPath.RemoveAt(0);
                }

                if (_currentPath.Count == 0)
                {
                    // Path was just our current position, we're already there
                    return NodeStatus.Success;
                }

                Console.WriteLine($"[AI:{context.EntityId}] Recalculated path to target ({_currentPath.Count} steps)");
            }

            // Get next cell in path
            var nextCell = _currentPath[0];

            // Validate it's a cardinal move (no diagonals)
            int dx = Math.Abs(nextCell.X - context.CurrentPosition.X);
            int dy = Math.Abs(nextCell.Y - context.CurrentPosition.Y);

            if (dx + dy != 1)
            {
                // Path is invalid (diagonal or jump), recalculate
                Console.WriteLine($"[AI:{context.EntityId}] Invalid move detected ({context.CurrentPosition.X},{context.CurrentPosition.Y}) -> ({nextCell.X},{nextCell.Y}), recalculating");
                _currentPath = null;
                return NodeStatus.Running;
            }

            // Check if we're already at the next cell (move completed)
            if (context.CurrentPosition.X == nextCell.X && context.CurrentPosition.Y == nextCell.Y)
            {
                _currentPath.RemoveAt(0);

                if (_currentPath.Count == 0)
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
            float speed = 2f;
            float distance = Math.Abs(to.X - from.X) + Math.Abs(to.Y - from.Y);
            float duration = distance / speed;

            context.MovementService.BroadcastNpcMovement(
                context.EntityId,
                from,
                to,
                duration
            );

            context.MovementService.OnPositionUpdate(context.EntityId, to);
            context.CurrentPosition = to;

            Console.WriteLine($"[AI:{context.EntityId}] Moving to chase: ({from.X},{from.Y}) -> ({to.X},{to.Y})");
        }

        public override void Reset()
        {
            _currentPath = null;
            _lastTargetPosition = null;
        }
    }
}