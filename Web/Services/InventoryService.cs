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

        // IMPORTANT: don't crash the server for a bad config id.
        // Treat as a business error (caller can translate to 4xx).
        var itemCfg = _cfg.GetItem(itemId);
        if (itemCfg is null)
            throw new InvalidOperationException($"Item {itemId} not found");

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
            var eqCfg = _cfg.GetEquipmentByEquipId(itemId);
            if (eqCfg is null)
                throw new InvalidOperationException($"Equip config not found for {itemId}");

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
                int? RollI(double[]? arr)
                {
                    if (arr is null || arr.Length != 2) return null;
                    var min = (int)Math.Round(arr[0]);
                    var max = (int)Math.Round(arr[1]);
                    if (max < min) (min, max) = (max, min);
                    return _rand.Next(min, max + 1);
                }

                double? RollD(double[]? arr)
                {
                    if (arr is null || arr.Length != 2) return null;
                    var min = arr[0];
                    var max = arr[1];
                    if (max < min) (min, max) = (max, min);
                    return _rand.NextDouble() * (max - min) + min;
                }

                // roll the two fixed attributes based on equipment part
                switch (eqCfg.Part)
                {
                    // Weapon (攻击力 + 暴击率)
                    case 1:
                        rec.Attack = RollI(eqCfg.AttackRange);
                        rec.Critical = RollI(eqCfg.Critical);
                        break;
                    // Gloves (攻击力 + 攻速)
                    case 2:
                        rec.Attack = RollI(eqCfg.AttackRange);
                        rec.AttackSpeed = RollI(eqCfg.AttackSpeed);
                        break;
                    // Armor/Top (生命值 + 防御)
                    case 3:
                        rec.HP = RollI(eqCfg.HPRange);
                        rec.Defense = RollI(eqCfg.DefenseRange);
                        break;
                    // Pants (生命值 + 暴击伤害)
                    case 4:
                        rec.HP = RollI(eqCfg.HPRange);
                        rec.CriticalDamage = RollI(eqCfg.CriticalDamage);
                        break;
                }

                // random sub attribute (add one extra attribute if possible)
                try
                {
                    var randCfg = _cfg.GetRandomConfig(eqCfg.Random);
                    if (randCfg != null)
                    {
                        var possible = new List<string>();
                        // random attribute is chosen from the seed's configured ranges in Equipment-Random.
                        // It is allowed to be the same type as a fixed attribute.
                        if (randCfg.AttackRange is { Length: 2 }) possible.Add("Attack");
                        if (randCfg.HPRange is { Length: 2 }) possible.Add("HP");
                        if (randCfg.DefenseRange is { Length: 2 }) possible.Add("Defense");
                        if (randCfg.AttackSpeedRange is { Length: 2 }) possible.Add("AttackSpeed");
                        if (randCfg.CriticalRange is { Length: 2 }) possible.Add("Critical");
                        if (randCfg.CriticalDamageRange is { Length: 2 }) possible.Add("CriticalDamage");
                        if (randCfg.EfficiencyRange is { Length: 2 }) possible.Add("Efficiency");
                        if (randCfg.EnergyRange is { Length: 2 }) possible.Add("Energy");
                        if (randCfg.SpeedRange is { Length: 2 }) possible.Add("Speed");

                        if (possible.Count > 0)
                        {
                            var chosen = possible[_rand.Next(possible.Count)];
                            switch (chosen)
                            {
                                case "Attack":
                                    rec.Attack ??= RollI(randCfg.AttackRange) ?? rec.Attack;
                                    break;
                                case "HP":
                                    rec.HP ??= RollI(randCfg.HPRange) ?? rec.HP;
                                    break;
                                case "Defense":
                                    rec.Defense ??= RollI(randCfg.DefenseRange) ?? rec.Defense;
                                    break;
                                case "AttackSpeed":
                                    if (rec.AttackSpeed is null)
                                    {
                                        var v = RollD(randCfg.AttackSpeedRange);
                                        if (v is not null) rec.AttackSpeed = (int)Math.Round(v.Value);
                                    }
                                    break;
                                case "Critical":
                                    if (rec.Critical is null)
                                    {
                                        var v = RollD(randCfg.CriticalRange);
                                        if (v is not null) rec.Critical = (int)Math.Round(v.Value);
                                    }
                                    break;
                                case "CriticalDamage":
                                    if (rec.CriticalDamage is null)
                                    {
                                        var v = RollD(randCfg.CriticalDamageRange);
                                        if (v is not null) rec.CriticalDamage = (int)Math.Round(v.Value);
                                    }
                                    break;
                                case "Efficiency":
                                    rec.Efficiency ??= RollD(randCfg.EfficiencyRange);
                                    break;
                                case "Energy":
                                    rec.Energy ??= RollD(randCfg.EnergyRange);
                                    break;
                                case "Speed":
                                    rec.Speed ??= RollD(randCfg.SpeedRange);
                                    break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Config issues should not bring down the server.
                    Log.Warning(ex, "Failed to roll random sub attribute for equip {EquipId} (random={RandomId})", itemId, eqCfg.Random);
                }

                // weapon special attribute chance 20%
                if (eqCfg.Part == 1 && _rand.NextDouble() < 0.2)
                {
                    var entries = _cfg.GetAllEntryConfigs();
                    if (entries.Count > 0)
                    {
                        var entry = entries[_rand.Next(entries.Count)];
                        rec.SpecialEntryId = entry.ID;
                    }
                }

                _db.PlayerEquipmentItem.Add(rec);
            }
        }
        else
        {
            throw new InvalidOperationException($"Unsupported PropType {itemCfg.PropType} for item {itemId}");
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

