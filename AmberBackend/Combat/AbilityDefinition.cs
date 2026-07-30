using System.Collections.Generic;
using AmberBackend.Combat.Steps;

namespace AmberBackend.Combat
{
    public class AbilityDefinition
    {
        public string AbilityId { get; set; }
        public string Name { get; set; }
        public float Cooldown { get; set; }
        public int ManaCost { get; set; }
        public List<IAbilityStep> Steps { get; set; } = new List<IAbilityStep>();

        // Predefined abilities
        public static AbilityDefinition BasicAttack => new AbilityDefinition
        {
            AbilityId = "basic_attack",
            Name = "Basic Attack",
            Cooldown = 1.0f,
            ManaCost = 0,
            Steps = new List<IAbilityStep>
            {
                new TargetSelectionStep { TargetType = TargetType.SingleEnemy, Range = 1 },
                new DamageStep { BaseDamage = 10, AttackPowerScaling = 0.5f }
            }
        };

        public static AbilityDefinition Fireball => new AbilityDefinition
        {
            AbilityId = "fireball",
            Name = "Fireball",
            Cooldown = 3.0f,
            ManaCost = 3,
            Steps = new List<IAbilityStep>
            {
                new TargetSelectionStep { TargetType = TargetType.SingleEnemy, Range = 5 },
                new WaitStep { Seconds = 0.5f },
                new AreaEffectStep { Pattern = AreaPattern.Square3x3, OriginSource = OriginSource.ImpactPoint },
                new DamageStep { BaseDamage = 50, AttackPowerScaling = 1.0f }
            }
        };

        public static AbilityDefinition PowerStrike => new AbilityDefinition
        {
            AbilityId = "power_strike",
            Name = "Power Strike",
            Cooldown = 5.0f,  // 5 second cooldown
            ManaCost = 0,
            Steps = new List<IAbilityStep>
            {
                new Steps.TargetSelectionStep { TargetType = TargetType.SingleEnemy, Range = 1 },
                new Steps.DamageStep { BaseDamage = 10, AttackPowerScaling = 0.5f } // Much stronger
            }
        };
    }
}