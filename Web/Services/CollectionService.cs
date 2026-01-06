using Microsoft.EntityFrameworkCore;
using Serilog;
using Web.Data;
using Web.Data.Entities;

namespace Web.Services;

public interface ICollectionService
{
    Task<(bool success, string message, int? collectionId)> ObtainAsync(long userId, CancellationToken ct = default);
    Task<List<int>> GetMyCollectionIdsAsync(long userId, CancellationToken ct = default);
    Task<(bool success, string message)> ClaimComboAsync(long userId, int comboId, CancellationToken ct = default);
}

public class CollectionService : ICollectionService
{
    private readonly AppDbContext _db;
    private readonly ICollectionConfigService _cfg;
    private readonly IInventoryService _inv;
private readonly IGeneralConfigService _gcfg;
    private readonly Random _rand = new();

    public CollectionService(AppDbContext db, ICollectionConfigService cfg, IInventoryService inv, IGeneralConfigService gcfg)
    {
        _db = db; _cfg = cfg; _inv = inv; _gcfg = gcfg;
    }

    public async Task<List<int>> GetMyCollectionIdsAsync(long userId, CancellationToken ct = default)
        => await _db.Set<PlayerCollection>().Where(x => x.UserId == userId).Select(x => x.CollectionId).ToListAsync(ct);

    public async Task<(bool success, string message, int? collectionId)> ObtainAsync(long userId, CancellationToken ct = default)
    {
        // 1. 先消耗碎片道具（配置于 WorldConfig ID=16，默认3）
        const int FragmentItemId = 1003;
        var cost = _gcfg.GetCollectionObtainCost();
        try
        {
            await _inv.ConsumeItemAsync(userId, FragmentItemId, cost, ct);
        }
        catch (Exception)
        {
            return (false, "道具不足", null);
        }

        var items = _cfg.Items;
        if (items.Count == 0)
        {
            // 若随机池为空则返还全部道具
            await _inv.GrantItemAsync(userId, FragmentItemId, cost, ct);
            return (false, "暂无可用藏品", null);
        }

        // 2. 过滤超出全局上限的藏品
        var availability = await _db.Set<CollectionGlobalCounter>().ToDictionaryAsync(x => x.CollectionId, x => x.TotalObtained, ct);
        var candidates = new List<(int idx, int weight)>();
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            var cap = it.LimitedEditionCollectibles.GetValueOrDefault(0);
            if (cap > 0 && availability.TryGetValue(it.ID, out var used) && used >= cap)
                continue; // 已达上限
            if (it.Weight <= 0) continue;
            candidates.Add((i, it.Weight));
        }
        if (candidates.Count == 0)
        {
            await _inv.GrantItemAsync(userId, FragmentItemId, cost, ct);
            return (false, "藏品数量不足", null);
        }

        // 3. 加权随机
        var total = candidates.Sum(c => c.weight);
        var roll = _rand.Next(1, total + 1);
        int acc = 0, chosenIdx = -1;
        foreach (var c in candidates)
        {
            acc += c.weight;
            if (roll <= acc) { chosenIdx = c.idx; break; }
        }
        if (chosenIdx == -1) chosenIdx = candidates[^1].idx;
        var chosen = items[chosenIdx];

        // 4. 判断是否重复
        var alreadyOwned = await _db.Set<PlayerCollection>().AnyAsync(x => x.UserId == userId && x.CollectionId == chosen.ID, ct);
        if (alreadyOwned)
        {
            var refund = cost / 2; // 取整
            if (refund > 0)
            {
                await _inv.GrantItemAsync(userId, FragmentItemId, refund, ct);
            }
            return (false, "未获得新藏品", null);
        }

        // 5. 记录玩家获得 & 更新全局计数（若有限量）
        _db.Add(new PlayerCollection { UserId = userId, CollectionId = chosen.ID, ObtainedAt = DateTime.UtcNow });
        if (chosen.LimitedEditionCollectibles.GetValueOrDefault(0) > 0)
        {
            var rec = await _db.Set<CollectionGlobalCounter>().FirstOrDefaultAsync(x => x.CollectionId == chosen.ID, ct);
            if (rec is null)
            {
                rec = new CollectionGlobalCounter { CollectionId = chosen.ID, TotalObtained = 1, UpdatedAt = DateTime.UtcNow };
                _db.Add(rec);
            }
            else
            {
                rec.TotalObtained += 1; rec.UpdatedAt = DateTime.UtcNow;
            }
        }
        // 6. 赠送装备（若配置）
        if (chosen.ClothingParts is { Length: 2 })
        {
            var equipId = chosen.ClothingParts[1];
            try { await _inv.GrantItemAsync(userId, equipId, 1, ct); }
            catch (Exception ex) { Log.Warning(ex, "Grant equipment failed for collection {Id}", chosen.ID); }
        }

        await _db.SaveChangesAsync(ct);
        return (true, "ok", chosen.ID);
    }

    public async Task<(bool success, string message)> ClaimComboAsync(long userId, int comboId, CancellationToken ct = default)
    {
        var combo = _cfg.Combos.FirstOrDefault(c => c.ID == comboId);
        if (combo is null) return (false, "组合不存在");
        var claimed = await _db.Set<PlayerCollectionComboClaim>().AnyAsync(x => x.UserId == userId && x.ComboId == comboId, ct);
        if (claimed) return (false, "已领取");

        var myIds = await GetMyCollectionIdsAsync(userId, ct);
        var required = combo.MeetTheConditions is null or { Length: 0 }
            ? _cfg.Items.Select(i => i.ID).ToArray()
            : combo.MeetTheConditions;
        if (!required.All(myIds.Contains))
            return (false, "未满足领取条件");

        // 发放奖励
        foreach (var pair in combo.Reward)
        {
            if (pair.Length >= 2)
            {
                var itemId = pair[0]; var amount = pair[1];
                await _inv.GrantItemAsync(userId, itemId, amount, ct);
            }
        }

        _db.Add(new PlayerCollectionComboClaim { UserId = userId, ComboId = comboId, ClaimedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync(ct);
        return (true, "ok");
    }
}

