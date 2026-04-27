using System.Collections.Concurrent;

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
    }
}
