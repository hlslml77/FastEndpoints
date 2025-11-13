RoleGrowth API

获取玩家角色信息
POST /api/role-growth/get-player
请求 Body: GetPlayerRoleRequest
返回: PlayerRoleResponse

完成运动
POST /api/role-growth/complete-sport
请求 Body: CompleteSportRequest
返回: PlayerRoleResponse

数据模型

GetPlayerRoleRequest
{
  "userId": 0
}

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
  "userId": 0,
  "deviceType": 0, // 1=自行车, 2=跑步, 3=划船
  "distance": 0.0,
  "calorie": 0
}

---

MapSystem API

保存地图进度
POST /api/map/save-progress
请求 Body: SaveMapProgressRequest
返回: SaveMapProgressResponse

访问地图点位
POST /api/map/visit-location
请求 Body: VisitMapLocationRequest
返回: VisitMapLocationResponse

数据模型

SaveMapProgressRequest
{
  "userId": 0,
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
  "userId": 0,
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
