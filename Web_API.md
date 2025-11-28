Web API 文档（客户端版）

本项目为纯 API 服务（无静态站点）。所有业务接口均使用 JWT Bearer 认证，并要求权限 web_access（除换取 Token 的接口外）。

- 基础路径：/api
- Content-Type：application/json; charset=utf-8
- 认证头：Authorization: Bearer <webToken>

1. 获取访问令牌（无需鉴权）
POST /api/auth/exchange

受控后台签发（生产可用）。必须携带管理密钥与时间/nonce 防重放，仅用于受控环境（运维/GM/CI），不对公网开放。

请求头
- X-Exchange-Key: string 管理密钥（必须，与配置 AdminKey 一致）
- X-Timestamp: long 毫秒时间戳（允许 ±MaxSkewSeconds，默认 300 秒）
- X-Nonce: string 随机串，长度≥12（5 分钟内不可复用）


请求体
{
  "userId": "123456", // string
  "appToken": "可选" // string, optional
}

响应体
{
  "webToken": "<JWT字符串>", // string (JWT)
  "expiresIn": 21600,       // int (seconds)
  "tokenType": "Bearer",    // string
  "userId": "123456"        // string
}
说明
- 最小权限：web_access
- Token 有效期：6 小时（21600 秒）

示例（curl）
# 生成时间戳与随机 nonce（示例为 bash）
ts=$(python - <<<'import time;print(int(time.time()*1000))')
nonce=$(python - <<<'import secrets;print(secrets.token_hex(16))')

错误响应示例
- 403 Forbidden（管理密钥错误或未启用）
  {
    "statusCode": 403,
    "code": 2002,
    "message": "forbidden"
  }
- 400 Bad Request（时间戳非法/超出窗口/nonce 无效/缺少 userId）
  {
    "statusCode": 400,
    "code": 1001,
    "message": "invalid timestamp"
  }
- 409 Conflict（重复的 nonce，重放拦截）
  {
    "statusCode": 409,
    "code": 1003,
    "message": "replay detected"
  }

curl -X POST https://host/api/auth/exchange \
  -H "Content-Type: application/json" \
  -H "X-Exchange-Key: <YOUR_ADMIN_KEY>" \
  -H "X-Timestamp: $ts" \
  -H "X-Nonce: $nonce" \
  -d '{"userId":"123456","appToken":"<optional>"}'

拿到 webToken 后，访问其它接口时放入 Authorization 头。

---

1. 角色系统（Role）

2.1 获取玩家角色信息
POST /api/role/get-player

- 认证：需要 Bearer Token（权限 web_access）
- 请求体：空对象 {} 或不传（从 JWT 的 sub 解析用户ID）

响应示例
{
  "userId": 123456,                 // long
  "currentLevel": 5,               // int
  "currentExperience": 1200,       // int
  "experienceToNextLevel": 1500,   // int
  "upperLimb": 12,                 // int
  "lowerLimb": 11,                 // int
  "core": 10,                      // int
  "heartLungs": 9,                 // int
  "todayAttributePoints": 4,       // int
  "availableAttributePoints": 6,   // int
  "speedBonus": 0.23,              // double 等同于 secSpeed
  "secAttack": 1.75,               // double
  "secHP": 0.50,                   // double
  "secDefense": 0.40,              // double
  "secAttackSpeed": 0.30,          // double
  "secCritical": 0.20,             // double
  "secCriticalDamage": 0.35,       // double
  "secSpeed": 0.23,                // double
  "secEfficiency": 0.10,           // double
  "secEnergy": 0.15,               // double
  "lastUpdateTime": "2025-01-01T00:00:00Z" // DateTime (ISO 8601)
}

示例（curl）
curl -X POST https://host/api/role/get-player \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" -d '{}'

2.2 完成运动
POST /api/role/complete-sport

- 认证：需要 Bearer Token（权限 web_access）
- 说明：按设备类型与距离，根据策划表对四个主属性进行加点（UpperLimb/LowerLimb/Core/HeartLungs）
- 设备类型：0=跑步机，1=单车，2=划船机，3=手环
- 注意：distance 单位为“公里”

