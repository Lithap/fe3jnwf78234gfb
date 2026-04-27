using Microsoft.EntityFrameworkCore;

public class RetroRecDb : DbContext
{
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Room> Rooms { get; set; }
    public DbSet<UserRoom> UserRooms { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite("Data Source=retrorec.db");
    }
}
