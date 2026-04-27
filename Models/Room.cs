public class Room
{
    public int Id { get; set; }
    public long RoomId { get; set; }
    public string? PhotonRoomName { get; set; }
    public string? Region { get; set; }
    public int PlayerCount { get; set; }
    public bool IsPrivate { get; set; }
    public bool IsJoinable { get; set; }
    public DateTime CreatedAt { get; set; }
}
