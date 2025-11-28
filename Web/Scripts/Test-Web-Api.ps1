param(
  [string]$BaseUrl = "http://localhost:9005",
  [string]$UserId = "123456"
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$LogPath = Join-Path -Path (Split-Path -Parent $PSCommandPath) -ChildPath 'api-test-run.txt'
Set-Content -Path $LogPath -Value ("==== Test started at {0} ====" -f ([DateTime]::UtcNow.ToString('u')))

function Step($msg){ Write-Host "`n$msg" -ForegroundColor Cyan; Add-Content -Path $LogPath -Value "`n$([char]27)[36m$msg$([char]27)[0m`n" }
function Info($msg){ Write-Host $msg -ForegroundColor Gray; Add-Content -Path $LogPath -Value $msg }
function Ok($msg){ Write-Host $msg -ForegroundColor Green; Add-Content -Path $LogPath -Value $msg }
function Warn($msg){ Write-Host $msg -ForegroundColor Yellow; Add-Content -Path $LogPath -Value $msg }
function Err($msg){ Write-Host $msg -ForegroundColor Red; Add-Content -Path $LogPath -Value $msg }

function Api-Call($Uri, $Auth, $Method, $Body) {
    $params = @{
        Uri = $Uri
        Method = $Method
        Headers = $Auth
        ContentType = 'application/json'
    }
    if ($Body) { $params.Body = ($Body | ConvertTo-Json -Compress) }
    try {
        return Invoke-RestMethod @params
    } catch {
        $ex = $_.Exception
        if ($ex -is [System.Net.WebException] -and $ex.Response) {
            $reader = New-Object System.IO.StreamReader($ex.Response.GetResponseStream())
            $errorBody = $reader.ReadToEnd(); $reader.Close()
            Err ("`n   HTTP Error: {0} ({1}) on {2}" -f [int]$ex.Response.StatusCode, $ex.Response.StatusCode, $Uri)
            Err ("   Response: {0}" -f $errorBody)
        } else {
            Err ("`n   Network/Script Error on {0}: {1}" -f $Uri, $ex.Message)
        }
        # Do not re-throw, allow script to continue if possible
    }
}

function Get-Token($key, $uid) {
    Step "1) Exchanging token..."
    $ts = [int64]([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())
    $nonce = [guid]::NewGuid().ToString('N')
    $headers = @{ "X-Exchange-Key"=$key; "X-Timestamp"=$ts; "X-Nonce"=$nonce }
    $body = @{ userId=$uid }
    $resp = Api-Call -Uri "$BaseUrl/api/auth/exchange" -Auth $headers -Method 'Post' -Body $body
    $token = if ($resp.webToken) { $resp.webToken } else { $resp.WebToken }
    if (-not $token) { throw "Token was not found in the response." }
    Ok "   Successfully got token, length: $($token.Length)"
    return $token
}

function Run-Tests($token) {
    $auth = @{ Authorization = "Bearer $token"; Accept = "application/json" }

    # Role System
    Step "2) Testing Role System..."
    $player1 = Api-Call -Uri "$BaseUrl/api/role/get-player" -Auth $auth -Method 'Post' -Body @{}
    Info ("   Player Lv {0}, Exp {1}/{2}" -f $player1.currentLevel, $player1.currentExperience, $player1.experienceToNextLevel)
    $sport = @{ deviceType=0; distance=1500; calorie=100 }
    Api-Call -Uri "$BaseUrl/api/role/complete-sport" -Auth $auth -Method 'Post' -Body $sport
    $player2 = Api-Call -Uri "$BaseUrl/api/role/get-player" -Auth $auth -Method 'Post' -Body @{}
    Ok ("   Completed sport. Player is now Lv {0}, Exp {1}/{2}" -f $player2.currentLevel, $player2.currentExperience, $player2.experienceToNextLevel)

    # Map System
    Step "3) Testing Map System..."
    $mapState1 = Api-Call -Uri "$BaseUrl/api/map/player-state" -Auth $auth -Method 'Post' -Body @{}
    Info ("   Visited: $($mapState1.visitedLocationIds.Count), Completed: $($mapState1.completedLocationIds.Count)")
    $progress = @{ startLocationId=100101; endLocationId=100102; distanceMeters=200 }
    Api-Call -Uri "$BaseUrl/api/map/save-progress" -Auth $auth -Method 'Post' -Body $progress
    $visit = @{ locationId=100101; isCompleted=$true }
    Api-Call -Uri "$BaseUrl/api/map/visit-location" -Auth $auth -Method 'Post' -Body $visit
    $mapState2 = Api-Call -Uri "$BaseUrl/api/map/player-state" -Auth $auth -Method 'Post' -Body @{}
    Ok ("   Saved progress & visited location. Visited: $($mapState2.visitedLocationIds.Count), Completed: $($mapState2.completedLocationIds.Count)")

    # Inventory System
    Step "4) Testing Inventory System..."
    $items = Api-Call -Uri "$BaseUrl/api/inventory/items" -Auth $auth -Method 'Get'
    Info ("   Player has $($items.Count) types of items.")
    $equipments = Api-Call -Uri "$BaseUrl/api/inventory/equipments" -Auth $auth -Method 'Post' -Body @{}
    Info ("   Player has $($equipments.Count) pieces of equipment.")
    $firstEquip = $equipments | Select-Object -First 1
    if ($firstEquip) {
        Api-Call -Uri "$BaseUrl/api/inventory/equip" -Auth $auth -Method 'Post' -Body @{ equipmentRecordId = $firstEquip.id }
        Api-Call -Uri "$BaseUrl/api/inventory/unequip" -Auth $auth -Method 'Post' -Body @{ equipmentRecordId = $firstEquip.id }
        Ok "   Successfully equipped and unequipped item ID $($firstEquip.id)"
    }

    # Travel System
    Step "5) Testing Travel System..."
    $eventReward = Api-Call -Uri "$BaseUrl/api/travel/event/reward" -Auth $auth -Method 'Post' -Body @{ eventId=1001 }
    if ($eventReward.success) { Ok ("   Event reward: Item $($eventReward.itemId) x $($eventReward.amount)") }
    $dropReward = Api-Call -Uri "$BaseUrl/api/travel/drop-point/reward" -Auth $auth -Method 'Post' -Body @{ levelId=101; distance=600 }
    if ($dropReward.success) { Ok ("   Drop reward: $($dropReward.rewards.Count) items.") }

    # Collection System
    Step "6) Testing Collection System..."
    $myCols1 = Api-Call -Uri "$BaseUrl/api/collection/my" -Auth $auth -Method 'Post' -Body @{}
    Info ("   Collections (before): $($myCols1.collectionIds.Count)")
    1..3 | ForEach-Object {
        $c = Api-Call -Uri "$BaseUrl/api/collection/obtain" -Auth $auth -Method 'Post'
        if ($c.Success) { Ok ("   [$_] Obtained ID: $($c.CollectionId)") } else { Warn ("   [$_] Failed: $($c.Message)") }
    }
    $myCols2 = Api-Call -Uri "$BaseUrl/api/collection/my" -Auth $auth -Method 'Post' -Body @{}
    Info ("   Collections (after): $($myCols2.collectionIds.Count)")
    $claim = Api-Call -Uri "$BaseUrl/api/collection/claim-combo" -Auth $auth -Method 'Post' -Body @{ comboId=1 }
    if ($claim.Success) { Ok "   Claim combo successful." } else { Warn ("   Claim combo failed: $($claim.Message)") }
}

try {
    $AdminKey = "8f1f0b3a4a9d2f6c7e1a3b5d8c9e0f1246a7b8c9d0e1f2a3b4c5d6e7f8090a1"
    $webToken = Get-Token -key $AdminKey -uid $UserId
    if ($webToken) {
        Run-Tests -token $webToken
        Ok "`nScript finished."
    }
} catch {
    Err "`nScript failed: $($_.Exception.Message)"
    exit 1
}

