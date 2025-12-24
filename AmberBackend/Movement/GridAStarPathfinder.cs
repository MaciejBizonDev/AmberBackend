using AmberBackend.Movement;
using System;
using System.Collections.Generic;
using System.Linq;

public class GridAStarPathfinder
{
    private readonly TilemapRepository _tilemaps;
    public GridAStarPathfinder(TilemapRepository t) { _tilemaps = t; }

    public List<TilePosition> FindPath(TilePosition start, TilePosition target)
    {
        var open = new List<Node>();
        var closed = new HashSet<(int, int)>();
        var startNode = new Node(start, 0, H(start, target), null);
        open.Add(startNode);

        while (open.Count > 0)
        {
            var current = open.OrderBy(n => n.F).ThenBy(n => n.H).First();
            if (current.Pos.X == target.X && current.Pos.Y == target.Y)
                return Retrace(current);

            open.Remove(current);
            closed.Add((current.Pos.X, current.Pos.Y));

            foreach (var npos in Neigh(current.Pos))
            {
                if (closed.Contains((npos.X, npos.Y))) continue;
                if (!_tilemaps.IsWalkable(npos)) continue;

                int g = current.G + 1;
                var existing = open.FirstOrDefault(n => n.Pos.X == npos.X && n.Pos.Y == npos.Y);
                if (existing == null)
                    open.Add(new Node(npos, g, H(npos, target), current));
                else if (g < existing.G)
                {
                    existing.G = g;
                    existing.Parent = current;
                }
            }
        }
        return new List<TilePosition>(); // no path
    }

    private static IEnumerable<TilePosition> Neigh(TilePosition p)
    {
        yield return new TilePosition { X = p.X + 1, Y = p.Y };
        yield return new TilePosition { X = p.X - 1, Y = p.Y };
        yield return new TilePosition { X = p.X, Y = p.Y + 1 };
        yield return new TilePosition { X = p.X, Y = p.Y - 1 };
    }
    private static int H(TilePosition a, TilePosition b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private static List<TilePosition> Retrace(Node end)
    {
        var list = new List<TilePosition>();
        for (var n = end; n != null; n = n.Parent) list.Add(n.Pos);
        list.Reverse();
        return list;
    }

    private class Node
    {
        public TilePosition Pos; public int G; public int H; public int F => G + H; public Node Parent;
        public Node(TilePosition p, int g, int h, Node parent) { Pos = p; G = g; H = h; Parent = parent; }
    }
}
