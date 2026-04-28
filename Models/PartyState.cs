using System.Collections.Concurrent;
using System.Linq;

namespace RetroRec_Server.Controllers
{
    // Centralized in-memory state for the whole server.
    // Previously scattered across PlayerController, RoomsController, and this stub.
    public static class PartyState
    {
        // key = accountId, value = bio string
        public static readonly ConcurrentDictionary<long, string> Bios = new();

        // key = "senderId_targetId". Both directions present = mutual friends.
        public static readonly ConcurrentDictionary<string, bool> FriendRequests = new();

        // key = (roomId, subRoomId). Shared so all players going to the same
        // non-dorm room get the same instanceId and can see each other.
        public static readonly ConcurrentDictionary<(int, int), object> RoomInstancesByRoom = new();

        // Monotonic room instance ID counter, seeded from clock so restarts
        // never reuse IDs from a previous run.
        public static int NextRoomInstanceId = (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF);

        // key = memberId, value = partyLeaderId
        public static readonly ConcurrentDictionary<int, int> MemberOf = new();

        // key = inviteId
        public static readonly ConcurrentDictionary<string, InviteData> Invites = new();

        // Reuse an outstanding invite between the same two players instead of
        // stacking stale follow prompts for each room hop. The newest room info
        // always wins, which matches how the client expects "follow me" invites
        // to behave when a party leader keeps moving.
        public static string UpsertInvite(int senderId, int targetId, string? roomName, int roomId, bool isPartyInvite)
        {
            var existing = Invites.FirstOrDefault(kv =>
                kv.Value.SenderId == senderId &&
                kv.Value.TargetId == targetId &&
                kv.Value.IsPartyInvite == isPartyInvite);

            var now = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(existing.Key))
            {
                existing.Value.RoomName = roomName;
                existing.Value.RoomId = roomId;
                existing.Value.CreatedAt = now;
                return existing.Key;
            }

            var inviteId = $"inv_{senderId}_{targetId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            Invites[inviteId] = new InviteData
            {
                InviteId = inviteId,
                SenderId = senderId,
                TargetId = targetId,
                RoomName = roomName,
                RoomId = roomId,
                IsPartyInvite = isPartyInvite,
                CreatedAt = now
            };
            return inviteId;
        }
    }
}