请求体
{
  "deviceType": 1,   // int (0=跑步机, 1=单车, 2=划船机, 3=手环)
  "distance": 2.5,   // double, 单位: 公里
  "calorie": 180     // int
}

响应体：同“获取玩家角色信息”

---

3. 地图系统（MapSystem）

3.1 保存地图进度
POST /api/map/save-progress

- 认证：需要 Bearer Token（权限 web_access）
- 注意：distanceMeters 单位为“米”
- 仅保存进度，不会自动标记点位为完成
- 若需标记完成，请在“访问地图点位”接口上报 isCompleted=true

请求体
{
  "startLocationId": 10011, // int
  "endLocationId": 10012,   // int
  "distanceMeters": 850.5   // double, 单位: 米
}

响应体
{
  "id": 1,                        // long
  "userId": 123456,               // long
  "startLocationId": 10011,       // int
  "endLocationId": 10012,         // int
  "distanceMeters": 850.5,        // double, 单位: 米
  "createdAt": "2025-01-01T00:00:00Z" // DateTime (ISO 8601)
}

3.2 访问地图点位
POST /api/map/visit-location

- 认证：需要 Bearer Token（权限 web_access）
- 说明：客户端上报是否完成该点位（isCompleted）。奖励规则：
  - 首次访问发放“首次奖励”（FirstReward）
  - 完成发放“完成奖励”（使用 FixedReward 字段）
  - 若同时满足，两个奖励会合并返回

请求体
{
  "locationId": 10011, // int
  "isCompleted": true  // bool
}

响应体
{
  "isFirstVisit": true,                                        // bool
  "rewards": [{ "itemId": 8000, "amount": 20 }],            // List<Reward>
  "visitCount": 1,                                             // int
  "firstVisitTime": "2025-01-01T00:00:00Z",                   // DateTime (ISO 8601)
  "lastVisitTime": "2025-01-01T01:00:00Z",                    // DateTime (ISO 8601)
  "locationInfo": {
    "locationId": 10011,                                      // int
    "description": "黄岩镇",                                  // string
    "scenicSpot": "这点有xxxxx描述",                          // string
    "hierarchy": 1                                            // int
  }
}

3.3 获取玩家地图状态

- 认证：需要 Bearer Token（权限 web_access）

POST /api/map/player-state

请求体：空对象 {} 或不传（从 JWT 的 sub 解析用户ID）

响应体
{
  "visitedLocationIds": [10011, 10012],   // int[]
  "completedLocationIds": [10011, 10012], // int[]
  "progressRecords": [                    // List<ProgressRecord>
    {
      "startLocationId": 10011,          // int
      "endLocationId": 10012,            // int
      "distanceMeters": 10.0,            // double, 单位: 米
      "createdAt": "2025-01-01T00:00:00Z" // DateTime (ISO 8601)
    }
  ]
}

---

4. 背包与装备（Inventory）

4.1 查询玩家道具清单
GET /api/inventory/items
POST /api/inventory/items

- 认证：需要 Bearer Token（权限 web_access）
- 请求体（POST）：可为空 {}

响应示例
[
  { "itemId": 8000, "amount": 20, "updatedAt": "2025-01-01T00:00:00Z" }, // itemId:int, amount:int, updatedAt:DateTime(ISO 8601)
  { "itemId": 8001, "amount": 3,  "updatedAt": "2025-01-01T00:10:00Z" }
]

示例（curl）
curl -X GET https://host/api/inventory/items \
  -H "Authorization: Bearer <webToken>"

curl -X POST https://host/api/inventory/items \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" -d '{}'

4.2 查询玩家装备清单
GET /api/inventory/equipments
POST /api/inventory/equipments

- 认证：需要 Bearer Token（权限 web_access）
- 请求体（POST）：可为空 {}

