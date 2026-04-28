public class PlayerCheer
{
    public int Id { get; set; }
    public int FromAccountId { get; set; }
    public int TargetAccountId { get; set; }
    public int CheerCategory { get; set; }
    public int RoomId { get; set; }
    public bool Anonymous { get; set; }
    public DateTime CreatedAt { get; set; }
}
