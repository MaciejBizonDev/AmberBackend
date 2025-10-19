public class GridAStarPathfinder
{
    private readonly TilemapRepository _tilemap;

    public GridAStarPathfinder(TilemapRepository tilemap)
    {
        _tilemap = tilemap;
    }

    public List<TilePosition> FindPath(TilePosition start, TilePosition target)
    {
        var openList = new List<Node>();
        var closedSet = new HashSet<(int x, int y)>();

        var startNode = new Node(start) { GCost = 0, HCost = ManhattanDistance(start, target) };
        openList.Add(startNode);

        while (openList.Count > 0)
        {
            Node current = openList.OrderBy(n => n.FCost).ThenBy(n => n.HCost).First();

            if (current.Position.X == target.X && current.Position.Y == target.Y)
                return RetracePath(current);

            openList.Remove(current);
            closedSet.Add((current.Position.X, current.Position.Y));

            foreach (var neighborPos in GetNeighbors(current.Position))
            {
                if (closedSet.Contains((neighborPos.X, neighborPos.Y))) continue;
                if (!_tilemap.IsWalkable(neighborPos)) continue;

                int newGCost = current.GCost + 1;
                Node existing = openList.FirstOrDefault(n => n.Position.X == neighborPos.X && n.Position.Y == neighborPos.Y);

                if (existing == null)
                {
                    openList.Add(new Node(neighborPos) { GCost = newGCost, HCost = ManhattanDistance(neighborPos, target), Parent = current });
                }
                else if (newGCost < existing.GCost)
                {
                    existing.GCost = newGCost;
                    existing.Parent = current;
                }
            }
        }

        return new List<TilePosition>(); // No path found
    }

    private List<TilePosition> GetNeighbors(TilePosition pos)
    {
        return new List<TilePosition>
        {
            new TilePosition { X = pos.X + 1, Y = pos.Y },
            new TilePosition { X = pos.X - 1, Y = pos.Y },
            new TilePosition { X = pos.X, Y = pos.Y + 1 },
            new TilePosition { X = pos.X, Y = pos.Y - 1 }
        };
    }

    private int ManhattanDistance(TilePosition a, TilePosition b)
        => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private List<TilePosition> RetracePath(Node endNode)
    {
        var path = new List<TilePosition>();
        Node current = endNode;
        while (current != null)
        {
            path.Add(current.Position);
            current = current.Parent;
        }
        path.Reverse();
        return path;
    }

    private class Node
    {
        public TilePosition Position;
        public int GCost;
        public int HCost;
        public int FCost => GCost + HCost;
        public Node Parent;

        public Node(TilePosition pos) { Position = pos; }
    }
}