响应示例（按更新时间倒序）
[
  {
    "id": 101,                         // long
    "equipId": 20001,                  // int
    "quality": 3,                      // int
    "part": 1,                         // int
    "attack": 15,                      // int?
    "hp": 120,                         // int?
    "defense": null,                   // int?
    "critical": 2,                     // int?
    "attackSpeed": 1,                  // int?
    "criticalDamage": 5,               // int?
    "upperLimb": 1,                    // int?
    "lowerLimb": 0,                    // int?
    "core": 0,                         // int?
    "heartLungs": 0,                   // int?
    "isEquipped": true,                // bool
    "createdAt": "2025-01-01T00:00:00Z", // DateTime (ISO 8601)
    "updatedAt": "2025-01-01T01:00:00Z"  // DateTime (ISO 8601)
  }
]

示例（curl）
curl -X GET https://host/api/inventory/equipments \
  -H "Authorization: Bearer <webToken>"

curl -X POST https://host/api/inventory/equipments \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" -d '{}'

4.3 穿戴指定装备
POST /api/inventory/equip

- 认证：需要 Bearer Token（权限 web_access）
- 说明：同一部位会自动卸下原装备

请求体
{ "equipmentRecordId": 101 } // equipmentRecordId:int

响应体
{ "success": true }

示例（curl）
curl -X POST https://host/api/inventory/equip \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" \
  -d '{"equipmentRecordId":101}'

4.4 卸下指定装备
POST /api/inventory/unequip

- 认证：需要 Bearer Token（权限 web_access）

请求体
{ "equipmentRecordId": 101 } // equipmentRecordId:int

响应体
{ "success": true }

示例（curl）
curl -X POST https://host/api/inventory/unequip \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" \
  -d '{"equipmentRecordId":101}'

---

5. 旅行系统（Travel）

5.1 事件奖励（基于 Travel_EventList.json）
POST /api/travel/event/reward

- 认证：需要 Bearer Token（权限 web_access）
- 策划表：Web/Json/Travel_EventList.json
- 说明：客户端传入事件ID（配置表的 ID 字段），服务端按以下规则发放奖励：
  - 随机从 ResourceRandom 中抽取一个物品ID
  - 按 DropRandom：
    - 若为单个数 n，则数量 amount = max(1, n)
    - 若为两个数 [min, max]，则在区间内随机（含上限）

请求体
{
  "eventId": 1001 // int
}

响应体
{
  "success": true, // bool
  "itemId": 1001,  // int
  "amount": 6      // int
}

示例（curl）
curl -X POST https://host/api/travel/event/reward \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" \
  -d '{"eventId":1001}'

5.2 距离掉落奖励（基于 Travel_DropPoint.json）
POST /api/travel/drop-point/reward

- 认证：需要 Bearer Token（权限 web_access）
- 策划表：Web/Json/Travel_DropPoint.json
- 说明：客户端传入关卡ID与距离，服务端根据配置发放随机奖励与/或固定奖励：
  - 配置匹配：
    - 优先选择同 LevelID 下 Distance 列表包含请求 Distance 的配置
    - 若找不到，则回退到 Distance 为 null 的配置（取第一条）
  - 随机奖励：当 DropRandom 与 QuantitiesRandom 都配置且非空时触发
    - 从 DropRandom 中随机一个物品ID
    - 数量按 QuantitiesRandom：
      - 单个数 n => amount = max(1, n)
      - 两个数 [min, max] => 区间随机（含上限）
  - 固定奖励：遍历 FixReward（若配置）中每一对 [itemId, amount] 逐条发放
  - 两种奖励同时配置则都发；缺失任一则只发存在的那一种

请求体
{
  "levelId": 101, // int
  "distance": 600 // int (meters)
}

响应体
{
  "success": true, // bool
  "rewards": [     // List<Reward>
    { "itemId": 1001, "amount": 2 },  // itemId:int, amount:int
    { "itemId": 1002, "amount": 50 }
  ]
}

示例（curl）
curl -X POST https://host/api/travel/drop-point/reward \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" \
  -d '{"levelId":101, "distance":600}'


---

6. 配置热更新（Admin）

6.1 查看配置状态（不需要 admin 角色）
GET /api/admin/config/status

- 认证：需要 Bearer Token（权限 web_access）
- 说明：返回所有可热重载配置的状态（名称、上次重载时间、数量、目录等）

