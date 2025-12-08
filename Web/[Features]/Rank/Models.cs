namespace RankApi;

public class LeaderboardRequest
{
    public int PeriodType { get; set; } = 1; // 1=周榜,2=赛季榜
    public int DeviceType { get; set; } = 0; // 0=跑步,1=划船,2=单车,3=手环
    public int Top { get; set; } = 100;      // 默认100
}

public class LeaderboardItemDto
{
    public long UserId { get; set; }
    public decimal DistanceMeters { get; set; }
    public int Rank { get; set; }
}

public class LeaderboardResponse
{
    public int PeriodType { get; set; }
    public int DeviceType { get; set; }
    public int PeriodId { get; set; }
    public List<LeaderboardItemDto> Top { get; set; } = new();
    public LeaderboardItemDto? Me { get; set; }
}

public class ClaimRequest
{
    public int DeviceType { get; set; }
}

public class RewardDto
{
    public int ItemId { get; set; }
    public int Amount { get; set; }
}

public class ClaimResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "ok";
    public List<RewardDto> Rewards { get; set; } = new();
}

