using AmberBackend.Movement;
using System;
using System.Linq;

namespace AmberBackend.AI.Actions
{
    /// <summary>
    /// Scan for nearest player within aggro range and set as target.
    /// </summary>
    public class FindNearestPlayer : BehaviorNode
    {
        public override NodeStatus Execute(AIContext context)
        {
            // Get all entities in zone
            var allPositions = context.MovementService.GetAllEntitiesSnapshot();

            string nearestPlayerId = null;
            TilePosition nearestPosition = null;
            int nearestDistance = int.MaxValue;

            foreach (var entity in allPositions)
            {
                // Skip if it's the AI itself or an NPC
                if (entity.playerId == context.EntityId || entity.playerId.StartsWith("npc_"))
                    continue;

                // Calculate distance
                var entityPos = new TilePosition(entity.x, entity.y);
                int distance = Math.Abs(entityPos.X - context.CurrentPosition.X) +
                              Math.Abs(entityPos.Y - context.CurrentPosition.Y);

                // Check if within aggro range and closer than current nearest
                if (distance <= context.AggroRange && distance < nearestDistance)
                {
                    nearestPlayerId = entity.playerId;
                    nearestPosition = entityPos;
                    nearestDistance = distance;
                }
            }

            if (nearestPlayerId != null)
            {
                context.TargetPlayerId = nearestPlayerId;
                context.TargetPosition = nearestPosition;

                Console.WriteLine($"[AI:{context.EntityId}] Found target: {nearestPlayerId} at distance {nearestDistance}");
                return NodeStatus.Success;
            }

            // No players in range
            context.TargetPlayerId = null;
            context.TargetPosition = null;
            return NodeStatus.Failure;
        }
    }
}