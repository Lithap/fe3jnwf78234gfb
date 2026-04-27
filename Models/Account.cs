public class Account
{
    public int Id { get; set; }
    public string? Username { get; set; }
    public string? Platform { get; set; }
    public string? PlatformId { get; set; }
    public string? AuthToken { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Level { get; set; }
    public int XP { get; set; }
}
