using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

public class TilemapRepository
{
    private readonly HashSet<(int x, int y)> _walkable = new();
    private readonly HashSet<(int x, int y)> _obstacle = new();

    public TilemapRepository(string resourcePath)
    {
        Load(Path.Combine(resourcePath, "walkableTiles.json"), _walkable);
        Load(Path.Combine(resourcePath, "obstacleTiles.json"), _obstacle);

        System.Console.WriteLine($"[Tilemaps] Walkable={_walkable.Count} Obstacle={_obstacle.Count}");
    }

    private static void Load(string path, HashSet<(int, int)> set)
    {
        if (!File.Exists(path))
        {
            System.Console.WriteLine($"[Tilemaps] Missing: {path}");
            return;
        }

        var text = File.ReadAllText(path);
        var data = JsonConvert.DeserializeObject<TilemapData>(text);
        if (data?.Tiles != null)
        {
            foreach (var t in data.Tiles)
                set.Add((t.X, t.Y));
        }
    }

    // NEW: permissive rule — if walkable set is sparse, “not obstacle” is enough.
    public bool IsWalkable(TilePosition pos)
    {
        // hard block if obstacle
        if (_obstacle.Contains((pos.X, pos.Y))) return false;

        // if you painted a full whitelist of walkable tiles, enforce it
        if (_walkable.Count > 0) return _walkable.Contains((pos.X, pos.Y));

        // otherwise consider anything not in obstacle as walkable
        return true;
    }

    // Optional helpers for diagnostics
    public bool IsObstacle(TilePosition pos) => _obstacle.Contains((pos.X, pos.Y));
    public bool IsExplicitWalkable(TilePosition pos) => _walkable.Contains((pos.X, pos.Y));
}

public class TilemapData
{
    [JsonProperty("tiles")] public List<TilePosition> Tiles { get; set; }
}

//public class TilePosition
//{
//    [JsonProperty("x")] public int X { get; set; }
//    [JsonProperty("y")] public int Y { get; set; }
//}
