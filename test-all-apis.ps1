# API 测试脚本
$baseUrl = "http://localhost:9005"
$userId = "123456"
$headers = @{
    "Content-Type" = "application/json";
}

Write-Host "========================================" -ForegroundColor Yellow
Write-Host "开始测试所有 API" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow

# 1. 获取访问令牌
Write-Host "`n=== 1. 获取访问令牌 ===" -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/auth/exchange" -Method POST -Headers $headers -Body (ConvertTo-Json @{userId = $userId})
    Write-Host "✓ 成功获取Token" -ForegroundColor Green
    Write-Host ($response | ConvertTo-Json -Depth 5)
    $webToken = $response.webToken
} catch {
    Write-Host "✗ 获取Token失败: $_" -ForegroundColor Red
    exit 1
}

# 设置认证头
$authHeaders = @{
    "Content-Type" = "application/json"
    "Authorization" = "Bearer $webToken"
}

# 2.1 获取玩家角色信息
Write-Host "`n=== 2.1 获取玩家角色信息 ===" -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/role/get-player" -Method POST -Headers $authHeaders -Body (ConvertTo-Json @{})
    Write-Host "✓ 成功获取玩家信息" -ForegroundColor Green
    Write-Host ($response | ConvertTo-Json -Depth 5)
} catch {
    Write-Host "✗ 获取玩家信息失败: $_" -ForegroundColor Red
}

# 2.2 完成运动
Write-Host "`n=== 2.2 完成运动 ===" -ForegroundColor Cyan
try {
    $sportData = @{
        deviceType = 1
        distance = 2.5
        calorie = 180
    }
    $response = Invoke-RestMethod -Uri "$baseUrl/api/role/complete-sport" -Method POST -Headers $authHeaders -Body (ConvertTo-Json $sportData)
    Write-Host "✓ 成功完成运动" -ForegroundColor Green
    Write-Host ($response | ConvertTo-Json -Depth 5)
} catch {
    Write-Host "✗ 完成运动失败: $_" -ForegroundColor Red
}

# 3.1 保存地图进度
Write-Host "`n=== 3.1 保存地图进度 ===" -ForegroundColor Cyan
try {
    $mapData = @{
        startLocationId = 10011
        endLocationId = 10012
        distanceMeters = 850.5
    }
    $response = Invoke-RestMethod -Uri "$baseUrl/api/map/save-progress" -Method POST -Headers $authHeaders -Body (ConvertTo-Json $mapData)
    Write-Host "✓ 成功保存地图进度" -ForegroundColor Green
    Write-Host ($response | ConvertTo-Json -Depth 5)
} catch {
    Write-Host "✗ 保存地图进度失败: $_" -ForegroundColor Red
}

# 3.2 访问地图点位
Write-Host "`n=== 3.2 访问地图点位 ===" -ForegroundColor Cyan
try {
    $locationData = @{
        locationId = 10011
        isCompleted = $true
    }
    $response = Invoke-RestMethod -Uri "$baseUrl/api/map/visit-location" -Method POST -Headers $authHeaders -Body (ConvertTo-Json $locationData)
    Write-Host "✓ 成功访问地图点位" -ForegroundColor Green
    Write-Host ($response | ConvertTo-Json -Depth 5)
} catch {
    Write-Host "✗ 访问地图点位失败: $_" -ForegroundColor Red
}

# 3.3 获取玩家地图状态
Write-Host "`n=== 3.3 获取玩家地图状态 ===" -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/map/player-state" -Method POST -Headers $authHeaders -Body (ConvertTo-Json @{})
    Write-Host "✓ 成功获取地图状态" -ForegroundColor Green
    Write-Host ($response | ConvertTo-Json -Depth 5)
} catch {
    Write-Host "✗ 获取地图状态失败: $_" -ForegroundColor Red
}

# 4.1 查询玩家道具清单 (GET)
Write-Host "`n=== 4.1 查询玩家道具清单 (GET) ===" -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/inventory/items" -Method GET -Headers $authHeaders
    Write-Host "✓ 成功查询道具清单" -ForegroundColor Green
    Write-Host ($response | ConvertTo-Json -Depth 5)
} catch {
    Write-Host "✗ 查询道具清单失败: $_" -ForegroundColor Red
}

# 4.1 查询玩家道具清单 (POST)
Write-Host "`n=== 4.1 查询玩家道具清单 (POST) ===" -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/inventory/items" -Method POST -Headers $authHeaders -Body (ConvertTo-Json @{})
    Write-Host "✓ 成功查询道具清单" -ForegroundColor Green
    Write-Host ($response | ConvertTo-Json -Depth 5)
} catch {
    Write-Host "✗ 查询道具清单失败: $_" -ForegroundColor Red
}

# 4.2 查询玩家装备清单 (GET)
Write-Host "`n=== 4.2 查询玩家装备清单 (GET) ===" -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/inventory/equipments" -Method GET -Headers $authHeaders
    Write-Host "✓ 成功查询装备清单" -ForegroundColor Green
    Write-Host ($response | ConvertTo-Json -Depth 5)
} catch {
    Write-Host "✗ 查询装备清单失败: $_" -ForegroundColor Red
}

# 4.2 查询玩家装备清单 (POST)
Write-Host "`n=== 4.2 查询玩家装备清单 (POST) ===" -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/inventory/equipments" -Method POST -Headers $authHeaders -Body (ConvertTo-Json @{})
    Write-Host "✓ 成功查询装备清单" -ForegroundColor Green
    Write-Host ($response | ConvertTo-Json -Depth 5)
} catch {
    Write-Host "✗ 查询装备清单失败: $_" -ForegroundColor Red
}

# 4.3 穿戴指定装备
Write-Host "`n=== 4.3 穿戴指定装备 ===" -ForegroundColor Cyan
try {
    $equipData = @{
        equipmentRecordId = 101
    }
    $response = Invoke-RestMethod -Uri "$baseUrl/api/inventory/equip" -Method POST -Headers $authHeaders -Body (ConvertTo-Json $equipData)
    Write-Host "✓ 成功穿戴装备" -ForegroundColor Green
    Write-Host ($response | ConvertTo-Json -Depth 5)
} catch {
    Write-Host "✗ 穿戴装备失败: $_" -ForegroundColor Red
}

# 4.4 卸下指定装备
Write-Host "`n=== 4.4 卸下指定装备 ===" -ForegroundColor Cyan
try {
    $unequipData = @{
        equipmentRecordId = 101
    }
    $response = Invoke-RestMethod -Uri "$baseUrl/api/inventory/unequip" -Method POST -Headers $authHeaders -Body (ConvertTo-Json $unequipData)
    Write-Host "✓ 成功卸下装备" -ForegroundColor Green
    Write-Host ($response | ConvertTo-Json -Depth 5)
} catch {
    Write-Host "✗ 卸下装备失败: $_" -ForegroundColor Red
}

Write-Host "`n========================================" -ForegroundColor Yellow
Write-Host "API 测试完成" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow

