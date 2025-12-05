Web API 文档（客户端版）
目录

- [基础说明与认证](#toc-basics)
- [1. 获取访问令牌（/api/auth/exchange）](#toc-auth-exchange)
- [2. 角色系统（Role）](#toc-role)
  - [2.1 获取玩家角色信息（/api/role/get-player）](#toc-role-get-player)
  - [2.2 完成运动（/api/role/complete-sport）](#toc-role-complete-sport)
- [3. 地图系统（MapSystem）](#toc-map)
  - [3.1 保存地图进度（/api/map/save-progress）](#toc-map-save-progress)
  - [3.2 访问地图点位（/api/map/visit-location）](#toc-map-visit-location)
  - [3.3 获取玩家地图状态（/api/map/player-state）](#toc-map-player-state)
  - [3.4 使用存储能量解锁终点（/api/map/unlock-with-energy）](#toc-map-unlock-with-energy)
  - [3.5 查询点位信息（/api/map/location-info）](#toc-map-location-info)
- [4. 背包与装备（Inventory）](#toc-inventory)
  - [4.1 查询玩家道具清单（GET/POST /api/inventory/items）](#toc-inventory-items)
  - [4.2 查询玩家装备清单（GET/POST /api/inventory/equipments）](#toc-inventory-equipments)
  - [4.3 穿戴指定装备（/api/inventory/equip）](#toc-inventory-equip)
  - [4.4 卸下指定装备（/api/inventory/unequip）](#toc-inventory-unequip)
- [5. 旅行系统（Travel）](#toc-travel)
  - [5.1 事件奖励（/api/travel/event/reward）](#toc-travel-event-reward)
  - [5.2 距离掉落奖励（/api/travel/drop-point/reward）](#toc-travel-drop-point-reward)
  - [5.3 保存消息ID列表（/api/travel/stage/save-message）](#toc-travel-stage-save-message)
  - [5.4 获取随机消息ID列表（/api/travel/stage/get-random-message）](#toc-travel-stage-get-random-message)

- [6. 藏品系统（Collection）](#toc-collection)
  - [6.1 获取玩家已拥有的藏品ID列表（/api/collection/my）](#toc-collection-my)
  - [6.2 随机获取藏品（/api/collection/obtain）](#toc-collection-obtain)
  - [6.3 领取组合奖励（/api/collection/claim-combo）](#toc-collection-claim-combo)
- [7. 配置热更新（Admin）](#toc-admin)
  - [查看配置状态（/api/admin/config/status）](#toc-admin-status)
  - [手动重载配置（/api/admin/config/reload）](#toc-admin-reload)
- [8. 统计数据（Statistics）](#toc-stats)
  - [8.1 每日统计（/api/admin/statistics/daily）](#toc-stats-daily)
  - [8.2 小时在线快照（/api/admin/statistics/online-snapshots）](#toc-stats-snapshots)
  - [8.3 玩家活动统计（/api/admin/statistics/player-activity）](#toc-stats-activity)
  
- [调用要点](#toc-notes)
- [统一错误返回与错误码](#toc-errors)



<a id="toc-basics"></a>
本项目为纯 API 服务（无静态站点）。所有业务接口均使用 JWT Bearer 认证，并要求权限 web_access（除换取 Token 的接口外）。

- 基础路径：/api
- Content-Type：application/json; charset=utf-8
- 认证头：Authorization: Bearer <webToken>

<a id="toc-auth-exchange"></a>
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

<a id="toc-role"></a>
1. 角色系统（Role）

<a id="toc-role-get-player"></a>
2.1 获取玩家角色信息
POST /api/role/get-player

- 认证：需要 Bearer Token（权限 web_access）
-
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

<a id="toc-role-complete-sport"></a>
2.2 完成运动
POST /api/role/complete-sport

- 认证：需要 Bearer Token（权限 web_access）
- 说明：按设备类型与距离，根据策划表对四个主属性进行加点（UpperLimb/LowerLimb/Core/HeartLungs）
- 设备类型：0=跑步, 1=划船, 2=单车, 3=手环
- 注意：distance 单位为“米”

请求体
{
  "deviceType": 0,   // int (0=跑步, 1=划船, 2=单车, 3=手环)
  "distance": 2500,  // double, 单位: 米
  "calorie": 180     // int
}

响应体：同“获取玩家角色信息”

---

<a id="toc-map"></a>
3. 地图系统（MapSystem）

<a id="toc-map-save-progress"></a>
3.1 保存地图进度
POST /api/map/save-progress

- 认证：需要 Bearer Token（权限 web_access）
- 注意：distanceMeters 单位为“米”
- 仅保存进度，不会自动标记点位为完成
- 若需标记完成，请在“访问地图点位”接口上报 isCompleted=true
- 当 distanceMeters 超过终点位置配置的 UnlockDistance 时，自动解锁该位置，返回 unlockedLocationIds 列表包含该“终点”；若终点配置了 SurroundingPoints，则这些点位ID也会一并返回，并同时写入玩家已解锁列表。超出部分（distanceMeters - UnlockDistance）的增量会累加到“存储能量”
- 解锁的位置会被添加到 /api/map/player-state 接口的 unlockedLocationIds 列表中
- 人数统计：当本次 distanceMeters 达到/超过起点->终点的配置距离时，才会将玩家"当前所在点位"更新为 endLocationId，同时该点位的人数计数会自动增加 1

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
  "createdAt": "2025-01-01T00:00:00Z", // DateTime (ISO 8601)
  "unlockedLocationIds": [10012, 10010501], // int[], 本次解锁提示列表：包含终点以及其 SurroundingPoints（若配置），未解锁则为空数组
  "storedEnergyMeters": 120.0     // double, 当前玩家“存储能量”（米），最大 10000
}

<a id="toc-map-visit-location"></a>
3.2 访问地图点位
POST /api/map/visit-location

- 认证：需要 Bearer Token（权限 web_access）
- 说明：客户端上报是否完成该点位（isCompleted），以及是否需要消耗（needConsume）。每次调用此接口时，该点位的人数计数会自动增加 1。
  - 消耗道具规则：当 needConsume=true 且该点在配置中存在 Consumption=[itemId, amount] 时，将消耗对应道具；needConsume=false 时不消耗。若发生消耗，将在响应中返回：
    - consumedItems: [{ itemId, amount, remaining } ...] 与 rewards 同结构（remaining 为扣减后的剩余数量）
  - 奖励规则：
    - 首次访问发放“首次奖励”（FirstReward）
    - 完成发放“完成奖励”（使用 FixedReward 字段）
    - 若同时满足，两个奖励会合并返回
  - 人数统计：每次访问该点位时，该点位的人数计数会自动增加 1

请求体
{
  "locationId": 10011,  // int
  "isCompleted": true,  // bool
  "needConsume": false  // bool, 是否需要消耗（由客户端控制）
}

响应体
{
  "isFirstVisit": true,                                        // bool
  "didConsumeItem": false,                                     // bool, 当 needConsume=true 且配置 Consumption 时为 true
  "consumedItems": [{ "itemId": 1002, "amount": 3, "remaining": 7 }], // List<Reward>（消耗，包含剩余数量）
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

<a id="toc-map-player-state"></a>
3.3 获取玩家地图状态

- 认证：需要 Bearer Token（权限 web_access）

POST /api/map/player-state

请求体：空对象 {} 或不传（从 JWT 的 sub 解析用户ID）

响应体
{
  "unlockedLocationIds": [10011, 10012],  // int[], 已解锁点位ID列表
  "completedLocationIds": [10011, 10012], // int[]
  "progressRecords": [                    // List<ProgressRecord>
    {
      "startLocationId": 10011,          // int
      "endLocationId": 10012,            // int
      "distanceMeters": 10.0,            // double, 单位: 米
      "createdAt": "2025-01-01T00:00:00Z" // DateTime (ISO 8601)
    }
  ],
  "storedEnergyMeters": 240.0,            // double, 玩家当前“存储能量”（米），最大 10000
  "dailyRandomEvents": [                   // List<DailyRandomEventDto>
    {
      "locationId": 10011,                // int, 事件所在点位（PositioningPoint）
      "eventId": 1001,                    // int, 事件配置ID（WorldUiMap_RandomEvent.json 的 ID）
      "eventType": 1,                     // int, 事件类型（来自配置）
      "dialogue": "事件对话展示",         // string?, 事件对话（来自配置）
      "isCompleted": false                // bool, 今日是否已完成
    }
  ],
  "currentLocationId": 10011              // int?, 玩家当前所在点位ID（根据进度/访问设置）
}

<a id="toc-map-unlock-with-energy"></a>
3.4 使用存储能量解锁终点
POST /api/map/unlock-with-energy

- 认证：需要 Bearer Token（权限 web_access）
- 说明：当某条路线的当前 distanceMeters 未达到终点的 UnlockDistance 时，可消耗玩家的“存储能量”（上限 10000 米）来直接解锁终点。仅消耗差额部分。

请求体
{
  "startLocationId": 10011, // int
  "endLocationId": 10012    // int
}

响应体
{
  "isUnlocked": true,          // bool, 本次是否成功解锁
  "usedEnergyMeters": 240.0,   // double, 本次消耗的能量（米），不足则为 0
  "storedEnergyMeters": 760.0  // double, 玩家当前剩余“存储能量”（米）
}

  ],
  "storedEnergyMeters": 240.0             // double, 玩家当前“存储能量”（米），最大 10000
}

<a id="toc-map-location-info"></a>
3.5 查询点位信息
POST /api/map/location-info

- 认证：需要 Bearer Token（权限 web_access）
- 说明：返回指定点位的当前人数统计和玩家的下次挑战时间。当玩家调用 /api/map/visit-location 或 /api/map/save-progress 接口时，该点位的人数会自动增加。若统计人数为0，或统计为1且只有当前玩家自己，则按 Config.json 中的玩家选择大地图点位时机器人数量显示的 Value4 区间生成随机展示人数。
- 倒计时说明：当玩家完成某个点位且该点位配置了资源ID（Resources > 0）时，系统会根据 WorldUiMap_MapBase.json 中该点位的 RefreshTime 字段计算下次可挑战时间。只有倒计时结束后才能再次挑战该点位。

请求体
{
  "locationId": 10011  // int, 地图点位ID
}

响应体
{
  "peopleCount": 8,                                    // int, 该点位的当前人数（包括真实玩家和机器人显示数）
  "nextChallengeTime": "2025-01-01T12:00:00Z"         // DateTime (ISO 8601), 玩家的下次挑战时间（若无倒计时则为 0001-01-01T00:00:00Z（DateTime.MinValue））
}

示例（curl）
curl -X POST https://host/api/map/location-info \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" \
  -d '{"locationId":10011}'

---

<a id="toc-inventory"></a>
4. 背包与装备（Inventory）

<a id="toc-inventory-items"></a>
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

<a id="toc-inventory-equipments"></a>
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

<a id="toc-inventory-equip"></a>
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

<a id="toc-inventory-unequip"></a>
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

<a id="toc-travel"></a>
5. 旅行系统（Travel）

<a id="toc-travel-event-reward"></a>
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

<a id="toc-travel-drop-point-reward"></a>
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

<a id="toc-travel-stage-save-message"></a>
5.3 保存消息ID列表
POST /api/travel/stage/save-message

- 认证：需要 Bearer Token（权限 web_access）
- 说明：客户端提交三个整型ID的列表（IdList，长度必须为 3），服务端会将其与关卡ID、用户ID一起保存。
- 限制：IdList 必须包含 3 个大于 0 的整数。

请求体
{
  "levelId": 101,          // int 关卡LevelId
  "idList": [101, 202, 303] // int[3] 三个整型ID
}

响应体
{
  "success": true,  // bool 是否保存成功
  "messageId": 123  // long 留言ID（自增主键）
}

错误响应示例
- 400 Bad Request（参数无效）
  { "statusCode": 400, "code": 1001, "message": "关卡ID无效" }
  { "statusCode": 400, "code": 1001, "message": "IdList必须包含3个大于0的整数" }
- 500 Internal Error（服务器错误）
  { "statusCode": 500, "code": 1004, "message": "服务器内部错误" }

示例（curl）
curl -X POST https://host/api/travel/stage/save-message \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" \
  -d '{"levelId":101, "idList":[101,202,303]}'


<a id="toc-travel-stage-get-random-message"></a>
5.4 获取随机消息ID列表
POST /api/travel/stage/get-random-message

- 认证：需要 Bearer Token（权限 web_access）
- 说明：返回该关卡保存的随机一条 ID 列表及其元信息。
- 字段：success、userId、messageIDList（三个整型ID）、createdAt（ISO 8601）。
- 当该关卡暂无数据时，返回 success=true，userId=0，messageIDList=[]，createdAt=0001-01-01T00:00:00Z。

请求体
{
  "levelId": 101 // int 关卡LevelId
}

响应体
{
  "success": true,              // bool
  "userId": 987654,             // long（留言用户ID；无数据时为 0）
  "messageIDList": [101,202,303], // List<int> 三个整型ID；无数据时为空 []
  "createdAt": "2025-12-04T09:54:04Z" // DateTime (ISO 8601)；无数据时为 0001-01-01T00:00:00Z
}


示例（curl）
curl -X POST https://host/api/travel/stage/get-random-message \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" \
  -d '{"levelId":101}'




---

<a id="toc-collection"></a>
6. 藏品系统（Collection）

<a id="toc-collection-my"></a>
6.1 获取玩家已拥有的藏品ID列表
POST /api/collection/my

- 认证：需要 Bearer Token（权限 web_access）
- 请求体：空对象 {} 或不传

响应体
{
  "collectionIds": [ 1, 21, 31 ] // int[]
}

示例（curl）
curl -X POST https://host/api/collection/my \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" -d '{}'

<a id="toc-collection-obtain"></a>
6.2 随机获取藏品
POST /api/collection/obtain

- 认证：需要 Bearer Token（权限 web_access）
- 说明：根据权重和全局限量随机获取一个藏品。如果藏品配置了装备，会自动发放到玩家背包。

响应体
{
  "success": true,        // bool
  "message": "ok",        // string
  "collectionId": 5       // int? 成功时返回获取到的藏品ID
}
- 失败时 `success` 为 `false`，`message` 会包含原因（如“藏品数量不足”）。

示例（curl）
curl -X POST https://host/api/collection/obtain \
  -H "Authorization: Bearer <webToken>"

<a id="toc-collection-claim-combo"></a>
6.3 领取组合奖励
POST /api/collection/claim-combo

- 认证：需要 Bearer Token（权限 web_access）
- 说明：当玩家集齐指定组合的藏品后，可领取一次性奖励。

请求体
{ "comboId": 1 } // comboId: int, 对应 CollectionList_Combination.json 中的 ID

响应体
{
  "success": true,    // bool
  "message": "ok"     // string
}
- 失败时 `success` 为 `false`，`message` 会包含原因（如“已领取”、“未满足领取条件”）。

示例（curl）
curl -X POST https://host/api/collection/claim-combo \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" \
  -d '{"comboId":1}'

---

<a id="toc-admin"></a>
7. 配置热更新（Admin）

<a id="toc-admin-status"></a>
7.1 查看配置状态（不需要 admin 角色）
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

<a id="toc-admin-reload"></a>
7.2 手动重载配置（按文件名；不传则全量）
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

<a id="toc-stats"></a>
8. 统计数据（Statistics）

说明：以下接口用于读取 AddGameStatisticsTables.sql 创建的三张统计表的数据，均为只读查询接口。
- 表：daily_game_statistics（每日统计）
- 表：online_players_snapshot（在线人数小时快照）
- 表：player_activity_statistics（玩家活动统计）

<a id="toc-stats-daily"></a>
8.1 每日统计（daily_game_statistics）
GET /api/admin/statistics/daily

- 认证：可匿名（AllowAnonymous）；若需加权限可后续开启
- 说明：
  - 不传 date 时，返回最近 days 天的统计（默认 7 天）
  - 传 date（yyyy-MM-dd）时，返回该日的统计

请求参数（query）
- date: string?（yyyy-MM-dd）
- days: int?（默认 7）

响应（当按区间查询时为数组；按单日查询为单对象）
{
  "date": "2025-12-04",       // string yyyy-MM-dd
  "newRegistrations": 0,       // int 当日新注册
  "activePlayers": 0,          // int 当日活跃
  "maxOnlinePlayers": 0,       // int 当日在线峰值
  "avgOnlinePlayers": 0.0,     // decimal 当日平均在线
  "totalPlayers": 0            // int 截止该日累计玩家数
}

示例
GET /api/admin/statistics/daily            （最近7天）
GET /api/admin/statistics/daily?days=30    （最近30天）
GET /api/admin/statistics/daily?date=2025-12-04

<a id="toc-stats-snapshots"></a>
8.2 小时在线快照（online_players_snapshot）
GET /api/admin/statistics/online-snapshots

- 认证：可匿名（AllowAnonymous）
- 说明：返回指定日期（默认今天）的每小时在线人数快照

请求参数（query）
- date: string?（yyyy-MM-dd；默认今天）

响应（数组）
[
  {
    "hour": 9,                          // int 小时 0-23
    "onlineCount": 123,                 // int 在线人数
    "recordedAt": "2025-12-05 09:05:00" // string 记录时间（本地时区格式化）
  }
]

示例
GET /api/admin/statistics/online-snapshots
GET /api/admin/statistics/online-snapshots?date=2025-12-04

<a id="toc-stats-activity"></a>
8.3 玩家活动统计（player_activity_statistics）
GET /api/admin/statistics/player-activity

- 认证：可匿名（AllowAnonymous）
- 说明：返回指定日期（默认今天）的活动统计汇总

请求参数（query）
- date: string?（yyyy-MM-dd；默认今天）

响应
{
  "date": "2025-12-05",               // string yyyy-MM-dd
  "totalLocationsCompleted": 0,       // int 当日完成的地图点位总数
  "totalEventsCompleted": 0,          // int 当日完成的事件总数
  "totalDistanceMeters": 0.0,         // decimal 当日总跑步距离（米）
  "avgPlayerLevel": 0.0               // decimal 平均玩家等级
}

示例
GET /api/admin/statistics/player-activity
GET /api/admin/statistics/player-activity?date=2025-12-04

  -H "Authorization: Bearer <adminToken>" -H "Content-Type: application/json" \
  -d '{"files":["Item.json","Role_Upgrade.json"]}'

-------------------------------------------------------------------------------------------------------


<a id="toc-errors"></a>
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

<a id="toc-notes"></a>
调用要点
- 所有业务接口需携带 Authorization: Bearer <webToken>

备注
- 管理员 Token 获取方式：在“获取访问令牌”接口中，受控环境可额外携带头 X-Admin: 1 以获得 admin 角色（仅限内网/运维场景）。
- 若返回包含 status=ignored 的文件，表示该文件名未匹配到任何已注册的配置服务（请核对文件名）。