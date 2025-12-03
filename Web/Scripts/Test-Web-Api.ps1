param(
  [string]$BaseUrl = "http://localhost:9005",
  [string]$UserId = "123456",
  [string]$AdminKey = "8f1f0b3a4a9d2f6c7e1a3b5d8c9e0f1246a7b8c9d0e1f2a3b4c5d6e7f8090a1"
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

# Force UTF-8 console/codepage for proper Chinese/Unicode output
try { chcp 65001 | Out-Null } catch {}
try { [Console]::InputEncoding = New-Object System.Text.UTF8Encoding($false) } catch {}
try { [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false) } catch {}
try { if ($PSStyle) { $PSStyle.OutputRendering = 'Host' } } catch {}
$env:DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTION = '1'

$LogPath = Join-Path -Path (Split-Path -Parent $PSCommandPath) -ChildPath 'api-test-run.txt'
Set-Content -Path $LogPath -Value ("==== Test started at {0} ====" -f ([DateTime]::UtcNow.ToString('u')))

# Test statistics
$script:TestStats = @{ Passed = 0; Failed = 0; Skipped = 0 }

function Sanitize([string]$s) {
    if ($null -eq $s) { return '' }
    $t = ($s -replace '[^\x00-\x7F]', '')    # remove non-ASCII
    $t = ($t -replace '^\s+', '')             # trim leading whitespace
    $t = ($t -replace '^\?\s*', '')          # drop leading '?'
    return $t
}
function Step($msg){ $t = Sanitize $msg; Write-Host "`n$t" -ForegroundColor Cyan; Add-Content -Path $LogPath -Value "`n$t`n" }
function Info($msg){ $t = Sanitize $msg; Write-Host $t -ForegroundColor Gray; Add-Content -Path $LogPath -Value $t }
function Ok($msg){ $t = Sanitize $msg; $t = ("OK " + $t.Trim()); Write-Host $t -ForegroundColor Green; Add-Content -Path $LogPath -Value $t; $script:TestStats.Passed++ }
function Warn($msg){ $t = Sanitize $msg; $t = ("WARN " + $t.Trim()); Write-Host $t -ForegroundColor Yellow; Add-Content -Path $LogPath -Value $t; $script:TestStats.Skipped++ }
function Err($msg){ $t = Sanitize $msg; $t = ("FAIL " + $t.Trim()); Write-Host $t -ForegroundColor Red; Add-Content -Path $LogPath -Value $t; $script:TestStats.Failed++ }

function Invoke-ApiCall($Uri, $Auth, $Method, $Body, $SkipErrorHandling = $false) {
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
        if ($SkipErrorHandling) { return $null }
        $ex = $_.Exception
        if ($ex.Response) {
            try {
                $reader = New-Object System.IO.StreamReader($ex.Response.GetResponseStream())
                $errorBody = $reader.ReadToEnd(); $reader.Close()
                Err ("`n   HTTP Error: {0} on {1}" -f [int]$ex.Response.StatusCode, $Uri)
                Err ("   Response: {0}" -f $errorBody)
            } catch {
                Err ("`n   HTTP Error on {0}: {1}" -f $Uri, $ex.Message)
            }
        } else {
            Err ("`n   Network/Script Error on {0}: {1}" -f $Uri, $ex.Message)
        }
    }
    return $null
}

