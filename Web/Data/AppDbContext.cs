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
    public DbSet<PlayerUnlockedLocation> PlayerUnlockedLocation { get; set; }
    public DbSet<PlayerDailyRandomEvent> PlayerDailyRandomEvent { get; set; }
    public DbSet<LocationPeopleCount> LocationPeopleCount { get; set; }
    public DbSet<TravelStageMessage> TravelStageMessage { get; set; }

    // Inventory/Equipment
    public DbSet<PlayerItem> PlayerItem { get; set; }
    public DbSet<PlayerEquipmentItem> PlayerEquipmentItem { get; set; }

    // Collections
    public DbSet<PlayerCollection> PlayerCollection { get; set; }
    public DbSet<PlayerCollectionComboClaim> PlayerCollectionComboClaim { get; set; }
    public DbSet<CollectionGlobalCounter> CollectionGlobalCounter { get; set; }

    // Game Statistics
    public DbSet<DailyGameStatistics> DailyGameStatistics { get; set; }
    public DbSet<OnlinePlayersSnapshot> OnlinePlayersSnapshot { get; set; }
    public DbSet<PlayerActivityStatistics> PlayerActivityStatistics { get; set; }

    public DbSet<PlayerTutorialProgress> PlayerTutorialProgress { get; set; }

    // PVE Rank
    public DbSet<Web.Data.Entities.PlayerSportDaily> PlayerSportDaily { get; set; }
    public DbSet<Web.Data.Entities.PveRankBoard> PveRankBoard { get; set; }
    public DbSet<Web.Data.Entities.PveRankRewardGrant> PveRankRewardGrant { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 配置复合主键
        modelBuilder.Entity<PlayerMapLocationVisit>()
            .HasKey(e => new { e.UserId, e.LocationId });

        modelBuilder.Entity<PlayerCompletedLocation>()
            .HasKey(e => new { e.UserId, e.LocationId });

        modelBuilder.Entity<PlayerUnlockedLocation>()
            .HasKey(e => new { e.UserId, e.LocationId });

        modelBuilder.Entity<PlayerItem>()
            .HasKey(e => new { e.UserId, e.ItemId });

        modelBuilder.Entity<PlayerCollection>()
            .HasKey(e => new { e.UserId, e.CollectionId });

        modelBuilder.Entity<PlayerCollectionComboClaim>()
            .HasKey(e => new { e.UserId, e.ComboId });

        modelBuilder.Entity<CollectionGlobalCounter>()
            .HasKey(e => e.CollectionId);

        // PVE Rank keys and indexes
        modelBuilder.Entity<Web.Data.Entities.PlayerSportDaily>()
            .HasKey(e => new { e.UserId, e.Date, e.DeviceType });

        modelBuilder.Entity<Web.Data.Entities.PveRankBoard>()
            .HasKey(e => new { e.PeriodType, e.PeriodId, e.DeviceType, e.UserId });
        modelBuilder.Entity<Web.Data.Entities.PveRankBoard>()
            .HasIndex(e => new { e.PeriodType, e.PeriodId, e.DeviceType, e.TotalDistanceMeters });

    }
}

