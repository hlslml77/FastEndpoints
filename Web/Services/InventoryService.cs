using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Data.Entities;
using Web.Data.Config;
using Serilog;

namespace Web.Services;

public interface IInventoryService
{
    Task GrantItemAsync(long userId, int itemId, int amount, CancellationToken ct = default);
    Task ConsumeItemAsync(long userId, int itemId, int amount, CancellationToken ct = default);

    Task<List<PlayerItem>> GetItemsAsync(long userId, CancellationToken ct = default);
    Task<List<PlayerEquipmentItem>> GetEquipmentsAsync(long userId, CancellationToken ct = default);

    Task EquipAsync(long userId, long equipmentRecordId, CancellationToken ct = default);
    Task UnequipAsync(long userId, long equipmentRecordId, CancellationToken ct = default);
}

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _db;
    private readonly IItemConfigService _cfg;
    private readonly Random _rand = new();

    public InventoryService(AppDbContext db, IItemConfigService cfg)
    {
        _db = db; _cfg = cfg;
    }

    public async Task GrantItemAsync(long userId, int itemId, int amount, CancellationToken ct = default)
    {
        if (amount <= 0) return;
        var itemCfg = _cfg.GetItem(itemId) ?? throw new ArgumentException($"Item {itemId} not found");

        if (itemCfg.PropType == ItemPropTypes.Stackable)
        {
            var rec = await _db.PlayerItem.FirstOrDefaultAsync(x => x.UserId == userId && x.ItemId == itemId, ct);
            if (rec == null)
            {
                rec = new PlayerItem { UserId = userId, ItemId = itemId, Amount = amount, UpdatedAt = DateTime.UtcNow };
                _db.PlayerItem.Add(rec);
            }
            else
            {
                rec.Amount += amount;
                rec.UpdatedAt = DateTime.UtcNow;
            }
        }
        else if (itemCfg.PropType == ItemPropTypes.Equipment)
        {
            // create equipment records with rolled stats
            var eqCfg = _cfg.GetEquipmentByEquipId(itemId) ?? throw new ArgumentException($"Equip config not found for {itemId}");
            for (int i = 0; i < amount; i++)
            {
                var rec = new PlayerEquipmentItem
                {
                    UserId = userId,
                    EquipId = itemId,
                    Quality = eqCfg.Quality,
                    Part = eqCfg.Part,
                    IsEquipped = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                // roll stat value from [min,max] array
                int? Roll(int[]? arr)
                {
                    if (arr == null || arr.Length != 2) return null;
                    var min = arr[0];
                    var max = arr[1];
                    if (max < min) (min, max) = (max, min);
                    return _rand.Next(min, max + 1);
                }
                rec.Attack          = Roll(eqCfg.AttackRange);
                rec.HP              = Roll(eqCfg.HPRange);
                rec.Defense         = Roll(eqCfg.DefenseRange);
                rec.Critical        = Roll(eqCfg.Critical);
                rec.AttackSpeed     = Roll(eqCfg.AttackSpeed);
                rec.CriticalDamage  = Roll(eqCfg.CriticalDamage);
                rec.UpperLimb       = Roll(eqCfg.UpperLimbRange);
                rec.LowerLimb       = Roll(eqCfg.LowerLimbRange);
                rec.Core            = Roll(eqCfg.CoreRange);
                rec.HeartLungs      = Roll(eqCfg.HeartLungsRange);

                    // random sub attribute
                    var randCfg = _cfg.GetRandomConfig(eqCfg.Random);
                    if (randCfg != null)
                    {
                        var possible = new List<string>();
                        if (randCfg.AttackRange != null) possible.Add("Attack");
                        if (randCfg.HPRange != null) possible.Add("HP");
                        if (randCfg.DefenseRange != null) possible.Add("Defense");
                        if (randCfg.AttackSpeedRange != null) possible.Add("AttackSpeed");
                        if (randCfg.CriticalRange != null) possible.Add("Critical");
                        if (randCfg.CriticalDamageRange != null) possible.Add("CriticalDamage");
                        if (randCfg.EfficiencyRange != null) possible.Add("Efficiency");
                        if (randCfg.EnergyRange != null) possible.Add("Energy");
                        if (randCfg.SpeedRange != null) possible.Add("Speed");
                        if (possible.Count > 0)
                        {
                            var chosen = possible[_rand.Next(possible.Count)];
                            double RollD(double[] arr)
                            {
                                var min = arr[0]; var max = arr[1]; if (max < min) (min, max) = (max, min); return _rand.NextDouble() * (max - min) + min;
                            }
                            switch (chosen)
                            {
                                case "Attack": rec.Attack ??= _rand.Next((int)randCfg.AttackRange![0], (int)randCfg.AttackRange![1] + 1); break;
                                case "HP": rec.HP ??= _rand.Next((int)randCfg.HPRange![0], (int)randCfg.HPRange![1] + 1); break;
                                case "Defense": rec.Defense ??= _rand.Next((int)randCfg.DefenseRange![0], (int)randCfg.DefenseRange![1] + 1); break;
                                case "AttackSpeed": rec.AttackSpeed ??= (int)Math.Round(RollD(randCfg.AttackSpeedRange!)); break;
                                case "Critical": rec.Critical ??= (int)Math.Round(RollD(randCfg.CriticalRange!)); break;
                                case "CriticalDamage": rec.CriticalDamage ??= (int)Math.Round(RollD(randCfg.CriticalDamageRange!)); break;
                                case "Efficiency": rec.Efficiency ??= RollD(randCfg.EfficiencyRange!); break;
                                case "Energy": rec.Energy ??= RollD(randCfg.EnergyRange!); break;
                                case "Speed": rec.Speed ??= RollD(randCfg.SpeedRange!); break;
                            }
                        }
                    }

                    // weapon special attribute chance 20%
                    if (eqCfg.Part == 1)
                    {
                        if (_rand.NextDouble() < 0.2)
                        {
                            var entries = _cfg.GetAllEntryConfigs();
                            if (entries.Count > 0)
                            {
                                var entry = entries[_rand.Next(entries.Count)];
                                rec.SpecialEntryId = entry.ID;
                            }
                        }
                    }

                    _db.PlayerEquipmentItem.Add(rec);
            }
        }
        else
        {
            throw new ArgumentException($"Unsupported PropType {itemCfg.PropType} for item {itemId}");
        }

        await _db.SaveChangesAsync(ct);
        Log.Information("Granted item {ItemId} x{Amount} to user {UserId}", itemId, amount, userId);
    }

    public async Task ConsumeItemAsync(long userId, int itemId, int amount, CancellationToken ct = default)
    {
        if (amount <= 0) return;
        var itemCfg = _cfg.GetItem(itemId) ?? throw new ArgumentException($"Item {itemId} not found");
        if (itemCfg.PropType != ItemPropTypes.Stackable) throw new ArgumentException("Only stackable items can be consumed");
        var rec = await _db.PlayerItem.FirstOrDefaultAsync(x => x.UserId == userId && x.ItemId == itemId, ct);
        if (rec == null || rec.Amount < amount) throw new ArgumentException("Not enough items");
        rec.Amount -= amount;
        rec.UpdatedAt = DateTime.UtcNow;
        if (rec.Amount == 0) _db.PlayerItem.Remove(rec);
        await _db.SaveChangesAsync(ct);
    }

    public Task<List<PlayerItem>> GetItemsAsync(long userId, CancellationToken ct = default)
        => _db.PlayerItem.Where(x => x.UserId == userId).OrderBy(x => x.ItemId).ToListAsync(ct);

    public Task<List<PlayerEquipmentItem>> GetEquipmentsAsync(long userId, CancellationToken ct = default)
        => _db.PlayerEquipmentItem.Where(x => x.UserId == userId).OrderByDescending(x => x.UpdatedAt).ToListAsync(ct);

    public async Task EquipAsync(long userId, long equipmentRecordId, CancellationToken ct = default)
    {
        var rec = await _db.PlayerEquipmentItem.FirstOrDefaultAsync(x => x.Id == equipmentRecordId && x.UserId == userId, ct)
                  ?? throw new ArgumentException("Equipment not found");
        // unequip same part
        var samePart = await _db.PlayerEquipmentItem.Where(x => x.UserId == userId && x.Part == rec.Part && x.IsEquipped).ToListAsync(ct);
        foreach (var s in samePart) { s.IsEquipped = false; s.UpdatedAt = DateTime.UtcNow; }
        rec.IsEquipped = true; rec.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task UnequipAsync(long userId, long equipmentRecordId, CancellationToken ct = default)
    {
        var rec = await _db.PlayerEquipmentItem.FirstOrDefaultAsync(x => x.Id == equipmentRecordId && x.UserId == userId, ct)
                  ?? throw new ArgumentException("Equipment not found");
        rec.IsEquipped = false; rec.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

