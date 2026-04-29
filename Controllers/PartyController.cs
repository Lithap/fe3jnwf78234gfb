using Microsoft.AspNetCore.Mvc;

namespace RetroRec_Server.Controllers
{
    // Party membership and invite endpoints.
    // State lives in PartyState.MemberOf and PartyState.Invites.
    [ApiController]
    public class PartyController : RetroRecBase
    {
        [HttpGet("/api/invites/v1")]
        [HttpGet("/api/invites/v1/")]
        [HttpGet("/invites/v1")]
        [HttpGet("/invites/v1/")]
        public IActionResult GetInvites()
        {
            int myId = GetAccountIdFromAuth();
            if (myId == 0) return Unauthorized();
            var pending = PartyState.Invites.Values
                .Where(i => i.TargetId == myId)
                // Drop expired invites so stale "X wants to go with you"
                // notifications stop popping up after the party has moved on.
                .Where(i => i.CreatedAt.AddMinutes(5) > DateTime.UtcNow)
                .Select(i => new
                {
                    InviteId = i.InviteId,
                    SenderAccountId = i.SenderId,
                    RoomName = i.RoomName,
                    RoomId = i.RoomId,
                    IsPartyInvite = i.IsPartyInvite,
                    CreatedAt = i.CreatedAt,
                    ExpiresAt = i.CreatedAt.AddMinutes(5)
                })
                .ToList();
            return Pascal(pending);
        }

        // Pull the target id + room hints from form fields, query string, or
        // a JSON body. The client's invite UI flips between content types
        // depending on what menu opened it; supporting all three is what
        // makes party invites work consistently instead of "sometimes".
        private (int targetId, string roomName, int roomId) ReadInviteFields(Dictionary<string, string> form)
        {
            string? targetStr = null, roomName = null, roomIdStr = null;

            if (form != null)
            {
                form.TryGetValue("targetAccountId", out targetStr);
                if (string.IsNullOrEmpty(targetStr)) form.TryGetValue("targetId", out targetStr);
                if (string.IsNullOrEmpty(targetStr)) form.TryGetValue("accountId", out targetStr);
                form.TryGetValue("roomName", out roomName);
                form.TryGetValue("roomId", out roomIdStr);
            }

            if (string.IsNullOrEmpty(targetStr))
            {
                foreach (var key in new[] { "targetAccountId", "targetId", "accountId" })
                {
                    if (Request.Query.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                    { targetStr = v; break; }
                }
                if (string.IsNullOrEmpty(roomName) && Request.Query.TryGetValue("roomName", out var rn))
                    roomName = rn;
                if (string.IsNullOrEmpty(roomIdStr) && Request.Query.TryGetValue("roomId", out var ri))
                    roomIdStr = ri;
            }

            if (string.IsNullOrEmpty(targetStr) &&
                (Request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                try
                {
                    Request.EnableBuffering();
                    Request.Body.Position = 0;
                    using var reader = new StreamReader(Request.Body, leaveOpen: true);
                    var body = reader.ReadToEnd();
                    Request.Body.Position = 0;
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(body);
                        foreach (var key in new[] { "targetAccountId", "TargetAccountId", "targetId", "TargetId", "accountId", "AccountId" })
                        {
                            if (doc.RootElement.TryGetProperty(key, out var el))
                            {
                                if (el.ValueKind == System.Text.Json.JsonValueKind.Number) targetStr = el.GetRawText();
                                else if (el.ValueKind == System.Text.Json.JsonValueKind.String) targetStr = el.GetString() ?? "";
                                if (!string.IsNullOrEmpty(targetStr)) break;
                            }
                        }
                        foreach (var key in new[] { "roomName", "RoomName" })
                        {
                            if (string.IsNullOrEmpty(roomName) &&
                                doc.RootElement.TryGetProperty(key, out var el) &&
                                el.ValueKind == System.Text.Json.JsonValueKind.String)
                                roomName = el.GetString() ?? "";
                        }
                        foreach (var key in new[] { "roomId", "RoomId" })
                        {
                            if (string.IsNullOrEmpty(roomIdStr) &&
                                doc.RootElement.TryGetProperty(key, out var el))
                            {
                                roomIdStr = el.ValueKind == System.Text.Json.JsonValueKind.Number
                                    ? el.GetRawText()
                                    : (el.GetString() ?? "");
                            }
                        }
                    }
                }
                catch { }
            }

            int.TryParse(targetStr, out var targetId);
            int.TryParse(roomIdStr, out var roomId);
            return (targetId, roomName, roomId);
        }

        [HttpPost("/api/invites/v1")]
        [HttpPost("/api/invites/v1/")]
        [HttpPost("/invites/v1")]
        [HttpPost("/invites/v1/")]
        public IActionResult SendInvite([FromForm] Dictionary<string, string> form)
        {
            int myId = GetAccountIdFromAuth();
            if (myId == 0) return Unauthorized();

            var (targetId, roomName, roomIdParsed) = ReadInviteFields(form);

            if (targetId == 0)
                return Pascal(new { ErrorCode = 1, Error = "missing_target" });

            UserRoomInstances.TryGetValue(myId, out var myRoomObj);
            var invRoomName = "DormRoom";
            var invRoomId = 1;
            if (myRoomObj != null)
            {
                try
                {
                    dynamic myRoom = myRoomObj;
                    invRoomName = ((string)myRoom.Name).TrimStart('^');
                    invRoomId = (int)myRoom.RoomId;
                }
                catch { }
            }
            if (!string.IsNullOrWhiteSpace(roomName)) invRoomName = roomName;
            if (roomIdParsed != 0) invRoomId = roomIdParsed;

            var inviteId = PartyState.UpsertInvite(myId, targetId, invRoomName, invRoomId, isPartyInvite: true);

            return Pascal(new
            {
                ErrorCode = 0,
                InviteId = inviteId,
                SenderAccountId = myId,
                TargetAccountId = targetId,
                RoomName = invRoomName,
                RoomId = invRoomId,
                IsPartyInvite = true,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5)
            });
        }

