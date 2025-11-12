RoleGrowth API

获取玩家角色信息
GET /api/role-growth/player/{UserId}
路径参数: UserId (long)
返回: PlayerRoleResponse

完成运动
POST /api/role-growth/complete-sport
请求 Body: CompleteSportRequest
返回: PlayerRoleResponse

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
  "userId": 0,
  "deviceType": 0, // 1=自行车, 2=跑步, 3=划船
  "distance": 0.0,
  "calorie": 0
}
