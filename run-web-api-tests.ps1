# Web API quick tests
$ErrorActionPreference = 'Stop'
$base = 'http://localhost:9005'
$api  = "$base/api"

function ShowJson($title, $obj) {
  Write-Host "`n=== $title ===" -ForegroundColor Cyan
  if ($null -eq $obj) { Write-Host '<null>'; return }
  try { $obj | ConvertTo-Json -Depth 10 } catch { Write-Host $obj }
}

try {
  # 1) auth/exchange
  $exchangeBody = @{ userId = '123456' } | ConvertTo-Json
  $resp = Invoke-RestMethod -Method Post -Uri "$api/auth/exchange" -ContentType 'application/json' -Body $exchangeBody
  ShowJson 'auth/exchange' $resp
  $token = $resp.webToken
  if ([string]::IsNullOrWhiteSpace($token)) { throw 'No webToken returned!' }
  $headers = @{ Authorization = "Bearer $token" }

  # 2) role/get-player
  $getPlayer = Invoke-RestMethod -Method Post -Uri "$api/role/get-player" -Headers $headers -ContentType 'application/json' -Body '{}'
  ShowJson 'role/get-player' $getPlayer

  # 3) role/complete-sport
  $sportBody = @{ deviceType = 1; distance = 2.5; calorie = 180 } | ConvertTo-Json
  $sportResp = Invoke-RestMethod -Method Post -Uri "$api/role/complete-sport" -Headers $headers -ContentType 'application/json' -Body $sportBody
  ShowJson 'role/complete-sport' $sportResp

  # 4) map/save-progress
  $progressBody = @{ startLocationId = 10011; endLocationId = 10012; distanceMeters = 850.5 } | ConvertTo-Json
  $progressResp = Invoke-RestMethod -Method Post -Uri "$api/map/save-progress" -Headers $headers -ContentType 'application/json' -Body $progressBody
  ShowJson 'map/save-progress' $progressResp

  # 5) map/visit-location
  $visitBody = @{ locationId = 10011 } | ConvertTo-Json
  $visitResp = Invoke-RestMethod -Method Post -Uri "$api/map/visit-location" -Headers $headers -ContentType 'application/json' -Body $visitBody
  ShowJson 'map/visit-location' $visitResp

  # 6) map/player-state
  $stateResp = Invoke-RestMethod -Method Post -Uri "$api/map/player-state" -Headers $headers -ContentType 'application/json' -Body '{}'
  ShowJson 'map/player-state' $stateResp

  Write-Host "`nAll API calls completed." -ForegroundColor Green
}
catch {
  Write-Host "`nRequest failed:" -ForegroundColor Red
  Write-Host $_.Exception.Message -ForegroundColor Red
  if ($_.Exception.Response -and $_.Exception.Response.GetResponseStream) {
    try {
      $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
      $reader.BaseStream.Position = 0
      $reader.DiscardBufferedData()
      Write-Host "Response:" -ForegroundColor Red
      Write-Host ($reader.ReadToEnd())
    } catch {}
  }
  exit 1
}

