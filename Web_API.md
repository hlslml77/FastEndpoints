Web API 文档（客户端版）

本项目为纯 API 服务（无静态站点）。所有业务接口均使用 JWT Bearer 认证，并要求权限 web_access（除换取 Token 的接口外）。

- 基础路径：/api
- Content-Type：application/json; charset=utf-8
- 认证头：Authorization: Bearer <webToken>

1. 获取访问令牌（无需鉴权）
POST /api/auth/exchange

用客户端提供的 userId  换取 Web 专用 Token。

请求体
{
  "userId": "123456",
  "appToken": "可选"
}

响应体
{
  "webToken": "<JWT字符串>",
  "expiresIn": 43200,
  "tokenType": "Bearer",
  "userId": "123456"
}

示例（curl）
curl -X POST https://host/api/auth/exchange \
  -H "Content-Type: application/json" \
  -d '{"userId":"123456"}'

拿到 webToken 后，访问其它接口时放入 Authorization 头。

---

2. 角色系统（Role）

2.1 获取玩家角色信息
POST /api/role/get-player

- 认证：需要 Bearer Token（权限 web_access）
- 请求体：空对象 {} 或不传（从 JWT 的 sub 解析用户ID）

响应示例
{
  "userId": 123456,
  "currentLevel": 5,
  "currentExperience": 1200,
  "experienceToNextLevel": 1500,
  "upperLimb": 12,
  "lowerLimb": 11,
  "core": 10,
  "heartLungs": 9,
  "todayAttributePoints": 4,
  "availableAttributePoints": 6,
  "speedBonus": 0.23,                  // 等同于 secSpeed
  "secAttack": 1.75,
  "secHP": 0.50,
  "secDefense": 0.40,
  "secAttackSpeed": 0.30,
  "secCritical": 0.20,
  "secCriticalDamage": 0.35,
  "secSpeed": 0.23,
  "secEfficiency": 0.10,
  "secEnergy": 0.15,
  "lastUpdateTime": "2025-01-01T00:00:00Z"
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
  "deviceType": 1,   // 0=跑步机, 1=单车, 2=划船机, 3=手环
  "distance": 2.5,   // 公里
  "calorie": 180
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
  "startLocationId": 10011,
  "endLocationId": 10012,
  "distanceMeters": 850.5
}

响应体
{
  "id": 1,
  "userId": 123456,
  "startLocationId": 10011,
  "endLocationId": 10012,
  "distanceMeters": 850.5,
  "createdAt": "2025-01-01T00:00:00Z"
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
  "locationId": 10011,
  "isCompleted": true
}

响应体
{
  "isFirstVisit": true,
  "rewards": [{ "itemId": 8000, "amount": 20 }],
  "visitCount": 1,
  "firstVisitTime": "2025-01-01T00:00:00Z",
  "lastVisitTime": "2025-01-01T01:00:00Z",
  "locationInfo": {
    "locationId": 10011,
    "description": "黄岩镇",
    "scenicSpot": "这点有xxxxx描述",
    "hierarchy": 1
  }
}

3.3 获取玩家地图状态

- 认证：需要 Bearer Token（权限 web_access）

POST /api/map/player-state

请求体：空对象 {} 或不传（从 JWT 的 sub 解析用户ID）

响应体
{
  "visitedLocationIds": [10011, 10012],
  "completedLocationIds": [10011, 10012],
  "progressRecords": [
    {
      "startLocationId": 10011,
      "endLocationId": 10012,
      "distanceMeters": 10.0,
      "createdAt": "2025-01-01T00:00:00Z"
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
  { "itemId": 8000, "amount": 20, "updatedAt": "2025-01-01T00:00:00Z" },
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
    "id": 101,
    "equipId": 20001,
    "quality": 3,
    "part": 1,
    "attack": 15,
    "hp": 120,
    "defense": null,
    "critical": 2,
    "attackSpeed": 1,
    "criticalDamage": 5,
    "upperLimb": 1,
    "lowerLimb": 0,
    "core": 0,
    "heartLungs": 0,
    "isEquipped": true,
    "createdAt": "2025-01-01T00:00:00Z",
    "updatedAt": "2025-01-01T01:00:00Z"
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
{ "equipmentRecordId": 101 }

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
{ "equipmentRecordId": 101 }

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
  "eventId": 1001
}

响应体
{
  "success": true,
  "itemId": 1001,
  "amount": 6
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
  "levelId": 101,
  "distance": 600
}

响应体
{
  "success": true,
  "rewards": [
    { "itemId": 1001, "amount": 2 },
    { "itemId": 1002, "amount": 50 }
  ]
}

示例（curl）
curl -X POST https://host/api/travel/drop-point/reward \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" \
  -d '{"levelId":101, "distance":600}'

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
- 交换 Token 时 appToken 为可选；若提交，服务端可验证 appToken 并校验其中用户与 userId 一致
- 所有业务接口需携带 Authorization: Bearer <webToken>
