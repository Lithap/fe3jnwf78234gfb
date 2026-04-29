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
                UrlOverride = (string?)null
            },
            FeaturedRoomGroup = new
            {
                FeaturedRoomGroupId = 0,
                Name = "Get Started",
                Rooms = Array.Empty<object>(),
                FeaturedRooms = Array.Empty<object>()
            },
            CurrentAnnouncement = new
            {
                Message = "Welcome to RetroRec!",
                MoreInfoUrl = (string?)null
            },
            InstagramImages = (object?)null,
            Videos = (object?)null
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
        public IActionResult StoreFronts(long _id = 0) => NoContent();

        [HttpGet("/api/storefronts/v1/balanceAddType/{id}/{type}")]
        public IActionResult BalanceAddType(long _id = 0, int _type = 0) => Ok(new { });

        [HttpGet("/api/messages/v1/favoriteFriendOnlineStatus")]
        public IActionResult FriendStatus() => Ok(Array.Empty<object>());

        [HttpGet("/api/messages/v2/get")]
        public IActionResult MessagesGet() => Ok(Array.Empty<object>());

        // Text chat moderation / filter — client POSTs the outgoing message and
        // expects 200 + JSON with a purified string. Without this, requests hit
        // the image catchall (404) and the UI shows "Error purifying string".
        [HttpPost("/api/chat/v2/purify")]
        [HttpPost("/chat/v2/purify")]
        [HttpPost("/api/chat/v1/purify")]
        [HttpPost("/chat/v1/purify")]
        public async Task<IActionResult> ChatPurify()
        {
            // Echo the input text back — no server-side filter.
            // Client sends {"text":"..."} or form field "text"; we echo whatever
            // was passed so the chat message is not lost.
            string text = "";
            try
            {
                if (Request.HasFormContentType && Request.Form.TryGetValue("text", out var formText))
                {
                    text = formText.ToString();
                }
                else if (Request.ContentLength.GetValueOrDefault() > 0)
                {
                    Request.EnableBuffering();
                    if (Request.Body.CanSeek) Request.Body.Position = 0;
                    using var reader = new System.IO.StreamReader(Request.Body, leaveOpen: true);
                    var body = await reader.ReadToEndAsync();
                    if (Request.Body.CanSeek) Request.Body.Position = 0;
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        if (body.TrimStart().StartsWith("{"))
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(body);
                            foreach (var key in new[] { "text", "Text", "message", "Message" })
                            {
                                if (doc.RootElement.TryGetProperty(key, out var el) &&
                                    el.ValueKind == System.Text.Json.JsonValueKind.String)
                                {
                                    text = el.GetString() ?? "";
                                    break;
                                }
                            }
                        }
                        else
                        {
                            text = body;
                        }
                    }
                }
            }
            catch { }
            return Pascal(new { PurifiedText = text, ErrorCode = 0 });
        }

        [HttpGet("/thread")]
        public IActionResult Thread() => Ok(Array.Empty<object>());

        [HttpGet("/club/home/me")]
        public IActionResult ClubHome() => NoContent();

        [HttpGet("/club/mine/member")]
        public IActionResult ClubMine() => NoContent();

        // ============ PRESENCE ============

        // Older 2018/2021 builds call presence/v3/heartbeat. Returns same
        // shape as /player/heartbeat so the client stays online.
        [HttpPost("/api/presence/v3/heartbeat")]
        [HttpPost("/presence/v3/heartbeat")]
        [HttpPost("/api/presence/v2/heartbeat")]
        [HttpPost("/presence/v2/heartbeat")]
        [HttpPost("/api/presence/v1/heartbeat")]
        [HttpPost("/presence/v1/heartbeat")]
        public IActionResult PresenceHeartbeat()
        {
            int playerId = GetAccountIdFromAuth();
            if (playerId == 0) playerId = 2;
            UserRoomInstances.TryGetValue(playerId, out var roomInstance);
            return Pascal(new
            {
                PlayerId = playerId,
                StatusVisibility = 0,
                DeviceClass = 0,
                RoomInstance = roomInstance,
                IsOnline = true
            });
        }

        [HttpGet("/api/presence/v1/setplayertype")]
        [HttpPost("/api/presence/v1/setplayertype")]
        [HttpPut("/api/presence/v1/setplayertype")]
        [HttpGet("/presence/v1/setplayertype")]
        [HttpPost("/presence/v1/setplayertype")]
        [HttpPut("/presence/v1/setplayertype")]
        public IActionResult SetPlayerType() => Ok(new { });

        // ============ NOTIFICATIONS / EVENTS ============

        [HttpGet("/api/notification/v2")]
        [HttpGet("/notification/v2")]
        [HttpGet("/api/notification/v1")]
        [HttpGet("/notification/v1")]
        public IActionResult Notifications() => Ok(Array.Empty<object>());

        [HttpGet("/api/playersubscriptions/v1/my")]
        [HttpGet("/playersubscriptions/v1/my")]
        public IActionResult PlayerSubscriptions() => Ok(Array.Empty<object>());

        // ============ CHAT ============

        // Some 2018/2021 builds hit /api/sanitize/v1/isPure for text chat.
        // Return IsPure=true so the message is never dropped.
        [HttpGet("/api/sanitize/v1/isPure")]
        [HttpPost("/api/sanitize/v1/isPure")]
        [HttpGet("/sanitize/v1/isPure")]
        [HttpPost("/sanitize/v1/isPure")]
        public IActionResult SanitizeIsPure() => Ok(new { IsPure = true });

        [HttpGet("/api/chat/v2/myChats")]
        [HttpGet("/chat/v2/myChats")]
        [HttpGet("/api/chat/v1/myChats")]
        [HttpGet("/chat/v1/myChats")]
        public IActionResult MyChats() => Ok(Array.Empty<object>());

        // ============ CHECKLIST / CHALLENGES ============

        // 2018/2021 builds check in-game checklist on boot. Return three dummy
        // objectives so the client's checklist panel populates and doesn't hang.
        [HttpGet("/api/checklist/v1/current")]
        [HttpGet("/checklist/v1/current")]
        public IActionResult ChecklistCurrent() => Ok(new object[] {
            new { Order = 0, Objective = 3000, Count = 3, CreditAmount = 100 },
            new { Order = 1, Objective = 3001, Count = 3, CreditAmount = 100 },
            new { Order = 2, Objective = 3002, Count = 3, CreditAmount = 100 }
        });

        [HttpGet("/api/challenge/v1/getCurrent")]
        [HttpGet("/challenge/v1/getCurrent")]
        public IActionResult ChallengeCurrent() => Ok(new { Success = true, Message = "RetroRec" });

        // ============ IMAGE CATCHALL ============
        // Any unknown /something request returns a 1x1 PNG. This catches
        // requests for player profile pics, room images, and arbitrary asset
        // names the client invents — without it those 404 and the watch UI
        // shows broken-image icons everywhere.
        // MUST be the lowest-precedence route in the project. The {imageName}
        // parameter will match anything single-segment, so any specific
        // route in the other controllers takes priority.
        [HttpGet("/{imageName}")]
        public IActionResult ImageCatchall(string? imageName = null)
        {
            return File(RRConstants.PlaceholderPng, "image/png");
        }
    }
}
