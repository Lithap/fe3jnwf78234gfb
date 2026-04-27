using Microsoft.AspNetCore.Mvc;
using System.Text;

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

            var account = db.Accounts.FirstOrDefault(a => a.PlatformId == platformId);
            if (account == null)
            {
                account = new Account
                {
                    Username = GenerateUsername(),
                    Platform = platform,
                    PlatformId = platformId,
                    AuthToken = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow,
                    // New accounts start at Level 1, XP 0. Real progression
                    // (XP earned from quests, etc.) gets added via PlayerController's
                    // progression endpoints — they read from these columns now.
                    Level = 1,
                    XP = 0
                };
                db.Accounts.Add(account);
                db.SaveChanges();
            }

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
            var chosenName = username ?? displayName;

            int accountId = GetAccountIdFromAuth();
            Account account = null;

            if (accountId > 0)
                account = db.Accounts.FirstOrDefault(a => a.Id == accountId);

            if (account == null)
            {
                account = new Account
                {
                    Username = chosenName ?? GenerateUsername(),
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
            return Ok(new
            {
                access_token = jwt,
                refresh_token = jwt,
                key = "",
                account = BuildAccountJson(account)
            });
        }

        // Client calls /account/bulk?id=X — returns an ARRAY
        [HttpGet("account/bulk")]
        public IActionResult AccountBulk([FromQuery] int id)
        {
            using var db = new RetroRecDb();
            var account = db.Accounts.FirstOrDefault(a => a.Id == id);
            if (account == null) return Ok(new object[] { });
            return Ok(new[] { BuildAccountJson(account) });
        }

        // Client calls /account/me to get the currently logged-in user.
        // Look up the caller's account from the JWT "sub" claim so each user
        // gets their OWN account (not just the first row in the database).
        [HttpGet("account/me")]
        public IActionResult AccountMe()
        {
            using var db = new RetroRecDb();

            int accountId = GetAccountIdFromAuth();
            Account account = null;

            if (accountId > 0)
                account = db.Accounts.FirstOrDefault(a => a.Id == accountId);

            // Fallback if no auth header: return first account
            if (account == null)
                account = db.Accounts.FirstOrDefault();

            if (account == null) return NotFound();
            return Ok(BuildAccountJson(account));
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
            var account = db.Accounts.FirstOrDefault(a => a.PlatformId == platformId);
            if (account == null)
            {
                account = new Account
                {
                    Username = GenerateUsername(),
                    Platform = platform.ToString(),
                    PlatformId = platformId,
                    AuthToken = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow,
                    Level = 1,
                    XP = 0
                };
                db.Accounts.Add(account);
                db.SaveChanges();
            }
            return Ok(new[] {
                new {
                    platform = platform,
                    platformId = platformId,
                    accountId = account.Id,
                    lastLoginTime = "0001-01-01T00:00:00"
                }
            });
        }

        // EAC challenge — client accepts literally ":3"
        [HttpGet("eac/challenge")]
        public IActionResult EacChallenge() => Ok("\":3\"");

        private static string GenerateUsername()
        {
            var rng = new Random();
            return $"Player{rng.Next(1000, 9999)}";
        }

        // Reads Level/XP from the actual account row now instead of hardcoded
        // 50/9999 placeholders. Existing accounts (created before the migration)
        // will start at Level 0 / XP 0 since SQLite defaults int to 0 — that's
        // fine, they earn XP normally from there.
        private static object BuildAccountJson(Account a) => new
        {
            accountId = a.Id,
            displayName = a.Username,
            bannerImage = a.Username,
            createdAt = a.CreatedAt,
            isJunior = false,
            platforms = 0,
            profileImage = a.Username,
            username = a.Username,
            level = a.Level,
            xp = a.XP,
            platformTags = new object[] { }
        };

        // Produces a fake 3-segment JWT that at least PARSES as base64.
        // Client doesn't verify the signature, just decodes the payload.
        private static string MakeFakeJwt(int accountId)
        {
            string B64(string s) =>
                Convert.ToBase64String(Encoding.UTF8.GetBytes(s))
                       .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var header = B64("{\"alg\":\"RS256\",\"typ\":\"at+jwt\"}");
            var payload = B64($"{{\"sub\":\"{accountId}\",\"iss\":\"https://auth.rec.net\",\"role\":\"webClient\"}}");
            var sig = B64("signature");
            return $"{header}.{payload}.{sig}";
        }
    }
}
