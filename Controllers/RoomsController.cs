using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Threading;

namespace RetroRec_Server.Controllers
{
    // Rooms, room instances, matchmaking, room currencies, room keys, room
    // images. Now also handles USER-created rooms via the /clone endpoint —
    // players pick a base room template, give it a name, and we create a
    // UserRoom row in the DB owned by them.
    [ApiController]
    public class RoomsController : RetroRecBase
    {
        // User-created rooms get IDs starting at 100000 so they never collide
        // with base/hot/community rooms (those are all in the 1-99 range).
        private const int USER_ROOM_ID_BASE = 100000;

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, List<FlatRoom>>
            _roomsCache = new();

        // ---- photo store (in-memory, resets on restart) ----
        private static long _nextImageId = 1000;

        private class ImageEntry
        {
            public byte[] Data { get; set; }
            public string ContentType { get; set; }
            public int RoomId { get; set; }
            public int AccountId { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ImageEntry>
            _imageStore = new();

        private static readonly (string file, string tag)[] _roomFiles = new[]
        {
            ("hotrooms.txt", "rro"),
            ("baserooms.txt", "base"),
            ("communityrooms.txt", "community")
        };

        private class FlatRoom
        {
            public int RoomId { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public long CreatorPlayerId { get; set; }
            public string ImageName { get; set; }
            public int State { get; set; }
            public int Accessibility { get; set; }
            public bool SupportsLevelVoting { get; set; }
            public bool IsAGRoom { get; set; }
            public bool CloningAllowed { get; set; }
            public bool SupportsScreens { get; set; }
            public bool SupportsWalkVR { get; set; }
            public bool SupportsTeleportVR { get; set; }
        }

        private List<FlatRoom> LoadRooms(string fileName)
        {
            return _roomsCache.GetOrAdd(fileName, key =>
            {
                try
                {
                    var path = Path.Combine(Directory.GetCurrentDirectory(), key);
                    if (System.IO.File.Exists(path))
                    {
                        var json = System.IO.File.ReadAllText(path);
                        return JsonSerializer.Deserialize<List<FlatRoom>>(json,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                            ?? new List<FlatRoom>();
                    }
                }
                catch { }
                return new List<FlatRoom>();
            });
        }

        private FlatRoom FindBaseRoom(int roomId)
        {
            foreach (var pair in _roomFiles)
            {
                var rooms = LoadRooms(pair.file);
                var match = rooms.FirstOrDefault(r => r.RoomId == roomId);
                if (match != null) return match;
            }
            return null;
        }

        private object ExpandRoom(FlatRoom r, string tag)
        {
            var safeTag = string.IsNullOrEmpty(tag) ? "rro" : tag.Replace("#", "");
            var sceneId = RRConstants.GetSceneIdForRoom(r.RoomId);
            int callerId = GetAccountIdFromAuth();
            long currentUserId = callerId > 0 ? callerId : 2L;
            return new
            {
                RoomId = r.RoomId,
                Name = r.Name,
                Description = r.Description ?? "",
                ImageName = r.ImageName ?? "",
                WarningMask = 0,
                CustomWarning = (string)null,
                CreatorAccountId = currentUserId,
                State = r.State,
                Accessibility = r.Accessibility,
                SupportsLevelVoting = r.SupportsLevelVoting,
                IsRRO = r.IsAGRoom,
                SupportsScreens = r.SupportsScreens,
                SupportsWalkVR = r.SupportsWalkVR,
                SupportsTeleportVR = r.SupportsTeleportVR,
                SupportsVRLow = true,
                SupportsMobile = true,
                SupportsJuniors = true,
                MinLevel = 0,
                CreatedAt = "2021-04-18T01:59:14.864Z",
                Stats = new { CheerCount = 0, FavoriteCount = 0, VisitorCount = 1, VisitCount = 1 },
                IsDorm = false,
                MaxPlayerCalculationMode = 0,
                MaxPlayers = 20,
                CloningAllowed = r.CloningAllowed,
                DisableMicAutoMute = true,
                DisableRoomComments = true,
                EncryptVoiceChat = true,
                SubRooms = new object[] {
                    new {
                        SubRoomId = r.RoomId,
                        RoomId = r.RoomId,
                        Name = "Home",
                        DataBlob = "",
                        IsSandbox = false,
                        MaxPlayers = 20,
                        Accessibility = r.Accessibility,
                        UnitySceneId = sceneId,
                        SavedByAccountId = currentUserId
                    }
                },
                Roles = new object[] { new { AccountId = currentUserId, Role = 255 } },
                Tags = new object[] { new { Tag = safeTag, Type = 2 } },
                DataBlob = "",
                PromoImages = new object[] { },
                PromoExternalContent = new object[] { },
                LoadScreens = new object[] { }
            };
        }

        private object ExpandUserRoom(UserRoom u)
        {
            var roomIdForResponse = USER_ROOM_ID_BASE + u.Id;
            var tag = u.IsPublished ? "community" : "rro";
            return new
            {
                RoomId = roomIdForResponse,
                Name = u.Name,
                Description = u.Description ?? "",
                ImageName = u.ImageName ?? "",
                WarningMask = 0,
                CustomWarning = (string)null,
                CreatorAccountId = u.CreatorAccountId,
                State = 0,
                Accessibility = u.Accessibility,
                SupportsLevelVoting = false,
                IsRRO = false,
                SupportsScreens = true,
                SupportsWalkVR = true,
                SupportsTeleportVR = true,
                SupportsVRLow = true,
                SupportsMobile = true,
                SupportsJuniors = true,
                MinLevel = 0,
                CreatedAt = u.CreatedAt,
                Stats = new { CheerCount = 0, FavoriteCount = 0, VisitorCount = 1, VisitCount = 1 },
                IsDorm = false,
                MaxPlayerCalculationMode = 0,
                MaxPlayers = 20,
                CloningAllowed = true,
                DisableMicAutoMute = true,
                DisableRoomComments = true,
                EncryptVoiceChat = true,
                SubRooms = new object[] {
                    new {
                        SubRoomId = roomIdForResponse,
                        RoomId = roomIdForResponse,
                        Name = "Home",
                        DataBlob = u.DataBlob ?? "",
                        IsSandbox = true,
                        MaxPlayers = 20,
                        Accessibility = u.Accessibility,
                        UnitySceneId = u.UnitySceneId,
                        SavedByAccountId = u.CreatorAccountId
                    }
                },
                Roles = new object[] { new { AccountId = u.CreatorAccountId, Role = 255 } },
                Tags = new object[] { },
                DataBlob = u.DataBlob ?? "",
                PromoImages = new object[] { },
                PromoExternalContent = new object[] { },
                LoadScreens = new object[] { }
            };
        }

        private object BuildDormRoom(int roomId)
        {
            int callerId = GetAccountIdFromAuth();
            long currentUserId = callerId > 0 ? callerId : 2L;
            return new
            {
                RoomId = roomId,
                Name = "DormRoom",
                Description = "Your private dorm",
                ImageName = "",
                WarningMask = 0,
                CustomWarning = (string)null,
                CreatorAccountId = currentUserId,
                State = 0,
                Accessibility = 2,
                SupportsLevelVoting = false,
                IsRRO = true,
                SupportsScreens = true,
                SupportsWalkVR = true,
                SupportsTeleportVR = true,
                SupportsVRLow = true,
                SupportsMobile = true,
                SupportsJuniors = true,
                MinLevel = 0,
                CreatedAt = "2021-04-18T01:59:14.864Z",
                Stats = new { CheerCount = 0, FavoriteCount = 0, VisitorCount = 1, VisitCount = 1 },
                IsDorm = true,
                MaxPlayerCalculationMode = 0,
                MaxPlayers = 20,
                CloningAllowed = false,
                DisableMicAutoMute = true,
                DisableRoomComments = true,
                EncryptVoiceChat = true,
                SubRooms = new object[] {
                    new {
                        SubRoomId = 1,
                        RoomId = roomId,
                        Name = "Home",
                        DataBlob = "",
                        IsSandbox = true,
                        MaxPlayers = 20,
                        Accessibility = 2,
                        UnitySceneId = RRConstants.DormSceneId,
                        SavedByAccountId = currentUserId
                    }
                },
                Roles = new object[] { new { AccountId = currentUserId, Role = 255 } },
                Tags = new object[] { new { Tag = "rro", Type = 2 } },
                DataBlob = "",
                PromoImages = new object[] { },
                PromoExternalContent = new object[] { },
                LoadScreens = new object[] { }
            };
        }

        // ============ ROOM DETAILS ============

        [HttpGet("/rooms/{roomId:int}")]
        public IActionResult RoomDetails(int roomId)
        {
            if (roomId == 1) return Pascal(BuildDormRoom(roomId));

            if (roomId >= USER_ROOM_ID_BASE)
            {
                using var db = new RetroRecDb();
                var userRoom = db.UserRooms.FirstOrDefault(u => u.Id == roomId - USER_ROOM_ID_BASE);
                if (userRoom != null) return Pascal(ExpandUserRoom(userRoom));
                return Pascal(BuildDormRoom(1));
            }

            foreach (var pair in _roomFiles)
            {
                var rooms = LoadRooms(pair.file);
                foreach (var r in rooms)
                {
                    if (r.RoomId == roomId)
                        return Pascal(ExpandRoom(r, pair.tag));
                }
            }

            return Pascal(BuildDormRoom(roomId));
        }

        [HttpGet("/rooms/bulk")]
        [HttpGet("/api/rooms/bulk")]
        public IActionResult RoomsBulk([FromQuery(Name = "name")] string[] names, [FromQuery(Name = "id")] int[] ids)
        {
            var results = new List<object>();

            if (names != null)
            {
                foreach (var name in names)
                {
                    var roomId = RRConstants.RoomNameToId(name);
                    if (roomId == 1)
                    {
                        results.Add(BuildDormRoom(roomId));
                        continue;
                    }
                    foreach (var pair in _roomFiles)
                    {
                        var rooms = LoadRooms(pair.file);
                        var match = rooms.FirstOrDefault(r => r.RoomId == roomId);
                        if (match != null)
                        {
                            results.Add(ExpandRoom(match, pair.tag));
                            break;
                        }
                    }
                }
            }

            if (ids != null)
            {
                foreach (var roomId in ids)
                {
                    if (roomId == 1) { results.Add(BuildDormRoom(roomId)); continue; }

                    if (roomId >= USER_ROOM_ID_BASE)
                    {
                        using var db = new RetroRecDb();
                        var userRoom = db.UserRooms.FirstOrDefault(u => u.Id == roomId - USER_ROOM_ID_BASE);
                        if (userRoom != null) results.Add(ExpandUserRoom(userRoom));
                        continue;
                    }

                    foreach (var pair in _roomFiles)
                    {
                        var rooms = LoadRooms(pair.file);
                        var match = rooms.FirstOrDefault(r => r.RoomId == roomId);
                        if (match != null)
                        {
                            results.Add(ExpandRoom(match, pair.tag));
                            break;
                        }
                    }
                }
            }

            return Pascal(results);
        }

        [HttpGet("/rooms/{roomId:int}/playerdata/me")]
        public IActionResult RoomPlayerData(int roomId) => Ok(new { data = "" });

        [HttpGet("/rooms/{roomId:int}/interactionby/me")]
        public IActionResult RoomInteractionByMe(int roomId) => Ok(new
        {
            Cheered = false,
            Favorited = false
        });

        [HttpGet("/rooms/v1/filters")]
        [HttpGet("/api/rooms/v1/filters")]
        public IActionResult RoomFilters() => Ok(new
        {
            PinnedFilters = new[] { "recroomoriginal", "community" },
            PopularFilters = new string[] { }
        });

        [HttpGet("/rooms/favoritedby/me")]
        [HttpGet("/api/rooms/favoritedby/me")]
        public IActionResult FavoritedByMe() => Content("[]", "application/json");

        [HttpPost("/api/rooms/v1/verifyRole")]
        [HttpPost("/rooms/v1/verifyRole")]
        public IActionResult VerifyRole() => Ok(new { verified = true });

        [HttpGet("/api/inventions/v2/mine")]
        [HttpGet("/inventions/v2/mine")]
        public IActionResult MyInventions() => Ok(new object[] { });

        [HttpGet("/api/inventions/v2/search")]
        [HttpGet("/inventions/v2/search")]
        public IActionResult InventionsSearch() => Ok(new object[] { });

        // ============ CREATE ROOM (clone from base) ============

        // Watch's Create menu calls this when you pick a base room and
        // give it a name. Body is form-encoded: name=Testing.
        // We create a new UserRoom row in the DB owned by the calling user,
        // copying the scene/template from the base room.
        [HttpPost("/rooms/{roomId:int}/clone")]
        [HttpPost("/api/rooms/{roomId:int}/clone")]
        public async Task<IActionResult> CloneRoom(int roomId, [FromForm] string name = null, [FromQuery] string nameQ = null)
        {
            int callerId = GetAccountIdFromAuth();
            if (callerId == 0) callerId = 2;

            var roomName = !string.IsNullOrWhiteSpace(name) ? name : nameQ;

            // The Watch UI sometimes posts the new room name as a JSON body
            // (e.g. {"name":"Testing"}) instead of form-encoded. Without this
            // the previous [FromForm] binding gave roomName = null and the
            // newly-cloned room ended up with the auto-generated default name
            // "<Template> Clone" — which the client interpreted as "the create
            // dialog didn't actually save my chosen name → clone failed",
            // even though a room WAS created in the DB. Hence the bug
            // "creating rooms it says clone failed but creates it".
            if (string.IsNullOrWhiteSpace(roomName) && Request.ContentLength.GetValueOrDefault() > 0)
            {
                try
                {
                    Request.EnableBuffering();
                    Request.Body.Position = 0;
                    using var reader = new StreamReader(Request.Body, leaveOpen: true);
                    var body = await reader.ReadToEndAsync();
                    Request.Body.Position = 0;
                    if (!string.IsNullOrWhiteSpace(body) &&
                        (Request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) ?? false))
                    {
                        using var doc = JsonDocument.Parse(body);
                        foreach (var key in new[] { "name", "Name", "roomName", "RoomName" })
                        {
                            if (doc.RootElement.TryGetProperty(key, out var el) &&
                                el.ValueKind == JsonValueKind.String)
                            {
                                roomName = el.GetString();
                                break;
                            }
                        }
                    }
                }
                catch { }
            }

            string templateName, sceneId, imageName;

            if (roomId >= USER_ROOM_ID_BASE)
            {
                using var db = new RetroRecDb();
                var sourceRoom = db.UserRooms.FirstOrDefault(u => u.Id == roomId - USER_ROOM_ID_BASE);
                if (sourceRoom == null)
                {
                    // Fall back instead of BadRequest so the client doesn't
                    // surface a hard "clone failed" error. Treat unknown user
                    // room ids as a generic blank-template clone.
                    templateName = $"Room{roomId}";
                    sceneId = RRConstants.DormSceneId;
                    imageName = "";
                }
                else
                {
                    templateName = sourceRoom.Name;
                    sceneId = sourceRoom.UnitySceneId;
                    imageName = sourceRoom.ImageName ?? "";
                }
            }
            else
            {
                var template = FindBaseRoom(roomId);
                sceneId = RRConstants.GetSceneIdForRoom(roomId);

                // Fall back to dorm scene as last resort so clone never hard-fails
                // just because a room isn't in our flat files or RRConstants mapping.
                if (string.IsNullOrEmpty(sceneId))
                    sceneId = RRConstants.DormSceneId;

                templateName = template?.Name ?? $"Room{roomId}";
                imageName = template?.ImageName ?? "";
            }

            UserRoom newRoom;
            try
            {
                newRoom = new UserRoom
                {
                    Name = string.IsNullOrWhiteSpace(roomName) ? $"{templateName} Clone" : roomName,
                    Description = $"Cloned from {templateName}",
                    CreatorAccountId = callerId,
                    BaseRoomId = roomId,
                    UnitySceneId = sceneId,
                    ImageName = imageName,
                    Accessibility = 2,
                    IsPublished = false,
                    DataBlob = "",
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow
                };

                using var db2 = new RetroRecDb();
                db2.UserRooms.Add(newRoom);
                db2.SaveChanges();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[clone] DB write failed: {ex}");
                return StatusCode(500, new { ErrorCode = 1, Error = "clone_failed" });
            }

            // Wrap the room in the same envelope shape the client expects from
            // every other "I just performed a mutating action" endpoint:
            //   { ErrorCode: 0, Room: <expanded room>, RoomId: <id> }
            // Returning the bare ExpandUserRoom payload made the client treat
            // the response as malformed and surface "clone failed" even though
            // the row was created successfully.
            var expanded = ExpandUserRoom(newRoom);
            var newRoomId = USER_ROOM_ID_BASE + newRoom.Id;
            return Pascal(new
            {
                ErrorCode = 0,
                RoomId = newRoomId,
                Room = expanded,
                Name = newRoom.Name,
                CreatorAccountId = newRoom.CreatorAccountId
            });
        }

        // Save room data (e.g. when user edits with maker pen). Stores the
        // serialized scene blob into the UserRoom row.
        // The Rec Room client uses several different routes for "save my
        // edits", depending on which menu issued the save (Watch's room
        // menu vs. CV2 maker pen vs. legacy 'edit room' flow). We accept
        // every variation so saves don't fall through to the image catchall
        // (which silently returned a 1x1 PNG and left the client with
        // "save failed").
        [HttpPut("/rooms/{roomId:int}")]
        [HttpPut("/api/rooms/{roomId:int}")]
        [HttpPatch("/rooms/{roomId:int}")]
        [HttpPatch("/api/rooms/{roomId:int}")]
        [HttpPost("/rooms/{roomId:int}/save")]
        [HttpPost("/api/rooms/{roomId:int}/save")]
        [HttpPost("/rooms/{roomId:int}/savesubroom")]
        [HttpPost("/api/rooms/{roomId:int}/savesubroom")]
        [HttpPost("/api/rooms/v3/{roomId:int}/save")]
        [HttpPost("/api/rooms/v3/{roomId:int}/savesubroom")]
        [HttpPost("/rooms/{roomId:int}/{subRoomId:int}/save")]
        [HttpPost("/api/rooms/{roomId:int}/{subRoomId:int}/save")]
        [HttpPut("/rooms/{roomId:int}/subroom/{subRoomId:int}")]
        [HttpPut("/api/rooms/{roomId:int}/subroom/{subRoomId:int}")]
        public async Task<IActionResult> SaveRoom(int roomId, int subRoomId = 0)
        {
            // Always read the body first so the client's payload doesn't
            // dangle and trigger a connection-reset retry loop.
            string body;
            try
            {
                Request.EnableBuffering();
                if (Request.Body.CanSeek) Request.Body.Position = 0;
                using var reader = new StreamReader(Request.Body, leaveOpen: true);
                body = await reader.ReadToEndAsync();
                if (Request.Body.CanSeek) Request.Body.Position = 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[save] body read failed for room {roomId}: {ex.Message}");
                return StatusCode(500, new { ErrorCode = 1, Error = "body_read_failed" });
            }

            // Some payloads are JSON envelopes like {"DataBlob":"..."} rather
            // than a raw scene string. Unwrap when we can recognize that.
            string dataToSave = body;
            if (!string.IsNullOrEmpty(body) && (body.TrimStart().StartsWith("{") || body.TrimStart().StartsWith("[")))
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var key in new[] { "DataBlob", "dataBlob", "data", "Data" })
                        {
                            if (doc.RootElement.TryGetProperty(key, out var el) &&
                                el.ValueKind == JsonValueKind.String)
                            {
                                dataToSave = el.GetString() ?? body;
                                break;
                            }
                        }
                    }
                }
                catch { }
            }

