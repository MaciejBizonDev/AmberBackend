using AmberBackend.AI;

namespace AmberBackend.Zones
{
    /// <summary>
    /// Defines what an enemy IS - stats and behavior.
    /// Loaded from the enemy_templates table.
    /// </summary>
    public class EnemyTemplate
    {
        public string TemplateId { get; set; }
        public string DisplayName { get; set; }
        public int MaxHp { get; set; }
        public int AttackPower { get; set; }
        public float Speed { get; set; }
        public AIBehaviorType AIBehavior { get; set; }
        public float RespawnTime { get; set; }
        public int AggroRange { get; set; }
        public string ModelId { get; set; }
    }
}