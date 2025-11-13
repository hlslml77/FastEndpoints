# 测试角色成长和地图系统 API

$baseUrl = "http://localhost:9005/api"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "测试 1: 获取玩家角色信息 (GET)" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan

try {
    $response = Invoke-WebRequest -Uri "$baseUrl/role-growth/player/1" -Method GET -ContentType "application/json"
    Write-Host "状态码: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "响应内容:" -ForegroundColor Green
    $response.Content | ConvertFrom-Json | ConvertTo-Json -Depth 10
} catch {
    Write-Host "错误: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $reader.BaseStream.Position = 0
        $reader.DiscardBufferedData()
        Write-Host "响应内容: $($reader.ReadToEnd())" -ForegroundColor Red
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "测试 2: 完成运动 (POST)" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan

$sportBody = @{
    userId = 1
    deviceType = 2
    distance = 5.0
    calorie = 300
} | ConvertTo-Json

try {
    $response = Invoke-WebRequest -Uri "$baseUrl/role-growth/complete-sport" -Method POST -Body $sportBody -ContentType "application/json"
    Write-Host "状态码: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "响应内容:" -ForegroundColor Green
    $response.Content | ConvertFrom-Json | ConvertTo-Json -Depth 10
} catch {
    Write-Host "错误: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $reader.BaseStream.Position = 0
        $reader.DiscardBufferedData()
        Write-Host "响应内容: $($reader.ReadToEnd())" -ForegroundColor Red
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "测试 3: 保存地图进度 (POST)" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan

$mapProgressBody = @{
    userId = 1
    startLocationId = 1
    endLocationId = 2
    distanceMeters = 1000.5
} | ConvertTo-Json

try {
    $response = Invoke-WebRequest -Uri "$baseUrl/map/save-progress" -Method POST -Body $mapProgressBody -ContentType "application/json"
    Write-Host "状态码: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "响应内容:" -ForegroundColor Green
    $response.Content | ConvertFrom-Json | ConvertTo-Json -Depth 10
} catch {
    Write-Host "错误: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $reader.BaseStream.Position = 0
        $reader.DiscardBufferedData()
        Write-Host "响应内容: $($reader.ReadToEnd())" -ForegroundColor Red
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "测试 4: 访问地图点位 (POST)" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan

$visitLocationBody = @{
    userId = 1
    locationId = 1
} | ConvertTo-Json

try {
    $response = Invoke-WebRequest -Uri "$baseUrl/map/visit-location" -Method POST -Body $visitLocationBody -ContentType "application/json"
    Write-Host "状态码: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "响应内容:" -ForegroundColor Green
    $response.Content | ConvertFrom-Json | ConvertTo-Json -Depth 10
} catch {
    Write-Host "错误: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $reader.BaseStream.Position = 0
        $reader.DiscardBufferedData()
        Write-Host "响应内容: $($reader.ReadToEnd())" -ForegroundColor Red
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "所有测试完成!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan

