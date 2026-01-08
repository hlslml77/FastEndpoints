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
  - [3.6 怪物奖励（/api/map/monster/reward）](#toc-map-monster-reward)
  - [3.7 主动灌输能量（/api/map/feed-energy）](#toc-map-feed-energy)
  - [3.8 查询设备可灌输距离（/api/map/device-distance）](#toc-map-device-distance)
- [4. 背包与装备（Inventory）](#toc-inventory)
  - [4.1 查询玩家道具清单（GET/POST /api/inventory/items）](#toc-inventory-items)
  - [4.2 查询玩家装备清单（GET/POST /api/inventory/equipments）](#toc-inventory-equipments)
  - [4.3 穿戴指定装备（/api/inventory/equip）](#toc-inventory-equip)
  - [4.4 卸下指定装备（/api/inventory/unequip）](#toc-inventory-unequip)
  - [4.5 查询道具状态（/api/inventory/item-status）](#toc-inventory-item-status)
- [5. 旅行系统（Travel）](#toc-travel)
  - [5.1 事件奖励（/api/travel/event/reward）](#toc-travel-event-reward)
  - [5.2 距离掉落奖励（/api/travel/drop-point/reward）](#toc-travel-drop-point-reward)
  - [5.3 保存消息ID列表（/api/travel/stage/save-message）](#toc-travel-stage-save-message)
  - [5.4 获取随机消息ID列表（/api/travel/stage/get-random-message）](#toc-travel-stage-get-random-message)
  - [5.5 获取玩家某关卡的所有节点留言（/api/travel/stage/my-messages）](#toc-travel-stage-my-messages)
  - [5.6 随机获取玩家信息（/api/travel/random-player）](#toc-travel-random-player)
  - [5.7 修改关注状态（/api/travel/follow/set）](#toc-travel-follow-set)
  - [5.8 获取关注状态（/api/travel/follow/status）](#toc-travel-follow-status)
- [6. 藏品系统（Collection）](#toc-collection)
  - [6.1 获取玩家已拥有的藏品ID列表（/api/collection/my）](#toc-collection-my)
  - [6.2 随机获取藏品（/api/collection/obtain）](#toc-collection-obtain)
  - [6.3 领取组合奖励（/api/collection/claim-combo）](#toc-collection-claim-combo)
- [7. 配置热更新（Admin）](#toc-admin)
  - [查看配置状态（/api/admin/config/status）](#toc-admin-status)
  - [手动重载配置（/api/admin/config/reload）](#toc-admin-reload)
  - [批量更新配置文件（/api/admin/config/update）](#toc-admin-update)
- [8. 统计数据（Statistics）](#toc-stats)
  - [8.1 每日统计（/api/admin/statistics/daily）](#toc-stats-daily)
  - [8.2 小时在线快照（/api/admin/statistics/online-snapshots）](#toc-stats-snapshots)
  - [8.3 玩家活动统计（/api/admin/statistics/player-activity）](#toc-stats-activity)
- [9. 排行榜（Rank）](#toc-rank)
  - [9.1 获取排行榜（/api/rank/leaderboard）](#toc-rank-leaderboard)
  - [9.2 领取周榜奖励（/api/rank/claim-week）](#toc-rank-claim-week)
  - [9.3 领取赛季奖励（/api/rank/claim-season）](#toc-rank-claim-season)
- [10. GM 工具（GM）](#toc-gm)
  - [10.1 发放物品/装备给玩家（/api/gm/grant-item）](#toc-gm-grant-item)
  - [10.2 [DEV] 给自己发放物品/装备（/api/gm/dev/grant-item）](#toc-gm-grant-item-dev)
- [11. 新手引导（Tutorial）](#toc-tutorial)
  - [11.1 设置步骤（/api/tutorial/set-step）](#toc-tutorial-set-step)
  - [11.2 获取步骤（/api/tutorial/get-step）](#toc-tutorial-get-step)
- [调用要点](#toc-notes)
- [统一错误返回与错误码](#toc-errors)
## 属性枚举（客户端常量）

### PlayerAttributeType
| 数值 | 枚举名 | 含义 |
|------|--------|------|
| 1    | UpperLimb   | 上肢力量 |
| 2    | LowerLimb   | 下肢力量 |
| 3    | CoreRange   | 核心控制 |
| 4    | HeartLungs  | 心肺功能 |
| 100 | Attack         | 攻击力 |
| 101 | Hp             | 生命力 |
| 102 | Defense        | 防御力 |
| 103 | Critical       | 暴击率 |
| 104 | AttackSpeed    | 攻击速度 |
| 105 | CriticalDamage | 暴击伤害 |
| 106 | Speed          | 平均速度 |
| 107 | Efficiency     | 产出效率 |
| 108 | Energy         | 能量储存上限 |
| 109 | Experience     | 经验获取效率 |


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
  "todayAttributePoints": 4,       // int
  "availableAttributePoints": 6,   // int
  "lastUpdateTime": "2025-01-01T00:00:00Z", // DateTime (ISO 8601)
  "attributes": [
    { "type": 1   "value": 12 },    // UpperLimb
    { "type": 2,   "value": 11 },    // LowerLimb
    { "type": 3,   "value": 10 },    // CoreRange
    { "type": 4,   "value": 9  },    // HeartLungs
  ]
}

备注：服务端仍可能同时返回旧字段（upperLimb/secAttack...）用于兼容；客户端推荐以 attributes 为准。

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
- 当 distanceMeters 超过“起点配置（start）的 TheNextPointDistance 中对应 end 的要求距离”时，自动解锁该 end 点位；若 end 配置了 SurroundingPoints，则这些点位ID也会一并返回，并同时写入玩家已解锁列表。超出部分（distanceMeters - 要求距离）的增量会累加到“存储能量”
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
  "storedEnergyMeters": 120.0,    // double, 当前玩家“存储能量”（米），最大 10000
  "rewards": [{ "itemId": 8000, "amount": 10 }] // List<Reward>, 本次下发的首次奖励（若有）
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
- 说明：当某条路线（start→end）的当前 distanceMeters 未达到“起点配置（start）的 TheNextPointDistance 中对应 end 的要求距离”时，可消耗玩家的“存储能量”（上限 10000 米）来直接解锁终点，仅消耗差额（要求距离 - 当前进度）。若该路段在配置中不存在或要求距离≤0，则视为无需消耗能量，直接解锁。

请求体
{
  "startLocationId": 10011, // int
  "endLocationId": 10012    // int
}

响应体
{
  "isUnlocked": true,          // bool, 本次是否成功解锁
  "usedEnergyMeters": 240.0,   // double, 本次消耗的能量（米），不足则为 0
  "storedEnergyMeters": 760.0, // double, 玩家当前剩余“存储能量”（米）
  "unlockedLocationIds": [10012, 10010501] // int[], 本次解锁提示列表：包含终点以及其 SurroundingPoints（若配置），未解锁则为空数组
}

<a id="toc-map-location-info"></a>
3.5 查询点位信息
POST /api/map/location-info

- 认证：需要 Bearer Token（权限 web_access）
- 说明：返回指定点位的当前人数统计和玩家的下次挑战时间。当玩家调用 /api/map/visit-location 或 /api/map/save-progress 接口时，该点位的人数会自动增加。若统计人数为0，或统计为1且只有当前玩家自己，则按 WorldConfig.json 中的“玩家选择大地图点位时机器人数量显示”的 Value4 区间生成随机展示人数。
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

<a id="toc-map-monster-reward"></a>
3.6 怪物奖励（基于 Monster.json）
POST /api/map/monster/reward

- 认证：需要 Bearer Token（权限 web_access）
- 策划表：Web/Json/Monster.json
- 说明：
  - 支持一次请求发多个怪物ID：使用 `monsterIds` 数组（int[]），或兼容旧参数 `monsterId`（int）。
  - 服务端会读取各怪物配置的 `Reward` 列表，并将相同 `itemId` 的数量合并后一次性发放。
  - 对于请求中不存在或无有效奖励的怪物ID，会跳过并返回 `failedMonsterIds` 数组而不会导致整单失败。
  - 当所有怪物ID均失败时返回 404。

请求体（多 ID 示例）
```json
{
  "monsterIds": [1000, 1001, 1002]
}
```
请求体（兼容旧版）
```json
{
  "monsterId": 1000
}
```

响应体
```json
{
  "success": true,
  "rewards": [
    { "itemId": 1000, "amount": 2 },
    { "itemId": 1001, "amount": 3 }
  ],
  "failedMonsterIds": [1003]
}
```
- `success` 为 `true` 表示至少成功发放了一条奖励。
- `failedMonsterIds` 为未找到配置或无有效奖励的怪物ID列表（可能为空）。

示例（curl）
```bash
curl -X POST https://host/api/map/monster/reward \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" \
  -d '{"monsterIds":[1000,1001]}'
```

说明：该接口已从自动 Swagger 文档中排除（仅在本文档说明使用）。



<a id="toc-map-feed-energy"></a>
3.7 主动灌输能量
POST /api/map/feed-energy

- 认证：需要 Bearer Token（权限 web_access）
- 说明：客户端上传本次运动的设备类型（deviceType）与距离（distanceMeters，单位米）。服务端按设备效率倍率（跑步1.2、划船2.0、单车1.5、手环/无设备1.0）将距离折算为能量并累加到玩家的 "存储能量"（上限 10000 米）。当存储能量已满时，只会注入差值。
- 设备类型：0=跑步, 1=划船, 2=单车, 3=手环/无设备


请求体
{
  "deviceType": 0,      // int
  "distanceMeters": 1200 // double, 单位: 米
}

响应体
{
  "usedDistanceMeters": 1000.0, // double, 实际被计入的距离（米）
  "storedEnergyMeters": 8600.0  // double, 注入后玩家当前存储能量（米）
}

示例（curl）
curl -X POST https://host/api/map/feed-energy \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" \
  -d '{"deviceType":0,"distanceMeters":1200}'

<a id="toc-map-device-distance"></a>
3.8 查询设备可灌输距离
POST /api/map/device-distance

- 认证：需要 Bearer Token（权限 web_access）
- 说明：返回玩家距能量上限还可存储的能量（米），以及按四个设备类型分别还能灌输的最大距离（米）。

请求体：空对象 {} 或不传
{
}

响应体
{
  "remainingEnergyMeters": 1400.0, // double, 距上限还可注入的能量（米）
  "deviceDistances": [             // List<{ deviceType, distanceMeters }>
    { "deviceType": 0, "distanceMeters": 1166.7 },
    { "deviceType": 1, "distanceMeters": 700.0 },
    { "deviceType": 2, "distanceMeters": 933.3 },
    { "deviceType": 3, "distanceMeters": 1400.0 }
  ]
}

示例（curl）
curl -X POST https://host/api/map/energy-capacity \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" -d '{}'


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

示例（curl）
curl -X GET https://host/api/inventory/equipments \
  -H "Authorization: Bearer <webToken>"

curl -X POST https://host/api/inventory/equipments \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" -d '{}'
响应示例
```json
{
  "equipments": [
    {
      "id": 101,            // long 装备实例ID
      "equipId": 20001,     // int  配置ID
      "quality": 3,         // int  品质
      "part": 1,            // int  装备部位
      "attrs": [            // 属性数组，只包含有值的属性
        { "type": 100, "value": 15   },  // 攻击力 (参考 EquipBuffType 枚举)
        { "type": 101, "value": 120  },  // 生命值
        { "type": 103, "value": 2    },  // 暴击率
        { "type": 104, "value": 1    },  // 攻击速度
        { "type": 105, "value": 5    },  // 暴击伤害
        { "type": 106, "value": 4.5  },  // 产出效率
        { "type": 0, "value": 1      }   // 上肢力量 (参考 PlayerBuffType 枚举)
      ],
      "specialEntryId": 3,  // int? 武器特殊词条ID (参考 EquipmentEntry.json)
      "isEquipped": true,   // bool 是否已装备
      "createdAt": "2025-01-01T00:00:00Z",  // DateTime 创建时间
      "updatedAt": "2025-01-01T01:00:00Z"   // DateTime 更新时间
    }
  ]
}
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

<a id="toc-inventory-item-status"></a>
4.5 查询道具状态（支持体力下次恢复时间）
POST /api/inventory/item-status

- 认证：需要 Bearer Token（权限 web_access）
- 说明：客户端批量查询指定 itemId 的当前数量。当请求的 itemId 为“体力道具”（1002）等可自动恢复类型时，响应还会包含 `nextRefreshTime` 字段，表示服务器预计的下次自动 +1 时间（ISO 8601）。当数量已达上限或道具不自动恢复时，该字段为 `0001-01-01T00:00:00Z`（DateTime.MinValue）。

请求体
```json
{
  "itemIds": [1002, 8000, 8001]
}
```
- `itemIds`：int[]，要查询的道具 ID 列表，最多 100 个；为空或缺省时返回空数组。

响应体
```json
[
  { "itemId": 1002, "amount": 87, "nextRefreshTime": "2026-01-07T09:32:00Z" },
  { "itemId": 8000, "amount": 15, "nextRefreshTime": "0001-01-01T00:00:00Z" },
  { "itemId": 8001, "amount": 0,  "nextRefreshTime": "0001-01-01T00:00:00Z" }
]
```
字段说明
| 字段 | 类型 | 含义 |
|------|------|------|
| itemId | int | 道具 ID |
| amount | long | 当前拥有数量（不存在视为 0）|
| nextRefreshTime | DateTime | 下次 +1 的服务器 UTC 时间（仅自动恢复型道具有效）|

示例（curl）
```bash
curl -X POST https://host/api/inventory/item-status \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" \
  -d '{"itemIds":[1002,8000]}'
```
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
5.3 保存消息ID列表（带节点）
POST /api/travel/stage/save-message

- 认证：需要 Bearer Token（权限 web_access）
- 说明：客户端提交节点ID（nodeId）与三个整型ID的列表（IdList，长度必须为 3），服务端会将其与关卡ID、用户ID一起保存。
- 限制：IdList 必须包含 3 个大于 0 的整数；nodeId 必须 > 0。

请求体
{
  "levelId": 101,           // int 关卡LevelId
  "nodeId": 10011,          // int 关卡内的节点/点位ID
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
  { "statusCode": 400, "code": 1001, "message": "节点ID无效" }
  { "statusCode": 400, "code": 1001, "message": "IdList必须包含3个大于0的整数" }
- 500 Internal Error（服务器错误）
  { "statusCode": 500, "code": 1004, "message": "服务器内部错误" }

示例（curl）
curl -X POST https://host/api/travel/stage/save-message \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" \
  -d '{"levelId":101, "nodeId":10011, "idList":[101,202,303]}'


<a id="toc-travel-stage-get-random-message"></a>
5.4 获取随机消息ID列表（按节点）
POST /api/travel/stage/get-random-message

- 认证：需要 Bearer Token（权限 web_access）
- 说明：返回该关卡某节点保存的随机一条 ID 列表及其元信息；会自动过滤掉“我自己在该关卡该节点曾经保存过”的ID。
- 字段：success、userId、messageIDList（三个整型ID）、createdAt（ISO 8601）。
- 当该关卡该节点暂无数据时，返回 success=true，userId=0，messageIDList=[]，createdAt=0001-01-01T00:00:00Z。

请求体
{
  "levelId": 101,   // int 关卡LevelId
  "nodeId": 10011   // int 节点/点位ID
}

响应体
{
  "success": true,                // bool
  "userId": 987654,               // long（留言用户ID；无数据时为 0）
  "messageIDList": [101,202,303], // List<int> 三个整型ID；无数据时为空 []
  "createdAt": "2025-12-04T09:54:04Z" // DateTime (ISO 8601)；无数据时为 0001-01-01T00:00:00Z
}


示例（curl）
curl -X POST https://host/api/travel/stage/get-random-message \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" \

<a id="toc-travel-stage-my-messages"></a>
5.5 获取玩家某关卡的所有节点留言
POST /api/travel/stage/my-messages

- 认证：需要 Bearer Token（权限 web_access）
- 说明：返回当前玩家在指定关卡（levelId）下，各“节点（nodeId）”最近一次保存的留言（每个节点最多返回一条）。

请求体
{
  "levelId": 101 // int 关卡LevelId
}

响应体
{
  "success": true,    // bool
  "levelId": 101,     // int
  "items": [          // List<{ nodeId, messageIDList, messageId, createdAt }>
    {
      "nodeId": 10011,
      "messageIDList": [101,202,303],
      "messageId": 12345,
      "createdAt": "2025-12-04T09:54:04Z"
    }
  ]
}

示例（curl）
curl -X POST https://host/api/travel/stage/my-messages \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" \
  -d '{"levelId":101}'

  -d '{"levelId":101, "nodeId":10011}'

---

<a id="toc-travel-random-player"></a>
5.6 随机获取玩家信息
POST /api/travel/random-player

- 认证：需要 Bearer Token（权限 web_access）
- 说明：随机返回一名除自己以外的玩家信息，以及当前玩家对其的关注状态与双方偶遇次数。

响应体
{
  "success": true,            // bool              // object
  "userId": 987654,           // long
  "isFollowed": false,        // bool 当前玩家是否已关注对方
  "encounterCount": 3         // int 与该玩家的偶遇次数
}

示例（curl）
curl -X POST https://host/api/travel/random-player \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" -d '{}'

<a id="toc-travel-follow-set"></a>
5.7 修改关注状态
POST /api/travel/follow/set

- 认证：需要 Bearer Token（权限 web_access）
- 说明：关注或取消关注指定玩家。

请求体
{
  "targetUserId": 987654, // long 要关注/取消关注的玩家ID
  "follow": true          // bool true=关注, false=取消关注
}

响应体
{
  "success": true, // bool
  "isFollowed": true // bool 修改后是否已关注
}

示例（curl）
curl -X POST https://host/api/travel/follow/set \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" \
  -d '{"targetUserId":987654,"follow":true}'

<a id="toc-travel-follow-status"></a>
5.8 获取关注状态
POST /api/travel/follow/status

- 认证：需要 Bearer Token（权限 web_access）
- 说明：查询当前玩家是否已关注目标玩家（支持批量）。当同时传入 `targetUserIds` 数组时，将返回一个字典，表示各玩家的关注状态；如只传单个 `targetUserId` 字段，则保持向后兼容。
请求体（批量查询）
{
  "targetUserIds": [987654, 123456] // long[] 最多 100 个
}
响应体（批量查询）
{
  "success": true,             // bool
  "status": {                  // object  key=targetUserId, value=isFollowed
    "987654": true,
    "123456": false
  }
}

示例（curl，单个）
curl -X POST https://host/api/travel/follow/status \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" \
  -d '{"targetUserId":987654}'

示例（curl，批量）
curl -X POST https://host/api/travel/follow/status \
  -H "Authorization: Bearer <webToken>" -H "Content-Type: application/json" \
  -d '{"targetUserIds":[987654,123456]}'

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
  - role：role_attribute.json、role_attributeid.json、role_upgrade.json、role_sport.json、role_experience.json
  - map：worlduimap_mapbase.json
  - event：travel_eventlist.json
  - drop：travel_droppoint.json
  - collection：collectionlist_item.json、collectionlist_combination.json
  - pverank：pverank_config.json、pverank_weekreward.json、pverank_seasonreward.json
  - general：worldconfig.json

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

<a id="toc-admin-update"></a>
7.3 批量更新配置文件（Web/Json *.json）
POST /api/admin/config/update

- 认证：需要 Bearer Token（admin 角色）
- 范围：仅允许更新 Web/Json 目录下“已有的 .json 文件”；不会创建新文件
- 校验：不做 schema 校验；仅做基础安全校验（必须为 .json、禁止目录穿越、目标文件必须存在、JSON 语法必须有效）
- 写入策略：
  - 将请求体中的 content 写入同目录临时文件，再原子替换目标文件（File.Move overwrite）
  - 自动生成同名 .bak 备份（带 UTC 时间戳）
  - 替换完成后由文件监控（JsonConfigWatcher）自动触发对应配置服务的热重载
- 频控与大小：当前接口未内置限流与最大体积限制

请求体（示例）
{
  "files": [
    {
      "file": "Item.json",
      "content": [
        { "ID": 1, "Name": "Potion", "Type": 1 },
        { "ID": 2, "Name": "Sword",  "Type": 2 }
      ]
    },
    {
      "file": "Role_Upgrade.json",
      "content": [
        { "Rank": 1, "Experience": 0,   "UpperLimb": 1, "LowerLimb": 1, "Core": 1, "HeartLungs": 1 },
        { "Rank": 2, "Experience": 200, "UpperLimb": 1, "LowerLimb": 1, "Core": 1, "HeartLungs": 1 }
      ]
    }
  ]
}

响应示例（成功）
{
  "ok": 2,
  "fail": 0,
  "results": [
    { "file": "Item.json", "status": "ok", "backup": "Item.json.20251210xxxxxx.bak", "bytesWritten": 2345 },
    { "file": "Role_Upgrade.json", "status": "ok", "backup": "Role_Upgrade.json.20251210xxxxxx.bak", "bytesWritten": 4567 }
  ]
}

响应示例（部分失败）
{
  "ok": 1,
  "fail": 1,
  "results": [
    { "file": "Item.json", "status": "ok", "backup": "Item.json.20251210xxxxxx.bak", "bytesWritten": 2345 },
    { "file": "WorldUiMap_NotExist.json", "status": "error", "error": "target json not found" }
  ]
}

错误说明
- 400 Bad Request：请求体缺少 files 或 files 为空
- 403/401：鉴权失败或无 admin 角色
- 415 Unsupported Media Type：Content-Type 非 application/json
- 422/500：
  - only .json files are allowed（文件扩展名非法）
  - path traversal detected（目录穿越尝试）
  - target json not found（目标文件不存在）

示例（curl）
cat <<'JSON' > body.json
{
  "files": [
    { "file": "Item.json", "content": [{"ID":1,"Name":"Potion"}] },
    { "file": "Role_Upgrade.json", "content": [{"Rank":1,"Experience":0}] }
  ]
}
JSON

curl -X POST https://host/api/admin/config/update \
  -H "Authorization: Bearer <adminToken>" \
  -H "Content-Type: application/json" \
  --data-binary @body.json


<a id="toc-stats"></a>
1. 统计数据（Statistics）

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

---

<a id="toc-rank"></a>
9. 排行榜（Rank）

说明与规则概览
- 周期：周榜与赛季榜两种（periodType：1=周榜，2=赛季榜）。
- 结算：周榜按 PVERank_Config.json 的 WeeklySettlement（1=周一...7=周日）结算，上周数据可在任意时刻领取；赛季按年简化结算（可根据需要改为季度）。
- 设备：deviceType：0=跑步 Run，1=划船 Rowing，2=单车 Bicycle，3=手环 Bracelet。
- 统计：以“总距离（米）”作为排序依据，距离相同则按更新时间早者在前（稳定排名）。
- 性能：读走 pve_rank_board 聚合表，并有复合索引（period_type, period_id, device_type, total_distance_meters desc, updated_at）。写在完成运动时进行累加，O(1)。

<a id="toc-rank-leaderboard"></a>
9.1 获取排行榜
POST /api/rank/leaderboard

请求体
{
  "periodType": 1,   // int 1=周榜,2=赛季榜
  "deviceType": 0,   // int 0=跑步,1=划船,2=单车,3=手环
  "top": 100         // int? 返回前 N 名，默认 100，最大 100
}

响应体
{
  "periodType": 1,           // int
  "deviceType": 0,           // int
  "periodId": 202549,        // int 周榜: yyyyWW；赛季: yyyy
  "top": [                   // List<LeaderboardItem>
    { "userId": 123, "distanceMeters": 5600.0, "rank": 1 },
    { "userId": 456, "distanceMeters": 4200.0, "rank": 2 }
  ],
  "me": { "userId": 999, "distanceMeters": 3500.0, "rank": 5 } // 可为空（未上榜/无数据）
}

说明
- 排名计算：返回 TopN 的名次；“我”的名次为“超过我距离的人数 + 1”。
- 客户端可按设备展示四个 tab；支持拉取前 100 名并在本地分页展示。

<a id="toc-rank-claim-week"></a>
9.2 领取周榜奖励
POST /api/rank/claim-week

请求体
{ "deviceType": 0 }

响应体
{
  "success": true,
  "message": "ok",
  "rewards": [ { "itemId": 1000, "amount": 100 } ]
}

说明
- 仅针对“上一周”的最终名次发奖（幂等：重复领取返回失败消息）。
- 发奖规则来自 Web/Json/PVERank_WeekReward.json；区间匹配 Rank 落点，按设备读取对应奖励。

<a id="toc-rank-claim-season"></a>
9.3 领取赛季奖励
POST /api/rank/claim-season

请求体
{ "deviceType": 0 }

响应体
{
  "success": true,
  "message": "ok",
  "rewards": [ { "itemId": 1000, "amount": 100 } ]
}

说明
- 赛季奖励规则来自 Web/Json/PVERank_SeasonReward.json（当前简化为按年）。
- 幂等防重：同周期+设备+用户仅能成功一次；发放记录写入 pve_rank_reward_grant。


<a id="toc-gm"></a>
10. GM 工具（GM）

<a id="toc-gm-grant-item"></a>
10.1 发放物品/装备给玩家
POST /gm/grant-item

- 认证：需要 Bearer Token（admin 角色）
- 说明：GM 后台或脚本使用，给指定玩家发放道具或装备。若为装备，则按配置随机生成词条数值。

请求体
```json
{ "userId": 123456, "itemId": 20001, "amount": 1 }
```

响应体
```json
{ "success": true }
```

示例（curl）
```bash
curl -X POST https://host/gm/grant-item \
  -H "Authorization: Bearer <adminToken>" \
  -H "Content-Type: application/json" \
  -d '{"userId":123456,"itemId":20001,"amount":1}'
```

<a id="toc-gm-grant-item-dev"></a>
10.2 [DEV] 给自己发放物品/装备
POST /gm/dev/grant-item

- 认证：需要 Bearer Token（权限 web_access）
- 说明：仅用于开发/测试环境。根据令牌中的 userId 给自己发放道具/装备。

请求体
```json
{ "itemId": 20001, "amount": 1 }
```

响应体 同上

示例（curl）
```bash
curl -X POST https://host/gm/dev/grant-item \
  -H "Authorization: Bearer <webToken>" \
  -H "Content-Type: application/json" \
  -d '{"itemId":20001,"amount":1}'
```

---

<a id="toc-tutorial"></a>
11. 新手引导（Tutorial）

<a id="toc-tutorial-set-step"></a>
11.1 设置步骤
POST /api/tutorial/set-step

- 认证：需要 Bearer Token（权限 web_access）
- 说明：客户端上报当前的新手引导步骤 ID，服务端保存。

请求体
```json
{ "stepId": 3 }
```

响应体
```json
{ "success": true, "stepId": 3 }
```

<a id="toc-tutorial-get-step"></a>
11.2 获取步骤
POST /api/tutorial/get-step（也可 GET）

- 认证：需要 Bearer Token（权限 web_access）
- 说明：返回当前玩家已保存的新手引导步骤 ID；若无记录则返回 0。

请求体：空对象 `{}` 或不传

响应体
```json
{ "stepId": 3 }
```

---


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