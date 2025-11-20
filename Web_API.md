Role API

获取玩家角色信息
POST /api/role/get-player
请求 Body: 空对象 {} 或不传（从JWT解析用户ID）
返回: PlayerRoleResponse
认证: 需要JWT token，权限: web_access

完成运动
POST /api/role/complete-sport
请求 Body: CompleteSportRequest（从JWT解析用户ID）
返回: PlayerRoleResponse
认证: 需要JWT token，权限: web_access

数据模型

PlayerRoleResponse
{
  "userId": 0,
  "currentLevel": 0,
  "currentExperience": 0,
  "experienceToNextLevel": 0,
  "upperLimb": 0,
  "lowerLimb": 0,
  "core": 0,
  "heartLungs": 0,
  "todayAttributePoints": 0,
  "availableAttributePoints": 0,
  "speedBonus": 0.0,
  "lastUpdateTime": "2023-01-01T00:00:00Z"
}

CompleteSportRequest
{
  "deviceType": 0, // 1=自行车, 2=跑步, 3=划船
  "distance": 0.0,
  "calorie": 0
}

--------------------------------------------------------------------------------

MapSystem API

保存地图进度
POST /api/map/save-progress
请求 Body: SaveMapProgressRequest
返回: SaveMapProgressResponse
认证: 需要JWT token，权限: web_access

访问地图点位
POST /api/map/visit-location
请求 Body: VisitMapLocationRequest
返回: VisitMapLocationResponse
认证: 需要JWT token，权限: web_access

数据模型

SaveMapProgressRequest
{
  "startLocationId": 0,
  "endLocationId": 0,
  "distanceMeters": 0.0
}

SaveMapProgressResponse
{
  "id": 0,
  "userId": 0,
  "startLocationId": 0,
  "endLocationId": 0,
  "distanceMeters": 0.0,
  "createdAt": "2023-01-01T00:00:00Z"
}

VisitMapLocationRequest
{
  "locationId": 0
}

VisitMapLocationResponse
{
  "isFirstVisit": true,
  "rewards": [
    {
      "itemId": 8000,
      "amount": 20
    }
  ],
  "visitCount": 1,
  "firstVisitTime": "2023-01-01T00:00:00Z",
  "lastVisitTime": "2023-01-01T00:00:00Z",
  "locationInfo": {
    "locationId": 10011,
    "description": "黄岩镇",
    "scenicSpot": "这点有xxxxx描述",
    "hierarchy": 1
  }
}

获取玩家地图状态
POST /api/map/player-state
请求 Body: 空对象 {} 或不传（从JWT解析用户ID）
返回: GetPlayerMapStateResponse
认证: 需要JWT token，权限: web_access

数据模型（仅与本接口相关）

GetPlayerMapStateResponse
{
  "visitedLocationIds": [10011, 10012],
  "completedLocationIds": [10011, 10012],
  "progressRecords": [
    {
      "startLocationId": 10011,
      "endLocationId": 10012,
      "distanceMeters": 10.0,
      "createdAt": "2023-01-01T00:00:00Z"
    }
  ]
}


--------------------------------------------------------------------------------

Token Exchange API

Token交换端点
POST `/api/auth/exchange`

根据客户端传入的 userId 生成 Web 服务专用 Token（把 userId 写入 JWT 的 sub）。

请求模型
json
{
  "userId": "123456",
  "appToken": "..." // 可选，不解析
}

响应模型
json
{
  "webToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
}

使用流程
1. 客户端调用交换接口并传入 userId
2. 服务端生成 Web Token，并把 userId 写入 JWT 的 sub
3. 客户端使用 Web Token 访问其他 API

示例代码
// 1. Token交换
const response = await fetch('/api/auth/exchange', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ userId: '123456' })
});

const { webToken } = await response.json();

// 2. 使用Web Token访问API（示例：获取地图状态）
const apiResponse = await fetch('/api/map/player-state', {
    method: 'POST',
    headers: {
        'Authorization': `Bearer ${webToken}`,
        'Content-Type': 'application/json'
    },
    body: JSON.stringify({})
});
```


--------------------------------------------------------------------------------
