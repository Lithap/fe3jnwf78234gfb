using Microsoft.AspNetCore.Mvc;

namespace RetroRec_Server.Controllers
{
    // Player session endpoints: presence, heartbeat, login/logout, save data,
    // photon pings, bio, progression, reputation, roles, subscription, and
    // relationships. Party and invite endpoints live in PartyController.
    [ApiController]
    public class PlayerController : RetroRecBase
    {
        // ============ PRESENCE / SESSION ============

        [HttpGet("/player")]
        public IActionResult PlayerPresence()
        {
            int playerId = GetAccountIdFromAuth();
            if (playerId == 0) playerId = 2;
            UserRoomInstances.TryGetValue(playerId, out var roomInstance);
            return Pascal(new object[] {
                new {
                    PlayerId = playerId,
                    StatusVisibility = 0,
                    DeviceClass = 0,
                    RoomInstance = roomInstance,
                    IsOnline = true
                }
            });
        }

        [HttpPost("/player/login")]
        public IActionResult PlayerLogin() => Ok(new { });

        // Same fix as /player above — return THIS user's id and THIS user's
        // room instance, not a global hardcoded one.
        [HttpPost("/player/heartbeat")]
        public IActionResult Heartbeat()
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

        [HttpPost("/player/logout")]
        public IActionResult Logout()
        {
            int playerId = GetAccountIdFromAuth();
            if (playerId > 0) UserRoomInstances.TryRemove(playerId, out _);
            return Ok(new { });
        }

        // Client calls this when its hub/SignalR connection drops unexpectedly
        // (network blip, alt-tab, etc.). Body is form-encoded:
        // PlayerId=X&RoomInstanceId=Y. Clears the user from room state so they
        // can rejoin cleanly next time. Without this, the disconnect leaves
        // stale state and re-entering rooms gets stuck on loading forever.
        [HttpPost("/player/notifydisconnect")]
        public IActionResult NotifyDisconnect()
        {
            int playerId = GetAccountIdFromAuth();
            if (playerId > 0) UserRoomInstances.TryRemove(playerId, out _);
            return Ok(new { });
        }

        [HttpGet("/player/photonregionpings")]
        [HttpPost("/player/photonregionpings")]
        [HttpPut("/player/photonregionpings")]
        public IActionResult PhotonPings() => NoContent();

        [HttpGet("/player/statusvisibility")]
        [HttpPost("/player/statusvisibility")]
        [HttpPut("/player/statusvisibility")]
        public IActionResult StatusVis() => NoContent();

        [HttpGet("/player/save/{id}")]
        public IActionResult PlayerSave(long id) => Ok(new { data = "" });

        [HttpPost("/player/save/{id}")]
        [HttpPut("/player/save/{id}")]
        public IActionResult SavePlayer(long id) => Ok(new { });

        // ============ PROGRESSION ============

        // Reads from the actual Accounts table now (Level + XP columns added
        // via the AddLevelAndXP migration) instead of hardcoded 50/9999.
        [HttpGet("/api/players/v1/progression/{id}")]
        public IActionResult Progression(long id)
        {
            using var db = new RetroRecDb();
            var account = db.Accounts.FirstOrDefault(a => a.Id == (int)id);
            return Pascal(new
            {
                PlayerId = id,
                Level = account?.Level ?? 0,
                XP = account?.XP ?? 0
            });
        }

        [HttpGet("/api/players/v2/progression/bulk")]
        public IActionResult ProgressionBulk([FromQuery(Name = "id")] long[] ids)
        {
            var results = new List<object>();
            if (ids != null && ids.Length > 0)
            {
                using var db = new RetroRecDb();
                var intIds = ids.Select(id => (int)id).ToList();
                var accounts = db.Accounts
                    .Where(a => intIds.Contains(a.Id))
                    .ToDictionary(a => a.Id);

                foreach (var id in ids)
                {
                    accounts.TryGetValue((int)id, out var account);
                    results.Add(new
                    {
                        PlayerId = id,
                        Level = account?.Level ?? 0,
                        XP = account?.XP ?? 0
                    });
                }
            }
            return Pascal(results);
        }

