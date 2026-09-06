namespace AmberBackend.Combat
{
    /// <summary>
    /// Ability metadata loaded from DB (for sending to client).
    /// Execution steps still live in AbilityDefinition (code).
    /// </summary>
    public class AbilityData
    {
        public string AbilityId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string IconPath { get; set; }
        public float Cooldown { get; set; }
        public int ManaCost { get; set; }
        public int Range { get; set; }
        public bool IsAutoAttack { get; set; }
        public int? SlotIndex { get; set; }  // player-specific, from player_abilities
    }
}