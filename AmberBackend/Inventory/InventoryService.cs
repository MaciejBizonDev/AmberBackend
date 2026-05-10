using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;

namespace AmberBackend.Inventory
{
    /// <summary>
    /// Manages player inventory operations.
    /// </summary>
    public class InventoryService
    {
        private readonly string _connectionString;
        private readonly ItemDatabase _itemDatabase;

        public InventoryService(string connectionString, ItemDatabase itemDatabase)
        {
            _connectionString = connectionString;
            _itemDatabase = itemDatabase;
        }

        /// <summary>
        /// Load player's entire inventory.
        /// </summary>
        public List<InventoryItem> LoadInventory(string playerId)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT InventoryId, PlayerId, ItemId, Quantity, SlotIndex
                FROM PlayerInventory
                WHERE PlayerId = @playerId
                ORDER BY SlotIndex";
            command.Parameters.AddWithValue("playerId", playerId);

            var items = new List<InventoryItem>();
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var item = new InventoryItem
                {
                    InventoryId = reader.GetString(0),
                    ItemId = reader.GetString(2),
                    Quantity = reader.GetInt32(3),
                    SlotIndex = reader.IsDBNull(4) ? null : reader.GetInt32(4)
                };

                // Load item definition
                item.Definition = _itemDatabase.LoadItemDefinition(item.ItemId);
                items.Add(item);
            }

