using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using RetroRec_Server.Models;

namespace RetroRec_Server.Controllers
{
    [ApiController]
    public class AuthController : RetroRecBase
    {
        // The client POSTs here with form data including platform_id, grant_type, etc.
        [HttpPost("connect/token")]
        public IActionResult ConnectToken([FromForm] Dictionary<string, string> form)
        {
            using var db = new RetroRecDb();

            form.TryGetValue("platform_id", out var platformId);
            form.TryGetValue("platform", out var platform);
            platformId ??= Guid.NewGuid().ToString();
            platform ??= "0";

            var account = GetOrCreateAccountForPlatform(db, platform, platformId);

            var fakeJwt = MakeFakeJwt(account.Id);
            return Ok(new
            {
                access_token = fakeJwt,
                refresh_token = fakeJwt,
                key = ""
            });
        }

        // "Create Account" button in-game POSTs here with the player's chosen
        // username and password as form fields. We save the username to the DB
        // (password is ignored — private server doesn't need auth).
        [HttpPost("account")]
        [HttpPost("account/create")]
        [HttpPost("api/account")]
        public IActionResult CreateAccount([FromForm] Dictionary<string, string> form)
        {
            using var db = new RetroRecDb();

            form.TryGetValue("username", out var username);
            form.TryGetValue("displayName", out var displayName);
            var chosenName = (username ?? displayName)?.Trim();

            int accountId = GetAccountIdFromAuth();
            Account? account = null;

            if (accountId > 0)
                account = db.Accounts.FirstOrDefault(a => a.Id == accountId);

            if (!string.IsNullOrWhiteSpace(chosenName))
            {
                // Reject duplicates so two players can't both pick "Skibidi"
                // and confuse the friend / @mention / search flows. Our own
                // row is allowed to "rename" to its current name as a no-op.
                bool takenBySomeoneElse = db.Accounts.Any(a =>
                    a.Username == chosenName &&
                    (account == null || a.Id != account.Id));

                if (takenBySomeoneElse)
                {
                    return Conflict(new
                    {
                        ErrorCode = 1,
                        Error = "username_taken",
                        Message = $"The username '{chosenName}' is already taken."
                    });
                }
            }

            if (account == null)
            {
                account = new Account
                {
                    Username = string.IsNullOrWhiteSpace(chosenName) ? GenerateUniqueUsername(db) : chosenName,
                    Platform = "0",
                    PlatformId = Guid.NewGuid().ToString(),
                    AuthToken = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow,
                    Level = 1,
                    XP = 0
                };
                db.Accounts.Add(account);
                db.SaveChanges();
            }
            else if (!string.IsNullOrWhiteSpace(chosenName))
            {
                account.Username = chosenName;
                db.SaveChanges();
            }

            var jwt = MakeFakeJwt(account.Id);
            // Serialize manually so the embedded account object stays
            // PascalCase (BuildAccountJson is consumed by the watch UI to
            // populate the local-player display name). Returning Ok() with
            // an anonymous object would camelCase everything via the
            // default MVC serializer settings.
            var responseJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                access_token = jwt,
                refresh_token = jwt,
                key = "",
                Account = BuildAccountJson(account)
            }, PascalOpts);
            return Content(responseJson, "application/json");
        }

        // Client calls /account/bulk?id=X&id=Y&id=Z — returns an ARRAY of
        // account JSONs in PascalCase, one per id. The party panel, friend
        // list, and player-tag system all hit this endpoint with multiple
        // ids in a single request to avoid round-trips.
        //
        // The previous version took a single `int id` so only the first
        // account in the request came back populated. Every other slot got
        // the client's "no data" fallback (DisplayName = "PlayerName"),
        // which is why every player looked like "PlayerName" in the watch
        // and why friend/party/bio UIs broke (they all key off
        // DisplayName != "PlayerName" before showing anything).
        [HttpGet("account/bulk")]
        public IActionResult AccountBulk([FromQuery(Name = "id")] int[] ids)
        {
            using var db = new RetroRecDb();
            if (ids == null || ids.Length == 0) return Pascal(Array.Empty<object>());

            var distinct = ids.Where(i => i > 0).Distinct().ToList();
            var accounts = db.Accounts
                .Where(a => distinct.Contains(a.Id))
                .ToDictionary(a => a.Id);

            var results = new List<object>();
            foreach (var id in distinct)
            {
                if (accounts.TryGetValue(id, out var account))
                {
                    results.Add(BuildAccountJson(account));
                }
                else
                {
                    // Synthesize a row for ids we don't have so the client
                    // doesn't fall back to "PlayerName" for unknown players.
                    // Username pattern matches the GenerateUsername() format
                    // so the watch UI and chat all show a stable name.
                    results.Add(new
                    {
                        AccountId = id,
                        DisplayName = $"Player{id}",
                        BannerImage = "",
                        CreatedAt = DateTime.UtcNow,
                        IsJunior = false,
                        Platforms = 0,
                        ProfileImage = "",
                        Username = $"Player{id}",
                        Level = 0,
                        XP = 0,
                        PlatformTags = Array.Empty<object>()
                    });
                }
            }
            return Pascal(results);
        }

        // Client calls /account/me to get the currently logged-in user.
        // Look up the caller's account from the JWT "sub" claim so each user
        // gets their OWN account (not just the first row in the database).
        [HttpGet("account/me")]
        public IActionResult AccountMe()
        {
            using var db = new RetroRecDb();

            int accountId = GetAccountIdFromAuth();
            Account? account = null;

            if (accountId > 0)
                account = db.Accounts.FirstOrDefault(a => a.Id == accountId);

            // Fallback if no auth header: return first account
            if (account == null)
                account = db.Accounts.FirstOrDefault();

            if (account == null) return NotFound();
            return Pascal(BuildAccountJson(account));
        }

        // /account/{id} — single-account lookup. Watch profile cards,
        // mention auto-complete, and the chat 'who is this?' menu hit
        // this endpoint. Same Pascal casing requirement as /account/me.
        [HttpGet("account/{id:int}")]
        [HttpGet("api/account/{id:int}")]
        public IActionResult AccountById(int id)
        {
            using var db = new RetroRecDb();
            var account = db.Accounts.FirstOrDefault(a => a.Id == id);
            if (account == null)
            {
                return Pascal(new
                {
                    AccountId = id,
                    DisplayName = $"Player{id}",
                    BannerImage = "",
                    CreatedAt = DateTime.UtcNow,
                    IsJunior = false,
                    Platforms = 0,
                    ProfileImage = "",
                    Username = $"Player{id}",
                    Level = 0,
                    XP = 0,
                    PlatformTags = Array.Empty<object>()
                });
            }
            return Pascal(BuildAccountJson(account));
        }

        // Bulk lookup by username — same pattern as /account/bulk but
        // keyed off display name. Used by friend-search and @mention.
        [HttpGet("accounts/bulk/byUsername")]
        [HttpGet("account/bulk/byUsername")]
        [HttpGet("api/accounts/bulk/byUsername")]
        [HttpGet("api/account/bulk/byUsername")]
        public IActionResult AccountBulkByUsername([FromQuery(Name = "username")] string[] usernames)
        {
            using var db = new RetroRecDb();
            if (usernames == null || usernames.Length == 0) return Pascal(Array.Empty<object>());

            // Friend search can send different casing than the stored username.
            // Normalize both sides so "myFriend" and "MYFRIEND" resolve equally.
            var lookup = usernames
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(u => u.Trim().ToLower())
                .Distinct()
                .ToList();
            var matches = db.Accounts
                .Where(a => a.Username != null && lookup.Contains(a.Username.ToLower()))
                .ToList();

            var results = matches.Select(a => (object)BuildAccountJson(a)).ToList();
            return Pascal(results);
        }

        // Same content as /account/me but the older `/api/account/me` /
        // `/api/me` paths some client builds use during boot.
        [HttpGet("api/account/me")]
        [HttpGet("me")]
        [HttpGet("api/me")]
        public IActionResult AccountMeAlias() => AccountMe();

        // ============ RENAME / DISPLAY NAME ============
        //
        // Dedicated rename endpoints. The watch's "Edit Profile" submenu and
        // the in-game "Change Username" flow each post here with the new
        // name. Same uniqueness rule as CreateAccount: rejects 409 if
        // someone else already has the chosen name, allows your own current
        // name as a no-op. Persisted to the Accounts table immediately.
        [HttpPut("account/me/displayName")]
        [HttpPost("account/me/displayName")]
        [HttpPatch("account/me/displayName")]
        [HttpPut("api/account/me/displayName")]
        [HttpPost("api/account/me/displayName")]
        [HttpPatch("api/account/me/displayName")]
        [HttpPut("account/me/username")]
        [HttpPost("account/me/username")]
        [HttpPatch("account/me/username")]
        [HttpPut("api/account/me/username")]
        [HttpPost("api/account/me/username")]
        [HttpPatch("api/account/me/username")]
        [HttpPut("account/me/name")]
        [HttpPost("account/me/name")]
        [HttpPatch("account/me/name")]
        [HttpPut("api/account/me/name")]
        [HttpPost("api/account/me/name")]
        [HttpPatch("api/account/me/name")]
        [HttpPut("account/me")]
        [HttpPost("account/me")]
        [HttpPatch("account/me")]
        [HttpPut("api/account/me")]
        [HttpPost("api/account/me")]
        [HttpPatch("api/account/me")]
        public async Task<IActionResult> RenameMe()
        {
            int accountId = GetAccountIdFromAuth();
            string? chosenName = null;

            try
            {
                Request.EnableBuffering();
                if (Request.Body.CanSeek) Request.Body.Position = 0;
                using var reader = new StreamReader(Request.Body, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                if (Request.Body.CanSeek) Request.Body.Position = 0;
                if (!string.IsNullOrWhiteSpace(body))
                {
                    if (body.TrimStart().StartsWith("{"))
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(body);
                        foreach (var key in new[] { "displayName", "DisplayName", "username", "Username", "name", "Name" })
                        {
                            if (doc.RootElement.TryGetProperty(key, out var el) &&
                                el.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                chosenName = el.GetString();
                                break;
                            }
                        }
                    }
                }
            }
            catch { }

            if (chosenName == null && Request.HasFormContentType)
            {
                foreach (var key in new[] { "displayName", "username", "name" })
                {
                    if (Request.Form.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
                    {
                        chosenName = v.ToString();
                        break;
                    }
                }
            }

            if (chosenName == null)
            {
                foreach (var key in new[] { "displayName", "username", "name" })
                {
                    if (Request.Query.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
                    {
                        chosenName = v.ToString();
                        break;
                    }
                }
            }

            chosenName = chosenName?.Trim();
            if (string.IsNullOrWhiteSpace(chosenName))
                return BadRequest(new { ErrorCode = 1, Error = "missing_name" });

            using var db = new RetroRecDb();
            Account? account = null;
            if (accountId > 0)
                account = db.Accounts.FirstOrDefault(a => a.Id == accountId);

            // Some client paths hit rename without a valid auth header.
            // Fall back to the first local account so profile editing still works.
            account ??= db.Accounts.FirstOrDefault();

            if (account == null)
                return NotFound(new { ErrorCode = 1, Error = "account_not_found" });

            // Self-renames to the same name are no-ops; otherwise the name
            // must not be taken by anyone else.
            if (!string.Equals(account.Username, chosenName, StringComparison.OrdinalIgnoreCase))
            {
                bool taken = db.Accounts.Any(a => a.Username == chosenName && a.Id != account.Id);
                if (taken)
                {
                    return Conflict(new
                    {
                        ErrorCode = 2,
                        Error = "username_taken",
                        Message = $"The username '{chosenName}' is already taken."
                    });
                }
                account.Username = chosenName;
                db.SaveChanges();
            }

            return Pascal(BuildAccountJson(account));
        }

        // Client-side "is this username available?" probe. Avoids surfacing
        // a 409 in the rename UI by letting the client pre-check.
        [HttpGet("account/usernameAvailable")]
        [HttpGet("api/account/usernameAvailable")]
        [HttpGet("account/checkusername")]
        [HttpGet("api/account/checkusername")]
        public IActionResult UsernameAvailable([FromQuery] string username)
        {
            int myId = GetAccountIdFromAuth();
            if (string.IsNullOrWhiteSpace(username))
                return Pascal(new { Username = username ?? "", Available = false });

            using var db = new RetroRecDb();
            bool taken = db.Accounts.Any(a => a.Username == username && a.Id != myId);
            return Pascal(new { Username = username, Available = !taken });
        }

        // Client checks if the account has set up a password (for login on web,
        // not used in our setup since we auth purely via Steam platform ID).
        // Always return false — our private server doesn't use passwords.
        [HttpGet("account/me/haspassword")]
        public IActionResult AccountMeHasPassword() => Content("{\"HasPassword\":false}", "application/json");

        // Client calls /cachedlogin/forplatformid/{platform}/{platformId} BEFORE /connect/token
        [HttpGet("cachedlogin/forplatformid/{platform}/{platformId}")]
        public IActionResult CachedLogin(int platform, string platformId)
        {
            using var db = new RetroRecDb();
            var account = GetOrCreateAccountForPlatform(db, platform.ToString(), platformId);

            // Pascal cased so the client populates the cached account row
            // properly on subsequent boots. CamelCase here was part of why
            // the second login session ended up showing "PlayerName" in
            // place of the saved username — the cached login response
            // didn't bind, so the watch fell back to an empty placeholder.
            var json = System.Text.Json.JsonSerializer.Serialize(new[] {
                new {
                    Platform = platform,
                    PlatformId = platformId,
                    AccountId = account.Id,
                    LastLoginTime = "0001-01-01T00:00:00"
                }
            }, PascalOpts);
            return Content(json, "application/json");
        }

        // EAC challenge — client accepts literally ":3"
        [HttpGet("eac/challenge")]
        public IActionResult EacChallenge() => Ok("\":3\"");

        // ============ OLD PLATFORM LOGIN (2018 / some 2021 builds) ============
        //
        // Before the OpenID / connect/token flow the 2018 client used
        // /api/platformlogin/v1/* paths. 2021 uses connect/token but some
        // builds still probe these during the login sequence. All three
        // variants delegate to GetOrCreateAccountForPlatform like
        // /cachedlogin/forplatformid does.

        [HttpPost("api/platformlogin/v1/getcachedlogins")]
        [HttpPost("platformlogin/v1/getcachedlogins")]
        public IActionResult PlatformLoginGetCachedLogins([FromForm] Dictionary<string, string> form)
        {
            form.TryGetValue("platform_id", out var platformId);
            form.TryGetValue("platform", out var platform);
            platformId ??= Guid.NewGuid().ToString();
            platform ??= "0";

            using var db = new RetroRecDb();
            var account = GetOrCreateAccountForPlatform(db, platform, platformId);
            var json = System.Text.Json.JsonSerializer.Serialize(new[] {
                new {
                    Platform = int.TryParse(platform, out var p) ? p : 0,
                    PlatformId = platformId,
                    AccountId = account.Id,
                    LastLoginTime = "0001-01-01T00:00:00"
                }
            }, PascalOpts);
            return Content(json, "application/json");
        }

        [HttpPost("api/platformlogin/v1/loginaccount")]
        [HttpPost("platformlogin/v1/loginaccount")]
        [HttpPost("api/platformlogin/v1/createaccount")]
        [HttpPost("platformlogin/v1/createaccount")]
        [HttpPost("api/platformlogin/v1/logincached")]
        [HttpPost("platformlogin/v1/logincached")]
        [HttpPost("api/platformlogin/v6")]
        [HttpPost("platformlogin/v6")]
        [HttpPost("api/platformlogin/v1/profiles")]
        [HttpPost("platformlogin/v1/profiles")]
        public IActionResult PlatformLoginCompat([FromForm] Dictionary<string, string> form)
        {
            form ??= new Dictionary<string, string>();
            form.TryGetValue("platform_id", out var platformId);
            form.TryGetValue("platform", out var platform);
            platformId ??= Guid.NewGuid().ToString();
            platform ??= "0";

            using var db = new RetroRecDb();
            var account = GetOrCreateAccountForPlatform(db, platform, platformId);
            var jwt = MakeFakeJwt(account.Id);
            return Ok(new
            {
                access_token = jwt,
                refresh_token = jwt,
                key = "",
                Account = BuildAccountJson(account)
            });
        }

        // Serializes account creation so two parallel requests for the same
        // brand-new Steam id don't both pass the FirstOrDefault null check
        // and create duplicate rows. Cheap because we only hold the lock
        // long enough to do a re-check + insert; existing accounts skip
        // the lock entirely.
        private static readonly Lock _accountCreateLock = new();

        // One Steam id (or other platform id) = one account, period. Both
        // /connect/token and /cachedlogin/forplatformid go through this so
        // we have exactly one place that decides "is this a registration
        // or a returning login?".
        private static Account GetOrCreateAccountForPlatform(RetroRecDb db, string platform, string platformId)
        {
            var account = db.Accounts.FirstOrDefault(a => a.PlatformId == platformId);
            if (account != null) return account;

            lock (_accountCreateLock)
            {
                // Re-check inside the lock — if we lost the race, another
                // request just created the row; reuse it instead of inserting
                // a duplicate.
                account = db.Accounts.FirstOrDefault(a => a.PlatformId == platformId);
                if (account != null) return account;

                account = new Account
                {
                    Username = GenerateUniqueUsername(db),
                    Platform = platform,
                    PlatformId = platformId,
                    AuthToken = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow,
                    // New accounts start at Level 1, XP 0. Real progression
                    // (XP earned from quests, etc.) gets added via
                    // PlayerController's progression endpoints — they read
                    // from these columns now.
                    Level = 1,
                    XP = 0
                };
                db.Accounts.Add(account);
                db.SaveChanges();
                return account;
            }
        }

        // Generates a "Player####" name that is guaranteed to be unique
        // against the current Accounts table. The old version did
        // `new Random().Next(1000, 9999)` with no DB check, so two new
        // accounts could collide on the same name (and the new unique
        // index would then reject the second one). Probes up to 50 random
        // candidates, then falls back to incrementing from the highest
        // existing Player#### so we always succeed.
        private static string GenerateUniqueUsername(RetroRecDb db)
        {
            var rng = new Random();
            for (int i = 0; i < 50; i++)
            {
                var candidate = $"Player{rng.Next(1000, 100000)}";
                if (!db.Accounts.Any(a => a.Username == candidate))
                    return candidate;
            }

            int next = 1000;
            while (db.Accounts.Any(a => a.Username == $"Player{next}")) next++;
            return $"Player{next}";
        }

        // Reads Level/XP from the actual account row now instead of hardcoded
        // 50/9999 placeholders. Existing accounts (created before the migration)
        // will start at Level 0 / XP 0 since SQLite defaults int to 0 — that's
        // fine, they earn XP normally from there.
        //
        // Property names MUST be PascalCase. The Rec Room client deserializes
        // account JSON with case-sensitive PascalCase keys (AccountId,
        // DisplayName, Username, etc.). When everything was camelCase the
        // client failed to bind any of these fields and silently fell back
        // to a hardcoded "PlayerName" placeholder for every account — which
        // is why "everyone is named PlayerName" broke party/friends/bio
        // (those features key off DisplayName != "PlayerName" / Username
        // being non-empty before they let you act on a player).
        private static object BuildAccountJson(Account a)
        {
            // Don't reuse the username for ProfileImage / BannerImage —
            // those are supposed to be image GUIDs and the client tries to
            // resolve them as URLs. Empty string falls through to the
            // image catchall (1x1 transparent PNG) instead of erroring.
            //
            // CanReceiveInvites=true is required for the 2021 client to show
            // the "Invite to party" and "Friend request" buttons on player
            // cards. Without it those interactive elements are hidden.
            return new
            {
                AccountId = a.Id,
                DisplayName = a.Username ?? $"Player{a.Id}",
                Username = a.Username ?? $"Player{a.Id}",
                BannerImage = "",
                ProfileImage = "",
                CreatedAt = a.CreatedAt,
                IsJunior = false,
                Platforms = 0,
                Level = a.Level,
                XP = a.XP,
                CanReceiveInvites = true,
                PlatformTags = Array.Empty<object>()
            };
        }


        // Produces a signed JWT using HMAC-SHA256. The server verifies this
        // signature in GetAccountIdFromAuth() before trusting any claim.
        private static string MakeFakeJwt(int accountId)
        {
            string B64(byte[] bytes) =>
                Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            string B64Str(string s) => B64(Encoding.UTF8.GetBytes(s));

            var header = B64Str("{\"alg\":\"HS256\",\"typ\":\"at+jwt\"}");
            var payload = B64Str($"{{\"sub\":\"{accountId}\",\"iss\":\"https://auth.rec.net\",\"role\":\"webClient\"}}");
            var signingInput = Encoding.UTF8.GetBytes($"{header}.{payload}");
            var sig = B64(HMACSHA256.HashData(JwtSecret, signingInput));
            return $"{header}.{payload}.{sig}";
        }
    }
}
