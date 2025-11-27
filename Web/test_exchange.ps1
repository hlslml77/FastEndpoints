$ErrorActionPreference = 'Stop'
$base = 'http://localhost:9005/api'
$adminKey = '8f1f0b3a4a9d2f6c7e1a3b5d8c9e0f1246a7b8c9d0e1f2a3b4c5d6e7f8090a1'
$ts = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
$nonce = [guid]::NewGuid().ToString('N')
$headers = @{
  'X-Exchange-Key' = $adminKey
  'X-Timestamp'    = $ts
  'X-Nonce'        = $nonce
  'Content-Type'   = 'application/json'
}
$body = @{ userId = '123456' } | ConvertTo-Json

Write-Host 'Request headers:' ($headers | ConvertTo-Json)
Write-Host 'Body:' $body

$resp = Invoke-RestMethod -Method Post -Uri "$base/auth/exchange" -Headers $headers -Body $body
Write-Host 'Exchange response:' ($resp | ConvertTo-Json -Depth 5)
$token = $resp.webToken
if (-not $token) { throw 'No token received from exchange.' }

$authHeaders = @{ 'Authorization' = "Bearer $token" }
$inv = Invoke-RestMethod -Method Get -Uri "$base/inventory/items" -Headers $authHeaders
Write-Host 'Inventory items:' ($inv | ConvertTo-Json -Depth 5)

