using Microsoft.EntityFrameworkCore;
using Web.Data.Entities;

namespace Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<PlayerRoleGrowth> PlayerRoleGrowth { get; set; }
    public DbSet<PlayerMapProgress> PlayerMapProgress { get; set; }
    public DbSet<PlayerMapLocationVisit> PlayerMapLocationVisit { get; set; }

    // Add your DbSet properties here. For example:
    // public DbSet<Product> Products { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 配置复合主键
        modelBuilder.Entity<PlayerMapLocationVisit>()
            .HasKey(e => new { e.UserId, e.LocationId });
    }
}

