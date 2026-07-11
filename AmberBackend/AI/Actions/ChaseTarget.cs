using AmberBackend.Movement;
using System;
using System.Collections.Generic;

namespace AmberBackend.AI.Actions
{
    public class ChaseTarget : BehaviorNode
    {
        private TilePosition _lastTargetPosition;
        private List<TilePosition> _currentPath;
        private DateTime _nextMoveTime = DateTime.MinValue;
        private const float MOVE_SPEED = 2f; // tiles per second

        public override NodeStatus Execute(AIContext context)
        {
            if (string.IsNullOrEmpty(context.TargetPlayerId) || context.TargetPosition == null)
                return NodeStatus.Failure;

            // Chebyshev distance
            int distance = Math.Max(
                Math.Abs(context.TargetPosition.X - context.CurrentPosition.X),
                Math.Abs(context.TargetPosition.Y - context.CurrentPosition.Y)
            );

            if (distance <= 1)
                return NodeStatus.Success;

            // Respect movement speed
            if (DateTime.UtcNow < _nextMoveTime)
                return NodeStatus.Running;

            // Re-path if target moved or no path exists
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

                if (_currentPath.Count > 0 &&
                    _currentPath[0].X == context.CurrentPosition.X &&
                    _currentPath[0].Y == context.CurrentPosition.Y)
                {
                    _currentPath.RemoveAt(0);
                }

                if (_currentPath.Count == 0)
                    return NodeStatus.Success;

                Console.WriteLine($"[AI:{context.EntityId}] Recalculated path to target ({_currentPath.Count} steps)");
            }

            var nextCell = _currentPath[0];

            int dx = Math.Abs(nextCell.X - context.CurrentPosition.X);
            int dy = Math.Abs(nextCell.Y - context.CurrentPosition.Y);

            if (dx + dy != 1)
            {
                Console.WriteLine($"[AI:{context.EntityId}] Invalid move detected, recalculating");
                _currentPath = null;
                return NodeStatus.Running;
            }

            // Trigger movement + set cooldown
            TriggerMove(context, context.CurrentPosition, nextCell);
            _currentPath.RemoveAt(0);
            _nextMoveTime = DateTime.UtcNow.AddSeconds(1.0 / MOVE_SPEED);

            return NodeStatus.Running;
        }

        private void TriggerMove(AIContext context, TilePosition from, TilePosition to)
        {
            float duration = 1.0f / MOVE_SPEED;

            context.MovementService.BroadcastNpcMovement(
                context.EntityId, from, to, duration
            );

            context.MovementService.OnPositionUpdate(context.EntityId, to);
            context.CurrentPosition = to;

            Console.WriteLine($"[AI:{context.EntityId}] Moving: ({from.X},{from.Y}) -> ({to.X},{to.Y})");
        }

        public override void Reset()
        {
            _currentPath = null;
            _lastTargetPosition = null;
            // Don't reset _nextMoveTime - it should persist across resets
        }
    }
}