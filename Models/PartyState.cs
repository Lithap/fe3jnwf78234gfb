using System.Collections.Concurrent;
using System.Linq;

// PartyState lives in Models/ but keeps the Controllers namespace so it is
// accessible from all controllers without an extra using directive.
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
        // never reuse IDs from a previous run. Marked volatile so reads in
        // other threads see the latest Interlocked.Increment result.
        public static volatile int NextRoomInstanceId = (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF);

        // key = memberId, value = partyLeaderId
        public static readonly ConcurrentDictionary<int, int> MemberOf = new();

        // key = inviteId
        public static readonly ConcurrentDictionary<string, InviteData> Invites = new();

        private static readonly object _inviteLock = new();

        // Reuse an outstanding invite between the same two players instead of
        // stacking stale follow prompts for each room hop. The newest room info
        // always wins, which matches how the client expects "follow me" invites
        // to behave when a party leader keeps moving.
        //
        // The lock makes the find-or-create atomic and doubles as the eviction
        // point for expired invites so the dictionary doesn't grow unboundedly.
        public static string UpsertInvite(int senderId, int targetId, string? roomName, int roomId, bool isPartyInvite)
        {
            lock (_inviteLock)
            {
                var now = DateTime.UtcNow;
                var expiryCutoff = now.AddMinutes(-5);

                string? existingKey = null;
                foreach (var kv in Invites)
                {
                    if (kv.Value.CreatedAt < expiryCutoff)
                    {
                        Invites.TryRemove(kv.Key, out _);
                        continue;
                    }
                    if (kv.Value.SenderId == senderId &&
                        kv.Value.TargetId == targetId &&
                        kv.Value.IsPartyInvite == isPartyInvite)
                    {
                        existingKey = kv.Key;
                    }
                }

                if (existingKey != null && Invites.TryGetValue(existingKey, out var existing))
                {
                    existing.RoomName = roomName;
                    existing.RoomId = roomId;
                    existing.CreatedAt = now;
                    return existingKey;
                }

                var inviteId = $"inv_{senderId}_{targetId}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}";
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
}
