using AmberBackend.Combat;
using Npgsql;
using System;
using System.Collections.Generic;

namespace AmberBackend.Database
{
    /// <summary>
    /// Loads ability definitions and player ability assignments from the DB.
    /// </summary>
    public class AbilityRepository
    {
        private readonly string _connectionString;

        public AbilityRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Load the abilities a specific player has, joined with ability definitions.
        /// </summary>
        public List<AbilityData> LoadPlayerAbilities(string playerId)
        {
            var abilities = new List<AbilityData>();

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand(@"
                SELECT a.ability_id, a.name, a.description, a.icon_path,
                       a.cooldown, a.mana_cost, a.range, a.is_auto_attack,
                       pa.slot_index
                FROM player_abilities pa
                JOIN abilities a ON a.ability_id = pa.ability_id
                WHERE pa.player_id = @playerId
                ORDER BY pa.slot_index NULLS LAST", conn);
            cmd.Parameters.AddWithValue("playerId", playerId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                abilities.Add(new AbilityData
                {
                    AbilityId = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    IconPath = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Cooldown = reader.GetFloat(4),
                    ManaCost = reader.GetInt32(5),
                    Range = reader.GetInt32(6),
                    IsAutoAttack = reader.GetBoolean(7),
                    SlotIndex = reader.IsDBNull(8) ? null : reader.GetInt32(8)
                });
            }

            Console.WriteLine($"[AbilityRepository] Loaded {abilities.Count} abilities for {playerId}");
            return abilities;
        }
    }
}