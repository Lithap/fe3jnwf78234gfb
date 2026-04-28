using Microsoft.AspNetCore.Mvc;

namespace RetroRec_Server.Controllers
{
    // Catch-all for everything that didn't fit in the topic-specific
    // controllers — community board, slideshow, named images, campus card,
    // storefronts/balance, messages, club. Most of these return empty
    // arrays / placeholders to silence 404 errors that would otherwise
    // spam the player log.
    [ApiController]
    public class MiscController : RetroRecBase
    {
        [HttpGet("/api/communityboard/v2/current")]
        public IActionResult CommunityBoard() => Pascal(new
        {
            FeaturedPlayer = new
            {
                Id = 2,
                TitleOverride = "You!",
                UrlOverride = (string)null
            },
            FeaturedRoomGroup = new
            {
                FeaturedRoomGroupId = 0,
                Name = "Get Started",
                Rooms = new object[] { },
                FeaturedRooms = new object[] { }
            },
            CurrentAnnouncement = new
            {
                Message = "Welcome to RetroRec!",
                MoreInfoUrl = (string)null
            },
            InstagramImages = (object)null,
            Videos = (object)null
        });

        [HttpGet("/api/images/v2/named")]
        public IActionResult NamedImages() => Pascal(new object[] {
            new { FriendlyImageName = "DormRoomBucket", ImageName = "", StartTime = "2021-12-27T21:27:38.188Z", EndTime = "2030-12-27T21:27:38.188Z" },
            new { FriendlyImageName = "Loft", ImageName = "", StartTime = "2021-12-27T21:27:38.188Z", EndTime = "2030-12-27T21:27:38.188Z" },
            new { FriendlyImageName = "BackStairs", ImageName = "", StartTime = "2021-12-27T21:27:38.188Z", EndTime = "2030-12-27T21:27:38.188Z" }
        });

        [HttpGet("/featuredrooms/current")]
        public IActionResult FeaturedRooms() => NoContent();

        // RecCenter slideshow. Empty array `[]` is the WRONG shape — client
        // expects an object with Images[] and ValidTill. Returning [] causes
        // an IndexOutOfRange when the client looks up .Images. Per RebornRec
        // logs, the right shape has 4 entries (PascalCase). The image GUIDs
        // can be empty/dummy; the structure itself is what matters.
        [HttpGet("/api/images/v1/slideshow")]
        [HttpGet("/images/v1/slideshow")]
        public IActionResult Slideshow() => Pascal(new
        {
            Images = new object[] {
                new { SavedImageId = 1, ImageName = "", Username = "RetroRec", RoomName = "DormRoom" },
                new { SavedImageId = 2, ImageName = "", Username = "RetroRec", RoomName = "DormRoom" },
                new { SavedImageId = 3, ImageName = "", Username = "RetroRec", RoomName = "DormRoom" },
                new { SavedImageId = 4, ImageName = "", Username = "RetroRec", RoomName = "DormRoom" }
            },
            ValidTill = "2030-12-27T21:27:38.188Z"
        });

        [HttpPost("/api/CampusCard/v1/UpdateAndGetSubscription")]
        [HttpGet("/api/CampusCard/v1/UpdateAndGetSubscription")]
        public IActionResult CampusCard() => Ok(new
        {
            subscription = new
            {
                subscriptionId = 1,
                recNetPlayerId = 2,
                platformId = "1",
                platformPurchaseId = "1",
                level = 0,
                period = 0,
                expirationDate = "2030-12-27T21:27:38.188Z",
                isAutoRenewing = true,
                createdAt = "2021-12-27T21:27:38.188Z",
                modifiedAt = "2021-12-27T21:27:38.188Z"
            }
        });

        [HttpGet("/api/storefronts/v4/balance/{id}")]
        [HttpGet("/api/storefronts/v3/giftdropstore/{id}")]
        [HttpGet("/api/storefronts/v3/balance/{id}")]
        public IActionResult StoreFronts() => NoContent();

        [HttpGet("/api/storefronts/v1/balanceAddType/{id}/{type}")]
        public IActionResult BalanceAddType(long id, int type) => Ok(new { });

        [HttpGet("/api/messages/v1/favoriteFriendOnlineStatus")]
        public IActionResult FriendStatus() => Ok(new object[] { });

        [HttpGet("/api/messages/v2/get")]
        public IActionResult MessagesGet() => Ok(new object[] { });

        // Text chat moderation / filter — client POSTs the outgoing message and
        // expects 200 + JSON with a purified string. Without this, requests hit
        // the image catchall (404) and the UI shows "Error purifying string".
        [HttpPost("/api/chat/v2/purify")]
        [HttpPost("/chat/v2/purify")]
        [HttpPost("/api/chat/v1/purify")]
        [HttpPost("/chat/v1/purify")]
        public IActionResult ChatPurify()
        {
            // Echo-through stub: no profanity filter server-side for RetroRec.
            return Pascal(new { PurifiedText = "", ErrorCode = 0 });
        }

        [HttpGet("/thread")]
        public IActionResult Thread() => Ok(new object[] { });

        [HttpGet("/club/home/me")]
        public IActionResult ClubHome() => NoContent();

        [HttpGet("/club/mine/member")]
        public IActionResult ClubMine() => NoContent();

        // ============ IMAGE CATCHALL ============
        // Any unknown /something request returns a 1x1 PNG. This catches
        // requests for player profile pics, room images, and arbitrary asset
        // names the client invents — without it those 404 and the watch UI
        // shows broken-image icons everywhere.
        // MUST be the lowest-precedence route in the project. The {imageName}
        // parameter will match anything single-segment, so any specific
        // route in the other controllers takes priority.
        [HttpGet("/{imageName}")]
        public IActionResult ImageCatchall(string imageName)
        {
            return File(RRConstants.PlaceholderPng, "image/png");
        }
    }
}
