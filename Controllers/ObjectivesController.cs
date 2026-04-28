using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace RetroRec_Server.Controllers
{
    // Challenges, daily/weekly objectives, gift drop store, gamerewards,
    // leaderboards. The big additions here are XP-aware endpoints — when
    // the client reports a completed objective or quest progress, we
    // actually award XP to the calling user's Account row in the DB and
    // recompute their level.
    //
    // XP/Level math (intentionally simple):
    //   level = (XP / XP_PER_LEVEL) + 1
    //   XP awarded:
    //     - Completed objective       → +50 XP
    //     - Storefront/v2 tick        → +5  XP each (quests send hundreds)
    //     - Quest gift generated      → +25 XP
    [ApiController]
    public class ObjectivesController : RetroRecBase
    {
        // XP rewards per event. Easy to tune later — bump these up for
        // faster leveling, down for slower. Each completed objective is
        // worth a chunk; each tiny storefront tick is worth a sliver
        // (because quests send hundreds of these per playthrough).
        private const int XP_PER_OBJECTIVE = 50;
        private const int XP_PER_STOREFRONT_TICK = 5;
        private const int XP_PER_GIFT = 25;
        // Set to 100 for fast testing — flip to 4500 for "3-4 quests per level"
        // pacing once XP is confirmed working end-to-end.
        private const int XP_PER_LEVEL = 100;

        // Pull XP into the calling user's account, recompute level, save.
        private (int level, int xp) AwardXP(int amount)
        {
            int playerId = GetAccountIdFromAuth();
            if (playerId <= 0) return (0, 0);

            using var db = new RetroRecDb();
            var account = db.Accounts.FirstOrDefault(a => a.Id == playerId);
            if (account == null) return (0, 0);

            account.XP += amount;
            account.Level = (account.XP / XP_PER_LEVEL) + 1;
            db.SaveChanges();

            return (account.Level, account.XP);
        }

        [HttpGet("/api/challenge/v2/getCurrent")]
        public new IActionResult Challenge() => Pascal(new
        {
            ChallengeMapId = 0,
            StartAt = "2021-12-27T21:27:38.188Z",
            EndAt = "2030-12-27T21:27:38.188Z",
            ServerTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            Challenges = new object[] { },
            Gift = new
            {
                GiftDropId = 1,
                AvatarItemDesc = "",
                Xp = 2,
                Level = 0,
                EquipmentPrefabName = "[WaterBottle]"
            },
            ChallengeThemeString = "RetroRec",
            FallbackGiftName = ""
        });

        [HttpGet("/api/objectives/v1/myprogress")]
        public IActionResult MyProgress() => Pascal(new
        {
            Objectives = new object[] {
                new { Index = 2, Group = 0, Progress = 0, VisualProgress = 0, IsCompleted = false, IsRewarded = false, HasClaimedReward = false },
                new { Index = 1, Group = 0, Progress = 0, VisualProgress = 0, IsCompleted = false, IsRewarded = false, HasClaimedReward = false },
                new { Index = 0, Group = 0, Progress = 0, VisualProgress = 0, IsCompleted = false, IsRewarded = false, HasClaimedReward = false }
            },
            ObjectiveGroups = new object[] {
                new { Group = 0, IsCompleted = false, ClearedAt = "2021-04-18T01:59:14.864Z" }
            }
        });

        // RebornRec returns a COMPLETELY EMPTY body for this endpoint (not
        // `{}`, not JSON — literally 0 bytes). Any JSON body at all triggers
        // the client's parser, which then fails with "malformed" because the
        // expected shape isn't what we guessed. Empty body = client treats
        // as "success, nothing to update" and continues.
        [HttpPost("/api/objectives/v1/cleargroup")]
        [HttpPost("/objectives/v1/cleargroup")]
        public IActionResult ClearGroup() => new OkResult();

        [HttpPost("/api/objectives/v1/complete")]
        public IActionResult CompleteObjective()
        {
            // /complete is the explicit "this objective is done, give me
            // credit" call — always award XP here.
            AwardXP(XP_PER_OBJECTIVE);
            return Ok(new { });
        }

        // Client fires this when a daily challenge progress changes (e.g. you
        // got 3 of 3 boss kills). Body: {Index, Group, Progress, VisualProgress,
        // IsCompleted, IsRewarded}. ONLY award XP when IsCompleted flips true,
        // not for every progress tick — otherwise the spam-update would give
        // hundreds of XP per challenge.
        [HttpPost("/api/objectives/v1/updateobjective")]
        [HttpPost("/objectives/v1/updateobjective")]
        public async Task<IActionResult> UpdateObjective()
        {
            try
            {
                Request.EnableBuffering();
                if (Request.Body.CanSeek) Request.Body.Position = 0;
                using var reader = new StreamReader(Request.Body, leaveOpen: true);
                var json = await reader.ReadToEndAsync();
                if (Request.Body.CanSeek) Request.Body.Position = 0;
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("IsCompleted", out var prop) &&
                    prop.ValueKind == JsonValueKind.True)
                {
                    AwardXP(XP_PER_OBJECTIVE);
                }
            }
            catch
            {
                // Malformed body — don't award XP, but still return Ok so the
                // client doesn't retry-spam.
            }
            return Ok(new { });
        }

        // V2 of objectives reporting (newer client). Each entry is a gameplay
        // event (kill, score, pickup). Body shape: array of
        // {objectiveType, additionalXp, inParty}. We award a small XP per
        // entry; quest end sends ~200 of these.
        [HttpPost("/api/players/v2/objectives")]
        [HttpPost("/players/v2/objectives")]
        public async Task<IActionResult> PlayersV2Objectives()
        {
            try
            {
                Request.EnableBuffering();
                if (Request.Body.CanSeek) Request.Body.Position = 0;
                using var reader = new StreamReader(Request.Body, leaveOpen: true);
                var json = await reader.ReadToEndAsync();
                if (Request.Body.CanSeek) Request.Body.Position = 0;
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    int count = doc.RootElement.GetArrayLength();
                    if (count > 0)
                        AwardXP(count * XP_PER_STOREFRONT_TICK);
                }
            }
            catch { }
            return Ok(new { });
        }

        // Storefront-side objectives report (older v1 path, still used by
        // some flows). Quest sends a HUGE batch of these when finishing a
        // quest (~250 entries). Each entry has completionPercentage which
        // is usually 1 (=100% done). One small XP per "completed" entry.
        [HttpPost("/api/storefronts/v1/objectives")]
        [HttpPost("/storefronts/v1/objectives")]
        public async Task<IActionResult> StorefrontObjectives()
        {
            try
            {
                Request.EnableBuffering();
                if (Request.Body.CanSeek) Request.Body.Position = 0;
                using var reader = new StreamReader(Request.Body, leaveOpen: true);
                var json = await reader.ReadToEndAsync();
                if (Request.Body.CanSeek) Request.Body.Position = 0;
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    int completedCount = 0;
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.TryGetProperty("completionPercentage", out var pct) &&
                            pct.ValueKind == JsonValueKind.Number &&
                            pct.GetDouble() >= 1.0)
                        {
                            completedCount++;
                        }
                    }
                    if (completedCount > 0)
                        AwardXP(completedCount * XP_PER_STOREFRONT_TICK);
                }
            }
            catch { }
            return Ok(new { });
        }

        // Quest reward gift generation. Hits this at quest end to roll a gift.
        // We award some XP here too (=quest end bonus). Returns a stub gift —
        // actual item drops would need an inventory system (future work).
        [HttpPost("/gift/v1/generate")]
        [HttpGet("/gift/v1/generate")]
        [HttpPost("/api/gift/v1/generate")]
        [HttpGet("/api/gift/v1/generate")]
        public IActionResult GenerateGift()
        {
            AwardXP(XP_PER_GIFT);
            return Pascal(new
            {
                GiftDropId = 0,
                AvatarItemDesc = "",
                Xp = XP_PER_GIFT,
                Level = 0,
                EquipmentPrefabName = ""
            });
        }

        [HttpPost("/gift/v1/claim")]
        [HttpPost("/api/gift/v1/claim")]
        public IActionResult ClaimGift() => Ok(new { });

        [HttpGet("/api/gamerewards/v1/pending")]
        public IActionResult PendingRewards() => Ok(new object[] { });

        [HttpGet("/api/playerevents/v1/all")]
        public IActionResult PlayerEvents() => Ok(new
        {
            Created = new object[] { },
            Responses = new object[] { }
        });

        [HttpGet("/api/playerevents/v1/room/{roomId:int}")]
        [HttpGet("/playerevents/v1/room/{roomId:int}")]
        public IActionResult PlayerEventsForRoom(int roomId) => Ok(new object[] { });

        // ============ LEADERBOARDS ============
        // Quest scoreboards live here. GetPlayerRank/CheckAndSetStat are
        // called constantly during gameplay (every score change). Returning
        // 204 / empty rank prevents spam without us having to actually
        // implement persistent leaderboards.

        [HttpPost("/leaderboard/GetPlayerRank")]
        [HttpPost("/api/leaderboard/GetPlayerRank")]
        public IActionResult LeaderboardGetPlayerRank() => Pascal(new
        {
            PlayerId = 0,
            Rank = 0,
            Score = 0,
            DisplayName = ""
        });

        [HttpPost("/leaderboard/CheckAndSetStat")]
        [HttpPost("/api/leaderboard/CheckAndSetStat")]
        public IActionResult LeaderboardCheckAndSetStat() => Ok(new
        {
            success = true,
            newStatValue = 0
        });

        [HttpPost("/leaderboard/GetTopScores")]
        [HttpPost("/api/leaderboard/GetTopScores")]
        public IActionResult LeaderboardTopScores() => Ok(new object[] { });

        [HttpGet("/leaderboard/GetNearbyScores")]
        [HttpPost("/leaderboard/GetNearbyScores")]
        [HttpGet("/api/leaderboard/GetNearbyScores")]
        [HttpPost("/api/leaderboard/GetNearbyScores")]
        public IActionResult LeaderboardNearby() => NoContent();
    }
}