function Get-Token($key, $uid, $isAdmin = $false) {
    Step "1) Exchanging token..."
    $ts = [int64]([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())
    $nonce = [guid]::NewGuid().ToString('N')
    $headers = @{ "X-Exchange-Key"=$key; "X-Timestamp"=$ts; "X-Nonce"=$nonce }
    if ($isAdmin) { $headers["X-Admin"] = "1" }
    $body = @{ userId=$uid }
    $resp = Invoke-ApiCall -Uri "$BaseUrl/api/auth/exchange" -Auth $headers -Method 'Post' -Body $body
    if (-not $resp) { throw "Failed to get token" }
    $token = if ($resp.webToken) { $resp.webToken } else { $resp.WebToken }
    if (-not $token) { throw "Token was not found in the response." }
    Ok "   Successfully got token, length: $($token.Length)"
    return $token
}

function Test-RoleSystem($auth) {
    Step "2) Testing Role System..."

    # 2.1 Get player info
    Info "   [2.1] GET /api/role/get-player"
    $player1 = Invoke-ApiCall -Uri "$BaseUrl/api/role/get-player" -Auth $auth -Method 'Post' -Body @{}
    if ($player1) {
        Info ("   Player Lv {0}, Exp {1}/{2}" -f $player1.currentLevel, $player1.currentExperience, $player1.experienceToNextLevel)
        Ok "   ✓ Get player info successful"
    } else {
        Err "   ✗ Get player info failed"
        return
    }

    # 2.2 Complete sport
    Info "   [2.2] POST /api/role/complete-sport"
    $sport = @{ deviceType=0; distance=1500; calorie=100 }
    $sportResult = Invoke-ApiCall -Uri "$BaseUrl/api/role/complete-sport" -Auth $auth -Method 'Post' -Body $sport
    if ($sportResult) {
        $player2 = Invoke-ApiCall -Uri "$BaseUrl/api/role/get-player" -Auth $auth -Method 'Post' -Body @{}
        if ($player2) {
            Ok ("   ✓ Completed sport. Player is now Lv {0}, Exp {1}/{2}" -f $player2.currentLevel, $player2.currentExperience, $player2.experienceToNextLevel)
        }
    } else {
        Err "   ✗ Complete sport failed"
    }
}

function Test-MapSystem($auth) {
    Step "3) Testing Map System..."

    # 3.1 Save progress
    Info "   [3.1] POST /api/map/save-progress"
    $progress = @{ startLocationId=100102; endLocationId=100103; distanceMeters=850.5 }
    $saveResult = Invoke-ApiCall -Uri "$BaseUrl/api/map/save-progress" -Auth $auth -Method 'Post' -Body $progress
    if ($saveResult) {
        Ok ("   ✓ Saved progress: ID $($saveResult.id), Distance $($saveResult.distanceMeters)m")
        $unlocked = if ($saveResult.unlockedLocationIds) { $saveResult.unlockedLocationIds } else { @() }
        if ($unlocked.Count -gt 0) {
            Info ("   Unlocked IDs this run: $($unlocked -join ', ')")
            Info ("   Stored energy: $($saveResult.storedEnergyMeters)m")
        }
    } else {
        Err "   ✗ Save progress failed"
    }

    # 3.2 Visit location
    Info "   [3.2] POST /api/map/visit-location"
    $visit = @{ locationId=100102; isCompleted=$true; needConsume=$false }
    $visitResult = Invoke-ApiCall -Uri "$BaseUrl/api/map/visit-location" -Auth $auth -Method 'Post' -Body $visit
    if ($visitResult) {
        Info ("   First visit: $($visitResult.isFirstVisit), Visit count: $($visitResult.visitCount)")
        if ($visitResult.rewards) {
            Info ("   Rewards: $($visitResult.rewards.Count) items")
            $visitResult.rewards | ForEach-Object {
                Info ("     - Item $($_.itemId): x$($_.amount)")
            }
        }
        if ($visitResult.locationInfo) {
            Info ("   Location: $($visitResult.locationInfo.description)")
        }
        Ok "   ✓ Visit location successful"
    } else {
        Err "   ✗ Visit location failed"
    }

    # 3.3 Get player map state
    Info "   [3.3] POST /api/map/player-state"
    $mapState = Invoke-ApiCall -Uri "$BaseUrl/api/map/player-state" -Auth $auth -Method 'Post' -Body @{}
    if ($mapState) {
        $unlockedCount = if ($mapState.unlockedLocationIds) { $mapState.unlockedLocationIds.Count } else { 0 }
        $completedCount = if ($mapState.completedLocationIds) { $mapState.completedLocationIds.Count } else { 0 }
        Info ("   Unlocked: $unlockedCount, Completed: $completedCount")
        if ($mapState.progressRecords) {
            Info ("   Progress records: $($mapState.progressRecords.Count)")
        }
        if ($mapState.storedEnergyMeters) {
            Info ("   Stored energy: $($mapState.storedEnergyMeters) meters")
        }
        if ($mapState.currentLocationId) {
            Info ("   Current location: $($mapState.currentLocationId)")
        }
        if ($mapState.dailyRandomEvents) {
            Info ("   Daily random events: $($mapState.dailyRandomEvents.Count)")
        }
        Ok "   ✓ Get player map state successful"
    } else {
        Err "   ✗ Get player map state failed"
    }

    # 3.4 Query location info
    Info "   [3.4] POST /api/map/location-info"
    $locInfo = Invoke-ApiCall -Uri "$BaseUrl/api/map/location-info" -Auth $auth -Method 'Post' -Body @{ locationId=100102 }
    if ($locInfo) {
        Info ("   People count: $($locInfo.peopleCount)")
        if ($locInfo.nextChallengeTime) {
            Info ("   Next challenge time: $($locInfo.nextChallengeTime)")
        }
        Ok "   ✓ Query location info successful"
    } else {
        Err "   ✗ Query location info failed"
    }

    # 3.5 Unlock with energy (optional, only if we have stored energy)
    if ($mapState -and $mapState.storedEnergyMeters -gt 0) {
        Info "   [3.5] POST /api/map/unlock-with-energy"
        $unlockEnergy = Invoke-ApiCall -Uri "$BaseUrl/api/map/unlock-with-energy" -Auth $auth -Method 'Post' -Body @{ startLocationId=100102; endLocationId=100104 }
        if ($unlockEnergy) {
            Info ("   Unlocked: $($unlockEnergy.isUnlocked), Used energy: $($unlockEnergy.usedEnergyMeters)m, Remaining: $($unlockEnergy.storedEnergyMeters)m")
            Ok "   ✓ Unlock with energy successful"
        } else {
            Warn "   ⚠ Unlock with energy failed or not applicable"
        }
    }
}

function Test-InventorySystem($auth) {
    Step "4) Testing Inventory System..."

    # 4.1 Query items (GET)
    Info "   [4.1a] GET /api/inventory/items"
    $items = Invoke-ApiCall -Uri "$BaseUrl/api/inventory/items" -Auth $auth -Method 'Get'
    if ($items) {
        Info ("   Player has $($items.Count) types of items")
        Ok "   ✓ Query items (GET) successful"
    } else {
        Err "   ✗ Query items (GET) failed"
    }

    # 4.1 Query items (POST)
    Info "   [4.1b] POST /api/inventory/items"
    $itemsPost = Invoke-ApiCall -Uri "$BaseUrl/api/inventory/items" -Auth $auth -Method 'Post' -Body @{}
    if ($itemsPost) {
        Ok "   ✓ Query items (POST) successful"
    } else {
        Err "   ✗ Query items (POST) failed"
    }

    # 4.2 Query equipments (GET)
    Info "   [4.2a] GET /api/inventory/equipments"
    $equipmentsGet = Invoke-ApiCall -Uri "$BaseUrl/api/inventory/equipments" -Auth $auth -Method 'Get'
    if ($equipmentsGet) {
        Info ("   Player has $($equipmentsGet.Count) pieces of equipment")
        Ok "   ✓ Query equipments (GET) successful"
    } else {
        Err "   ✗ Query equipments (GET) failed"
    }

    # 4.2 Query equipments (POST)
    Info "   [4.2b] POST /api/inventory/equipments"
    $equipments = Invoke-ApiCall -Uri "$BaseUrl/api/inventory/equipments" -Auth $auth -Method 'Post' -Body @{}
    if ($equipments) {
        Info ("   Player has $($equipments.Count) pieces of equipment")
        Ok "   ✓ Query equipments (POST) successful"
    } else {
        Err "   ✗ Query equipments (POST) failed"
        return
    }

    # 4.3 Equip and 4.4 Unequip
    $firstEquip = $equipments | Select-Object -First 1
    if ($firstEquip) {
        Info "   [4.3] POST /api/inventory/equip"
        $equipResult = Invoke-ApiCall -Uri "$BaseUrl/api/inventory/equip" -Auth $auth -Method 'Post' -Body @{ equipmentRecordId = $firstEquip.id }
        if ($equipResult -and $equipResult.success) {
            Ok ("   ✓ Equipped item ID $($firstEquip.id)")
        } else {
            Warn ("   ⚠ Equip may have failed or returned unexpected format")
        }

        Info "   [4.4] POST /api/inventory/unequip"
        $unequipResult = Invoke-ApiCall -Uri "$BaseUrl/api/inventory/unequip" -Auth $auth -Method 'Post' -Body @{ equipmentRecordId = $firstEquip.id }
        if ($unequipResult -and $unequipResult.success) {
            Ok ("   ✓ Unequipped item ID $($firstEquip.id)")
        } else {
            Warn ("   ⚠ Unequip may have failed or returned unexpected format")
        }
    } else {
        Warn "   ⚠ No equipment available to test equip/unequip"
    }
}

function Test-TravelSystem($auth) {
    Step "5) Testing Travel System..."

    # 5.1 Event reward
    Info "   [5.1] POST /api/travel/event/reward"
    $eventReward = Invoke-ApiCall -Uri "$BaseUrl/api/travel/event/reward" -Auth $auth -Method 'Post' -Body @{ eventId=1001 }
    if ($eventReward) {
        if ($eventReward.success) {
            Ok ("   ✓ Event reward: Item $($eventReward.itemId) x $($eventReward.amount)")
        } else {
            Warn ("   ⚠ Event reward returned: $($eventReward.message)")
        }
    } else {
        Err "   ✗ Event reward failed"
    }

    # 5.2 Drop point reward
    Info "   [5.2] POST /api/travel/drop-point/reward"
    $dropReward = Invoke-ApiCall -Uri "$BaseUrl/api/travel/drop-point/reward" -Auth $auth -Method 'Post' -Body @{ levelId=101; distance=600 }
    if ($dropReward) {
        if ($dropReward.success) {
            if ($dropReward.rewards) {
                Ok ("   ✓ Drop reward: $($dropReward.rewards.Count) items")
            } else {
                Ok "   ✓ Drop reward successful"
            }
        } else {
            Warn ("   ⚠ Drop reward returned: $($dropReward.message)")
        }
    } else {
        Err "   ✗ Drop reward failed"
    }
}

function Test-CollectionSystem($auth) {
    Step "6) Testing Collection System..."

    # 6.1 Get my collections
    Info "   [6.1] POST /api/collection/my"
    $myCols1 = Invoke-ApiCall -Uri "$BaseUrl/api/collection/my" -Auth $auth -Method 'Post' -Body @{}
    if ($myCols1) {
        $count1 = if ($myCols1.collectionIds) { $myCols1.collectionIds.Count } else { 0 }
        Info ("   Collections (before): $count1")
        Ok "   ✓ Get my collections successful"
    } else {
        Err "   ✗ Get my collections failed"
        return
    }

    # 6.2 Obtain collection
    Info "   [6.2] POST /api/collection/obtain"
    $obtainedCount = 0
    1..3 | ForEach-Object {
        $c = Invoke-ApiCall -Uri "$BaseUrl/api/collection/obtain" -Auth $auth -Method 'Post' -Body @{}
        if ($c) {
            if ($c.success -or $c.Success) {
                $collId = if ($c.collectionId) { $c.collectionId } else { $c.CollectionId }
                Info ("   [$_] Obtained ID: $collId")
                $obtainedCount++
            } else {
                $msg = if ($c.message) { $c.message } else { $c.Message }
                Warn ("   [$_] Failed: $msg")
            }
        } else {
            Warn ("   [$_] No response")
        }
    }
    if ($obtainedCount -gt 0) {
        Ok "   ✓ Obtained $obtainedCount collections"
    }

    # 6.1 Get my collections again
    Info "   [6.1] POST /api/collection/my (after obtain)"
    $myCols2 = Invoke-ApiCall -Uri "$BaseUrl/api/collection/my" -Auth $auth -Method 'Post' -Body @{}
    if ($myCols2) {
        $count2 = if ($myCols2.collectionIds) { $myCols2.collectionIds.Count } else { 0 }
        Info ("   Collections (after): $count2")
        if ($count2 -gt $count1) {
            Ok "   ✓ Collection count increased"
        }
    }

    # 6.3 Claim combo
    Info "   [6.3] POST /api/collection/claim-combo"
    $claim = Invoke-ApiCall -Uri "$BaseUrl/api/collection/claim-combo" -Auth $auth -Method 'Post' -Body @{ comboId=1 }
    if ($claim) {
        $success = if ($claim.success) { $claim.success } else { $claim.Success }
        $msg = if ($claim.message) { $claim.message } else { $claim.Message }
        if ($success) {
            Ok "   ✓ Claim combo successful"
        } else {
            Warn ("   ⚠ Claim combo: $msg")
        }
    } else {
        Err "   ✗ Claim combo failed"
    }
}

function Test-AdminConfigStatus($auth) {
    Step "7) Testing Admin Config Status..."

    # 6.1 Get config status
    Info "   [6.1] GET /api/admin/config/status"
    $configStatus = Invoke-ApiCall -Uri "$BaseUrl/api/admin/config/status" -Auth $auth -Method 'Get'
    if ($configStatus) {
        if ($configStatus -is [System.Collections.IEnumerable] -and $configStatus -isnot [string]) {
            Info ("   Found $($configStatus.Count) config services")
            $configStatus | ForEach-Object {
                Info ("   - $($_.name): $($_.items) items, last reload: $($_.lastReloadTime)")
            }
            Ok "   ✓ Get config status successful"
        } else {
            Ok "   ✓ Get config status successful"
        }
    } else {
        Err "   ✗ Get config status failed"
    }
}

function Invoke-AllTests($token, $adminToken) {
    $auth = @{ Authorization = "Bearer $token"; Accept = "application/json" }

    # Test all systems
    Test-RoleSystem $auth
    Test-MapSystem $auth
    Test-InventorySystem $auth
    Test-TravelSystem $auth
    Test-CollectionSystem $auth
    Test-AdminConfigStatus $auth
}

function Show-TestSummary {
    Step "Test Summary"
    Info ("Passed: $($script:TestStats.Passed) | Failed: $($script:TestStats.Failed) | Skipped: $($script:TestStats.Skipped)")
    $total = $script:TestStats.Passed + $script:TestStats.Failed + $script:TestStats.Skipped
    if ($total -gt 0) {
        $passRate = [math]::Round(($script:TestStats.Passed / $total) * 100, 2)
        Info ("Pass Rate: $passRate%")
    }
}

try {
    $webToken = Get-Token -key $AdminKey -uid $UserId -isAdmin $false
    if ($webToken) {
        $adminToken = Get-Token -key $AdminKey -uid $UserId -isAdmin $true
        Invoke-AllTests -token $webToken -adminToken $adminToken
        Show-TestSummary
        Ok "`nScript finished successfully."
    }
} catch {
    Err "`nScript failed: $($_.Exception.Message)"
    exit 1
}

