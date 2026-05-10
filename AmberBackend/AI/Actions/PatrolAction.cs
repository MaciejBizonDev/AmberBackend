using AmberBackend.Movement;
using System;
using System.Collections.Generic;

namespace AmberBackend.AI.Actions
{
    /// <summary>
    /// Patrol along a predefined path.
    /// </summary>
    public class PatrolAction : BehaviorNode
    {
        private List<TilePosition> _patrolPath;
        private int _currentWaypointIndex = 0;
        private float _waitTimer = 0f;
        private const float WaitTimeAtWaypoint = 2f;

        public override NodeStatus Execute(AIContext context)
        {
            // Get patrol path from context
            if (context.PatrolPath == null || context.PatrolPath.Count == 0)
            {
                // No patrol path, just idle
                return NodeStatus.Success;
            }

            _patrolPath = context.PatrolPath;

            // Waiting at waypoint?
            if (_waitTimer > 0f)
            {
                _waitTimer -= 0.1f;
                return NodeStatus.Running;
            }

            // Get current waypoint
            var targetWaypoint = _patrolPath[_currentWaypointIndex];

            // Already at waypoint?
            if (context.CurrentPosition.X == targetWaypoint.X &&
                context.CurrentPosition.Y == targetWaypoint.Y)
            {
                // Wait here, then move to next
                _waitTimer = WaitTimeAtWaypoint;
                _currentWaypointIndex = (_currentWaypointIndex + 1) % _patrolPath.Count;
                return NodeStatus.Running;
            }

            // Move toward waypoint
            MoveTowardWaypoint(context, targetWaypoint);
            return NodeStatus.Running;
        }

        private void MoveTowardWaypoint(AIContext context, TilePosition waypoint)
        {
            // Calculate direction
            int dx = Math.Sign(waypoint.X - context.CurrentPosition.X);
            int dy = Math.Sign(waypoint.Y - context.CurrentPosition.Y);

            // Prefer cardinal movement
            TilePosition nextCell;
            if (dx != 0 && dy != 0)
            {
                // Diagonal - pick one axis
                if (Math.Abs(waypoint.X - context.CurrentPosition.X) >
                    Math.Abs(waypoint.Y - context.CurrentPosition.Y))
                {
                    nextCell = new TilePosition(context.CurrentPosition.X + dx, context.CurrentPosition.Y);
                }
                else
                {
                    nextCell = new TilePosition(context.CurrentPosition.X, context.CurrentPosition.Y + dy);
                }
            }
            else if (dx != 0)
            {
                nextCell = new TilePosition(context.CurrentPosition.X + dx, context.CurrentPosition.Y);
            }
            else if (dy != 0)
            {
                nextCell = new TilePosition(context.CurrentPosition.X, context.CurrentPosition.Y + dy);
            }
            else
            {
                return; // Already at waypoint
            }

            // Trigger movement
            context.MovementService.BroadcastNpcMovement(
                context.EntityId,
                context.CurrentPosition,
                nextCell,
                0.5f
            );
            context.MovementService.OnPositionUpdate(context.EntityId, nextCell);
            context.CurrentPosition = nextCell;
        }

        public override void Reset()
        {
            _waitTimer = 0f;
        }
    }
}