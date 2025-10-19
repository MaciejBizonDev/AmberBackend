public class TilemapRepository
{
    public TilemapData Walkable { get; private set; }
    public TilemapData Obstacle { get; private set; }

    private readonly string resourcePath;

    public TilemapRepository(string resourcePath)
    {
        this.resourcePath = resourcePath;

        // Use full path to folder where JSONs live
        Walkable = LoadTilemap("walkableTiles.json");
        Obstacle = LoadTilemap("obstacleTiles.json");
    }

    private TilemapData LoadTilemap(string fileName)
    {
        string path = Path.GetFullPath(Path.Combine(resourcePath, fileName));

        if (!File.Exists(path))
        {
            Console.WriteLine($"Tilemap file not found: {path}");
            return new TilemapData { Tiles = new List<TilePosition>() };
        }

        string json = File.ReadAllText(path);

        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var result = System.Text.Json.JsonSerializer.Deserialize<TilemapData>(json, options);

        if (result == null)
        {
            Console.WriteLine($"Failed to deserialize tilemap: {path}");
            return new TilemapData { Tiles = new List<TilePosition>() };
        }

        return result;
    }



    public bool IsWalkable(TilePosition pos)
    {
        bool walkable = Walkable.Tiles.Exists(t => t.X == pos.X && t.Y == pos.Y);
        bool blocked = Obstacle.Tiles.Exists(t => t.X == pos.X && t.Y == pos.Y);
        return walkable && !blocked;
    }
}

public class TilemapData
{
    public List<TilePosition> Tiles { get; set; }
}

