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
                .Where(i => i.TargetId == myId && i.IsPartyInvite)
                .Select(i => new
                {
                    inviteId = i.InviteId,
                    InviteId = i.InviteId,
                    senderAccountId = i.SenderId,
                    SenderAccountId = i.SenderId,
                    roomName = i.RoomName,
                    RoomName = i.RoomName,
                    roomId = i.RoomId,
                    RoomId = i.RoomId,
                    isPartyInvite = i.IsPartyInvite,
                    IsPartyInvite = i.IsPartyInvite,
                    createdAt = i.CreatedAt,
                    CreatedAt = i.CreatedAt,
                    expiresAt = i.CreatedAt.AddMinutes(5),
                    ExpiresAt = i.CreatedAt.AddMinutes(5)
                });
            return Ok(pending);
        }

        [HttpPost("/api/invites/v1")]
        [HttpPost("/api/invites/v1/")]
        [HttpPost("/invites/v1")]
        [HttpPost("/invites/v1/")]
        [HttpPost("/api/invites/v1/{routeTargetId:int}")]
        [HttpPost("/invites/v1/{routeTargetId:int}")]
        public async Task<IActionResult> SendInvite(int routeTargetId = 0)
        {
            int myId = GetAccountIdFromAuth();
            if (myId == 0) myId = 2;

            var values = await ReadRequestValuesAsync();
            var targetId = routeTargetId != 0
                ? routeTargetId
                : GetIntValue(values,
                    "targetAccountId", "targetId", "accountId", "playerId",
                    "recipientAccountId", "toAccountId", "PlayerID");
            values.TryGetValue("roomName", out var roomName);
            values.TryGetValue("RoomName", out var roomNamePascal);
            values.TryGetValue("roomId", out var roomIdStr);
            values.TryGetValue("RoomId", out var roomIdStrPascal);

            if (targetId == 0 || targetId == myId)
                return BadRequest(new { ErrorCode = 1, Error = "Missing target account id" });

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
            if (!string.IsNullOrWhiteSpace(roomNamePascal)) roomName = roomNamePascal;
            if (!string.IsNullOrWhiteSpace(roomName)) invRoomName = roomName;
            if (string.IsNullOrWhiteSpace(roomIdStr)) roomIdStr = roomIdStrPascal;
            if (int.TryParse(roomIdStr, out var parsedRoomId) && parsedRoomId != 0) invRoomId = parsedRoomId;

            var inviteId = $"inv_{myId}_{targetId}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
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

            return Ok(new { inviteId = inviteId, InviteId = inviteId, ErrorCode = 0 });
        }

        [HttpPost("/api/invites/v1/{inviteId}/accept")]
        [HttpPost("/invites/v1/{inviteId}/accept")]
        public IActionResult AcceptInvite(string inviteId)
        {
            int myId = GetAccountIdFromAuth();
            if (myId == 0) myId = 2;

            if (!PartyState.Invites.TryRemove(inviteId, out var invite))
                return Ok(new { });

            if (invite.IsPartyInvite)
                PartyState.MemberOf[myId] = invite.SenderId;

            return Ok(new
            {
                errorCode = 0,
                ErrorCode = 0,
                roomName = invite.RoomName,
                RoomName = invite.RoomName,
                roomId = invite.RoomId,
                RoomId = invite.RoomId
            });
        }

        [HttpDelete("/api/invites/v1/{inviteId}")]
        [HttpDelete("/invites/v1/{inviteId}")]
        public IActionResult DeclineInvite(string inviteId)
        {
            PartyState.Invites.TryRemove(inviteId, out _);
            return Ok(new { });
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
