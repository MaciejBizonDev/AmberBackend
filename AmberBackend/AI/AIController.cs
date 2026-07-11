using System;

namespace AmberBackend.AI
{
    /// <summary>
    /// Controls a single AI entity.
    /// Runs behavior tree and maintains state.
    /// </summary>
    public class AIController
    {
        public string EntityId { get; }
        public AIContext Context { get; }
        public BehaviorNode RootNode { get; }

        private float _deltaTimeAccumulator = 0f;

        public AIController(string entityId, BehaviorNode rootNode, AIContext context)
        {
            EntityId = entityId;
            RootNode = rootNode;
            Context = context;
        }

        /// <summary>
        /// Tick the AI. Call this regularly (e.g., 10 Hz).
        /// </summary>
        public void Tick(float deltaTime)
        {
            Context.CurrentTime += deltaTime;

            // Update current position from movement service
            var currentPos = Context.MovementService.GetEntityPosition(EntityId);
            if (currentPos != null)
            {
                Context.CurrentPosition = currentPos;
            }

            // Update stats from combat service
            var stats = Context.CombatService.GetStats(EntityId);
            if (stats != null)
            {
                Context.Stats = stats;
            }

            // Find nearest player as target
            UpdateTarget();

            // Execute behavior tree
            var status = RootNode.Execute(Context);

            if (status == NodeStatus.Success || status == NodeStatus.Failure)
            {
                RootNode.Reset();
            }
        }

        private void UpdateTarget()
        {
            // Update target position every tick if we have a target
            if (!string.IsNullOrEmpty(Context.TargetPlayerId))
            {
                var targetPos = Context.MovementService.GetEntityPosition(Context.TargetPlayerId);

                if (targetPos != null)
                {
                    Context.TargetPosition = targetPos;
                }
                else
                {
                    // Target no longer exists (logged out, died, etc.)
                    Console.WriteLine($"[AI:{EntityId}] Lost target {Context.TargetPlayerId} (no longer exists)");
                    Context.TargetPlayerId = null;
                    Context.TargetPosition = null;
                    return;
                }

                // Check if target escaped (out of chase range)
                int distance = Math.Max(
                    Math.Abs(Context.TargetPosition.X - Context.CurrentPosition.X),
                    Math.Abs(Context.TargetPosition.Y - Context.CurrentPosition.Y)
                );

                int chaseRange = Context.AggroRange * 2;
                if (distance > chaseRange)
                {
                    Console.WriteLine($"[AI:{EntityId}] Lost target {Context.TargetPlayerId} (too far: {distance} > {chaseRange})");
                    Context.TargetPlayerId = null;
                    Context.TargetPosition = null;
                }
            }
        }
    }
}