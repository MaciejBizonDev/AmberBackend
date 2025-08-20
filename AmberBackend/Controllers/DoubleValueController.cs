using Microsoft.AspNetCore.Mvc;

namespace YourNamespace.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DoubleValueController : ControllerBase
    {
        // POST api/DoubleValue
        [HttpPost]
        public ActionResult<int> Post([FromBody] int value)
        {
            int doubled = value * 2;
            return Ok(doubled);
        }
    }
}
