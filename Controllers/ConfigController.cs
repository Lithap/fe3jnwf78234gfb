using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace RetroRec_Server.Controllers
{
    // Game configuration: /api/config/v2, gameconfigs, settings, announcements,
    // version check. The biggest file behaviorally because config/v2 is what
    // unblocks Photon CRC checking — without it the client uses strict defaults
    // and silently drops rig/kill packets in some rooms (RecCenter arms bug).
    [ApiController]
    public class ConfigController : RetroRecBase
    {
        // Reverse-engineered from RebornRec's /api/config/v2 response. Fields
        // that matter most:
        //   - PhotonConfig.CrcCheckEnabled = false  -> stops packet drops
        //   - PhotonConfig.CloudRegion = "us"       -> picks the right region
        //   - MatchmakingParams                     -> client matchmaking init
        //   - DailyObjectives                       -> watch -> challenges UI
        // CdnBaseUri / ShareBaseUrl must match the hostname the client actually
        // uses (from name server + forwarded headers). Hardcoding an old ngrok
        // URL breaks images, shares, and anything that builds absolute URLs.
        //
        // Build JSON with JsonNode — never a giant interpolated string: `$@"..."`
        // treats `{` as interpolation and breaks the compiler (CS8076 / CS8361).
        private static readonly JsonSerializerOptions ConfigV2SerializeOptions = new()
        {
            PropertyNamingPolicy = null
        };

        private static JsonObject ObjectiveCell(int type, int score, int xp) =>
            new()
            {
                ["type"] = type,
                ["score"] = score,
                ["xp"] = xp
            };

        private static string BuildConfigV2Json(HttpRequest request)
        {
            var cdnUri = PublicUrlHelper.GetPublicBaseUrlWithTrailingSlash(request);
            var shareUri = PublicUrlHelper.GetPublicBaseUrl(request).TrimEnd('/') + "/{0}";

            var dailyObjectives = new JsonArray(
                new JsonArray(ObjectiveCell(21, 1, 0), ObjectiveCell(802, 3, 0), ObjectiveCell(100, 2, 0)),
                new JsonArray(ObjectiveCell(502, 5, 0), ObjectiveCell(400, 3, 0), ObjectiveCell(101, 2, 0)),
                new JsonArray(ObjectiveCell(301, 3, 0), ObjectiveCell(202, 4, 0), ObjectiveCell(603, 2, 0)),
                new JsonArray(ObjectiveCell(21, 1, 0), ObjectiveCell(802, 3, 0), ObjectiveCell(100, 2, 0)),
                new JsonArray(ObjectiveCell(502, 5, 0), ObjectiveCell(400, 3, 0), ObjectiveCell(101, 2, 0)),
                new JsonArray(ObjectiveCell(301, 3, 0), ObjectiveCell(202, 4, 0), ObjectiveCell(603, 2, 0)),
                new JsonArray(ObjectiveCell(302, 3, 0), ObjectiveCell(401, 2, 0), ObjectiveCell(800, 1, 0))
            );

            var root = new JsonObject
            {
                ["MessageOfTheDay"] = "Welcome to RetroRec! Be excellent to each other!",
                ["CdnBaseUri"] = cdnUri,
                ["ShareBaseUrl"] = shareUri,
                ["LevelProgressionMaps"] = new JsonArray(),
                ["MatchmakingParams"] = new JsonObject
                {
                    ["PreferFullRoomsFrequency"] = 1,
                    ["PreferEmptyRoomsFrequency"] = 0
                },
                ["DailyObjectives"] = dailyObjectives,
                ["ConfigTable"] = new JsonArray(
                    new JsonObject { ["Key"] = "Gift.DropChance", ["Value"] = "0.5" },
                    new JsonObject { ["Key"] = "Gift.XP", ["Value"] = "0.5" }
                ),
                ["PhotonConfig"] = new JsonObject
                {
                    ["CloudRegion"] = "us",
                    ["CrcCheckEnabled"] = false,
                    ["EnableServerTracingAfterDisconnect"] = false
                },
                ["AutoMicMutingConfig"] = new JsonObject
                {
                    ["MicSpamVolumeThreshold"] = 0,
                    ["MicVolumeSampleInterval"] = 0,
                    ["MicVolumeSampleRollingWindowLength"] = 0,
                    ["MicSpamSamplePercentageForWarning"] = 0,
                    ["MicSpamSamplePercentageForWarningToEnd"] = 0,
                    ["MicSpamSamplePercentageForForceMute"] = 0,
                    ["MicSpamSamplePercentageForForceMuteToEnd"] = 0,
                    ["MicSpamWarningStateVolumeMultiplier"] = 0
                }
            };

            return root.ToJsonString(ConfigV2SerializeOptions);
        }

        [HttpGet("/api/config/v2")]
        public IActionResult ConfigV2() => Content(BuildConfigV2Json(Request), "application/json");

        // GameConfigs are individual key/value pairs read by various subsystems
        // at runtime. Each missing config the client looks up triggers an
        // IndexOutOfRangeException cascade — easiest fix is to keep adding
        // entries here as new "GameConfig not found:" warnings appear in logs.
        private static readonly object[] GameConfigsList =
        {
            new { Key = "Gift.MaxDaily", Value = "100", StartTime = (string?)null, EndTime = (string?)null },
            new { Key = "Gift.Falloff", Value = "1", StartTime = (string?)null, EndTime = (string?)null },
            new { Key = "Gift.DropChance", Value = "100", StartTime = (string?)null, EndTime = (string?)null },
            new { Key = "UseHeartbeatWebSocket", Value = "0", StartTime = (string?)null, EndTime = (string?)null },
            new { Key = "Screens.ForceVerification", Value = "0", StartTime = (string?)null, EndTime = (string?)null },
            new { Key = "forceRegistration", Value = "0", StartTime = (string?)null, EndTime = (string?)null },
            new { Key = "Door.Creative.Query", Value = "#puzzle", StartTime = (string?)null, EndTime = (string?)null },
            new { Key = "Door.Creative.Title", Value = "PUZZLE", StartTime = (string?)null, EndTime = (string?)null },
            new { Key = "Door.Featured.Query", Value = "#featured", StartTime = (string?)null, EndTime = (string?)null },
            new { Key = "Door.Featured.Title", Value = "Featured", StartTime = (string?)null, EndTime = (string?)null },
            new { Key = "Door.Quests.Query", Value = "#quest", StartTime = (string?)null, EndTime = (string?)null },
            new { Key = "Door.Quests.Title", Value = "QUESTS", StartTime = (string?)null, EndTime = (string?)null },
            new { Key = "Door.Shooters.Query", Value = "#pvp", StartTime = (string?)null, EndTime = (string?)null },
            new { Key = "Door.Shooters.Title", Value = "PVP", StartTime = (string?)null, EndTime = (string?)null },
            new { Key = "Door.Sports.Query", Value = "#sport", StartTime = (string?)null, EndTime = (string?)null },
            new { Key = "Door.Sports.Title", Value = "SPORTS & REC", StartTime = (string?)null, EndTime = (string?)null },
            new { Key = "UGC.Persistence.AutosaveIntervalSeconds", Value = "60", StartTime = (string?)null, EndTime = (string?)null },
            new { Key = "UGC.RoomSavingEnabled", Value = "1", StartTime = (string?)null, EndTime = (string?)null },
            new { Key = "UGC.MaxChipsVisible", Value = "5000", StartTime = (string?)null, EndTime = (string?)null },
            new { Key = "Rewards.UseRewardSelection", Value = "0", StartTime = (string?)null, EndTime = (string?)null },
            new { Key = "ClickOnName.MaxRaycastDistance", Value = "5", StartTime = (string?)null, EndTime = (string?)null }
        };

        private static readonly object[] SettingsList =
        {
            new { Key = "MOD_BLOCKED_TIME", Value = "0" },
            new { Key = "MOD_BLOCKED_DURATION", Value = "0" },
            new { Key = "PlayerSessionCount", Value = "13" },
            new { Key = "ShowRoomCenter", Value = "0" },
            new { Key = "QualitySettings", Value = "2" },
            new { Key = "Recroom.OOBE", Value = "100" },
            new { Key = "VoiceFilter2", Value = "1" },
            new { Key = "VIGNETTED_TELEPORT_ENABLED", Value = "0" },
            new { Key = "CONTINUOUS_ROTATION_MODE", Value = "0" },
            new { Key = "ROTATION_INCREMENT", Value = "0" },
            new { Key = "ROTATE_IN_PLACE_ENABLED", Value = "0" },
            new { Key = "OOBE_OBJECTIVES_GRANTED", Value = "0" },
            new { Key = "TeleportBuffer", Value = "0" },
            new { Key = "VoiceChat", Value = "1" },
            new { Key = "PersonalBubble", Value = "0" },
            new { Key = "IgnoreBuffer", Value = "0" },
            new { Key = "H.264 plugin", Value = "1" },
            new { Key = "USER_TRACKING", Value = "55" },
            new { Key = "SplitTestAssignedSegments", Value = "1|{}" },
            new { Key = "Recroom.AccountCreation.HasStarted", Value = "True" },
            new { Key = "Recroom.AccountCreation.HasFinished", Value = "True" },
            new { Key = "Recroom.AccountCreation.HasChosenUsername", Value = "True" },
            new { Key = "Recroom.AccountCreation.HasCreatedPassword", Value = "True" },
            new { Key = "TUTORIAL_COMPLETE_MASK", Value = "57" },
            new { Key = "BACKPACK_FAVORITE_TOOL", Value = "-1" },
            new { Key = "MakerPen_SnappingMode", Value = "0" },
            new { Key = "Recroom.ChallengeMap", Value = "0" },
            new { Key = "HasCheckedForPlatformReferrers", Value = "True" },
            new { Key = "HAS_OPENED_WATCH_MENU_BEFORE", Value = "True" }
        };

        [HttpGet("/api/gameconfigs/v1/all")]
        public IActionResult GameConfigs() => Ok(GameConfigsList);

        [HttpGet("/api/config/v1/amplitude")]
        public IActionResult Amplitude() => Ok(new { amplitudeKey = "retrorec" });

        [HttpGet("/api/versioncheck/v4")]
        public IActionResult VersionCheck() => Ok(new { versionStatus = 0 });

        [HttpGet("/config/LoadingScreenTipData")]
        public IActionResult LoadingTips() => Ok(Array.Empty<object>());

        [HttpGet("/api/announcement/v1/get")]
        public IActionResult Announcement() => Ok(Array.Empty<object>());

        [HttpGet("/announcements/v2/mine/unread")]
        public IActionResult AnnouncementsUnread() => Ok(Array.Empty<object>());

        [HttpGet("/announcements/v2/subscription/mine/unread")]
        public IActionResult AnnouncementsSubscriptionUnread() => Ok(Array.Empty<object>());

        // Per-user client settings (graphics, voice, comfort options, etc.)
        // Returning a populated list with sensible defaults stops the client
        // from spamming setting-prompts on every login.
        [HttpGet("/api/settings/v2")]
        public IActionResult Settings() => Ok(SettingsList);

        [HttpPost("/api/settings/v2/set")]
        public IActionResult SetSettings() => Ok(new { });

        // RecCenter (and any social/public room) checks this before letting
        // the player interact. Returning a "no restrictions" payload keeps
        // arms / animation / interaction enabled.
        [HttpGet("/parentalcontrol/me")]
        [HttpGet("/api/parentalcontrol/me")]
        public IActionResult ParentalControl() => Ok(new
        {
            isJunior = false,
            disableVoiceChat = false,
            disableTextChat = false,
            disableMessages = false,
            disableTrading = false,
            disableMatureContent = false,
            disableScreenShare = false,
            disableUGC = false,
            restrictRoomCreation = false,
            restrictMakerPen = false
        });
    }
}
