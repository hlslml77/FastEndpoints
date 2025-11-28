param(
  [string]$BaseUrl = "http://localhost:9005",
  [string]$UserId = "123456",
  [int]$ObtainTimes = 5
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$LogPath = Join-Path -Path (Split-Path -Parent $PSCommandPath) -ChildPath 'collections-run.txt'
Set-Content -Path $LogPath -Value ("==== Test started at {0} ====" -f ([DateTime]::UtcNow.ToString('u')))

function Step($msg){ Write-Host "`n$msg" -ForegroundColor Cyan; Add-Content -Path $LogPath -Value "`n$msg`n" }
function Info($msg){ Write-Host $msg -ForegroundColor Gray; Add-Content -Path $LogPath -Value $msg }
function Ok($msg){ Write-Host $msg -ForegroundColor Green; Add-Content -Path $LogPath -Value $msg }
function Warn($msg){ Write-Host $msg -ForegroundColor Yellow; Add-Content -Path $LogPath -Value $msg }
function Err($msg){ Write-Host $msg -ForegroundColor Red; Add-Content -Path $LogPath -Value $msg }

function Get-Token($key, $uid) {
    Step "1) Exchanging token..."
    $ts = [int64]([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())
    $nonce = [guid]::NewGuid().ToString('N')
    $headers = @{ "X-Exchange-Key"=$key; "X-Timestamp"=$ts; "X-Nonce"=$nonce }
    $body = @{ userId=$uid } | ConvertTo-Json -Compress

    try {
        $resp = Invoke-RestMethod -Uri "$BaseUrl/api/auth/exchange" -Method Post -Headers $headers -ContentType "application/json" -Body $body
        $token = if ($resp.webToken) { $resp.webToken } else { $resp.WebToken }
        if (-not $token) { throw "Token was not found in the response." }
        Ok "   Successfully got token, length: $($token.Length)"
        Info "   Token: $token`n"
        return $token
    } catch {
        $ex = $_.Exception
        if ($ex -is [System.Net.WebException]) {
            if ($ex.Response) {
                $reader = New-Object System.IO.StreamReader($ex.Response.GetResponseStream())
                $errorBody = $reader.ReadToEnd(); $reader.Close()
                Err ("   HTTP Error: {0} ({1})" -f [int]$ex.Response.StatusCode, $ex.Response.StatusCode)
                Err ("   Response: {0}" -f $errorBody)
            } else {
                Err ("   Network Error: {0}" -f $ex.Message)
            }
        } else {
            Err ("   Unexpected Error: {0}" -f $ex.Message)
        }
        throw "Failed to get token."
    }
}

function Run-Tests($token) {
    $authHeader = "Bearer $token"
    $auth = @{ Authorization = $authHeader; Accept = "application/json" }
    Info "   Using Authorization Header: $authHeader`n"

    Step "2) Getting my collections (before)..."
    $my1 = Invoke-RestMethod -Uri "$BaseUrl/api/collection/my" -Headers $auth
    Info ("   Count: $($my1.CollectionIds.Count)")

    Step "3) Obtaining collections ($ObtainTimes times)..."
    1..$ObtainTimes | ForEach-Object {
        $r = Invoke-RestMethod -Uri "$BaseUrl/api/collection/obtain" -Headers $auth -Method Post
        if ($r.Success) { Ok ("   [$_] Obtained ID: $($r.CollectionId)") } else { Warn ("   [$_] Failed: $($r.Message)") }
    }

    Step "4) Getting my collections (after)..."
    $my2 = Invoke-RestMethod -Uri "$BaseUrl/api/collection/my" -Headers $auth
    Info ("   Count: $($my2.CollectionIds.Count)")

    Step "5) Trying to claim combo ID 1..."
    $claimBody = @{ comboId = 1 } | ConvertTo-Json -Compress
    $claim = Invoke-RestMethod -Uri "$BaseUrl/api/collection/claim-combo" -Headers $auth -Method Post -ContentType "application/json" -Body $claimBody
    if ($claim.Success) { Ok "   Claim successful." } else { Warn ("   Claim failed: $($claim.Message)") }
}

try {
    $AdminKey = "8f1f0b3a4a9d2f6c7e1a3b5d8c9e0f1246a7b8c9d0e1f2a3b4c5d6e7f8090a1"
    $webToken = Get-Token -key $AdminKey -uid $UserId
    Run-Tests -token $webToken
    Ok "`nScript finished successfully."
} catch {
    Err "`nScript failed: $($_.Exception.Message)"
    exit 1
}

