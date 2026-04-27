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
        [HttpGet("/api/invites/v2")]
        [HttpGet("/api/invites/v2/")]
        [HttpGet("/invites/v2")]
        [HttpGet("/invites/v2/")]
        public IActionResult GetInvites()
        {
            int myId = GetAccountIdFromAuth();
            if (myId == 0) myId = 2;
            var now = DateTime.UtcNow;

            foreach (var kv in PartyState.Invites)
            {
                if (kv.Value.CreatedAt.AddMinutes(5) < now)
                    PartyState.Invites.TryRemove(kv.Key, out _);
            }

            var pending = PartyState.Invites.Values
                .Where(i => i.TargetId == myId)
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new
                {
                    InviteId = i.InviteId,
                    inviteId = i.InviteId,
                    SenderAccountId = i.SenderId,
                    senderAccountId = i.SenderId,
                    RoomName = i.RoomName,
                    roomName = i.RoomName,
                    RoomId = i.RoomId,
                    roomId = i.RoomId,
                    IsPartyInvite = i.IsPartyInvite,
                    isPartyInvite = i.IsPartyInvite,
                    CreatedAt = i.CreatedAt,
                    createdAt = i.CreatedAt,
                    ExpiresAt = i.CreatedAt.AddMinutes(5),
                    expiresAt = i.CreatedAt.AddMinutes(5)
                })
                .ToList();
            return Pascal(pending);
        }

        [HttpPost("/api/invites/v1")]
        [HttpPost("/api/invites/v1/")]
        [HttpPost("/invites/v1")]
        [HttpPost("/invites/v1/")]
        [HttpPost("/api/invites/v2")]
        [HttpPost("/api/invites/v2/")]
        [HttpPost("/invites/v2")]
        [HttpPost("/invites/v2/")]
        [HttpPost("/api/party/v1/invite")]
        [HttpPost("/party/v1/invite")]
        [HttpPost("/api/party/v2/invite")]
        [HttpPost("/party/v2/invite")]
        [HttpPost("/api/party/v1/invite/{targetId:int}")]
        [HttpPost("/party/v1/invite/{targetId:int}")]
        [HttpPost("/api/party/v2/invite/{targetId:int}")]
        [HttpPost("/party/v2/invite/{targetId:int}")]
        public async Task<IActionResult> SendInvite()
        {
            int myId = GetAccountIdFromAuth();
            if (myId == 0) myId = 2;

            var values = await CollectRequestValuesAsync();
            int targetId = GetIntValue(values,
                "targetAccountId",
                "targetId",
                "accountId",
                "id",
                "playerId",
                "objectAccountId");
            if (targetId == 0 || targetId == myId)
                return Pascal(new { ErrorCode = 0 });

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
            var roomName = GetStringValue(values, "roomName", "room", "RoomName");
            var parsedRoomId = GetIntValue(values, "roomId", "RoomId");
            if (!string.IsNullOrWhiteSpace(roomName)) invRoomName = roomName;
            if (parsedRoomId != 0) invRoomId = parsedRoomId;

            var inviteId = $"inv_{myId}_{targetId}_{Guid.NewGuid():N}";
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

            return Pascal(new { ErrorCode = 0, InviteId = inviteId, inviteId = inviteId });
        }

        [HttpPost("/api/invites/v1/{inviteId}/accept")]
        [HttpPost("/invites/v1/{inviteId}/accept")]
        [HttpPost("/api/invites/v2/{inviteId}/accept")]
        [HttpPost("/invites/v2/{inviteId}/accept")]
        [HttpPost("/api/invites/v1/accept")]
        [HttpPost("/invites/v1/accept")]
        [HttpPost("/api/invites/v2/accept")]
        [HttpPost("/invites/v2/accept")]
        [HttpPost("/api/party/v1/acceptinvite")]
        [HttpPost("/party/v1/acceptinvite")]
        [HttpPost("/api/party/v2/acceptinvite")]
        [HttpPost("/party/v2/acceptinvite")]
        public async Task<IActionResult> AcceptInvite(string? inviteId = null)
        {
            int myId = GetAccountIdFromAuth();
            if (myId == 0) myId = 2;

            if (string.IsNullOrWhiteSpace(inviteId))
            {
                var values = await CollectRequestValuesAsync();
                inviteId = GetStringValue(values, "inviteId", "InviteId", "id");
            }
            if (string.IsNullOrWhiteSpace(inviteId))
                return Ok(new { });

            if (!PartyState.Invites.TryGetValue(inviteId, out var invite))
                return Ok(new { });

            if (invite.TargetId != myId)
                return Ok(new { });

            if (!PartyState.Invites.TryRemove(inviteId, out invite))
                return Ok(new { });

            if (invite.IsPartyInvite)
                PartyState.MemberOf[myId] = invite.SenderId;

            return Pascal(new
            {
                ErrorCode = 0,
                RoomName = invite.RoomName,
                roomName = invite.RoomName,
                RoomId = invite.RoomId,
                roomId = invite.RoomId,
                IsPartyInvite = invite.IsPartyInvite,
                isPartyInvite = invite.IsPartyInvite
            });
        }

        [HttpDelete("/api/invites/v1/{inviteId}")]
        [HttpDelete("/invites/v1/{inviteId}")]
        [HttpDelete("/api/invites/v2/{inviteId}")]
        [HttpDelete("/invites/v2/{inviteId}")]
        [HttpPost("/api/invites/v1/{inviteId}/decline")]
        [HttpPost("/invites/v1/{inviteId}/decline")]
        [HttpPost("/api/invites/v2/{inviteId}/decline")]
        [HttpPost("/invites/v2/{inviteId}/decline")]
        [HttpPost("/api/invites/v1/decline")]
        [HttpPost("/invites/v1/decline")]
        [HttpPost("/api/invites/v2/decline")]
        [HttpPost("/invites/v2/decline")]
        [HttpPost("/api/party/v1/declineinvite")]
        [HttpPost("/party/v1/declineinvite")]
        [HttpPost("/api/party/v2/declineinvite")]
        [HttpPost("/party/v2/declineinvite")]
        public async Task<IActionResult> DeclineInvite(string? inviteId = null)
        {
            if (string.IsNullOrWhiteSpace(inviteId))
            {
                var values = await CollectRequestValuesAsync();
                inviteId = GetStringValue(values, "inviteId", "InviteId", "id");
            }
            if (string.IsNullOrWhiteSpace(inviteId))
                return Ok(new { });

            PartyState.Invites.TryRemove(inviteId, out _);
            return Ok(new { });
        }

        [HttpPost("/api/party/v1/leave")]
        [HttpPost("/party/v1/leave")]
        [HttpPost("/api/party/v2/leave")]
        [HttpPost("/party/v2/leave")]
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
