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

        [HttpGet("/account/{id}/bio")]
        [HttpGet("/api/account/{id}/bio")]
        public IActionResult GetBio(long id)
        {
            PartyState.Bios.TryGetValue(id, out var bio);
            return Pascal(new { AccountId = id, Bio = bio ?? "" });
        }

        [HttpPut("/account/{id}/bio")]
        [HttpPut("/api/account/{id}/bio")]
        public async Task<IActionResult> SetBio(long id)
        {
            using var reader = new System.IO.StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            string? bio = null;
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("bio", out var bioEl))
                    bio = bioEl.GetString();
            }
            catch { }
            PartyState.Bios[(long)id] = bio ?? "";
            return Pascal(new { AccountId = id, Bio = bio ?? "" });
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

            var results = new List<object>();
            foreach (var key in PartyState.FriendRequests.Keys)
            {
                var parts = key.Split('_');
                if (parts.Length != 2) continue;
                if (!int.TryParse(parts[0], out var sender) || !int.TryParse(parts[1], out var target)) continue;
                if (sender != myId && target != myId) continue;

                bool iSent = sender == myId;
                int otherId = iSent ? target : sender;
                bool theyAlsoSent = PartyState.FriendRequests.ContainsKey($"{otherId}_{myId}");

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
        [HttpGet("/api/relationships/v2/addfriend/{id:int}")]
        [HttpPost("/api/relationships/v2/addfriend/{id:int}")]
        [HttpGet("/relationships/v2/addfriend/{id:int}")]
        [HttpPost("/relationships/v2/addfriend/{id:int}")]
        [HttpGet("/api/relationships/v2/sendfriendrequest/{id:int}")]
        [HttpPost("/api/relationships/v2/sendfriendrequest/{id:int}")]
        [HttpGet("/relationships/v2/sendfriendrequest/{id:int}")]
        [HttpPost("/relationships/v2/sendfriendrequest/{id:int}")]
        [HttpGet("/api/relationships/v1/addfriend/{id:int}")]
        [HttpPost("/api/relationships/v1/addfriend/{id:int}")]
        [HttpGet("/relationships/v1/addfriend/{id:int}")]
        [HttpPost("/relationships/v1/addfriend/{id:int}")]
        [HttpGet("/api/relationships/v1/sendfriendrequest/{id:int}")]
        [HttpPost("/api/relationships/v1/sendfriendrequest/{id:int}")]
        [HttpGet("/relationships/v1/sendfriendrequest/{id:int}")]
        [HttpPost("/relationships/v1/sendfriendrequest/{id:int}")]
        public async Task<IActionResult> AddFriend()
        {
            int myId = GetAccountIdFromAuth();
            if (myId == 0) myId = 2;

            var values = await CollectRequestValuesAsync();
            int friendId = GetIntValue(values,
                "id",
                "accountId",
                "targetId",
                "targetAccountId",
                "friendId",
                "friendAccountId",
                "objectAccountId");

            if (friendId == 0 || friendId == myId)
                return Pascal(new { ErrorCode = 0 });

            PartyState.FriendRequests.TryAdd($"{myId}_{friendId}", true);
            bool mutual = PartyState.FriendRequests.ContainsKey($"{friendId}_{myId}");
            return Pascal(new { ErrorCode = 0, SubjectAccountId = myId, ObjectAccountId = friendId, Type = mutual ? 4 : 2 });
        }

        [HttpGet("/api/relationships/v2/removefriend")]
        [HttpPost("/api/relationships/v2/removefriend")]
        [HttpGet("/relationships/v2/removefriend")]
        [HttpPost("/relationships/v2/removefriend")]
        [HttpGet("/api/relationships/v1/removefriend")]
        [HttpPost("/api/relationships/v1/removefriend")]
        [HttpGet("/relationships/v1/removefriend")]
        [HttpPost("/relationships/v1/removefriend")]
        [HttpGet("/api/relationships/v2/unfriend")]
        [HttpPost("/api/relationships/v2/unfriend")]
        [HttpGet("/relationships/v2/unfriend")]
        [HttpPost("/relationships/v2/unfriend")]
        [HttpGet("/api/relationships/v1/unfriend")]
        [HttpPost("/api/relationships/v1/unfriend")]
        [HttpGet("/relationships/v1/unfriend")]
        [HttpPost("/relationships/v1/unfriend")]
        [HttpGet("/api/relationships/v2/unfriend/{id:int}")]
        [HttpPost("/api/relationships/v2/unfriend/{id:int}")]
        [HttpGet("/relationships/v2/unfriend/{id:int}")]
        [HttpPost("/relationships/v2/unfriend/{id:int}")]
        [HttpGet("/api/relationships/v1/unfriend/{id:int}")]
        [HttpPost("/api/relationships/v1/unfriend/{id:int}")]
        [HttpGet("/relationships/v1/unfriend/{id:int}")]
        [HttpPost("/relationships/v1/unfriend/{id:int}")]
        [HttpGet("/api/relationships/v2/removefriend/{id:int}")]
        [HttpPost("/api/relationships/v2/removefriend/{id:int}")]
        [HttpGet("/relationships/v2/removefriend/{id:int}")]
        [HttpPost("/relationships/v2/removefriend/{id:int}")]
        [HttpGet("/api/relationships/v1/removefriend/{id:int}")]
        [HttpPost("/api/relationships/v1/removefriend/{id:int}")]
        [HttpGet("/relationships/v1/removefriend/{id:int}")]
        [HttpPost("/relationships/v1/removefriend/{id:int}")]
        public async Task<IActionResult> RemoveFriend()
        {
            int myId = GetAccountIdFromAuth();
            if (myId == 0) myId = 2;

            var values = await CollectRequestValuesAsync();
            int friendId = GetIntValue(values,
                "id",
                "accountId",
                "targetId",
                "targetAccountId",
                "friendId",
                "friendAccountId",
                "objectAccountId");

            if (friendId == 0) return Ok(new { });
            PartyState.FriendRequests.TryRemove($"{myId}_{friendId}", out _);
            PartyState.FriendRequests.TryRemove($"{friendId}_{myId}", out _);
            return Ok(new { });
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
