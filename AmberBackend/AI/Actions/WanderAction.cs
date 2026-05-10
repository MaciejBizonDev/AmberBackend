using AmberBackend.Movement;
using System;

namespace AmberBackend.AI.Actions
{
    /// <summary>
    /// Wander randomly around spawn point.
    /// </summary>
    public class WanderAction : BehaviorNode
    {
        private TilePosition _wanderTarget;
        private float _idleTimer = 0f;
        private Random _random = new Random();

        public override NodeStatus Execute(AIContext context)
        {
            // Idle for a bit
            if (_idleTimer > 0f)
            {
                _idleTimer -= 0.1f;
                return NodeStatus.Running;
            }

            // Pick random direction
            if (_wanderTarget == null)
            {
                int maxWanderDistance = 3;
                int dx = _random.Next(-maxWanderDistance, maxWanderDistance + 1);
                int dy = _random.Next(-maxWanderDistance, maxWanderDistance + 1);

                _wanderTarget = new TilePosition(
                    context.SpawnPosition.X + dx,
                    context.SpawnPosition.Y + dy
                );
            }

            // Move toward wander target
            if (context.CurrentPosition.X == _wanderTarget.X &&
                context.CurrentPosition.Y == _wanderTarget.Y)
            {
                // Reached target, idle for a bit
                _wanderTarget = null;
                _idleTimer = _random.Next(2, 5); // 2-5 seconds
                return NodeStatus.Running;
            }

            // Move one step
            MoveToward(context, _wanderTarget);
            return NodeStatus.Running;
        }

        private void MoveToward(AIContext context, TilePosition target)
        {
            int dx = Math.Sign(target.X - context.CurrentPosition.X);
            int dy = Math.Sign(target.Y - context.CurrentPosition.Y);

            TilePosition nextCell;
            if (Math.Abs(dx) > Math.Abs(dy) && dx != 0)
                nextCell = new TilePosition(context.CurrentPosition.X + dx, context.CurrentPosition.Y);
            else if (dy != 0)
                nextCell = new TilePosition(context.CurrentPosition.X, context.CurrentPosition.Y + dy);
            else
                return;

            // Check if move is valid
            // TODO: Add walkability check

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
            _wanderTarget = null;
            _idleTimer = 0f;
        }
    }
}