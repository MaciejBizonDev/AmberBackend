namespace AmberBackend.Combat.Steps
{
    /// <summary>
    /// Applies damage to all affected entities.
    /// </summary>
    public class DamageStep : IAbilityStep
    {
        public int BaseDamage { get; set; }
        public float AttackPowerScaling { get; set; } = 0.5f; // How much of attack power to add

        public bool Execute(AbilityContext context, CombatService combatService)
        {
            if (context.AffectedEntities.Count == 0)
            {
                System.Console.WriteLine("[DamageStep] No targets to damage");
                return false;
            }

            // Calculate damage
            int damage = CalculateDamage(context);
            context.DamageAmount = damage;

            // Apply to all affected entities
            foreach (var entityId in context.AffectedEntities)
            {
                var stats = combatService.GetStats(entityId);
                if (stats != null && !stats.IsDead)
                {
                    stats.TakeDamage(damage);
                    System.Console.WriteLine($"[DamageStep] Dealt {damage} damage to {entityId}. HP: {stats.Hp}/{stats.MaxHp}");
                }
            }

            return true;
        }

        private int CalculateDamage(AbilityContext context)
        {
            int damage = BaseDamage;

            // Add attack power scaling
            if (context.SourceStats != null)
            {
                damage += (int)(context.SourceStats.AttackPower * AttackPowerScaling);
            }

            // Add variance (90% - 110%)
            var rng = new System.Random();
            float variance = 0.9f + (float)(rng.NextDouble() * 0.2);

            return (int)(damage * variance);
        }
    }
}