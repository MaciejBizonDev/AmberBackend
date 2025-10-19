using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/tilemaps")]
public class TilemapController : ControllerBase
{
    private readonly string resourcePath = Path.Combine(AppContext.BaseDirectory, "Resources", "Tilemaps");

    [HttpGet("walkable")]
    public IActionResult GetWalkableTilemap()
    {
        return ServeTilemap("walkableTiles.json");
    }

    [HttpGet("obstacle")]
    public IActionResult GetObstacleTilemap()
    {
        return ServeTilemap("obstacleTiles.json");
    }

    private IActionResult ServeTilemap(string fileName)
    {
        string path = Path.Combine(resourcePath, fileName);
        if (!System.IO.File.Exists(path))
            return NotFound($"{fileName} not found.");

        string json = System.IO.File.ReadAllText(path);
        return Content(json, "application/json");
    }
}