        // ============ REPUTATION ============

        [HttpGet("/api/playerReputation/v1/{id}")]
        public IActionResult Reputation(long id) => Pascal(new
        {
            AccountId = id,
            IsCheerful = false,
            Noteriety = 0,
            CheerGeneral = 1,
            CheerHelpful = 1,
            CheerGreatHost = 1,
            CheerSportsman = 1,
            CheerCreative = 1,
            CheerCredit = 77,
            SubscriberCount = 2,
            SubscribedCount = 0,
            SelectedCheer = 40
        });

        [HttpGet("/api/playerReputation/v2/bulk")]
        public IActionResult ReputationBulk([FromQuery(Name = "id")] long[] ids)
        {
            var results = new List<object>();
            if (ids != null)
            {
                foreach (var id in ids)
                {
                    results.Add(new
                    {
                        AccountId = id,
                        IsCheerful = false,
                        Noteriety = 0,
                        CheerGeneral = 1,
                        CheerHelpful = 1,
                        CheerGreatHost = 1,
                        CheerSportsman = 1,
                        CheerCreative = 1,
                        CheerCredit = 77,
                        SubscriberCount = 2,
                        SubscribedCount = 0,
                        SelectedCheer = 40
                    });
                }
            }
            return Pascal(results);
        }

        [HttpPost("/api/playerReputation/v1/cheer")]
        [HttpPost("/api/playerReputation/v1/cheer/{targetId}")]
        public IActionResult Cheer(long targetId = 0, [FromQuery] long id = 0)
        {
            long cheerTarget = targetId > 0 ? targetId : id;
            return Pascal(new
            {
                AccountId = cheerTarget,
                IsCheerful = true,
                Noteriety = 0,
                CheerGeneral = 2,
                CheerHelpful = 1,
                CheerGreatHost = 1,
                CheerSportsman = 1,
                CheerCreative = 1,
                CheerCredit = 77,
                SubscriberCount = 2,
                SubscribedCount = 0,
                SelectedCheer = 40
            });
        }

        // ============ REPORTING ============

        [HttpGet("/api/PlayerReporting/v1/moderationBlockDetails")]
        public IActionResult ModBlock() => Pascal(new
        {
            ReportCategory = 0,
            Duration = 0,
            GameSessionId = 0,
            Message = ""
        });

        [HttpGet("/api/PlayerReporting/v1/voteToKickReasons")]
        public IActionResult VoteKickReasons() => Ok(new object[] { });

        [HttpPost("/api/PlayerReporting/v1/hile")]
        public IActionResult Hile() => NoContent();

        // ============ BIO ============

        // Reads the bio from the persistent Bios table (replaces the old
        // in-memory PartyState.Bios dictionary). Falls back to in-memory
        // for accounts that wrote a bio before the DB-backed migration so
        // nothing is lost during the rollover. Once any bio is set in the
        // new code path it goes straight to disk.
        [HttpGet("/account/{id}/bio")]
        [HttpGet("/api/account/{id}/bio")]
        public IActionResult GetBio(long id)
        {
            using var db = new RetroRecDb();
            var row = db.Bios.FirstOrDefault(b => b.AccountId == id);
            if (row != null) return Pascal(new { AccountId = id, Bio = row.Bio ?? "" });

            PartyState.Bios.TryGetValue(id, out var bio);
            return Pascal(new { AccountId = id, Bio = bio ?? "" });
        }