            // Base/community rooms aren't backed by a UserRoom row, but the
            // client still calls save when you edit them (it just won't show
            // up to other players). Acknowledge with success so the editor
            // closes cleanly instead of yelling "save failed".
            if (roomId < USER_ROOM_ID_BASE)
            {
                return Pascal(new
                {
                    ErrorCode = 0,
                    RoomId = roomId,
                    SubRoomId = subRoomId == 0 ? roomId : subRoomId,
                    SavedAt = DateTime.UtcNow.ToString("O")
                });
            }

            try
            {
                using var db = new RetroRecDb();
                var room = db.UserRooms.FirstOrDefault(u => u.Id == roomId - USER_ROOM_ID_BASE);
                if (room == null)
                {
                    Console.WriteLine($"[save] room {roomId} not found in DB");
                    return NotFound(new { ErrorCode = 1, Error = "room_not_found" });
                }

                room.DataBlob = dataToSave ?? "";
                room.ModifiedAt = DateTime.UtcNow;
                db.SaveChanges();

                // Return the freshly-saved room in the same envelope as clone.
                // Some clients re-render their room panel from this response;
                // returning Ok({}) made them think the save was lost.
                return Pascal(new
                {
                    ErrorCode = 0,
                    RoomId = roomId,
                    SubRoomId = subRoomId == 0 ? roomId : subRoomId,
                    Room = ExpandUserRoom(room),
                    SavedAt = room.ModifiedAt.ToString("O")
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[save] DB write failed for room {roomId}: {ex}");
                return StatusCode(500, new { ErrorCode = 1, Error = "save_failed" });
            }
        }

        // Mark a user room as published — flips IsPublished=true and changes
        // accessibility so it shows up in the community rooms list.
        [HttpPost("/rooms/{roomId:int}/publish")]
        [HttpPost("/api/rooms/{roomId:int}/publish")]
        public IActionResult PublishRoom(int roomId)
        {
            if (roomId < USER_ROOM_ID_BASE) return BadRequest(new { });

            using var db = new RetroRecDb();
            var room = db.UserRooms.FirstOrDefault(u => u.Id == roomId - USER_ROOM_ID_BASE);
            if (room == null) return NotFound();

            room.IsPublished = true;
            room.Accessibility = 0;
            db.SaveChanges();

            return Pascal(ExpandUserRoom(room));
        }

        // ============ ROOM LISTS ============
        // Two response shapes the client uses depending on which menu:
        //   - Play menu (hot/new/top): expects {TotalResults, Results: [...]}
        //   - Create menu (base/community/ownedby/me): expects RAW ARRAY [...]
        // Returning the wrong shape gives "InvalidCastException Dictionary to List"
        // on the wrong endpoint, so they're split here.

        private List<object> BuildRoomsList(string fileName, string? tag, int? take)
        {
            var rooms = LoadRooms(fileName);
            var limited = take.HasValue && take.Value > 0
                ? rooms.Take(take.Value).ToList()
                : rooms;
            var expanded = new List<object>();
            foreach (var r in limited)
                expanded.Add(ExpandRoom(r, tag ?? "rro"));
            return expanded;
        }

        [HttpGet("/rooms/hot")]
        [HttpGet("/api/rooms/v3/hot")]
        public IActionResult HotRooms([FromQuery] string? tag, [FromQuery] int? take)
        {
            var list = BuildRoomsList("hotrooms.txt", tag, take);
            return Pascal(new { TotalResults = list.Count, Results = list });
        }

        [HttpGet("/rooms/new")]
        [HttpGet("/api/rooms/v3/new")]
        public IActionResult NewRooms([FromQuery] string? tag, [FromQuery] int? take)
        {
            var list = BuildRoomsList("hotrooms.txt", tag, take);
            return Pascal(new { TotalResults = list.Count, Results = list });
        }

        [HttpGet("/rooms/top")]
        [HttpGet("/api/rooms/v3/top")]
        public IActionResult TopRooms([FromQuery] string? tag, [FromQuery] int? take)
        {
            var list = BuildRoomsList("hotrooms.txt", tag, take);
            return Pascal(new { TotalResults = list.Count, Results = list });
        }

        // Create menu's Base Rooms tab — wants raw array, NOT wrapped.
        [HttpGet("/api/rooms/v3/base")]
        [HttpGet("/rooms/base")]
        public IActionResult BaseRooms([FromQuery] string? tag, [FromQuery] int? take)
            => Pascal(BuildRoomsList("baserooms.txt", tag, take));

        // Community rooms — raw array, includes user-published rooms.
        [HttpGet("/api/rooms/v3/community")]
        [HttpGet("/rooms/community")]
        public IActionResult CommunityRooms([FromQuery] string? tag, [FromQuery] int? take)
        {
            var staticRooms = LoadRooms("communityrooms.txt");
            var expanded = new List<object>();
            foreach (var r in staticRooms)
                expanded.Add(ExpandRoom(r, tag ?? "community"));

            using var db = new RetroRecDb();
            var publishedUserRooms = db.UserRooms.Where(u => u.IsPublished).ToList();
            foreach (var u in publishedUserRooms)
                expanded.Add(ExpandUserRoom(u));

            if (take.HasValue && take.Value > 0)
                expanded = expanded.Take(take.Value).ToList();

            return Pascal(expanded);
        }

        [HttpGet("/rooms/createdby/me")]
        [HttpGet("/rooms/ownedby/me")]
        [HttpGet("/api/rooms/createdby/me")]
        [HttpGet("/api/rooms/ownedby/me")]
        public IActionResult MyCreatedRooms()
        {
            int callerId = GetAccountIdFromAuth();
            if (callerId == 0) return Ok(new object[] { });

            using var db = new RetroRecDb();
            var myRooms = db.UserRooms.Where(u => u.CreatorAccountId == callerId).ToList();
            var expanded = myRooms.Select(u => ExpandUserRoom(u)).ToList();
            return Pascal(expanded);
        }

        [HttpGet("/rooms/ownedby/{playerId:int}")]
        [HttpGet("/api/rooms/ownedby/{playerId:int}")]
        public IActionResult RoomsOwnedBy(int playerId)
        {
            using var db = new RetroRecDb();
            var rooms = db.UserRooms.Where(u => u.CreatorAccountId == playerId).ToList();
            var expanded = rooms.Select(u => ExpandUserRoom(u)).ToList();
            return Pascal(expanded);
        }

        [HttpGet("/rooms/{roomId:int}/bans")]
        [HttpGet("/api/rooms/{roomId:int}/bans")]
        [HttpPost("/rooms/{roomId:int}/bans")]
        [HttpPost("/api/rooms/{roomId:int}/bans")]
        [HttpDelete("/rooms/{roomId:int}/bans")]
        [HttpDelete("/api/rooms/{roomId:int}/bans")]
        public IActionResult RoomBans(int roomId) => Ok(new object[] { });

        // ============ MATCHMAKING / GOTO ============

        [HttpPost("/goto/room/{roomName}")]
        [HttpGet("/goto/room/{roomName}")]
        [HttpPost("/goto/room/{roomName}/{subRoomName}")]
        [HttpGet("/goto/room/{roomName}/{subRoomName}")]
        [HttpPost("/matchmaking/v1/goto/room/{roomName}")]
        [HttpGet("/matchmaking/v1/goto/room/{roomName}")]
        [HttpPost("/matchmaking/v1/goto/room/{roomName}/{subRoomName}")]
        [HttpGet("/matchmaking/v1/goto/room/{roomName}/{subRoomName}")]
        [HttpPost("/matchmaking/v2/goto/room/{roomName}")]
        [HttpGet("/matchmaking/v2/goto/room/{roomName}")]
        [HttpPost("/matchmaking/v2/goto/room/{roomName}/{subRoomName}")]
        [HttpGet("/matchmaking/v2/goto/room/{roomName}/{subRoomName}")]
        [HttpPost("/api/matchmaking/v1/goto/room/{roomName}")]
        [HttpGet("/api/matchmaking/v1/goto/room/{roomName}")]
        [HttpPost("/api/matchmaking/v1/goto/room/{roomName}/{subRoomName}")]
        [HttpGet("/api/matchmaking/v1/goto/room/{roomName}/{subRoomName}")]
        [HttpPost("/api/matchmaking/v2/goto/room/{roomName}")]
        [HttpGet("/api/matchmaking/v2/goto/room/{roomName}")]
        [HttpPost("/api/matchmaking/v2/goto/room/{roomName}/{subRoomName}")]
        [HttpGet("/api/matchmaking/v2/goto/room/{roomName}/{subRoomName}")]
        public IActionResult GotoRoom(string roomName, string subRoomName = null)
        {
            int roomId = RRConstants.RoomNameToId(roomName);

            if (roomId == 1 && !string.Equals(roomName, "DormRoom", StringComparison.OrdinalIgnoreCase))
            {
                int callerId = GetAccountIdFromAuth();
                using var db = new RetroRecDb();
                var userRoom = db.UserRooms.FirstOrDefault(u =>
                    u.Name == roomName &&
                    (callerId == 0 || u.CreatorAccountId == callerId));
                if (userRoom != null)
                    return BuildGotoResponse(roomName, USER_ROOM_ID_BASE + userRoom.Id);
            }

            return BuildGotoResponse(roomName, roomId);
        }

        [HttpPost("/goto/roomId/{roomId:int}")]
        [HttpGet("/goto/roomId/{roomId:int}")]
        [HttpPost("/matchmaking/v1/goto/roomId/{roomId:int}")]
        [HttpGet("/matchmaking/v1/goto/roomId/{roomId:int}")]
        [HttpPost("/matchmaking/v2/goto/roomId/{roomId:int}")]
        [HttpGet("/matchmaking/v2/goto/roomId/{roomId:int}")]
        [HttpPost("/api/matchmaking/v1/goto/roomId/{roomId:int}")]
        [HttpGet("/api/matchmaking/v1/goto/roomId/{roomId:int}")]
        [HttpPost("/api/matchmaking/v2/goto/roomId/{roomId:int}")]
        [HttpGet("/api/matchmaking/v2/goto/roomId/{roomId:int}")]
        public IActionResult GotoRoomId(int roomId)
        {
            string roomName;
            if (roomId >= USER_ROOM_ID_BASE)
            {
                using var db = new RetroRecDb();
                var ur = db.UserRooms.FirstOrDefault(u => u.Id == roomId - USER_ROOM_ID_BASE);
                roomName = ur?.Name ?? "DormRoom";
            }
            else
            {
                roomName = RRConstants.RoomIdToName(roomId);
            }
            return BuildGotoResponse(roomName, roomId);
        }

        private IActionResult BuildGotoResponse(string roomName, int roomId)
        {
            var isDorm = roomId == 1;
            string sceneId;
            string dataBlob = "";

            if (roomId >= USER_ROOM_ID_BASE)
            {
                using var db = new RetroRecDb();
                var ur = db.UserRooms.FirstOrDefault(u => u.Id == roomId - USER_ROOM_ID_BASE);
                sceneId = ur?.UnitySceneId ?? RRConstants.DormSceneId;
                dataBlob = ur?.DataBlob ?? "";
            }
            else
            {
                sceneId = RRConstants.GetSceneIdForRoom(roomId);
            }

            var subRoomId = 1;

            object roomInstance;
            if (isDorm)
            {
                int instanceId = Interlocked.Increment(ref PartyState.NextRoomInstanceId);
                roomInstance = new
                {
                    RoomInstanceId = instanceId,
                    RoomId = roomId,
                    SubRoomId = subRoomId,
                    RoomInstanceType = 0,
                    Location = sceneId,
                    PhotonRegionId = "us",
                    PhotonRoomId = $"{roomId}Re5.2.0born",
                    Name = $"^{roomName}",
                    MaxCapacity = 20,
                    DataBlob = dataBlob,
                    IsFull = false,
                    IsPrivate = true,
                    IsInProgress = false
                };
            }
            else
            {
                var key = (roomId, subRoomId);
                roomInstance = PartyState.RoomInstancesByRoom.GetOrAdd(key, _ =>
                {
                    int instanceId = Interlocked.Increment(ref PartyState.NextRoomInstanceId);
                    return new
                    {
                        RoomInstanceId = instanceId,
                        RoomId = roomId,
                        SubRoomId = subRoomId,
                        RoomInstanceType = 0,
                        Location = sceneId,
                        PhotonRegionId = "us",
                        PhotonRoomId = $"{roomId}Re5.2.0born",
                        Name = $"^{roomName}",
                        MaxCapacity = 20,
                        DataBlob = dataBlob,
                        IsFull = false,
                        IsPrivate = false,
                        IsInProgress = false
                    };
                });
            }

            int playerId = GetAccountIdFromAuth();
            if (playerId == 0) playerId = 2;
            UserRoomInstances[playerId] = roomInstance;

            foreach (var kvp in PartyState.MemberOf.Where(m => m.Value == playerId))
            {
                // Immediately update the member's room so their next heartbeat
                // returns the leader's room instance and the client auto-follows.
                // Important: we do NOT also synthesize an invite here — members
                // are already in the leader's party, so re-issuing an invite
                // every time the leader teleports causes the "X wants to go
                // with you" notification to fire on every room change.
                // Updating their RoomInstance is enough; the heartbeat-driven
                // auto-follow path handles the rest.
                UserRoomInstances[kvp.Key] = roomInstance;
            }

            return Pascal(new
            {
                ErrorCode = 0,
                RoomInstance = roomInstance
            });
        }

        // ============ ROOM INSTANCES ============

        [HttpPost("/roominstance/{id}/reportjoinresult")]
        public IActionResult ReportJoin(long id) => NoContent();

        [HttpPost("/roominstance/{id}/inprogress")]
        [HttpPut("/roominstance/{id}/inprogress")]
        public IActionResult ReportInProgress(long id) => NoContent();

        // ============ ROOM CURRENCIES / EQUIPMENT / KEYS ============

        [HttpGet("/api/roomcurrencies/v1/currencies")]
        [HttpGet("/roomcurrencies/v1/currencies")]
        public IActionResult RoomCurrencies() => Ok(new object[] { });

        [HttpGet("/api/roomcurrencies/v1/getAllBalances")]
        [HttpGet("/roomcurrencies/v1/getAllBalances")]
        public IActionResult RoomCurrenciesAllBalances() => Ok(new object[] { });

        [HttpGet("/api/roomcurrencies/v1/betaEnabled")]
        [HttpGet("/roomcurrencies/v1/betaEnabled")]
        public IActionResult RoomCurrenciesBeta() => Ok(true);

        [HttpGet("/api/equipment/v2/getUnlocked")]
        public IActionResult EquipmentUnlocked() => Ok(new object[] { });

        [HttpGet("/api/consumables/v2/getUnlocked")]
        public IActionResult ConsumablesUnlocked() => Ok(new object[] { });

        [HttpGet("/api/roomkeys/v1/mine")]
        public IActionResult RoomKeysMine() => Ok(new object[] { });

        [HttpGet("/api/roomkeys/v1/room")]
        [HttpGet("/roomkeys/v1/room")]
        public IActionResult RoomKeysRoom() => Ok(new object[] { });

        [HttpGet("/api/quickPlay/v1/getandclear")]
        public IActionResult QuickPlayGetAndClear() => Ok(new { });

        // ============ IN-GAME CAMERA ============

        // Helper: builds the photo metadata object used in every image response.
        private object PhotoMeta(string id, ImageEntry e) => new
        {
            ImageId = id,
            ImageName = $"^img{id}",       // client strips ^ → requests GET /img{id}
            AccountId = e.AccountId,
            RoomId = e.RoomId,
            PlayerCreated = true,
            CreatedAt = e.CreatedAt.ToString("O"),
            CheerCount = 0,
            TaggedPlayerIds = new int[] { }
        };

        // Camera gadget upload. Tries multipart first, raw-body as fallback
        // so we catch however Unity decides to send it.
        [HttpPost("/api/images/v1")]
        [HttpPost("/images/v1")]
        [HttpPost("/api/images/v2")]
        [HttpPost("/images/v2")]
        public async Task<IActionResult> UploadImage()
        {
            int accountId = GetAccountIdFromAuth();
            if (accountId == 0) accountId = 2;

            int roomId = 1;
            byte[] imageData = null;
            string contentType = "image/jpeg";

            if (Request.HasFormContentType)
            {
                if (Request.Form.TryGetValue("roomId", out var rv))
                    int.TryParse(rv, out roomId);

                if (Request.Form.Files.Count > 0)
                {
                    var f = Request.Form.Files[0];
                    contentType = string.IsNullOrEmpty(f.ContentType) ? "image/jpeg" : f.ContentType;
                    using var ms = new System.IO.MemoryStream();
                    await f.CopyToAsync(ms);
                    imageData = ms.ToArray();
                }
            }

            if (imageData == null)           // raw-body fallback
            {
                using var ms = new System.IO.MemoryStream();
                await Request.Body.CopyToAsync(ms);
                if (ms.Length > 0) imageData = ms.ToArray();
            }

            if (imageData == null || imageData.Length == 0)
                return BadRequest(new { error = "no image data" });

            var id = Interlocked.Increment(ref _nextImageId).ToString();
            var entry = new ImageEntry
            {
                Data = imageData,
                ContentType = contentType,
                RoomId = roomId,
                AccountId = accountId,
                CreatedAt = DateTime.UtcNow
            };
            _imageStore[id] = entry;

            return Pascal(PhotoMeta(id, entry));
        }

        // Serve image bytes. Client resolves ImageName "^imgXXX" → strips "^" →
        // hits GET /imgXXX on this server. We also expose the /api/... form
        // as a direct alternative.
        [HttpGet("/img{imageId}")]
        public IActionResult ServePhotoByName(string imageId)
        {
            if (!_imageStore.TryGetValue(imageId, out var e)) return NotFound();
            return File(e.Data, e.ContentType);
        }

        [HttpGet("/api/images/v1/{imageId}/image")]
        [HttpGet("/images/v1/{imageId}/image")]
        [HttpGet("/api/images/v2/{imageId}/image")]
        [HttpGet("/images/v2/{imageId}/image")]
        public IActionResult ServePhotoById(string imageId)
        {
            if (!_imageStore.TryGetValue(imageId, out var e)) return NotFound();
            return File(e.Data, e.ContentType);
        }

        // Metadata-only (no bytes) — album/gallery UI uses this.
        [HttpGet("/api/images/v1/{imageId}")]
        [HttpGet("/images/v1/{imageId}")]
        [HttpGet("/api/images/v2/{imageId}")]
        [HttpGet("/images/v2/{imageId}")]
        public IActionResult GetImageMeta(string imageId)
        {
            if (!_imageStore.TryGetValue(imageId, out var e)) return NotFound();
            return Pascal(PhotoMeta(imageId, e));
        }

        // Room photo album — all photos taken in this room, newest first.
        [HttpGet("/api/images/v4/room/{roomId:int}")]
        [HttpGet("/images/v4/room/{roomId:int}")]
        public IActionResult RoomSavedImages(int roomId)
        {
            var photos = _imageStore
                .Where(kv => kv.Value.RoomId == roomId)
                .OrderByDescending(kv => kv.Value.CreatedAt)
                .Select(kv => PhotoMeta(kv.Key, kv.Value))
                .ToList<object>();

            return Pascal(photos);
        }

        // Cheer a photo — acknowledge, no persistent state needed.
        [HttpPost("/api/images/v1/cheer")]
        [HttpPost("/images/v1/cheer")]
        public IActionResult CheerImage() => Pascal(new { CheerCount = 1 });

        // Bulk cheered + named image stubs — stop 404 noise.
        [HttpGet("/api/images/v5/cheered/bulk")]
        [HttpGet("/images/v5/cheered/bulk")]
        public IActionResult CheeredBulk() => Ok(new object[] { });

        [HttpGet("/api/images/v2/named")]
        [HttpGet("/images/v2/named")]
        public IActionResult NamedImages() => Ok(new object[] { });
    }
}
