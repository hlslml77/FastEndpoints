param(
  [string]$BaseUrl = "http://localhost:9005",
  [string]$UserId = "123456"
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
$PSDefaultParameterValues['Out-File:Encoding'] = 'utf8'

function Step($msg){ Write-Host $msg -ForegroundColor Cyan }
function Info($msg){ Write-Host $msg -ForegroundColor Gray }
function Ok($msg){ Write-Host $msg -ForegroundColor Green }
function Warn($msg){ Write-Host $msg -ForegroundColor Yellow }
function Err($msg){ Write-Host $msg -ForegroundColor Red }

# 管理密钥，来自 appsettings.json AuthExchange.AdminKey
$AdminKey = "8f1f0b3a4a9d2f6c7e1a3b5d8c9e0f1246a7b8c9d0e1f2a3b4c5d6e7f8090a1"

try {
  Step "1) Exchange token (X-Admin=1) ..."
  $ts = [int64]([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())
  $nonce = [guid]::NewGuid().ToString('N')
  $headers = @{ "X-Exchange-Key"=$AdminKey; "X-Timestamp"=$ts; "X-Nonce"=$nonce; "X-Admin"="1" }
  $body = @{ userId=$UserId } | ConvertTo-Json -Compress

  $resp = Invoke-RestMethod -Uri "$BaseUrl/api/auth/exchange" -Method Post -Headers $headers -ContentType "application/json" -Body $body
  $token = $resp.webToken
  if (-not $token) { throw "no token returned" }
  Info ("   token length = {0}" -f $token.Length)

  $auth = @{ Authorization = ("Bearer " + $token) }
  $auth["Accept"] = "application/json"

  Step "2) Get status (baseline) ..."
  $status1 = Invoke-RestMethod -Uri "$BaseUrl/api/admin/config/status" -Headers $auth -Method Get
  $item1 = $status1 | Where-Object { $_.Name -eq "item" } | Select-Object -First 1
  if (-not $item1) { throw "item config status not found" }
  Info ("   Dir={0}" -f $item1.Dir)
  Info ("   T1={0}" -f $item1.LastReloadTime)

  $itemFile = Join-Path -Path $item1.Dir -ChildPath "Item.json"
  if (-not (Test-Path $itemFile)) { throw ("file not found: {0}" -f $itemFile) }

  Step "3) Touch Item.json (append newline) ..."
  [System.IO.File]::AppendAllText($itemFile, [Environment]::NewLine)
  Start-Sleep -Milliseconds 1200 # > debounce(300ms)

  Step "4) Get status again ..."
  $status2 = Invoke-RestMethod -Uri "$BaseUrl/api/admin/config/status" -Headers $auth -Method Get
  $item2 = $status2 | Where-Object { $_.Name -eq "item" } | Select-Object -First 1
  Info ("   T2={0}" -f $item2.LastReloadTime)

  if ($item1.LastReloadTime -ne $item2.LastReloadTime) {
    Ok "   HOT-RELOAD: OK (LastReloadTime changed)"
  } else {
    Warn "   HOT-RELOAD: NO-CHANGE (timestamp unchanged; maybe too quick or dir mismatch)"
  }

  Step "5) Manual reload (admin, by file) ..."
  $reload = Invoke-RestMethod -Uri "$BaseUrl/api/admin/config/reload?file=Item.json" -Headers $auth -Method Get
  Ok ("   Reload summary: ok={0} fail={1}" -f $reload.ok, $reload.fail)
}
catch {
  Err ("failed: {0}" -f $_.Exception.Message)
  if ($_.ErrorDetails) { Err $_.ErrorDetails.Message }
  exit 1
}