        // SetBio reads either {"bio":"..."} JSON OR a form field named bio
        // OR a raw-string body (depending on which menu issued the save)
        // and upserts a single Bios row keyed by AccountId. SaveChanges is
        // wrapped in try/catch so a transient DB error doesn't 500-out the
        // whole bio screen — the in-memory copy is updated either way so
        // the user sees their change immediately.
        [HttpPut("/account/{id}/bio")]
        [HttpPut("/api/account/{id}/bio")]
        [HttpPost("/account/{id}/bio")]
        [HttpPost("/api/account/{id}/bio")]
        public async Task<IActionResult> SetBio(long id)
        {
            Request.EnableBuffering();
            if (Request.Body.CanSeek) Request.Body.Position = 0;
            using var reader = new System.IO.StreamReader(Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            if (Request.Body.CanSeek) Request.Body.Position = 0;
            string? bio = null;

            try
            {
                if (!string.IsNullOrWhiteSpace(body) &&
                    (body.TrimStart().StartsWith("{") || body.TrimStart().StartsWith("[")))
                {
                    var doc = System.Text.Json.JsonDocument.Parse(body);
                    foreach (var key in new[] { "bio", "Bio" })
                    {
                        if (doc.RootElement.TryGetProperty(key, out var bioEl) &&
                            bioEl.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            bio = bioEl.GetString();
                            break;
                        }
                    }
                }
            }
            catch { }

            if (bio == null && Request.HasFormContentType &&
                Request.Form.TryGetValue("bio", out var formBio))
                bio = formBio.ToString();

            if (bio == null && !string.IsNullOrEmpty(body) &&
                !body.TrimStart().StartsWith("{") && !body.TrimStart().StartsWith("["))
                bio = body;

            bio ??= "";

            PartyState.Bios[id] = bio;

            try
            {
                using var db = new RetroRecDb();
                var row = db.Bios.FirstOrDefault(b => b.AccountId == id);
                if (row == null)
                {
                    db.Bios.Add(new PlayerBio
                    {
                        AccountId = id,
                        Bio = bio,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    row.Bio = bio;
                    row.UpdatedAt = DateTime.UtcNow;
                }
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[bio] persist failed for account {id}: {ex.Message}");
            }

            return Pascal(new { AccountId = id, Bio = bio });
        }

        [HttpGet("/account/me/bio")]
        [HttpGet("/api/account/me/bio")]
        public IActionResult GetMyBio()
        {
            int myId = GetAccountIdFromAuth();
            if (myId == 0) myId = 2;
            return GetBio(myId);
        }

        [HttpPut("/account/me/bio")]
        [HttpPut("/api/account/me/bio")]
        [HttpPost("/account/me/bio")]
        [HttpPost("/api/account/me/bio")]
        [HttpPatch("/account/me/bio")]
        [HttpPatch("/api/account/me/bio")]
        public async Task<IActionResult> SetMyBio()
        {
            int myId = GetAccountIdFromAuth();
            if (myId == 0) myId = 2;
            return await SetBio(myId);
        }

        // ============ SHOWCASE / SUBSCRIPTION ============

        [HttpGet("/showcase/{id}")]
        [HttpGet("/api/showcase/{id}")]
        public IActionResult Showcase(long id) => Ok(new object[] { });

        [HttpGet("/subscription/mine/member")]
        public IActionResult SubscriptionMine() => Ok(new object[] { });

        [HttpGet("/subscription/subscriberCount/{id}")]
        [HttpGet("/api/subscription/subscriberCount/{id}")]
        public IActionResult SubscriberCount(long id) => Pascal(new { AccountId = id, SubscriberCount = 0 });

        [HttpGet("/subscription/details/{id}")]
        public IActionResult SubscriptionDetails(long id) => Ok(new
        {
            accountId = id,
            clubId = 0,
            subscriberCount = 2
        });

        // ============ ROLES ============

        // Returns plain-text "true" or "false". Player 2435 is a developer.
        // All other roles and all other accounts return false so the client
        // doesn't treat every player as staff or apply any developer-only caps.
        [HttpGet("/role/{roleName}/{id}")]
        [HttpGet("/api/role/{roleName}/{id}")]
        public IActionResult GetRole(string roleName, long id)
        {
            bool hasRole = roleName == "developer" && id == 2435;
            return Content(hasRole ? "true" : "false", "text/plain");
        }

        // Bulk Steam ID lookup. Without this, "Failed to GetAccountsFromPlatformIds:
        // HTTP Error 404" spams the log. We don't have account data for arbitrary
        // Steam IDs so just return an empty list — the client handles it gracefully.
        [HttpPost("/cachedlogin/forplatformids")]
        public IActionResult CachedLoginForPlatformIds() => Ok(new object[] { });

        // ============ RELATIONSHIPS ============

        [HttpPost("/api/relationships/v1/bulkignoreplatformusers")]
        public IActionResult BulkIgnore() => Ok(new object[] { });

        [HttpGet("/api/externalfriendinvite/v1/getplatformreferrers")]
        public IActionResult PlatformReferrers() => Ok(new object[] { });

        // Returns the caller's relationships with correct directional types:
        //   type 2 = I sent a request to them (pending outgoing)
        //   type 3 = They sent a request to me (pending incoming)
        //   type 4 = Both sent requests = mutual friends
        [HttpGet("/api/relationships/v2/get")]
        [HttpGet("/relationships/v2/get")]
        [HttpGet("/api/relationships/v1/get")]
        [HttpGet("/relationships/v1/get")]
        public IActionResult RelationshipsV2Get()
        {
            int myId = GetAccountIdFromAuth();
            if (myId == 0) myId = 2;

            using var db = new RetroRecDb();
            // Pull every relationship this user is a party to from the DB so
            // friendships survive server restarts. Old in-memory rows from
            // PartyState.FriendRequests are merged in for backward compat
            // during the rollover (they migrate to disk as soon as the user
            // re-adds the friend or the partner sends a return request).
            var dbRows = db.FriendRelationships
                .Where(r => r.SenderId == myId || r.TargetId == myId)
                .ToList();

            var pairs = new HashSet<(int sender, int target)>();
            foreach (var r in dbRows) pairs.Add((r.SenderId, r.TargetId));

            foreach (var key in PartyState.FriendRequests.Keys)
            {
                var parts = key.Split('_');
                if (parts.Length != 2) continue;
                if (!int.TryParse(parts[0], out var sender) || !int.TryParse(parts[1], out var target)) continue;
                if (sender != myId && target != myId) continue;
                pairs.Add((sender, target));
            }

            var results = new List<object>();
            foreach (var (sender, target) in pairs)
            {
                bool iSent = sender == myId;
                int otherId = iSent ? target : sender;
                bool theyAlsoSent = pairs.Contains((otherId, myId));

                if (!iSent && theyAlsoSent) continue;

                int relType;
                if (theyAlsoSent)
                    relType = 4;
                else if (iSent)
                    relType = 2;
                else
                    relType = 3;

                results.Add(new
                {
                    SubjectAccountId = iSent ? myId : otherId,
                    ObjectAccountId = iSent ? otherId : myId,
                    Type = relType
                });
            }
            return Pascal(results);
        }

        // Resolves the "other player's id" from any of the half-dozen places
        // the client puts it: path segment, query string, form body, or JSON
        // body. Without all of these, friend requests silently no-op because
        // the client posts targetAccountId in the form body but the old code
        // only read [FromQuery]. That's why "friend request just fails completely".
        private int ResolveFriendIdFromRequest(int routeId, int qId, int qAccountId, int qTargetId, int qTargetAccountId)
        {
            if (routeId != 0) return routeId;
            if (qId != 0) return qId;
            if (qAccountId != 0) return qAccountId;
            if (qTargetId != 0) return qTargetId;
            if (qTargetAccountId != 0) return qTargetAccountId;

            try
            {
                if (Request.HasFormContentType)
                {
                    foreach (var key in new[] { "id", "accountId", "targetId", "targetAccountId", "playerId" })
                    {
                        if (Request.Form.TryGetValue(key, out var v) && int.TryParse(v, out var parsed) && parsed != 0)
                            return parsed;
                    }
                }
            }
            catch { }

            try
            {
                if (Request.ContentLength.GetValueOrDefault() > 0 &&
                    (Request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    Request.EnableBuffering();
                    Request.Body.Position = 0;
                    using var reader = new StreamReader(Request.Body, leaveOpen: true);
                    var body = reader.ReadToEnd();
                    Request.Body.Position = 0;
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(body);
                        foreach (var key in new[] { "id", "accountId", "targetId", "targetAccountId", "playerId" })
                        {
                            if (doc.RootElement.TryGetProperty(key, out var el))
                            {
                                if (el.ValueKind == System.Text.Json.JsonValueKind.Number && el.TryGetInt32(out var n) && n != 0)
                                    return n;
                                if (el.ValueKind == System.Text.Json.JsonValueKind.String && int.TryParse(el.GetString(), out var sn) && sn != 0)
                                    return sn;
                            }
                        }
                    }
                }
            }
            catch { }

            return 0;
        }

        [HttpGet("/api/relationships/v2/addfriend")]
        [HttpPost("/api/relationships/v2/addfriend")]
        [HttpGet("/relationships/v2/addfriend")]
        [HttpPost("/relationships/v2/addfriend")]
        [HttpGet("/api/relationships/v1/addfriend")]
        [HttpPost("/api/relationships/v1/addfriend")]
        [HttpGet("/relationships/v1/addfriend")]
        [HttpPost("/relationships/v1/addfriend")]
        [HttpGet("/api/relationships/v2/sendfriendrequest")]
        [HttpPost("/api/relationships/v2/sendfriendrequest")]
        [HttpGet("/relationships/v2/sendfriendrequest")]
        [HttpPost("/relationships/v2/sendfriendrequest")]
        [HttpGet("/api/relationships/v1/sendfriendrequest")]
        [HttpPost("/api/relationships/v1/sendfriendrequest")]
        [HttpGet("/relationships/v1/sendfriendrequest")]
        [HttpPost("/relationships/v1/sendfriendrequest")]
        // Path-style variants — client sometimes posts the id as the last
        // path segment (e.g. POST /api/relationships/v2/addfriend/1234).
        [HttpGet("/api/relationships/v2/addfriend/{routeId:int}")]
        [HttpPost("/api/relationships/v2/addfriend/{routeId:int}")]
        [HttpGet("/relationships/v2/addfriend/{routeId:int}")]
        [HttpPost("/relationships/v2/addfriend/{routeId:int}")]
        [HttpGet("/api/relationships/v2/sendfriendrequest/{routeId:int}")]
        [HttpPost("/api/relationships/v2/sendfriendrequest/{routeId:int}")]
        [HttpGet("/relationships/v2/sendfriendrequest/{routeId:int}")]
        [HttpPost("/relationships/v2/sendfriendrequest/{routeId:int}")]
        // Some client builds POST to /api/relationships/v2/{id}/addfriend
        [HttpPost("/api/relationships/v2/{routeId:int}/addfriend")]
        [HttpPost("/relationships/v2/{routeId:int}/addfriend")]
        [HttpPost("/api/relationships/v2/{routeId:int}/sendfriendrequest")]
        [HttpPost("/relationships/v2/{routeId:int}/sendfriendrequest")]
        public IActionResult AddFriend(
            int routeId = 0,
            [FromQuery] int id = 0,
            [FromQuery] int accountId = 0,
            [FromQuery] int targetId = 0,
            [FromQuery] int targetAccountId = 0)
        {
            int myId = GetAccountIdFromAuth();
            if (myId == 0) myId = 2;

            int friendId = ResolveFriendIdFromRequest(routeId, id, accountId, targetId, targetAccountId);
            if (friendId == 0 || friendId == myId)
                return Pascal(new { ErrorCode = 0, SubjectAccountId = myId, ObjectAccountId = friendId, Type = 0 });

            // Always update the in-memory cache so reads later in the same
            // session see the change immediately, and persist to the DB so
            // the friendship survives a restart.
            PartyState.FriendRequests.TryAdd($"{myId}_{friendId}", true);

            bool mutual;
            try
            {
                using var db = new RetroRecDb();
                bool alreadyHave = db.FriendRelationships
                    .Any(r => r.SenderId == myId && r.TargetId == friendId);
                if (!alreadyHave)
                {
                    db.FriendRelationships.Add(new FriendRelationship
                    {
                        SenderId = myId,
                        TargetId = friendId,
                        CreatedAt = DateTime.UtcNow
                    });
                    db.SaveChanges();
                }
                mutual = db.FriendRelationships
                    .Any(r => r.SenderId == friendId && r.TargetId == myId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[friends] persist failed: {ex.Message}");
                mutual = PartyState.FriendRequests.ContainsKey($"{friendId}_{myId}");
            }

            return Pascal(new
            {
                ErrorCode = 0,
                SubjectAccountId = myId,
                ObjectAccountId = friendId,
                Type = mutual ? 4 : 2
            });
        }

        [HttpGet("/api/relationships/v2/removefriend")]
        [HttpPost("/api/relationships/v2/removefriend")]
        [HttpGet("/relationships/v2/removefriend")]
        [HttpPost("/relationships/v2/removefriend")]
        [HttpGet("/api/relationships/v1/removefriend")]
        [HttpPost("/api/relationships/v1/removefriend")]
        [HttpGet("/relationships/v1/removefriend")]
        [HttpPost("/relationships/v1/removefriend")]
        [HttpGet("/api/relationships/v2/removefriend/{routeId:int}")]
        [HttpPost("/api/relationships/v2/removefriend/{routeId:int}")]
        [HttpGet("/relationships/v2/removefriend/{routeId:int}")]
        [HttpPost("/relationships/v2/removefriend/{routeId:int}")]
        [HttpPost("/api/relationships/v2/{routeId:int}/removefriend")]
        [HttpPost("/relationships/v2/{routeId:int}/removefriend")]
        // "Decline incoming request" is just a remove on the inbound side.
        [HttpPost("/api/relationships/v2/declinefriendrequest")]
        [HttpPost("/relationships/v2/declinefriendrequest")]
        [HttpPost("/api/relationships/v1/declinefriendrequest")]
        [HttpPost("/relationships/v1/declinefriendrequest")]
        [HttpPost("/api/relationships/v2/declinefriendrequest/{routeId:int}")]
        [HttpPost("/relationships/v2/declinefriendrequest/{routeId:int}")]
        [HttpPost("/api/relationships/v1/declinefriendrequest/{routeId:int}")]
        [HttpPost("/relationships/v1/declinefriendrequest/{routeId:int}")]
        public IActionResult RemoveFriend(
            int routeId = 0,
            [FromQuery] int id = 0,
            [FromQuery] int accountId = 0,
            [FromQuery] int targetId = 0,
            [FromQuery] int targetAccountId = 0)
        {
            int myId = GetAccountIdFromAuth();
            if (myId == 0) myId = 2;

            int friendId = ResolveFriendIdFromRequest(routeId, id, accountId, targetId, targetAccountId);
            if (friendId == 0) return Ok(new { ErrorCode = 0 });

            PartyState.FriendRequests.TryRemove($"{myId}_{friendId}", out _);
            PartyState.FriendRequests.TryRemove($"{friendId}_{myId}", out _);

            try
            {
                using var db = new RetroRecDb();
                var rows = db.FriendRelationships
                    .Where(r =>
                        (r.SenderId == myId && r.TargetId == friendId) ||
                        (r.SenderId == friendId && r.TargetId == myId))
                    .ToList();
                if (rows.Count > 0)
                {
                    db.FriendRelationships.RemoveRange(rows);
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[friends] remove persist failed: {ex.Message}");
            }

            return Ok(new { ErrorCode = 0 });
        }

        [HttpGet("/api/relationships/v2/block")]
        [HttpPost("/api/relationships/v2/block")]
        [HttpGet("/relationships/v2/block")]
        [HttpPost("/relationships/v2/block")]
        public IActionResult BlockPlayer([FromQuery] int id) => Ok(new { });

        [HttpGet("/api/relationships/v2/unblock")]
        [HttpPost("/api/relationships/v2/unblock")]
        [HttpGet("/relationships/v2/unblock")]
        [HttpPost("/relationships/v2/unblock")]
        public IActionResult UnblockPlayer([FromQuery] int id) => Ok(new { });
    }
}
