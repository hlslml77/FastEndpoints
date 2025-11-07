using Microsoft.EntityFrameworkCore;
using Web.Data.Entities;

namespace Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<PlayerRoleGrowth> PlayerRoleGrowth { get; set; }

    // Add your DbSet properties here. For example:
    // public DbSet<Product> Products { get; set; }
}

