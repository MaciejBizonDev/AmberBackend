namespace AmberBackend.Inventory
{
    public enum ItemType
    {
        Weapon,
        Armor,
        Consumable,
        QuestItem,
        Misc
    }

    /// <summary>
    /// Defines an item's properties.
    /// </summary>
    public class ItemDefinition
    {
        public string ItemId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string IconPath { get; set; }
        public ItemType ItemType { get; set; }
        public int MaxStackSize { get; set; } = 1;
        public Currency BuyPrice { get; set; }
        public Currency SellPrice { get; set; }

        // Stats
        public int? Damage { get; set; }
        public int? Defense { get; set; }
        public int? HealthRestore { get; set; }

        // Predefined items
        public static ItemDefinition HealthPotion => new ItemDefinition
        {
            ItemId = "health_potion",
            Name = "Health Potion",
            Description = "Restores 50 HP",
            IconPath = "items/health_potion",
            ItemType = ItemType.Consumable,
            MaxStackSize = 99,
            BuyPrice = Currency.FromCopper(50), // 50 copper
            SellPrice = Currency.FromCopper(25),
            HealthRestore = 50
        };

        public static ItemDefinition IronSword => new ItemDefinition
        {
            ItemId = "iron_sword",
            Name = "Iron Sword",
            Description = "A sturdy iron blade",
            IconPath = "items/iron_sword",
            ItemType = ItemType.Weapon,
            MaxStackSize = 1,
            BuyPrice = Currency.FromCopper(500), // 5 silver
            SellPrice = Currency.FromCopper(250),
            Damage = 15
        };

        public static ItemDefinition LeatherArmor => new ItemDefinition
        {
            ItemId = "leather_armor",
            Name = "Leather Armor",
            Description = "Basic protection",
            IconPath = "items/leather_armor",
            ItemType = ItemType.Armor,
            MaxStackSize = 1,
            BuyPrice = Currency.FromCopper(300),
            SellPrice = Currency.FromCopper(150),
            Defense = 10
        };

        public static ItemDefinition QuestScroll => new ItemDefinition
        {
            ItemId = "quest_scroll",
            Name = "Mysterious Scroll",
            Description = "A quest item",
            IconPath = "items/scroll",
            ItemType = ItemType.QuestItem,
            MaxStackSize = 1,
            BuyPrice = Currency.FromCopper(0), // Can't buy
            SellPrice = Currency.FromCopper(0)  // Can't sell
        };
    }
}