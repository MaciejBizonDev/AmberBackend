namespace AmberBackend.Inventory
{
    /// <summary>
    /// Defines what a merchant sells.
    /// </summary>
    public class MerchantData
    {
        public string MerchantId { get; set; }
        public string MerchantName { get; set; }
        public List<MerchantItem> Items { get; set; } = new List<MerchantItem>();

        public static MerchantData TestMerchant => new MerchantData
        {
            MerchantId = "npc_merchant_1",
            MerchantName = "Traveling Merchant",
            Items = new List<MerchantItem>
            {
                new MerchantItem
                {
                    ItemId = "health_potion",
                    Stock = null, // Unlimited
                    PriceMultiplier = 1.0f // Regular price
                },
                new MerchantItem
                {
                    ItemId = "iron_sword",
                    Stock = 3,
                    PriceMultiplier = 1.0f
                },
                new MerchantItem
                {
                    ItemId = "leather_armor",
                    Stock = 5,
                    PriceMultiplier = 1.0f
                }
            }
        };
    }

    public class MerchantItem
    {
        public string ItemId { get; set; }
        public int? Stock { get; set; } // null = unlimited
        public float PriceMultiplier { get; set; } = 1.0f; // 1.0 = normal price, 1.5 = 50% markup
    }
}