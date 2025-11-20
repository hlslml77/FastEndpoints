using Microsoft.EntityFrameworkCore;
using Web.Data.Entities;

namespace Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<PlayerRole> PlayerRole { get; set; }
    public DbSet<PlayerMapProgress> PlayerMapProgress { get; set; }
    public DbSet<PlayerMapLocationVisit> PlayerMapLocationVisit { get; set; }
    public DbSet<PlayerCompletedLocation> PlayerCompletedLocation { get; set; }

    // Add your DbSet properties here. For example:
    // public DbSet<Product> Products { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 配置复合主键
        modelBuilder.Entity<PlayerMapLocationVisit>()
            .HasKey(e => new { e.UserId, e.LocationId });

        modelBuilder.Entity<PlayerCompletedLocation>()
            .HasKey(e => new { e.UserId, e.LocationId });
    }
}

