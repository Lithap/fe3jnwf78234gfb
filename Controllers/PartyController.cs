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
            if (myId == 0) myId = 2;
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
            string targetStr = null, roomName = null, roomIdStr = null;

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
                                else if (el.ValueKind == System.Text.Json.JsonValueKind.String) targetStr = el.GetString();
                                if (!string.IsNullOrEmpty(targetStr)) break;
                            }
                        }
                        foreach (var key in new[] { "roomName", "RoomName" })
                        {
                            if (string.IsNullOrEmpty(roomName) &&
                                doc.RootElement.TryGetProperty(key, out var el) &&
                                el.ValueKind == System.Text.Json.JsonValueKind.String)
                                roomName = el.GetString();
                        }
                        foreach (var key in new[] { "roomId", "RoomId" })
                        {
                            if (string.IsNullOrEmpty(roomIdStr) &&
                                doc.RootElement.TryGetProperty(key, out var el))
                            {
                                roomIdStr = el.ValueKind == System.Text.Json.JsonValueKind.Number
                                    ? el.GetRawText()
                                    : el.GetString();
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
            if (myId == 0) myId = 2;

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
            if (myId == 0) myId = 2;

            if (!PartyState.Invites.TryRemove(inviteId, out var invite))
                return Pascal(new { ErrorCode = 0 });

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

        [HttpPost("/api/party/v1/leave")]
        [HttpPost("/party/v1/leave")]
        public IActionResult LeaveParty()
        {
            int myId = GetAccountIdFromAuth();
            if (myId == 0) myId = 2;
            PartyState.MemberOf.TryRemove(myId, out _);
            return Ok(new { });
        }

        // Returns the current party composition. The client calls this to
        // populate the party panel. Without it the list shows empty even though
        // Leave Party is still visible.
        [HttpGet("/api/party/v1")]
        [HttpGet("/party/v1")]
        [HttpGet("/api/party/v2")]
        [HttpGet("/party/v2")]
        public IActionResult GetParty()
        {
            int myId = GetAccountIdFromAuth();
            if (myId == 0) myId = 2;

            // Am I a member of someone else's party?
            if (PartyState.MemberOf.TryGetValue(myId, out var leaderId))
            {
                var coMembers = PartyState.MemberOf
                    .Where(m => m.Value == leaderId)
                    .Select(m => new { AccountId = m.Key })
                    .ToList();
                return Pascal(new { LeaderId = leaderId, Members = coMembers });
            }

            // Am I a leader with at least one member?
            var myMembers = PartyState.MemberOf
                .Where(m => m.Value == myId)
                .Select(m => new { AccountId = m.Key })
                .ToList();
            if (myMembers.Count > 0)
                return Pascal(new { LeaderId = myId, Members = myMembers });

            // Not in any party — return empty so the client clears the panel.
            return Pascal(new { LeaderId = 0, Members = new object[] { } });
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