        [HttpPost("/api/invites/v1/{inviteId}/accept")]
        [HttpPost("/invites/v1/{inviteId}/accept")]
        public IActionResult AcceptInvite(string inviteId)
        {
            int myId = GetAccountIdFromAuth();
            if (myId == 0) return Unauthorized();

            if (!PartyState.Invites.TryGetValue(inviteId, out var invite))
                return Pascal(new { ErrorCode = 0 });

            if (invite.TargetId != myId)
                return StatusCode(403, new { ErrorCode = 403, Error = "not_your_invite" });

            PartyState.Invites.TryRemove(inviteId, out _);

            if (invite.IsPartyInvite)
                PartyState.MemberOf[myId] = invite.SenderId;

            return Pascal(new
            {
                ErrorCode = 0,
                RoomName = invite.RoomName,
                RoomId = invite.RoomId,
                LeaderId = invite.SenderId,
                IsPartyInvite = invite.IsPartyInvite
            });
        }

        [HttpDelete("/api/invites/v1/{inviteId}")]
        [HttpDelete("/invites/v1/{inviteId}")]
        [HttpPost("/api/invites/v1/{inviteId}/decline")]
        [HttpPost("/invites/v1/{inviteId}/decline")]
        public IActionResult DeclineInvite(string inviteId)
        {
            PartyState.Invites.TryRemove(inviteId, out _);
            return Pascal(new { ErrorCode = 0 });
        }

