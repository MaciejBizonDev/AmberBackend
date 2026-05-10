namespace AmberBackend.Combat
{
    public class PlayerStats
    {
        public string PlayerId { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public int Mana { get; set; }
        public int MaxMana { get; set; }
        public int Level { get; set; }
        public int AttackPower { get; set; }

        public PlayerStats()
        {
            // Default starting stats
            Hp = 10000;
            MaxHp = 10000;
            Mana = 50;
            MaxMana = 50;
            Level = 1;
            AttackPower = 10;
        }

        public void TakeDamage(int amount)
        {
            Hp = System.Math.Max(0, Hp - amount);
        }

        public void Heal(int amount)
        {
            Hp = System.Math.Min(MaxHp, Hp + amount);
        }

        public bool IsDead => Hp <= 0;
    }
}