响应示例
[
  { "name": "item", "lastReloadTime": "2025-11-28T02:48:44.8626246Z", "items": 54, "equipments": 48, "dir": "E:/FastEndpoints/Web/bin/Debug/net9.0/Json" },
  { "name": "role", "lastReloadTime": "..." },
  { "name": "map",  "lastReloadTime": "..." },
  { "name": "event","lastReloadTime": "..." },
  { "name": "drop", "lastReloadTime": "..." }
]

示例（curl）
curl -X GET https://host/api/admin/config/status \
  -H "Authorization: Bearer <webToken>" -H "Accept: application/json"

6.2 手动重载配置（按文件名；不传则全量）
GET /api/admin/config/reload
POST /api/admin/config/reload

- 认证：需要 Bearer Token（admin 角色）
- 说明：
  - 通过文件名决定要重载的配置服务；支持 query 或 JSON body 两种形式；
  - 不传文件名时，重载全部配置；
  - 文件名大小写不敏感，仅需传文件名本身（无需路径）。
- 支持的映射（文件名 → 服务）：
  - item：item.json、equipment.json
  - role：role_config.json、role_attribute.json、role_upgrade.json、role_sport.json、role_experience.json
  - map：worlduimap_mapbase.json
  - event：travel_eventlist.json
  - drop：travel_droppoint.json

请求方式 A（query，多值或逗号分隔）
GET /api/admin/config/reload?file=Item.json&file=Role_Upgrade.json
GET /api/admin/config/reload?file=Item.json,Role_Upgrade.json

请求方式 B（JSON body）
{
  "files": ["Item.json", "Role_Upgrade.json"]
}

响应示例
{
  "requestedFiles": ["item.json"],
  "services": ["item"],
  "ok": 1,
  "fail": 0,
  "results": [
    { "name": "item", "status": "ok", "lastReloadTime": "2025-11-28T02:48:44.8626246Z" },
    { "name": null,   "status": "ignored", "file": "unknown.json" }
  ]
}

示例（curl）
# GET + query（推荐，无需 Content-Type）
curl -X GET "https://host/api/admin/config/reload?file=Item.json" \
  -H "Authorization: Bearer <adminToken>" -H "Accept: application/json"

# POST + JSON（明确 Content-Type）
curl -X POST https://host/api/admin/config/reload \
  -H "Authorization: Bearer <adminToken>" -H "Content-Type: application/json" \
  -d '{"files":["Item.json","Role_Upgrade.json"]}'

备注
- 管理员 Token 获取方式：在“获取访问令牌”接口中，受控环境可额外携带头 X-Admin: 1 以获得 admin 角色（仅限内网/运维场景）。
- 若返回包含 status=ignored 的文件，表示该文件名未匹配到任何已注册的配置服务（请核对文件名）。

-------------------------------------------------------------------------------------------------------
统一错误返回与错误码

- 统一错误返回结构：
  {
    "statusCode": 400,
    "code": 1001,
    "message": "未能从令牌解析用户ID"
  }

- 字段说明：
  - statusCode：HTTP 状态码
  - code：业务错误码（int），客户端可据此做分支
  - message：错误信息（中文），用于直接显示或调试

- 错误码：
  - 通用（Common）
    - 1001 BadRequest（参数错误/格式不正确等）
    - 1002 NotFound（资源不存在）
    - 1003 Conflict（资源冲突）
    - 1004 InternalError（服务器内部错误）
    - 1005 RateLimited（限流）
    - 1006 Unprocessable（语义错误，无法处理）
  - 认证（Auth）
    - 2001 Unauthorized（未认证/令牌无效）
    - 2002 Forbidden（无权限）
  - 背包与装备（Inventory）
    - 3001 EquipmentNotFound（装备不存在）
    - 3002 EquipPartConflict（同部位冲突）
    - 3003 ItemNotFound（道具配置不存在）
    - 3004 EquipmentConfigNotFound（装备词条配置不存在）
  - 角色（Role）
    - 4001 InvalidDeviceType（设备类型非法）
    - 4002 InvalidSportDistribution（无匹配的运动分配）
  - 地图（Map）
    - 5001 LocationNotFound（地点不存在）

调用要点
- 所有业务接口需携带 Authorization: Bearer <webToken>
