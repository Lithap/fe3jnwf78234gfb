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

            var expired = new List<string>();
            var now = DateTime.UtcNow;
            var pending = new List<object>();

            foreach (var i in PartyState.Invites.Values)
            {
                if (i.TargetId != myId) continue;
                if (i.InviteId != null && i.InviteId.StartsWith("party_")) continue;

                if (now > i.CreatedAt.AddMinutes(5))
                {
                    if (i.InviteId != null) expired.Add(i.InviteId);
                    continue;
                }

                pending.Add(new
                {
                    InviteId = i.InviteId,
                    SenderAccountId = i.SenderId,
                    RoomName = i.RoomName,
                    RoomId = i.RoomId,
                    IsPartyInvite = i.IsPartyInvite,
                    CreatedAt = i.CreatedAt,
                    ExpiresAt = i.CreatedAt.AddMinutes(5)
                });
            }

            foreach (var id in expired)
                PartyState.Invites.TryRemove(id, out _);

            return Pascal(pending);
        }

        [HttpPost("/api/invites/v1")]
        [HttpPost("/api/invites/v1/")]
        [HttpPost("/invites/v1")]
        [HttpPost("/invites/v1/")]
        public async Task<IActionResult> SendInvite()
        {
            int myId = GetAccountIdFromAuth();
            if (myId == 0) myId = 2;

            string targetStr = null, roomName = null, roomIdStr = null;

            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync();
                form.TryGetValue("targetAccountId", out var t); targetStr = t;
                form.TryGetValue("roomName", out var r); roomName = r;
                form.TryGetValue("roomId", out var ri); roomIdStr = ri;
            }
            else
            {
                try
                {
                    using var reader = new System.IO.StreamReader(Request.Body);
                    var body = await reader.ReadToEndAsync();
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        var doc = System.Text.Json.JsonDocument.Parse(body);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("targetAccountId", out var tEl))
                            targetStr = tEl.ValueKind == System.Text.Json.JsonValueKind.Number ? tEl.GetRawText() : tEl.GetString();
                        if (root.TryGetProperty("roomName", out var rEl))
                            roomName = rEl.GetString();
                        if (root.TryGetProperty("roomId", out var riEl))
                            roomIdStr = riEl.ValueKind == System.Text.Json.JsonValueKind.Number ? riEl.GetRawText() : riEl.GetString();
                    }
                }
                catch { }
            }

            if (!int.TryParse(targetStr, out var targetId) || targetId == 0)
            {
                int qTarget = 0;
                if (Request.Query.TryGetValue("targetAccountId", out var qv))
                    int.TryParse(qv, out qTarget);
                if (qTarget == 0) return BadRequest();
                targetId = qTarget;
            }

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
            if (int.TryParse(roomIdStr, out var parsedRoomId) && parsedRoomId != 0) invRoomId = parsedRoomId;

            var inviteId = $"inv_{myId}_{targetId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            PartyState.Invites[inviteId] = new InviteData
            {
                InviteId = inviteId,
                SenderId = myId,
                TargetId = targetId,
                RoomName = invRoomName,
                RoomId = invRoomId,
                IsPartyInvite = true,
                CreatedAt = DateTime.UtcNow
            };

            return Pascal(new { InviteId = inviteId });
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
                InviteId = inviteId,
                SenderAccountId = invite.SenderId
            });
        }

        [HttpDelete("/api/invites/v1/{inviteId}")]
        [HttpDelete("/invites/v1/{inviteId}")]
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
