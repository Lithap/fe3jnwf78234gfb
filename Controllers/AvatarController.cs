using Microsoft.AspNetCore.Mvc;

namespace RetroRec_Server.Controllers
{
    // All avatar/outfit/face/items endpoints. Pulled out of the old
    // CatchAllController so changing avatar logic doesn't require scrolling
    // past 1000 lines of unrelated stuff.
    [ApiController]
    public class AvatarController : RetroRecBase
    {
        // Cache the avatar items file in memory after first read. avataritems.json
        // is ~190 KB and gets requested on basically every login + room load —
        // re-reading from disk every time was noticeable in the server log.
        private static string? _avatarItemsCache = null;

        private string GetAvatarItemsJson()
        {
            if (_avatarItemsCache != null) return _avatarItemsCache;
            try
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "avataritems.json");
                if (System.IO.File.Exists(path))
                {
                    _avatarItemsCache = System.IO.File.ReadAllText(path);
                    return _avatarItemsCache;
                }
            }
            catch { }
            return "[]";
        }

        [HttpGet("/api/avatar/v2")]
        public IActionResult Avatar() => Pascal(new
        {
            OutfitSelections = RRConstants.WorkingOutfit,
            FaceFeatures = RRConstants.WorkingFaceFeatures,
            SkinColor = RRConstants.SkinColorGuid,
            HairColor = RRConstants.HairColorGuid
        });

        // Client POSTs here whenever avatar changes. Without handling, the
        // 404 cascade blocks quest spawn system from registering a valid
        // player avatar — "Activity Theater Department does not contain any
        // spawn points" comes from exactly this avatar-upload failure.
        [HttpPost("/api/avatar/v2/set")]
        [HttpPut("/api/avatar/v2/set")]
        [HttpPost("/api/avatar/v2")]
        [HttpPut("/api/avatar/v2")]
        public IActionResult AvatarSet() => Ok(new { });

        [HttpGet("/api/avatar/v3/saved")]
        public IActionResult AvatarSaved() => Pascal(new object[] {
            new {
                Slot = "1",
                PreviewImageName = "",
                OutfitSelections = RRConstants.WorkingOutfit,
                HairColor = RRConstants.HairColorGuid,
                SkinColor = RRConstants.SkinColorGuid,
                FaceFeatures = RRConstants.WorkingFaceFeatures
            }
        });

        [HttpGet("/api/avatar/v1/defaultunlocked")]
        public IActionResult DefaultUnlocked() => Ok(Array.Empty<object>());

        [HttpGet("/api/avatar/v4/items")]
        [HttpGet("/api/avatar/v3/items")]
        [HttpGet("/api/avatar/v2/items")]
        [HttpGet("/api/avatar/v1/items")]
        public IActionResult AvatarItems() => Content(GetAvatarItemsJson(), "application/json");

        [HttpGet("/api/avatar/v4/unlocked")]
        [HttpGet("/api/avatar/v3/unlocked")]
        [HttpGet("/api/avatar/v2/unlocked")]
        [HttpGet("/api/avatar/v1/unlocked")]
        public IActionResult AvatarUnlocked() => Content(GetAvatarItemsJson(), "application/json");

        [HttpGet("/api/avatar/v2/gifts")]
        public IActionResult AvatarGifts() => Ok(Array.Empty<object>());
    }
}