            return items;
        }

        /// <summary>
        /// Add item to player inventory (auto-stack if possible).
        /// </summary>
        public bool AddItem(string playerId, string itemId, int quantity, int? slotIndex = null)
        {
            var itemDef = _itemDatabase.LoadItemDefinition(itemId);
            if (itemDef == null)
            {
                Console.WriteLine($"[InventoryService] Item {itemId} not found");
                return false;
            }

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            // Try to stack with existing items first
            if (itemDef.MaxStackSize > 1)
            {
                var existingStacks = FindExistingStacks(connection, playerId, itemId, itemDef.MaxStackSize);

                foreach (var stack in existingStacks)
                {
                    int spaceInStack = itemDef.MaxStackSize - stack.Quantity;
                    if (spaceInStack > 0)
                    {
                        int amountToAdd = Math.Min(quantity, spaceInStack);
                        UpdateStackQuantity(connection, stack.InventoryId, stack.Quantity + amountToAdd);
                        quantity -= amountToAdd;

                        if (quantity == 0)
                            return true; // All items stacked
                    }
                }
            }

            // Create new stack(s) for remaining quantity
            while (quantity > 0)
            {
                int stackSize = Math.Min(quantity, itemDef.MaxStackSize);

                // Find empty slot if no slot specified
                if (slotIndex == null)
                {
                    slotIndex = FindEmptySlot(connection, playerId);
                    if (slotIndex == null)
                    {
                        Console.WriteLine($"[InventoryService] No empty slots for {playerId}");
                        return false; // Inventory full
                    }
                }

                CreateNewStack(connection, playerId, itemId, stackSize, slotIndex.Value);
                quantity -= stackSize;
                slotIndex = null; // Find next slot for additional stacks
            }

            return true;
        }

        /// <summary>
        /// Remove item from inventory.
        /// </summary>
        public bool RemoveItem(string playerId, string itemId, int quantity)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            var stacks = FindExistingStacks(connection, playerId, itemId, int.MaxValue);
            int totalAvailable = stacks.Sum(s => s.Quantity);

            if (totalAvailable < quantity)
            {
                Console.WriteLine($"[InventoryService] Not enough {itemId}. Has: {totalAvailable}, needs: {quantity}");
                return false;
            }

            // Remove from stacks
            int remaining = quantity;
            foreach (var stack in stacks.OrderBy(s => s.Quantity))
            {
                if (remaining == 0) break;

                if (stack.Quantity <= remaining)
                {
                    // Remove entire stack
                    DeleteStack(connection, stack.InventoryId);
                    remaining -= stack.Quantity;
                }
                else
                {
                    // Reduce stack
                    UpdateStackQuantity(connection, stack.InventoryId, stack.Quantity - remaining);
                    remaining = 0;
                }
            }

            return true;
        }

        /// <summary>
        /// Move item to different slot.
        /// </summary>
        public bool MoveItem(string playerId, string inventoryId, int newSlotIndex)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE PlayerInventory
                SET SlotIndex = @newSlot
                WHERE InventoryId = @invId AND PlayerId = @playerId";

            command.Parameters.AddWithValue("newSlot", newSlotIndex);
            command.Parameters.AddWithValue("invId", inventoryId);
            command.Parameters.AddWithValue("playerId", playerId);

            int rows = command.ExecuteNonQuery();
            return rows > 0;
        }

        /// <summary>
        /// Get player's currency.
        /// </summary>
        public Currency GetCurrency(string playerId)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Copper, Silver, Gold
                FROM Players
                WHERE PlayerId = @playerId";
            command.Parameters.AddWithValue("playerId", playerId);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new Currency(
                    reader.GetInt32(0),
                    reader.GetInt32(1),
                    reader.GetInt32(2)
                );
            }

            return new Currency();
        }

        /// <summary>
        /// Update player's currency.
        /// </summary>
        public void SetCurrency(string playerId, Currency currency)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Players
                SET Copper = @copper, Silver = @silver, Gold = @gold
                WHERE PlayerId = @playerId";

            command.Parameters.AddWithValue("copper", currency.Copper);
            command.Parameters.AddWithValue("silver", currency.Silver);
            command.Parameters.AddWithValue("gold", currency.Gold);
            command.Parameters.AddWithValue("playerId", playerId);

            command.ExecuteNonQuery();
        }

        // Helper methods
        private List<InventoryItem> FindExistingStacks(NpgsqlConnection connection, string playerId, string itemId, int maxStackSize)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT InventoryId, ItemId, Quantity, SlotIndex
                FROM PlayerInventory
                WHERE PlayerId = @playerId AND ItemId = @itemId AND Quantity < @maxStack
                ORDER BY Quantity DESC";

            command.Parameters.AddWithValue("playerId", playerId);
            command.Parameters.AddWithValue("itemId", itemId);
            command.Parameters.AddWithValue("maxStack", maxStackSize);

            var stacks = new List<InventoryItem>();
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                stacks.Add(new InventoryItem
                {
                    InventoryId = reader.GetString(0),
                    ItemId = reader.GetString(1),
                    Quantity = reader.GetInt32(2),
                    SlotIndex = reader.IsDBNull(3) ? null : reader.GetInt32(3)
                });
            }

            return stacks;
        }

        private int? FindEmptySlot(NpgsqlConnection connection, string playerId)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT SlotIndex FROM PlayerInventory WHERE PlayerId = @playerId AND SlotIndex IS NOT NULL";
            command.Parameters.AddWithValue("playerId", playerId);

            var usedSlots = new HashSet<int>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                usedSlots.Add(reader.GetInt32(0));
            }

            // Find first empty slot (0-49)
            for (int i = 0; i < 50; i++)
            {
                if (!usedSlots.Contains(i))
                    return i;
            }

            return null; // Inventory full
        }

        private void CreateNewStack(NpgsqlConnection connection, string playerId, string itemId, int quantity, int slotIndex)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO PlayerInventory (InventoryId, PlayerId, ItemId, Quantity, SlotIndex)
                VALUES (@invId, @playerId, @itemId, @quantity, @slot)";

            command.Parameters.AddWithValue("invId", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("playerId", playerId);
            command.Parameters.AddWithValue("itemId", itemId);
            command.Parameters.AddWithValue("quantity", quantity);
            command.Parameters.AddWithValue("slot", slotIndex);

            command.ExecuteNonQuery();
        }

        private void UpdateStackQuantity(NpgsqlConnection connection, string inventoryId, int newQuantity)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE PlayerInventory
                SET Quantity = @quantity
                WHERE InventoryId = @invId";

            command.Parameters.AddWithValue("quantity", newQuantity);
            command.Parameters.AddWithValue("invId", inventoryId);

            command.ExecuteNonQuery();
        }

        private void DeleteStack(NpgsqlConnection connection, string inventoryId)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"
                DELETE FROM PlayerInventory
                WHERE InventoryId = @invId";

            command.Parameters.AddWithValue("invId", inventoryId);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Get merchant's inventory.
        /// </summary>
        public List<MerchantInventoryItem> GetMerchantInventory(string merchantId)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
        SELECT m.ItemId, m.Stock, m.RestockTime
        FROM MerchantInventory m
        WHERE m.MerchantId = @merchantId";
            command.Parameters.AddWithValue("merchantId", merchantId);

            var items = new List<MerchantInventoryItem>();
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var itemId = reader.GetString(0);
                var itemDef = _itemDatabase.LoadItemDefinition(itemId);

                if (itemDef != null)
                {
                    items.Add(new MerchantInventoryItem
                    {
                        ItemId = itemId,
                        Definition = itemDef,
                        Stock = reader.IsDBNull(1) ? null : (int?)reader.GetInt32(1),
                        RestockTime = reader.IsDBNull(2) ? null : (int?)reader.GetInt32(2)
                    });
                }
            }

            return items;
        }

        /// <summary>
        /// Purchase item from merchant.
        /// </summary>
        public bool PurchaseItem(string playerId, string merchantId, string itemId, int quantity)
        {
            var itemDef = _itemDatabase.LoadItemDefinition(itemId);
            if (itemDef == null)
            {
                Console.WriteLine($"[InventoryService] Item {itemId} not found");
                return false;
            }

            // Calculate total cost
            var totalCost = Currency.FromCopper(itemDef.BuyPrice.ToCopper() * quantity);

            // Check if player can afford it
            var playerCurrency = GetCurrency(playerId);
            if (!playerCurrency.CanAfford(totalCost))
            {
                Console.WriteLine($"[InventoryService] {playerId} cannot afford {quantity}x {itemId}");
                return false;
            }

            // Check merchant stock
            var merchantItem = GetMerchantItem(merchantId, itemId);
            if (merchantItem != null && merchantItem.Stock.HasValue)
            {
                if (merchantItem.Stock.Value < quantity)
                {
                    Console.WriteLine($"[InventoryService] Merchant {merchantId} has insufficient stock of {itemId}");
                    return false;
                }
            }

            // Deduct currency
            playerCurrency.Subtract(totalCost);
            SetCurrency(playerId, playerCurrency);

            // Add item to inventory
            if (!AddItem(playerId, itemId, quantity))
            {
                // Refund if inventory full
                playerCurrency.Add(totalCost);
                SetCurrency(playerId, playerCurrency);
                Console.WriteLine($"[InventoryService] {playerId} inventory full, refunded");
                return false;
            }

            // Reduce merchant stock
            if (merchantItem != null && merchantItem.Stock.HasValue)
            {
                UpdateMerchantStock(merchantId, itemId, merchantItem.Stock.Value - quantity);
            }

            Console.WriteLine($"[InventoryService] {playerId} purchased {quantity}x {itemId} for {totalCost}");
            return true;
        }

        private MerchantInventoryItem GetMerchantItem(string merchantId, string itemId)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Stock, RestockTime
                FROM MerchantInventory
                WHERE MerchantId = @merchantId AND ItemId = @itemId";
            command.Parameters.AddWithValue("merchantId", merchantId);
            command.Parameters.AddWithValue("itemId", itemId);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new MerchantInventoryItem
                {
                    ItemId = itemId,
                    Stock = reader.IsDBNull(0) ? null : (int?)reader.GetInt32(0),
                    RestockTime = reader.IsDBNull(1) ? null : (int?)reader.GetInt32(1)
                };
            }

            return null;
        }

        private void UpdateMerchantStock(string merchantId, string itemId, int newStock)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE MerchantInventory
                SET Stock = @stock
                WHERE MerchantId = @merchantId AND ItemId = @itemId";
            command.Parameters.AddWithValue("stock", newStock);
            command.Parameters.AddWithValue("merchantId", merchantId);
            command.Parameters.AddWithValue("itemId", itemId);

            command.ExecuteNonQuery();
        }

        public class MerchantInventoryItem
        {
            public string ItemId { get; set; }
            public ItemDefinition Definition { get; set; }
            public int? Stock { get; set; } // null = unlimited
            public int? RestockTime { get; set; }
        }
    }
}