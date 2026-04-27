using Microsoft.AspNetCore.Mvc;

namespace RetroRec_Server.Controllers
{
    [ApiController]
    public class MatchmakingController : RetroRecBase
    {
        [HttpGet("/api/matchmaking/{**path}")]
        [HttpPost("/api/matchmaking/{**path}")]
        [HttpPut("/api/matchmaking/{**path}")]
        [HttpDelete("/api/matchmaking/{**path}")]
        [HttpGet("/matchmaking/{**path}")]
        [HttpPost("/matchmaking/{**path}")]
        [HttpPut("/matchmaking/{**path}")]
        [HttpDelete("/matchmaking/{**path}")]
        public IActionResult Matchmaking(string path)
        {
            Console.WriteLine($"[matchmaking] {Request.Method} {Request.Path}{Request.QueryString}");
            return Ok(new { });
        }
    }
}
