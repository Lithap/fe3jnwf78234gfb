using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace RetroRec_Server.Controllers
{
    // Helpers shared by every controller. Pulled out of the old monolith
    // CatchAllController so each topic-specific controller doesn't have
    // its own duplicate copy of GetAccountIdFromAuth / Pascal.
    //
    // RetroRecBase is just a base ControllerBase with our utilities tacked
    // on — every controller in the project inherits from it instead of
    // ControllerBase directly.
    public abstract class RetroRecBase : ControllerBase
    {
        // Per-user room instance state. Used by Player + Rooms controllers
        // to keep each player's "where am I" state independent. Without
        // per-user tracking, two players overwrite each other and the client
        // throws "RecNet presence out-of-sync" — that was friend's bug.
        public static readonly System.Collections.Concurrent.ConcurrentDictionary<int, object>
            UserRoomInstances = new();

        // Pulls the "sub" claim (account id) out of the Bearer JWT so endpoints
        // know which user is calling them, instead of hardcoding playerId = 2.
        // Same logic AuthController uses to produce the JWT in the first place.
        protected int GetAccountIdFromAuth()
        {
            try
            {
                var auth = Request.Headers["Authorization"].ToString();
                if (string.IsNullOrEmpty(auth)) return 0;
                var token = auth.StartsWith("Bearer ") ? auth.Substring(7) : auth;
                var parts = token.Split('.');
                if (parts.Length < 2) return 0;
                var payload = parts[1].Replace('-', '+').Replace('_', '/');
                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                }
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                var match = System.Text.RegularExpressions.Regex.Match(
                    json, "\"sub\"\\s*:\\s*\"?(\\d+)\"?");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var id))
                    return id;
            }
            catch { }
            return 0;
        }

        // Reads a target account ID from the request body, trying JSON
        // properties (targetAccountId, accountId, targetId, id) and then
        // form-encoded fields with the same names. Returns 0 if nothing found.
        protected async Task<int> ReadTargetIdFromBodyAsync()
        {
            try
            {
                Request.Body.Position = 0;
            }
            catch { }
            try
            {
                if (Request.HasFormContentType)
                {
                    var form = await Request.ReadFormAsync();
                    foreach (var key in new[] { "targetAccountId", "accountId", "targetId", "id" })
                    {
                        if (form.TryGetValue(key, out var val) && int.TryParse(val, out var fid) && fid != 0)
                            return fid;
                    }
                    return 0;
                }
                using var reader = new System.IO.StreamReader(Request.Body, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(body)) return 0;
                var doc = JsonDocument.Parse(body);
                foreach (var key in new[] { "targetAccountId", "accountId", "targetId", "id" })
                {
                    if (doc.RootElement.TryGetProperty(key, out var el))
                    {
                        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n) && n != 0) return n;
                        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var s) && s != 0) return s;
                    }
                }
            }
            catch { }
            return 0;
        }

        // Serialize with PascalCase preserved. Default System.Text.Json policy
        // is camelCase; the client expects PascalCase property names on most
        // domain objects (Avatar, Slideshow, room data, etc.).
        protected IActionResult Pascal(object obj)
        {
            var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions
            {
                PropertyNamingPolicy = null
            });
            return Content(json, "application/json");
        }
    }

    // Constants & magic strings shared across multiple controllers — outfit,
    // face features, scene IDs, the placeholder PNG, etc. Centralized so we
    // don't have copies drifting between Avatar and Rooms controllers.
    public static class RRConstants
    {
        public const string WorkingOutfit =
            "7db6b49f-3e2a-4da8-bdfa-e9ad9ebc5ba6,,,,0;" +
            "5b6535f0-86cd-417a-bcce-a1ace9a5f260,d1eb3573-672c-4bb4-9ec5-7c81e6a38d3d,,,1;" +
            "38b38e24-d636-41c6-b80e-a285ae3106dc,0f71b9a2-dd88-435c-a571-4503d12dcf2d,,,1;" +
            "03f8c394-28fa-4087-978b-8d108f0bd969,225209dc-3afc-40cf-858e-89f7afa00c0d,,,0;" +
            "8d10cc78-6b00-45f3-affb-205e9cc5b03f,,,,0;" +
            "5b01eaa3-0cac-40c0-b72a-b3f6a868ae0c,,,,2;" +
            "5b01eaa3-0cac-40c0-b72a-b3f6a868ae0c,,,,3;" +
            "1d27b674-f9e2-4ffc-9d8c-a58a1be06457,,,,0;" +
            "83be5ba4-525a-4781-a5f6-839c22e4d1d3,724c79a3-822c-422f-9462-a5374ee0211c,,,1;" +
            "b1bfe0b4-1ff6-420f-acfc-2331d34246dc,6e0f3af8-297f-4ea7-8f9e-4a1bc57d3e35,,,1";

        public const string WorkingFaceFeatures =
            "{\"ver\":3,\"eyeId\":\"pY0dY6IxOEaNv8uNL8qUgQ\",\"eyePos\":{\"x\":-0.01,\"y\":-0.04},\"eyeScl\":0.05," +
            "\"mouthId\":\"EvIQk4Q4IkCOBBZkSBU-8g\",\"mouthPos\":{\"x\":0.0,\"y\":0.08},\"mouthScl\":0.05," +
            "\"hairPrimaryColorId\":\"\",\"hairSecondaryColorId\":\"5ee30295-b05f-4e96-819e-5ac865b2c63d\"," +
            "\"hairPatternId\":\"\",\"beardColorId\":\"5ee30295-b05f-4e96-819e-5ac865b2c63d\"," +
            "\"beardSecondaryColorId\":\"5ee30295-b05f-4e96-819e-5ac865b2c63d\",\"faceShapeId\":\"\",\"bodyShapeId\":\"\"," +
            "\"useHatAnchorParams\":true,\"hideEars\":true," +
            "\"hatAnchorParams\":{\"NormalizedPosition\":{\"x\":0.5,\"y\":0.5}," +
            "\"HemisphereOffsets\":{\"x\":0.0,\"y\":0.0,\"z\":0.0}," +
            "\"HemisphereRotations\":{\"x\":-5.0,\"y\":0.0,\"z\":0.0}}}";

        public const string SkinColorGuid = "85343b16-d58a-4091-96d8-083a81fb03ae";
        public const string HairColorGuid = "5ee30295-b05f-4e96-819e-5ac865b2c63d";
        public const string DormSceneId = "76d98498-60a1-430c-ab76-b54a29b7a163";

        // Tiny 1x1 transparent PNG. Used as a placeholder for any image
        // request we don't have real content for. The image catchall serves
        // this as image/png so the client doesn't error out on missing assets.
        public static readonly byte[] PlaceholderPng = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
            0x89, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41,
            0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
            0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
            0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
            0x42, 0x60, 0x82
        };

        // Map of room id -> Unity scene GUID. Used by Rooms + Matchmaking
        // controllers to fill in UnitySceneId on /rooms/{id} and `location`
        // on /goto responses. GUIDs were extracted from the IL2CPP build —
        // these are baked into the scene asset bundles, can't change them.
        //
        // IDs above 50 are RebornRec's "base room" / standalone scene IDs —
        // distinct rooms that share scenes with the regular gameplay rooms
        // (e.g. RoomId 52 "Lake" uses the same scene as RoomId 4 "DiscGolfLake",
        // because Lake is a standalone explorable version of DiscGolfLake's map).
        public static readonly Dictionary<int, string> RoomSceneIds = new()
        {
            { 1,  DormSceneId },
            { 2,  "cbad71af-0831-44d8-b8ef-69edafa841f6" }, // RecCenter
            { 3,  "4078dfed-24bb-4db7-863f-578ba48d726b" }, // 3DCharades
            { 4,  "f6f7256c-e438-4299-b99e-d20bef8cf7e0" }, // DiscGolfLake
            { 5,  "d9378c9f-80bc-46fb-ad1e-1bed8a674f55" }, // DiscGolfPropulsion
            { 6,  "3d474b26-26f7-45e9-9a36-9b02847d5e6f" }, // Dodgeball
            { 7,  "d89f74fa-d51e-477a-a425-025a891dd499" }, // Paddleball
            { 8,  "e122fe98-e7db-49e8-a1b1-105424b6e1f0" }, // Paintball
            { 13, "91e16e35-f48f-4700-ab8a-a1b79e50e51b" }, // GoldenTrophy
            { 14, "acc06e66-c2d0-4361-b0cd-46246a4c455c" }, // TheRiseofJumbotron
            { 15, "949fa41f-4347-45c0-b7ac-489129174045" }, // CrimsonCauldron
            { 16, "7e01cfe0-820a-406f-b1b3-0a5bf575235c" }, // IsleOfLostSkulls
            { 17, "6d5eea4b-f069-4ed0-9916-0e2f07df0d03" }, // Soccer
            { 18, "239e676c-f12f-489f-bf3a-d4c383d692c3" }, // LaserTag
            { 20, "253fa009-6e65-4c90-91a1-7137a56a267f" }, // RecRoyaleSquads
            { 21, "b010171f-4875-4e89-baba-61e878cd41e1" }, // RecRoyaleSolos
            { 22, "a067557f-ca32-43e6-b6e5-daaec60b4f5a" }, // Lounge
            { 23, "9932f88f-3929-43a0-a012-a40b5128e346" }, // PerformanceHall
            { 24, "882e9b96-7115-4b03-86f6-c0c9d8e22e00" }, // StuntRunnerBaseRoom
            { 25, "0a864c86-5a71-4e18-8041-8124e4dc9d98" }, // Park
            { 27, "49cb8993-a956-43e2-86f4-1318f279b22a" }, // QuestForDracula
            { 28, "ae929543-9a07-41d5-8ee9-dbbee8c36800" }, // Bowling
            { 29, "0a864c86-5a71-4e18-8041-8124e4dc9d98" }, // PublicSandbox
            { 31, "b7281665-a715-4051-826b-8e08e69c6172" }, // StuntRunner
            { 34, "c79709d8-a31b-48aa-9eb8-cc31ba9505e8" }, // Orientation

            // Standalone base rooms — RebornRec's catalog of "explore the
            // gameplay scene without the activity" / clonable templates.
            { 50, "a75f7547-79eb-47c6-8986-6767abcb4f92" }, // MakerRoom (blank canvas)
            { 51, "239e676c-f12f-489f-bf3a-d4c383d692c3" }, // Hangar (Jumbotron base — same scene)
            { 52, "f6f7256c-e438-4299-b99e-d20bef8cf7e0" }, // Lake (DiscGolfLake scene)
            { 53, "e122fe98-e7db-49e8-a1b1-105424b6e1f0" }, // River (Paintball default scene)
            { 54, "3d474b26-26f7-45e9-9a36-9b02847d5e6f" }, // Gym (Dodgeball scene)
            { 55, "cbad71af-0831-44d8-b8ef-69edafa841f6" }, // RecCenterCustom
            { 56, "9d6456ce-6264-48b4-808d-2d96b3d91038" }, // CyberJunkCity (LaserTag map)
            { 57, "a785267d-c579-42ea-be43-fec1992d1ca7" }, // Homestead (Paintball map)
            { 58, "ff4c6427-7079-4f59-b22a-69b089420827" }, // Quarry (Paintball map)
            { 59, "380d18b5-de9c-49f3-80f7-f4a95c1de161" }, // Clearcut (Paintball map)
            { 60, "58763055-2dfb-4814-80b8-16fac5c85709" }, // Spillway (Paintball map)
            { 61, "6d5eea4b-f069-4ed0-9916-0e2f07df0d03" }, // Stadium (Soccer scene)
            { 62, "d9378c9f-80bc-46fb-ad1e-1bed8a674f55" }  // PropulsionTestRange (DiscGolfPropulsion)
        };

        public static string GetSceneIdForRoom(int roomId)
            => RoomSceneIds.TryGetValue(roomId, out var sceneId) ? sceneId : DormSceneId;

        public static int RoomNameToId(string name) => name switch
        {
            "DormRoom" => 1,
            "RecCenter" => 2,
            "3DCharades" => 3,
            "DiscGolfLake" => 4,
            "DiscGolfPropulsion" => 5,
            "Dodgeball" => 6,
            "Paddleball" => 7,
            "Paintball" => 8,
            "GoldenTrophy" => 13,
            "TheRiseofJumbotron" => 14,
            "CrimsonCauldron" => 15,
            "IsleOfLostSkulls" => 16,
            "Soccer" => 17,
            "LaserTag" => 18,
            "RecRoyaleSquads" => 20,
            "RecRoyaleSolos" => 21,
            "Lounge" => 22,
            "PerformanceHall" => 23,
            "StuntRunnerBaseRoom" => 24,
            "Park" => 25,
            "QuestForDracula" => 27,
            "Bowling" => 28,
            "PublicSandbox" => 29,
            "StuntRunner" => 31,
            "Orientation" => 34,
            "MakerRoom" => 50,
            "Hangar" => 51,
            "Lake" => 52,
            "River" => 53,
            "Gym" => 54,
            "RecCenterCustom" => 55,
            "CyberJunkCity" => 56,
            "Homestead" => 57,
            "Quarry" => 58,
            "Clearcut" => 59,
            "Spillway" => 60,
            "Stadium" => 61,
            "PropulsionTestRange" => 62,
            _ => 1
        };

        public static string RoomIdToName(int id) => id switch
        {
            1 => "DormRoom",
            2 => "RecCenter",
            3 => "3DCharades",
            4 => "DiscGolfLake",
            5 => "DiscGolfPropulsion",
            6 => "Dodgeball",
            7 => "Paddleball",
            8 => "Paintball",
            13 => "GoldenTrophy",
            14 => "TheRiseofJumbotron",
            15 => "CrimsonCauldron",
            16 => "IsleOfLostSkulls",
            17 => "Soccer",
            18 => "LaserTag",
            20 => "RecRoyaleSquads",
            21 => "RecRoyaleSolos",
            22 => "Lounge",
            23 => "PerformanceHall",
            24 => "StuntRunnerBaseRoom",
            25 => "Park",
            27 => "QuestForDracula",
            28 => "Bowling",
            29 => "PublicSandbox",
            31 => "StuntRunner",
            34 => "Orientation",
            50 => "MakerRoom",
            51 => "Hangar",
            52 => "Lake",
            53 => "River",
            54 => "Gym",
            55 => "RecCenterCustom",
            56 => "CyberJunkCity",
            57 => "Homestead",
            58 => "Quarry",
            59 => "Clearcut",
            60 => "Spillway",
            61 => "Stadium",
            62 => "PropulsionTestRange",
            _ => "DormRoom"
        };
    }
}
