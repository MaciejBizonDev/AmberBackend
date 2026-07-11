namespace AmberBackend.Combat.Steps
{
    /// <summary>
    /// Validates target selection.
    /// Ensures target exists, is in range, and is valid.
    /// </summary>
    public class TargetSelectionStep : IAbilityStep
    {
        public TargetType TargetType { get; set; }
        public int Range { get; set; }

        public bool Execute(AbilityContext context, CombatService combatService)
        {
            // Validate target exists
            var targetStats = combatService.GetStats(context.TargetId);
            if (targetStats == null)
            {
                System.Console.WriteLine($"[TargetSelectionStep] Target {context.TargetId} not found");
                return false;
            }

            // Check if target is dead
            if (targetStats.IsDead)
            {
                System.Console.WriteLine($"[TargetSelectionStep] Target {context.TargetId} is dead");
                return false;
            }

            if (!targetStats.IsAttackable)
            {
                System.Console.WriteLine($"[TargetSelectionStep] Target {context.TargetId} is not attackable");
                return false;
            }

            // Validate range
            int distance = System.Math.Max(
                System.Math.Abs(context.TargetPosition.X - context.SourcePosition.X),
                System.Math.Abs(context.TargetPosition.Y - context.SourcePosition.Y)
            );

            if (distance > Range)
            {
                System.Console.WriteLine($"[TargetSelectionStep] Target out of range. Distance: {distance}, Max: {Range}");
                return false;
            }

            // Store target in affected entities
            context.AffectedEntities.Add(context.TargetId);
            context.ImpactPoint = context.TargetPosition;

            System.Console.WriteLine($"[TargetSelectionStep] Target validated: {context.TargetId}");
            return true;
        }
    }

    public enum TargetType
    {
        SingleEnemy,
        SingleAlly,
        Self,
        Ground
    }
}