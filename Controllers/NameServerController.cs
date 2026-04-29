using Microsoft.AspNetCore.Mvc;

namespace RetroRec_Server.Controllers
{
    [ApiController]
    public class NameServerController : RetroRecBase
    {
        [HttpGet("/2")]
        public IActionResult NameServer()
        {
            string url = PublicUrlHelper.GetPublicBaseUrl(Request);

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
                SerializerSettings = PascalOpts
            };
        }
    }
}
