using System.Collections.Generic;
using System.IO;
using System.Linq;
using AmberBackend.Movement;
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

    public bool IsWalkable(TilePosition pos)
    {
        if (_obstacle.Contains((pos.X, pos.Y))) return false;
        if (_walkable.Count > 0) return _walkable.Contains((pos.X, pos.Y));
        return true;
    }

    public bool IsObstacle(TilePosition pos) => _obstacle.Contains((pos.X, pos.Y));
    public bool IsExplicitWalkable(TilePosition pos) => _walkable.Contains((pos.X, pos.Y));

    // NEW: Export walkability data for client
    public WalkabilityData GetWalkabilityData()
    {
        // Determine bounds
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        var allTiles = new HashSet<(int, int)>(_walkable);
        allTiles.UnionWith(_obstacle);

        foreach (var (x, y) in allTiles)
        {
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x > maxX) maxX = x;
            if (y > maxY) maxY = y;
        }

        // If no tiles loaded, use default bounds
        if (allTiles.Count == 0)
        {
            minX = -50; minY = -50;
            maxX = 50; maxY = 50;
        }

        var data = new WalkabilityData
        {
            MinX = minX,
            MinY = minY,
            MaxX = maxX,
            MaxY = maxY,
            WalkableTiles = new List<WalkableTile>()
        };

        // Export all walkable tiles
        if (_walkable.Count > 0)
        {
            // Use explicit walkable set
            foreach (var (x, y) in _walkable)
            {
                data.WalkableTiles.Add(new WalkableTile { X = x, Y = y });
            }
        }
        else
        {
            // Generate walkable tiles (everything not in obstacle)
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (!_obstacle.Contains((x, y)))
                    {
                        data.WalkableTiles.Add(new WalkableTile { X = x, Y = y });
                    }
                }
            }
        }

        System.Console.WriteLine($"[TilemapRepository] Exported {data.WalkableTiles.Count} walkable tiles");
        return data;
    }
}

public class TilemapData
{
    [JsonProperty("tiles")] public List<TilePosition> Tiles { get; set; }
}