        // Bulk invite: one call invites multiple players at once.
        // The client sends { PlayerEventId, InvitedPlayerIds: [int, ...] }.
        // PlayerEventId maps to a room; if we don't recognise it we fall back
        // to the sender's current room just like the single-invite path does.
        [HttpPost("/api/invites/v1/bulk")]
        [HttpPost("/api/invites/v1/bulk/")]
        [HttpPost("/invites/v1/bulk")]
        [HttpPost("/invites/v1/bulk/")]
        public async Task<IActionResult> BulkInvite()
        {
            int myId = GetAccountIdFromAuth();
            if (myId == 0) return Unauthorized();

            long playerEventId = 0;
            List<int>? invitedPlayerIds = null;

            try
            {
                Request.EnableBuffering();
                Request.Body.Position = 0;
                using var reader = new StreamReader(Request.Body, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                Request.Body.Position = 0;
                if (!string.IsNullOrWhiteSpace(body))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(body);
                    foreach (var key in new[] { "PlayerEventId", "playerEventId" })
                    {
                        if (doc.RootElement.TryGetProperty(key, out var el) &&
                            el.ValueKind == System.Text.Json.JsonValueKind.Number)
                        { playerEventId = el.GetInt64(); break; }
                    }
                    foreach (var key in new[] { "InvitedPlayerIds", "invitedPlayerIds" })
                    {
                        if (doc.RootElement.TryGetProperty(key, out var el) &&
                            el.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            invitedPlayerIds = new List<int>();
                            foreach (var item in el.EnumerateArray())
                                if (item.ValueKind == System.Text.Json.JsonValueKind.Number)
                                    invitedPlayerIds.Add(item.GetInt32());
                            break;
                        }
                    }
                }
            }
            catch { }

            if (invitedPlayerIds == null || invitedPlayerIds.Count == 0)
                return Pascal(new { ErrorCode = 1, Error = "missing_invited_player_ids" });

            // Resolve the room from the sender's current presence; PlayerEventId
            // is treated as a room id hint when we have it.
            UserRoomInstances.TryGetValue(myId, out var myRoomObj);
            var invRoomName = "DormRoom";
            var invRoomId = 1;
            if (myRoomObj != null)
            {
                try
                {
                    dynamic myRoom = myRoomObj;
                    invRoomName = ((string)myRoom.Name).TrimStart('^');
                    invRoomId = (int)myRoom.RoomId;
                }
                catch { }
            }
            if (playerEventId != 0)
            {
                var hintId = (int)(playerEventId & 0x7FFFFFFF);
                if (RRConstants.RoomSceneIds.ContainsKey(hintId))
                {
                    invRoomId = hintId;
                    invRoomName = RRConstants.RoomIdToName(hintId);
                }
            }

            var results = invitedPlayerIds.Select(targetId =>
            {
                var inviteId = PartyState.UpsertInvite(myId, targetId, invRoomName, invRoomId, isPartyInvite: true);
                return new
                {
                    ErrorCode = 0,
                    InviteId = inviteId,
                    SenderAccountId = myId,
                    TargetAccountId = targetId,
                    RoomName = invRoomName,
                    RoomId = invRoomId,
                    IsPartyInvite = true,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(5)
                };
            }).ToList();

            return Pascal(new { ErrorCode = 0, Invites = results });
        }

        [HttpPost("/api/party/v1/leave")]
        [HttpPost("/party/v1/leave")]
        [HttpPost("/api/party/v2/leave")]
        [HttpPost("/party/v2/leave")]
        public IActionResult LeaveParty()
        {
            int myId = GetAccountIdFromAuth();
            if (myId == 0) return Unauthorized();

            if (PartyState.MemberOf.TryRemove(myId, out _))
            {
                // I was a member — just remove myself.
                return Ok(new { });
            }

            // I might be the leader. Dissolve the party so members aren't
            // stuck in a ghost party whose leader has already left.
            var memberKeys = PartyState.MemberOf
                .Where(m => m.Value == myId)
                .Select(m => m.Key)
                .ToList();
            foreach (var memberId in memberKeys)
                PartyState.MemberOf.TryRemove(memberId, out _);

            return Ok(new { });
        }

        // Returns the current party composition. The client calls this to
        // populate the party panel. Without it the list shows empty even though
        // Leave Party is still visible.
        // The leader is included in the Members list so the client renders
        // the leader's own card in the party panel alongside their members.
        [HttpGet("/api/party/v1")]
        [HttpGet("/party/v1")]
        [HttpGet("/api/party/v2")]
        [HttpGet("/party/v2")]
        public IActionResult GetParty()
        {
            int myId = GetAccountIdFromAuth();
            if (myId == 0) return Unauthorized();

            // Am I a member of someone else's party?
            if (PartyState.MemberOf.TryGetValue(myId, out var leaderId))
            {
                var members = PartyState.MemberOf
                    .Where(m => m.Value == leaderId)
                    .Select(m => new { AccountId = m.Key })
                    .Append(new { AccountId = leaderId })
                    .ToList();
                return Pascal(new { LeaderId = leaderId, Members = members });
            }

            // Am I a leader with at least one member?
            var myMembers = PartyState.MemberOf
                .Where(m => m.Value == myId)
                .Select(m => new { AccountId = m.Key })
                .ToList();
            if (myMembers.Count > 0)
            {
                var allMembers = myMembers
                    .Append(new { AccountId = myId })
                    .ToList();
                return Pascal(new { LeaderId = myId, Members = allMembers });
            }

            // Not in any party — return empty so the client clears the panel.
            return Pascal(new { LeaderId = 0, Members = Array.Empty<object>() });
        }
    }

    public class InviteData
    {
        public string? InviteId { get; set; }
        public int SenderId { get; set; }
        public int TargetId { get; set; }
        public string? RoomName { get; set; }
        public int RoomId { get; set; }
        public bool IsPartyInvite { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
