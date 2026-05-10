namespace AmberBackend.Inventory
{
    /// <summary>
    /// Represents multi-tier currency.
    /// 100 Copper = 1 Silver, 100 Silver = 1 Gold
    /// </summary>
    public class Currency
    {
        public int Copper { get; set; }
        public int Silver { get; set; }
        public int Gold { get; set; }

        public Currency(int copper = 0, int silver = 0, int gold = 0)
        {
            Copper = copper;
            Silver = silver;
            Gold = gold;
            Normalize();
        }

        /// <summary>
        /// Convert total to copper for comparison.
        /// </summary>
        public int ToCopper()
        {
            return Gold * 10000 + Silver * 100 + Copper;
        }

        /// <summary>
        /// Create currency from total copper.
        /// </summary>
        public static Currency FromCopper(int totalCopper)
        {
            int gold = totalCopper / 10000;
            int silver = (totalCopper % 10000) / 100;
            int copper = totalCopper % 100;
            return new Currency(copper, silver, gold);
        }

        /// <summary>
        /// Normalize currency (convert 100 copper → 1 silver, etc).
        /// </summary>
        public void Normalize()
        {
            // Convert copper to silver
            if (Copper >= 100)
            {
                Silver += Copper / 100;
                Copper = Copper % 100;
            }

            // Convert silver to gold
            if (Silver >= 100)
            {
                Gold += Silver / 100;
                Silver = Silver % 100;
            }

            // Handle negative values
            while (Copper < 0)
            {
                Silver -= 1;
                Copper += 100;
            }

            while (Silver < 0)
            {
                Gold -= 1;
                Silver += 100;
            }
        }

        /// <summary>
        /// Add currency.
        /// </summary>
        public void Add(Currency other)
        {
            Copper += other.Copper;
            Silver += other.Silver;
            Gold += other.Gold;
            Normalize();
        }

        /// <summary>
        /// Subtract currency. Returns false if insufficient funds.
        /// </summary>
        public bool Subtract(Currency cost)
        {
            if (!CanAfford(cost))
                return false;

            Copper -= cost.Copper;
            Silver -= cost.Silver;
            Gold -= cost.Gold;
            Normalize();
            return true;
        }

        /// <summary>
        /// Check if can afford cost.
        /// </summary>
        public bool CanAfford(Currency cost)
        {
            return ToCopper() >= cost.ToCopper();
        }

        public override string ToString()
        {
            return $"{Gold}g {Silver}s {Copper}c";
        }
    }
}