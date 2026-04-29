using Microsoft.EntityFrameworkCore;

namespace RetroRec_Server.Models;

public class RetroRecDb : DbContext
{
    public DbSet<Account> Accounts { get; set; } = null!;
    public DbSet<Room> Rooms { get; set; } = null!;
    public DbSet<UserRoom> UserRooms { get; set; } = null!;
    public DbSet<PlayerBio> Bios { get; set; } = null!;
    public DbSet<FriendRelationship> FriendRelationships { get; set; } = null!;
    public DbSet<PlayerCheer> PlayerCheers { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite("Data Source=retrorec.db");
    }
}
