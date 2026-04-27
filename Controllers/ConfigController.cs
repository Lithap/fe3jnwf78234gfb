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
        // CdnBaseUri/MessageOfTheDay are nice-to-have, not load-bearing.
        private const string ConfigV2Json = @"{
  ""MessageOfTheDay"": ""Welcome to RetroRec! Be excellent to each other!"",
  ""CdnBaseUri"": ""https://overthrow-synergy-overhung.ngrok-free.dev/"",
  ""ShareBaseUrl"": ""https://overthrow-synergy-overhung.ngrok-free.dev/{0}"",
  ""LevelProgressionMaps"": [],
  ""MatchmakingParams"": {
    ""PreferFullRoomsFrequency"": 1,
    ""PreferEmptyRoomsFrequency"": 0
  },
  ""DailyObjectives"": [
    [ { ""type"": 21, ""score"": 1, ""xp"": 0 }, { ""type"": 802, ""score"": 3, ""xp"": 0 }, { ""type"": 100, ""score"": 2, ""xp"": 0 } ],
    [ { ""type"": 502, ""score"": 5, ""xp"": 0 }, { ""type"": 400, ""score"": 3, ""xp"": 0 }, { ""type"": 101, ""score"": 2, ""xp"": 0 } ],
    [ { ""type"": 301, ""score"": 3, ""xp"": 0 }, { ""type"": 202, ""score"": 4, ""xp"": 0 }, { ""type"": 603, ""score"": 2, ""xp"": 0 } ],
    [ { ""type"": 21, ""score"": 1, ""xp"": 0 }, { ""type"": 802, ""score"": 3, ""xp"": 0 }, { ""type"": 100, ""score"": 2, ""xp"": 0 } ],
    [ { ""type"": 502, ""score"": 5, ""xp"": 0 }, { ""type"": 400, ""score"": 3, ""xp"": 0 }, { ""type"": 101, ""score"": 2, ""xp"": 0 } ],
    [ { ""type"": 301, ""score"": 3, ""xp"": 0 }, { ""type"": 202, ""score"": 4, ""xp"": 0 }, { ""type"": 603, ""score"": 2, ""xp"": 0 } ],
    [ { ""type"": 302, ""score"": 3, ""xp"": 0 }, { ""type"": 401, ""score"": 2, ""xp"": 0 }, { ""type"": 800, ""score"": 1, ""xp"": 0 } ]
  ],
  ""ConfigTable"": [
    { ""Key"": ""Gift.DropChance"", ""Value"": ""0.5"" },
    { ""Key"": ""Gift.XP"", ""Value"": ""0.5"" }
  ],
  ""PhotonConfig"": {
    ""CloudRegion"": ""us"",
    ""CrcCheckEnabled"": false,
    ""EnableServerTracingAfterDisconnect"": false
  },
  ""AutoMicMutingConfig"": {
    ""MicSpamVolumeThreshold"": 0,
    ""MicVolumeSampleInterval"": 0,
    ""MicVolumeSampleRollingWindowLength"": 0,
    ""MicSpamSamplePercentageForWarning"": 0,
    ""MicSpamSamplePercentageForWarningToEnd"": 0,
    ""MicSpamSamplePercentageForForceMute"": 0,
    ""MicSpamSamplePercentageForForceMuteToEnd"": 0,
    ""MicSpamWarningStateVolumeMultiplier"": 0
  }
}";

        [HttpGet("/api/config/v2")]
        public IActionResult ConfigV2() => Content(ConfigV2Json, "application/json");

        // GameConfigs are individual key/value pairs read by various subsystems
        // at runtime. Each missing config the client looks up triggers an
        // IndexOutOfRangeException cascade — easiest fix is to keep adding
        // entries here as new "GameConfig not found:" warnings appear in logs.
        [HttpGet("/api/gameconfigs/v1/all")]
        public IActionResult GameConfigs() => Ok(new object[] {
            new { Key = "Gift.MaxDaily", Value = "100", StartTime = (string)null, EndTime = (string)null },
            new { Key = "Gift.Falloff", Value = "1", StartTime = (string)null, EndTime = (string)null },
            new { Key = "Gift.DropChance", Value = "100", StartTime = (string)null, EndTime = (string)null },
            new { Key = "UseHeartbeatWebSocket", Value = "0", StartTime = (string)null, EndTime = (string)null },
            new { Key = "Screens.ForceVerification", Value = "0", StartTime = (string)null, EndTime = (string)null },
            new { Key = "forceRegistration", Value = "0", StartTime = (string)null, EndTime = (string)null },
            new { Key = "Door.Creative.Query", Value = "#puzzle", StartTime = (string)null, EndTime = (string)null },
            new { Key = "Door.Creative.Title", Value = "PUZZLE", StartTime = (string)null, EndTime = (string)null },
            new { Key = "Door.Featured.Query", Value = "#featured", StartTime = (string)null, EndTime = (string)null },
            new { Key = "Door.Featured.Title", Value = "Featured", StartTime = (string)null, EndTime = (string)null },
            new { Key = "Door.Quests.Query", Value = "#quest", StartTime = (string)null, EndTime = (string)null },
            new { Key = "Door.Quests.Title", Value = "QUESTS", StartTime = (string)null, EndTime = (string)null },
            new { Key = "Door.Shooters.Query", Value = "#pvp", StartTime = (string)null, EndTime = (string)null },
            new { Key = "Door.Shooters.Title", Value = "PVP", StartTime = (string)null, EndTime = (string)null },
            new { Key = "Door.Sports.Query", Value = "#sport", StartTime = (string)null, EndTime = (string)null },
            new { Key = "Door.Sports.Title", Value = "SPORTS & REC", StartTime = (string)null, EndTime = (string)null },
            new { Key = "UGC.Persistence.AutosaveIntervalSeconds", Value = "60", StartTime = (string)null, EndTime = (string)null },
            new { Key = "UGC.MaxChipsVisible", Value = "5000", StartTime = (string)null, EndTime = (string)null },
            new { Key = "Rewards.UseRewardSelection", Value = "0", StartTime = (string)null, EndTime = (string)null },
            new { Key = "ClickOnName.MaxRaycastDistance", Value = "5", StartTime = (string)null, EndTime = (string)null }
        });

        [HttpGet("/api/config/v1/amplitude")]
        public IActionResult Amplitude() => Ok(new { amplitudeKey = "retrorec" });

        [HttpGet("/api/versioncheck/v4")]
        public IActionResult VersionCheck() => Ok(new { versionStatus = 0 });

        [HttpGet("/config/LoadingScreenTipData")]
        public IActionResult LoadingTips() => Ok(new object[] { });

        [HttpGet("/api/announcement/v1/get")]
        public IActionResult Announcement() => Ok(new object[] { });

        [HttpGet("/announcements/v2/mine/unread")]
        public IActionResult AnnouncementsUnread() => Ok(new object[] { });

        [HttpGet("/announcements/v2/subscription/mine/unread")]
        public IActionResult AnnouncementsSubscriptionUnread() => Ok(new object[] { });

        // Per-user client settings (graphics, voice, comfort options, etc.)
        // Returning a populated list with sensible defaults stops the client
        // from spamming setting-prompts on every login.
        [HttpGet("/api/settings/v2")]
        public IActionResult Settings() => Ok(new object[] {
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
        });

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
