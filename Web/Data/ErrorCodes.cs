namespace Web.Data;

/// <summary>
/// 统一错误码定义（int）。
/// </summary>
public static class ErrorCodes
{
    public static class Common // 1000
    {
        public const int BadRequest = 1001;
        public const int NotFound = 1002;
        public const int Conflict = 1003;
        public const int InternalError = 1004;
        public const int RateLimited = 1005;
        public const int Unprocessable = 1006;
    }

    public static class Auth // 2000+
    {
        public const int Unauthorized = 2001;
        public const int Forbidden = 2002;
    }

    public static class Inventory // 3000+
    {
        public const int EquipmentNotFound = 3001;
        public const int EquipPartConflict = 3002;
        public const int ItemNotFound = 3003;
        public const int EquipmentConfigNotFound = 3004;
    }

    public static class Role // 4000+
    {
        public const int InvalidDeviceType = 4001;
        public const int InvalidSportDistribution = 4002;
    }

    public static class Map // 5000+
    {
        public const int LocationNotFound = 5001;
    }
}

