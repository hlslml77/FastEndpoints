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

1. 统一错误格式（示例）

当参数或认证有误时，返回类似：
{
  "errors": ["未能从令牌解析用户ID"],
  "statusCode": 400
}

5. 调用要点
- 交换 Token 时 appToken 为可选；若提交，服务端可验证 appToken 并校验其中用户与 userId 一致
- 所有业务接口需携带 Authorization: Bearer <webToken>
- userId 来自换取到的 Token 的 sub
- 注意单位：complete-sport 使用 公里；save-progress 使用 米
- 时间均为 UTC ISO8601 字符串


