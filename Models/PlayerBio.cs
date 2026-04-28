// Persisted bio storage. Replaces the previous in-memory
// PartyState.Bios dictionary which was lost on every server restart —
// players reported "I set my bio, restarted the server, it's gone".
//
// Single row per AccountId. The PlayerController bio endpoints upsert
// against this table so a bio survives reboots, account-cache clears,
// and migrating to a new DB file (the file is the source of truth).
public class PlayerBio
{
    public int Id { get; set; }
    public long AccountId { get; set; }
    public string Bio { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}
