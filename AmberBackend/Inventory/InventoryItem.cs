namespace AmberBackend.Inventory
{
    /// <summary>
    /// Represents an item instance in a player's inventory.
    /// </summary>
    public class InventoryItem
    {
        public string InventoryId { get; set; } // Unique ID for this stack
        public string ItemId { get; set; }
        public int Quantity { get; set; }
        public int? SlotIndex { get; set; } // null = auto-find slot

        public ItemDefinition Definition { get; set; } // Reference to item definition
    }
}