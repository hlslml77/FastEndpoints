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
    function Get-PlayerState($auth){
        return Invoke-ApiCall -Uri "$BaseUrl/api/role/get-player" -Auth $auth -Method 'Post' -Body @{}
    }

    Step "2) Testing Role System (new Role_Sport + WorldConfig.DailyLimit)..."

    # 尝试用能量解锁一次（不阻塞，方便后续跑图）
    Info "   [2.0] POST /api/map/unlock-with-energy (100101 -> 100102)"
    $unlockTry = Invoke-ApiCall -Uri "$BaseUrl/api/map/unlock-with-energy" -Auth $auth -Method 'Post' -Body @{ startLocationId=100101; endLocationId=100102 }
    if ($unlockTry) {
        $ul = if ($unlockTry.unlockedLocationIds) { $unlockTry.unlockedLocationIds } else { @() }
        Info ("   Unlocked: $($unlockTry.isUnlocked), Used: $($unlockTry.usedEnergyMeters)m, Remaining: $($unlockTry.storedEnergyMeters)m")
        if ($ul.Count -gt 0) { Info ("   Unlocked IDs: $($ul -join ', ')") }
        Ok "   ✓ Unlock with energy call completed"
    } else {
        Warn "   ⚠ Unlock with energy not available"
    }

    # 2.1 获取玩家基线
    Info "   [2.1] GET /api/role/get-player (baseline)"
    $p0 = Get-PlayerState $auth
    if (-not $p0) { Err "   ✗ Get player failed"; return }
    Info ("   Lv {0}, EXP {1}/{2}, TodayPts {3}, Available {4}" -f $p0.currentLevel, $p0.currentExperience, $p0.experienceToNextLevel, $p0.todayAttributePoints, $p0.availableAttributePoints)
    Info ("   Main Attrs => Upper:{0} Lower:{1} Core:{2} Heart:{3}" -f $p0.upperLimb, $p0.lowerLimb, $p0.core, $p0.heartLungs)

    # 2.2 按新设备映射与距离做多组运动测试
    # 设备映射：0=跑步机；1=划船机；2=单车；3=手环
    $tests = @(
        @{ name='Treadmill 500m'; deviceType=0; distance=500; calorie=80 },
        @{ name='Band 500m';       deviceType=3; distance=500; calorie=80 },
        @{ name='Treadmill 1500m'; deviceType=0; distance=1500; calorie=120 },
        @{ name='Treadmill 2000m'; deviceType=0; distance=2000; calorie=150 },
        @{ name='Treadmill 2500m'; deviceType=0; distance=2500; calorie=180 }
    )

    foreach ($t in $tests) {
        Info ("   [2.2] POST /api/role/complete-sport — {0} (device={1}, dist={2}m, cal={3})" -f $t.name, $t.deviceType, $t.distance, $t.calorie)
        $before = Get-PlayerState $auth
        if (-not $before) { Err "   ✗ Get player before failed"; break }

        $resp = Invoke-ApiCall -Uri "$BaseUrl/api/role/complete-sport" -Auth $auth -Method 'Post' -Body @{ deviceType=$t.deviceType; distance=$t.distance; calorie=$t.calorie }
        if (-not $resp) { Err "   ✗ Complete sport failed"; continue }

        $after = Get-PlayerState $auth
        if (-not $after) { Err "   ✗ Get player after failed"; continue }

        $dU = ($after.upperLimb - $before.upperLimb)
        $dL = ($after.lowerLimb - $before.lowerLimb)
        $dC = ($after.core - $before.core)
        $dH = ($after.heartLungs - $before.heartLungs)
        $dPts = ($after.todayAttributePoints - $before.todayAttributePoints)
        $dExp = ($after.currentExperience - $before.currentExperience)
        Info ("      ΔMain => U:{0} L:{1} C:{2} H:{3} | ΔTodayPts:{4} | ΔExp:{5}" -f $dU, $dL, $dC, $dH, $dPts, $dExp)
        Ok  ("      ✓ Now Lv {0}, Exp {1}/{2}, TodayPts {3}/{4}" -f $after.currentLevel, $after.currentExperience, $after.experienceToNextLevel, $after.todayAttributePoints, ($after.todayAttributePoints + $after.availableAttributePoints))
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
        if ($visitResult.consumedItems) {
            Info ("   Consumed: $($visitResult.consumedItems.Count) items")
            $visitResult.consumedItems | ForEach-Object {
                $rem = $_.remaining
                if ($null -ne $rem) {
                    Info ("     - Item $($_.itemId): x$($_.amount), remaining $rem")
                } else {
                    Info ("     - Item $($_.itemId): x$($_.amount)")
                }
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

    # 4.1 Grant a test equipment to check rolled stats
    Info "   [4.1] POST /gm/dev/grant-item (equipId=20001)"
    $grantResult = Invoke-ApiCall -Uri "$BaseUrl/api/gm/dev/grant-item" -Auth $auth -Method 'Post' -Body @{ itemId = 20001; amount = 1 }
    if ($grantResult -and ($grantResult.success -or $grantResult.Success)) {
        Ok "   ✓ Granted test equipment 20001"
    } else {
        Err "   ✗ Failed to grant test equipment 20001"
        return # Stop if we can't get the test item
    }

    # 4.2 Query equipments and inspect the new one
    Info "   [4.2] POST /api/inventory/equipments"
    $equipmentsRaw = Invoke-ApiCall -Uri "$BaseUrl/api/inventory/equipments" -Auth $auth -Method 'Post' -Body @{}
    $equipments = if ($equipmentsRaw.equipments) { $equipmentsRaw.equipments } else { $equipmentsRaw }
    if ($equipments) {
        $equipCount = if ($equipments -is [System.Collections.IEnumerable] -and $equipments -isnot [string]) { $equipments.Count } else { 1 }
        Info ("   Player now has $equipCount pieces of equipment")
        $newestEquip = $equipments | Sort-Object -Property CreatedAt -Descending | Select-Object -First 1
        if ($newestEquip -and $newestEquip.equipId -eq 20001) {
            Ok "   ✓ Found newly granted equipment (ID $($newestEquip.id))"
            Info "     Attributes for EquipID 20001 (Quality: $($newestEquip.quality), Part: $($newestEquip.part))"
            # Prefer new array-style attrs if present
            if ($newestEquip.attrs) {
                foreach ($attr in $newestEquip.attrs) {
                    $t = $attr.type; $v = $attr.value
                    Info ("       - Type {0,-3}: {1}" -f $t, $v)
                }
            } else {
                # fallback to legacy top-level properties (for backward compat)
                $newestEquip.PSObject.Properties |
                    Where-Object { $_.MemberType -eq 'NoteProperty' -and @('Attack','HP','Defense','Critical','AttackSpeed','CriticalDamage','UpperLimb','LowerLimb','Core','HeartLungs','Efficiency','Energy','Speed') -contains $_.Name -and $null -ne $_.Value } |
                    ForEach-Object { Info ("       - {0,-15}: {1}" -f $_.Name, $_.Value) }
            }
            if ($newestEquip.specialEntryId) {
                Info ("       - {0,-15}: {1}" -f 'SpecialEntryId', $newestEquip.specialEntryId)
            }
        } else {
            Warn "   ⚠ Could not find the newly granted equipment 20001 in inventory list."
        }
    } else {
        Err "   ✗ Query equipments (POST) failed after granting"
        return
    }

    # 4.3 Equip and 4.4 Unequip the new item
    $equipToTest = $equipments | Select-Object -First 1
    if ($equipToTest) {
        Info "   [4.3] POST /api/inventory/equip (id: $($equipToTest.id))"
        $equipResult = Invoke-ApiCall -Uri "$BaseUrl/api/inventory/equip" -Auth $auth -Method 'Post' -Body @{ equipmentRecordId = $equipToTest.id }
        if ($equipResult -and ($equipResult.success -or $equipResult.Success)) {
            Ok ("   ✓ Equipped item ID $($equipToTest.id)")
        } else {
            Warn ("   ⚠ Equip may have failed or returned unexpected format")
        }

        Info "   [4.4] POST /api/inventory/unequip (id: $($equipToTest.id))"
        $unequipResult = Invoke-ApiCall -Uri "$BaseUrl/api/inventory/unequip" -Auth $auth -Method 'Post' -Body @{ equipmentRecordId = $equipToTest.id }
        if ($unequipResult -and ($unequipResult.success -or $unequipResult.Success)) {
            Ok ("   ✓ Unequipped item ID $($equipToTest.id)")
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
        $evSuccess = if ($eventReward.success) { $eventReward.success } else { $eventReward.Success }
        if ($evSuccess) {
            Ok ("   ✓ Event reward: Item $($eventReward.itemId) x $($eventReward.amount)")
        } else {
            $evMsg = if ($eventReward.message) { $eventReward.message } else { $eventReward.Message }
            Warn ("   ⚠ Event reward returned: $evMsg")
        }
    } else {
        Err "   ✗ Event reward failed"
    }

    # 5.2 Drop point reward
    Info "   [5.2] POST /api/travel/drop-point/reward"
    $dropReward = Invoke-ApiCall -Uri "$BaseUrl/api/travel/drop-point/reward" -Auth $auth -Method 'Post' -Body @{ levelId=501; distance=600 }
    if ($dropReward) {
        $dpSuccess = if ($dropReward.success) { $dropReward.success } else { $dropReward.Success }
        if ($dpSuccess) {
            if ($dropReward.rewards) {
                Ok ("   ✓ Drop reward: $($dropReward.rewards.Count) items")
            } else {
                Ok "   ✓ Drop reward successful"
            }
        } else {
            $dpMsg = if ($dropReward.message) { $dropReward.message } else { $dropReward.Message }
            Warn ("   ⚠ Drop reward returned: $dpMsg")
        }
    } else {
        Err "   ✗ Drop reward failed"
    }

    # 5.3 Save my stage message
    Info "   [5.3] POST /api/travel/stage/save-message"
    $saveBody = @{ levelId = 101; nodeId = 10011; idList = @(101,202,303) }
    $saveResp = Invoke-ApiCall -Uri "$BaseUrl/api/travel/stage/save-message" -Auth $auth -Method 'Post' -Body $saveBody
    if ($saveResp) {
        $svSuccess = if ($saveResp.success) { $saveResp.success } else { $saveResp.Success }
        if ($svSuccess) {
            $mid = if ($saveResp.messageId) { $saveResp.messageId } else { $saveResp.MessageId }
            Ok ("   ✓ Saved stage message. ID=$mid (level=101, node=10011, ids=101/202/303)")
        } else {
            Warn "   ⚠ Save stage message returned success=false"
        }
    } else {
        Err "   ✗ Save stage message failed"
    }

    Start-Sleep -Milliseconds 200

    # 5.4 Get my stage messages
    Info "   [5.4] POST /api/travel/stage/my-messages"
    $myReq = @{ levelId = 101 }
    $myMsgs = Invoke-ApiCall -Uri "$BaseUrl/api/travel/stage/my-messages" -Auth $auth -Method 'Post' -Body $myReq
    if ($myMsgs) {
        $mySuccess = if ($myMsgs.success) { $myMsgs.success } else { $myMsgs.Success }
        if ($mySuccess) {
            $items = $myMsgs.items
            $cnt = if ($items) { $items.Count } else { 0 }
            Info ("   Items: $cnt")
            if ($cnt -gt 0) {
                # find node 10011
                $node = $items | Where-Object { $_.nodeId -eq 10011 } | Select-Object -First 1
                if ($node) {
                    $ids = if ($node.messageIDList) { $node.messageIDList -join ', ' } else { '' }
                    Info ("   NodeId=10011, MessageId=$($node.messageId), CreatedAt=$($node.createdAt)")
                    Info ("   IDs: [$ids]")
                    Ok  "   ✓ My stage messages returned expected node"
                } else {
                    Warn "   ⚠ My stage messages have no entry for nodeId=10011"
                }
            } else {
                Warn "   ⚠ My stage messages returned empty items"
            }
        } else {
            Warn "   ⚠ My stage messages returned success=false"
        }
    } else {
        Err "   ✗ My stage messages request failed"
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
function Test-AdminConfigUpdate($adminAuth) {
    Step "7.2) Testing Admin Config Update (/api/admin/config/update)..."

    # NOTE:
    # /api/admin/config/update expects each file.content to be a JSON array.
    # PowerShell hashtables serialize to objects, and this test can break the server reload.
    # So we skip this test by default.
    Warn "   ⚠ Skipped: /api/admin/config/update (this test is disabled to avoid breaking server config)"
}

function Test-RankSystem($auth) {
    Step "8) Testing Rank System..."

    # 8.1 Leaderboard
    Info "   [8.1] POST /api/rank/leaderboard"
    $lbReq = @{ periodType = 1; deviceType = 0; top = 20 }
    $lb = Invoke-ApiCall -Uri "$BaseUrl/api/rank/leaderboard" -Auth $auth -Method 'Post' -Body $lbReq
    if ($lb) {
        $topList = if ($lb.Top) { $lb.Top } else { $lb.top }
        $me = if ($lb.Me) { $lb.Me } else { $lb.me }
        $count = if ($topList) { $topList.Count } else { 0 }
        Info ("   Top count: {0}, PeriodId: {1}" -f $count, $lb.PeriodId)
        if ($topList -and $count -gt 0) {
            $topList | Select-Object -First 3 | ForEach-Object {
                Info ("     - Rank {0}: User {1}, Distance {2}m" -f $_.Rank, $_.UserId, $_.DistanceMeters)
            }
        }
        if ($me) {
            Info ("   Me: Rank {0}, Distance {1}m" -f $me.Rank, $me.DistanceMeters)
        }
        Ok "   ✓ Leaderboard query successful"
    } else {
        Err "   ✗ Leaderboard query failed"
    }

    # 8.2 Claim weekly reward
    Info "   [8.2] POST /api/rank/claim-week"
    $cw = Invoke-ApiCall -Uri "$BaseUrl/api/rank/claim-week" -Auth $auth -Method 'Post' -Body @{ deviceType = 0 }
    if ($cw) {
        $success = if ($cw.success) { $cw.success } else { $cw.Success }
        $msg = if ($cw.message) { $cw.message } else { $cw.Message }
        if ($success) {
            Ok ("   ✓ Claim week successful: {0}" -f $msg)
            if ($cw.rewards) {
                $cw.rewards | ForEach-Object { Info ("     - Item {0} x {1}" -f $_.itemId, $_.amount) }
            }
        } else {
            Warn ("   ⚠ Claim week: {0}" -f $msg)
        }
    } else {
        Err "   ✗ Claim week failed"
    }
    


    # 8.3 Claim season reward
    Info "   [8.3] POST /api/rank/claim-season"
    $cs = Invoke-ApiCall -Uri "$BaseUrl/api/rank/claim-season" -Auth $auth -Method 'Post' -Body @{ deviceType = 0 }
    if ($cs) {
        $success = if ($cs.success) { $cs.success } else { $cs.Success }
        $msg = if ($cs.message) { $cs.message } else { $cs.Message }
        if ($success) {
            Ok ("   ✓ Claim season successful: {0}" -f $msg)
            if ($cs.rewards) {
                $cs.rewards | ForEach-Object { Info ("     - Item {0} x {1}" -f $_.itemId, $_.amount) }
            }
        } else {
            Warn ("   ⚠ Claim season: {0}" -f $msg)
        }
    } else {
        Err "   ✗ Claim season failed"
    }
}


function Invoke-AllTests($token, $adminToken) {
    $auth = @{ Authorization = "Bearer $token"; Accept = "application/json" }
    $adminAuth = @{ Authorization = "Bearer $adminToken"; Accept = "application/json" }

    # Test all systems
    Test-RoleSystem $auth
    Test-MapSystem $auth
    Test-InventorySystem $auth
    Test-TravelSystem $auth
    Test-CollectionSystem $auth
    Test-AdminConfigStatus $auth
    Test-AdminConfigUpdate $adminAuth
    Test-RankSystem $auth
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

