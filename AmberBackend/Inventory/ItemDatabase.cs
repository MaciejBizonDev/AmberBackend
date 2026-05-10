using System;
using System.Collections.Generic;
using Npgsql;

namespace AmberBackend.Inventory
{
    public class ItemDatabase
    {
        private readonly string _connectionString;

        public ItemDatabase(string host, int port, string database, string username, string password)
        {
            _connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";
            InitializeDatabase();
            SeedItems();
        }

        private void InitializeDatabase()
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();

            // Items table
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Items (
                    ItemId TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Description TEXT,
                    IconPath TEXT,
                    ItemType TEXT NOT NULL,
                    MaxStackSize INTEGER DEFAULT 1,
                    BuyPriceCopper INTEGER DEFAULT 0,
                    SellPriceCopper INTEGER DEFAULT 0,
                    Damage INTEGER,
                    Defense INTEGER,
                    HealthRestore INTEGER
                )";
            command.ExecuteNonQuery();

            // PlayerInventory table
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS PlayerInventory (
                    InventoryId TEXT PRIMARY KEY,
                    PlayerId TEXT NOT NULL,
                    ItemId TEXT NOT NULL,
                    Quantity INTEGER NOT NULL,
                    SlotIndex INTEGER,
                    FOREIGN KEY (PlayerId) REFERENCES Players(PlayerId),
                    FOREIGN KEY (ItemId) REFERENCES Items(ItemId)
                )";
            command.ExecuteNonQuery();

            // MerchantInventory table
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS MerchantInventory (
                    MerchantId TEXT NOT NULL,
                    ItemId TEXT NOT NULL,
                    Stock INTEGER,
                    RestockTime INTEGER,
                    PRIMARY KEY (MerchantId, ItemId),
                    FOREIGN KEY (ItemId) REFERENCES Items(ItemId)
                )";
            command.ExecuteNonQuery();

            Console.WriteLine("[ItemDatabase] Database initialized");
        }

        private void SeedItems()
        {
            // Add predefined items
            SaveItemDefinition(ItemDefinition.HealthPotion);
            SaveItemDefinition(ItemDefinition.IronSword);
            SaveItemDefinition(ItemDefinition.LeatherArmor);
            SaveItemDefinition(ItemDefinition.QuestScroll);
        }

        public void SaveItemDefinition(ItemDefinition item)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Items (ItemId, Name, Description, IconPath, ItemType, MaxStackSize, 
                                   BuyPriceCopper, SellPriceCopper, Damage, Defense, HealthRestore)
                VALUES (@itemId, @name, @desc, @icon, @type, @maxStack, @buy, @sell, @damage, @defense, @health)
                ON CONFLICT(ItemId) DO UPDATE SET
                    Name = @name,
                    Description = @desc,
                    IconPath = @icon,
                    ItemType = @type,
                    MaxStackSize = @maxStack,
                    BuyPriceCopper = @buy,
                    SellPriceCopper = @sell,
                    Damage = @damage,
                    Defense = @defense,
                    HealthRestore = @health";

            command.Parameters.AddWithValue("itemId", item.ItemId);
            command.Parameters.AddWithValue("name", item.Name);
            command.Parameters.AddWithValue("desc", item.Description ?? "");
            command.Parameters.AddWithValue("icon", item.IconPath ?? "");
            command.Parameters.AddWithValue("type", item.ItemType.ToString());
            command.Parameters.AddWithValue("maxStack", item.MaxStackSize);
            command.Parameters.AddWithValue("buy", item.BuyPrice?.ToCopper() ?? 0);
            command.Parameters.AddWithValue("sell", item.SellPrice?.ToCopper() ?? 0);
            command.Parameters.AddWithValue("damage", (object)item.Damage ?? DBNull.Value);
            command.Parameters.AddWithValue("defense", (object)item.Defense ?? DBNull.Value);
            command.Parameters.AddWithValue("health", (object)item.HealthRestore ?? DBNull.Value);

            command.ExecuteNonQuery();
        }

        public ItemDefinition LoadItemDefinition(string itemId)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT ItemId, Name, Description, IconPath, ItemType, MaxStackSize,
                       BuyPriceCopper, SellPriceCopper, Damage, Defense, HealthRestore
                FROM Items
                WHERE ItemId = @itemId";
            command.Parameters.AddWithValue("itemId", itemId);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new ItemDefinition
                {
                    ItemId = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    IconPath = reader.IsDBNull(3) ? null : reader.GetString(3),
                    ItemType = Enum.Parse<ItemType>(reader.GetString(4)),
                    MaxStackSize = reader.GetInt32(5),
                    BuyPrice = Currency.FromCopper(reader.GetInt32(6)),
                    SellPrice = Currency.FromCopper(reader.GetInt32(7)),
                    Damage = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                    Defense = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                    HealthRestore = reader.IsDBNull(10) ? null : reader.GetInt32(10)
                };
            }

            return null;
        }
    }
}