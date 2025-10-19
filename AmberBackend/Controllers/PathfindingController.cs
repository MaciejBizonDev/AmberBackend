using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/pathfinding")]
public class PathfindingController : ControllerBase
{
    private readonly GridAStarPathfinder _pathfinder;

    public PathfindingController(GridAStarPathfinder pathfinder)
    {
        _pathfinder = pathfinder;
    }

    [HttpGet("find")]
    public IActionResult FindPath(int startX, int startY, int targetX, int targetY)
    {
        var start = new TilePosition { X = startX, Y = startY };
        var target = new TilePosition { X = targetX, Y = targetY };

        var path = _pathfinder.FindPath(start, target);
        if (path == null || path.Count == 0)
            return NotFound("No path found");

        return Ok(path);
    }
}
