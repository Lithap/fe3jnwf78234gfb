namespace RetroRec_Server.Models;

// Persisted friend-request / friendship storage. Replaces the previous
// in-memory PartyState.FriendRequests dictionary which was lost on every
// server restart — players reported "I added them as a friend, server
// restarted, we're not friends anymore".
//
// One row per directional request: SenderId sent a request to TargetId.
// "Mutual friends" = both directional rows exist. Decline/remove deletes
// the relevant rows. Same key shape ("senderId_targetId") is preserved
// in the controller's helper methods so the rest of the friend code
// (RelationshipsV2Get, AddFriend response shapes) didn't have to change.
public class FriendRelationship
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public int TargetId { get; set; }
    public DateTime CreatedAt { get; set; }
}
