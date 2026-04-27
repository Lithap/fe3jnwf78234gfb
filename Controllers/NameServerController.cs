using Microsoft.AspNetCore.Mvc;

[ApiController]
public class NameServerController : ControllerBase
{
    private const string PublicUrl = "https://overthrow-synergy-overhung.ngrok-free.dev";
    private const string LocalUrl = "http://localhost:2059";

    [HttpGet("/2")]
    public IActionResult NameServer()
    {
        // ngrok forwards traffic locally, so source IP always looks like
        // 127.0.0.1 even for remote clients. The reliable way to tell is
        // checking for ngrok's forwarding headers — if X-Forwarded-For or
        // ngrok-trace-id is set, the request came through the tunnel.
        bool fromNgrok =
            Request.Headers.ContainsKey("X-Forwarded-For") ||
            Request.Headers.ContainsKey("ngrok-trace-id") ||
            Request.Headers.ContainsKey("X-Forwarded-Host");

        string url = fromNgrok ? PublicUrl : LocalUrl;

        return new JsonResult(new
        {
            Auth = url,
            API = url,
            WWW = url,
            Notifications = url,
            Images = url,
            CDN = url,
            Commerce = url,
            Matchmaking = url,
            Storage = url,
            Chat = url,
            Leaderboard = url,
            Accounts = url,
            Link = url,
            RoomComments = url,
            Clubs = url,
            Rooms = url,
            PlatformNotifications = url,
            Moderation = url,
            DataCollection = url
        })
        {
            SerializerSettings = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = null
            }
        };
    }
}
