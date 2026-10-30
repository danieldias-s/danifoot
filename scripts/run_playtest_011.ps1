$ErrorActionPreference = "Stop"
$baseUrl = "http://127.0.0.1:5089"
$outDir = "d:\elifoot_98\docs\evidence\gameplay_011"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

Write-Host "=== 1. STARTING FULL 22-ROUND SEASON PLAYTEST (TASK 011) ==="

# Step 1: Local Login
$auth = Invoke-RestMethod -Uri "$baseUrl/api/auth/local-login" -Method Post -ContentType "application/json" -Body (@{
  nickname = "Treinador 011"
  deviceId = "dev-playtest-011-main"
} | ConvertTo-Json)
$coachId = $auth.coachId
Write-Host "Logged in coachId=$coachId ($($auth.nickname))"

# Step 2: Create World (128 clubs, 1 human coach for full season playtest)
$createdWorld = Invoke-RestMethod -Uri "$baseUrl/api/worlds" -Method Post -ContentType "application/json" -Body (@{
  worldName = "Playtest Oficial 011"
  maxHumans = 1
  totalClubs = 128
  seed = 199811
  tickIntervalMs = 220
  pauseTimeoutSeconds = 60
  allowClassic505 = $true
} | ConvertTo-Json)
$worldId = if ($createdWorld.gameWorldId) { $createdWorld.gameWorldId } elseif ($createdWorld.id) { $createdWorld.id } else { $createdWorld.worldId }
Write-Host "Created worldId=$worldId"

# Step 3: Get clubs & choose a 1st division club
$clubs = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/clubs" -Method Get
$myClub = $clubs | Where-Object { $_.divisionTier -eq "PrimeiraDivisao" -or $_.divisionTier -eq 1 } | Select-Object -First 1
$myClubId = $myClub.id
Write-Host "Selected club: $($myClub.name) ($myClubId)"

$joinRes = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/join?coachId=$coachId&nickname=Treinador%20011&deviceId=dev-playtest-011-main&joinUnemployed=false&preferredClubId=$myClubId" -Method Post
$snapPrepR1 = $joinRes.snapshot

# Step 4: Get club detail, market, finances in Round 1
$clubDetailR1 = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/clubs/$myClubId" -Method Get
$marketR1 = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/market" -Method Get
$financesR1 = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/clubs/$myClubId/finances" -Method Get

# Step 8: Market operations in Round 1 (List a reserve player for sale + make an offer + place auction bid)
$squadR1 = $clubDetailR1.squad
$starterIds = @($clubDetailR1.preferredStarterIds)
$benchIds = @($clubDetailR1.preferredBenchIds)
$reserveToSell = $squadR1 | Where-Object { $starterIds -notcontains $_.id -and $benchIds -notcontains $_.id } | Select-Object -First 1
$marketOpsLog = @()
if ($null -ne $reserveToSell) {
  $askPrice = [math]::Round([double]$reserveToSell.fairValue)
  Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/market/list?coachId=$coachId&playerId=$($reserveToSell.id)&askingPrice=$askPrice" -Method Post | Out-Null
  $marketOpsLog += "Listed $($reserveToSell.displayName) for R`$ $askPrice"
}
$affordableListing = $marketR1.listings | Where-Object { $_.sellerClubId -ne $myClubId -and [double]$_.askingPrice -le ([double]$clubDetailR1.cashBalance * 0.45) } | Select-Object -First 1
if ($null -ne $affordableListing) {
  $offerAmt = [math]::Round([double]$affordableListing.askingPrice * 0.90)
  Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/market/offer?coachId=$coachId&playerId=$($affordableListing.playerId)&offeredAmount=$offerAmt" -Method Post | Out-Null
  $marketOpsLog += "Offered R`$ $offerAmt for $($affordableListing.displayName)"
}
if ($marketR1.auctions.Count -gt 0) {
  $auc = $marketR1.auctions[0]
  $bidAmt = [math]::Round([math]::Max([double]$auc.minimumBid, [double]$auc.currentBid + 15000))
  try {
    Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/market/auction/$($auc.auctionId)/bid?coachId=$coachId&bidAmount=$bidAmt" -Method Post | Out-Null
    $marketOpsLog += "Placed auction bid R`$ $bidAmt on $($auc.displayName)"
  } catch {}
}
$marketAfterOps = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/market" -Method Get

# Step 9: Stadium & Finance operation (Take bank loan)
try {
  Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/clubs/$myClubId/loans?coachId=$coachId&amount=300000&rounds=10" -Method Post | Out-Null
  $marketOpsLog += "Took bank loan of R`$ 300.000 (10 rounds)"
} catch {}
$financesAfterOps = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/clubs/$myClubId/finances" -Method Get
$clubDetailAfterOps = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/clubs/$myClubId" -Method Get

$snapRunningR1 = $null
$snapPausedR1 = $null
$snapAfterSubR1 = $null
$snapResultsR1 = $null

$roundSummaries = @()

for ($r = 1; $r -le 22; $r++) {
  $snap = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/snapshot" -Method Get
  $clubDetail = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/clubs/$myClubId" -Method Get
  $sIds = @($clubDetail.preferredStarterIds)
  $bIds = @($clubDetail.preferredBenchIds)
  if ($sIds.Count -ne 11) {
    $avail = $clubDetail.squad | Where-Object { $_.injuryRoundsRemaining -eq 0 -and $_.suspensionRoundsRemaining -eq 0 }
    $gks = @($avail | Where-Object { $_.position -eq "G" -or $_.position -eq 0 })
    $out = @($avail | Where-Object { $_.position -ne "G" -and $_.position -ne 0 })
    $sIds = @($gks[0].id) + @($out | Select-Object -First 10 | ForEach-Object { $_.id })
    $bIds = @($out | Select-Object -Skip 10 -First 7 | ForEach-Object { $_.id })
  }
  $form = if ($r % 3 -eq 0) { "4-3-3" } elseif ($r % 2 -eq 0) { "3-5-2" } else { "4-4-2" }

  Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/tactics" -Method Post -ContentType "application/json" -Body (@{
    gameWorldId = $worldId
    coachId = $coachId
    formation = $form
    starterPlayerIds = $sIds
    benchPlayerIds = $bIds
    idempotencyKey = "tac-$coachId-r$r-$([guid]::NewGuid().ToString('N'))"
    expectedRoundVersion = [int]$snap.roundVersion
    saveAsPreferred = $true
  } | ConvertTo-Json) | Out-Null

  $snap = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/snapshot" -Method Get
  $hasMyMatch = ($null -ne ($snap.matches | Where-Object { $_.homeClubId -eq $myClubId -or $_.awayClubId -eq $myClubId } | Select-Object -First 1))

  if ($null -eq $snapRunningR1 -and $hasMyMatch) {
    # Tick to minute 25 for live match screenshot
    $snap = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/tick?ticks=25" -Method Post
    $snapRunningR1 = $snap

    # Request global pause at minute 25
    Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/pause" -Method Post -ContentType "application/json" -Body (@{
      gameWorldId = $worldId
      coachId = $coachId
      idempotencyKey = "pause-$coachId-r$r"
    } | ConvertTo-Json) | Out-Null
    $snapPausedR1 = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/snapshot" -Method Get

    # Make substitution during pause if coach has a match
    $myM = $snapPausedR1.matches | Where-Object { $_.homeClubId -eq $myClubId -or $_.awayClubId -eq $myClubId } | Select-Object -First 1
    if ($null -ne $myM) {
      $isHome = ($myM.homeClubId -eq $myClubId)
      $mStarters = if ($isHome) { $myM.homeStarters } else { $myM.awayStarters }
      $mBench = if ($isHome) { $myM.homeBench } else { $myM.awayBench }
      $outP = $mStarters | Where-Object { $_.position -ne "G" -and $_.isSentOff -ne $true } | Select-Object -First 1
      $inP = $mBench | Where-Object { $_.position -ne "G" } | Select-Object -First 1
      if ($null -ne $outP -and $null -ne $inP) {
        $snapAfterSubR1 = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/in-match-adjust" -Method Post -ContentType "application/json" -Body (@{
          gameWorldId = $worldId
          coachId = $coachId
          matchId = $myM.matchId
          newFormation = "4-3-3"
          substitutions = @(
            @{ playerOutId = $outP.playerId; playerInId = $inP.playerId }
          )
          idempotencyKey = "adj-$coachId-r$r"
        } | ConvertTo-Json -Depth 5)
      }
    }
    # Confirm continue from global pause
    $contRes = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/continue" -Method Post -ContentType "application/json" -Body (@{
      gameWorldId = $worldId
      coachId = $coachId
      idempotencyKey = "cont-$coachId-r$r-1"
    } | ConvertTo-Json)
    $snap = $contRes.snapshot
  }

  # Advance match ticks until RESULTS
  $safety = 0
  while ($snap.roundState -ne "RESULTS" -and $safety -lt 25) {
    $safety++
    if ($snap.roundState -eq "GLOBAL_PAUSED" -or $snap.roundState -eq "HALFTIME_PAUSED" -or $snap.roundState -eq "EXTRA_TIME_BREAK" -or $snap.roundState -eq "EXTRA_TIME_HALFTIME") {
      $cRes = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/continue" -Method Post -ContentType "application/json" -Body (@{
        gameWorldId = $worldId
        coachId = $coachId
        idempotencyKey = "cont-$coachId-r$r-$safety-$([guid]::NewGuid().ToString('N'))"
      } | ConvertTo-Json)
      $snap = $cRes.snapshot
    } else {
      $snap = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/tick?ticks=45" -Method Post
    }
  }

  if ($null -eq $snapResultsR1 -and $hasMyMatch) {
    $snapResultsR1 = $snap
  }

  $myMatchFinished = $snap.matches | Where-Object { $_.homeClubId -eq $myClubId -or $_.awayClubId -eq $myClubId } | Select-Object -First 1
  $scoreDesc = if ($null -ne $myMatchFinished) {
    "$($myMatchFinished.homeClubName) $($myMatchFinished.homeGoals) x $($myMatchFinished.awayGoals) $($myMatchFinished.awayClubName)"
  } else {
    "Sem jogo do clube nesta fase da Taça"
  }

  $roundSummaries += [ordered]@{
    roundNumber = $r
    competition = $snap.competitionLabel
    formation = $form
    myMatchResult = $scoreDesc
    totalMatchesInRound = $snap.matches.Count
  }
  Write-Host "Completed Round $r ($($snap.competitionLabel)): $scoreDesc"

  if ($r -lt 22) {
    $snap = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/next-round" -Method Post
  }
}

# Capture end of Season 1 data (before advancing to Season 2) so Cup has all 8 stages including Final!
$cupEndS1 = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/cup" -Method Get
$standingsDiv1 = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/standings/PrimeiraDivisao" -Method Get
$standingsDiv2 = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/standings/SegundaDivisao" -Method Get
$standingsDiv3 = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/standings/TerceiraDivisao" -Method Get
$standingsDiv4 = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/standings/QuartaDivisao" -Method Get
$calendarEndS1 = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/calendar" -Method Get
$scorersEndS1 = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/top-scorers" -Method Get

# Now advance from Round 22 RESULTS to Season 2 Round 1 (triggers Season Rollover & Top Scorer Awards!)
$snapSeason2R1 = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/next-round" -Method Post
Write-Host "Rolled over to Season $($snapSeason2R1.seasonNumber) Round $($snapSeason2R1.roundNumber) (State: $($snapSeason2R1.roundState))"

$rankingAfterS1 = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/ranking" -Method Get
$invitationsAfterS1 = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/invitations/$coachId" -Method Get
$awardsSeason1 = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/season-top-scorers?season=1" -Method Get
$playerRankingsGeneral = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/player-rankings?category=scorers&scope=general&period=career" -Method Get
$topPlayerId = $playerRankingsGeneral.rows[0].playerId
$topPlayerHistory = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$worldId/players/$topPlayerId/history" -Method Get

# Step 22 & 23: Test Unemployed Coach + Invitations + Club Switch in a fast career room
$unempAuth = Invoke-RestMethod -Uri "$baseUrl/api/auth/local-login" -Method Post -ContentType "application/json" -Body (@{
  nickname = "Treinador Sem Clube 011"
  deviceId = "dev-playtest-011-unemp"
} | ConvertTo-Json)
$unempCoachId = $unempAuth.coachId
$unempWorld = Invoke-RestMethod -Uri "$baseUrl/api/worlds" -Method Post -ContentType "application/json" -Body (@{
  worldName = "Carreira Desempregado 011"
  maxHumans = 1
  totalClubs = 128
  seed = 199812
  tickIntervalMs = 220
  pauseTimeoutSeconds = 60
  allowClassic505 = $true
} | ConvertTo-Json)
$unempWorldId = if ($unempWorld.gameWorldId) { $unempWorld.gameWorldId } elseif ($unempWorld.id) { $unempWorld.id } else { $unempWorld.worldId }
$unempJoin = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$unempWorldId/join?coachId=$unempCoachId&nickname=Treinador%20Sem%20Clube%20011&deviceId=dev-playtest-011-unemp&joinUnemployed=true" -Method Post
$unempSnap = $unempJoin.snapshot
$unempInvites = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$unempWorldId/invitations/$unempCoachId" -Method Get

$uRound = 0
while ($unempInvites.invitations.Count -eq 0 -and $uRound -lt 4) {
  $uRound++
  Invoke-RestMethod -Uri "$baseUrl/api/worlds/$unempWorldId/ready-unemployed" -Method Post -ContentType "application/json" -Body (@{
    gameWorldId = $unempWorldId
    coachId = $unempCoachId
    idempotencyKey = "unemp-$unempCoachId-$uRound"
    expectedRoundVersion = [int]$unempSnap.roundVersion
  } | ConvertTo-Json) | Out-Null
  $unempSnap = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$unempWorldId/snapshot" -Method Get
  $uGuard = 0
  while ($unempSnap.roundState -ne "RESULTS" -and $uGuard -lt 25) {
    $uGuard++
    if ($unempSnap.roundState -like "*PAUSED*" -or $unempSnap.roundState -like "*BREAK*" -or $unempSnap.roundState -like "*HALFTIME*") {
      $unempSnap = (Invoke-RestMethod -Uri "$baseUrl/api/worlds/$unempWorldId/continue" -Method Post -ContentType "application/json" -Body (@{
        gameWorldId = $unempWorldId
        coachId = $unempCoachId
        idempotencyKey = "ucont-$unempCoachId-$uRound-$uGuard"
      } | ConvertTo-Json)).snapshot
    } else {
      $unempSnap = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$unempWorldId/tick?ticks=45" -Method Post
    }
  }
  $unempSnap = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$unempWorldId/next-round" -Method Post
  $unempInvites = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$unempWorldId/invitations/$unempCoachId" -Method Get
}
Write-Host "Unemployed coach received $($unempInvites.invitations.Count) invitation(s) after $uRound round(s)"

# === 2. MULTIPLAYER VALIDATION FOR 2, 4, 6, AND 8 HUMAN COACHES (SECTION 22) ===
Write-Host "=== 2. STARTING MULTIPLAYER 2, 4, 6, 8 COACH VALIDATION ==="
$mpResults = @()
foreach ($n in @(2, 4, 6, 8)) {
  $mpWorld = Invoke-RestMethod -Uri "$baseUrl/api/worlds" -Method Post -ContentType "application/json" -Body (@{
    worldName = "Sala Multiplayer $n Técnicos"
    maxHumans = $n
    totalClubs = 128
    seed = (199820 + $n)
    tickIntervalMs = 220
    pauseTimeoutSeconds = 60
    allowClassic505 = $true
  } | ConvertTo-Json)
  $mpWid = if ($mpWorld.gameWorldId) { $mpWorld.gameWorldId } elseif ($mpWorld.id) { $mpWorld.id } else { $mpWorld.worldId }
  $mpClubs = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$mpWid/clubs" -Method Get | Where-Object { $_.divisionTier -ne "Distrital" -and $_.divisionTier -ne 5 }

  $coaches = @()
  for ($i = 1; $i -le $n; $i++) {
    $cAuth = Invoke-RestMethod -Uri "$baseUrl/api/auth/local-login" -Method Post -ContentType "application/json" -Body (@{
      nickname = "Técnico MP $i"
      deviceId = "dev-mp-$n-$i"
    } | ConvertTo-Json)
    $clubForCoach = $mpClubs[$i - 1]
    Invoke-RestMethod -Uri "$baseUrl/api/worlds/$mpWid/join?coachId=$($cAuth.coachId)&nickname=Tecnico%20MP%20$i&deviceId=dev-mp-$n-$i&joinUnemployed=false&preferredClubId=$($clubForCoach.id)" -Method Post | Out-Null
    $coaches += @{
      coachId = $cAuth.coachId
      nickname = "Técnico MP $i"
      clubId = $clubForCoach.id
      clubName = $clubForCoach.name
    }
  }

  # Rounds 1 and 2 are District cup preliminaries (no matches for 1st-4th division clubs).
  # Play them through so the pause/substitution validation happens on Round 3 (first league round),
  # where every human club has a real match and a global pause is accepted by the API.
  $mpSnap0 = $null
  $initialLabel = $null
  $startedOnCoach = 0
  for ($mr = 1; $mr -le 3; $mr++) {
    $mpSnap0 = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$mpWid/snapshot" -Method Get
    $initialLabel = $mpSnap0.readyCounter.counterLabel
    $startedOnCoach = 0
    for ($i = 0; $i -lt $n; $i++) {
      $c = $coaches[$i]
      $cd = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$mpWid/clubs/$($c.clubId)" -Method Get
      $curSnap = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$mpWid/snapshot" -Method Get
      $tRes = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$mpWid/tactics" -Method Post -ContentType "application/json" -Body (@{
        gameWorldId = $mpWid
        coachId = $c.coachId
        formation = "4-4-2"
        starterPlayerIds = @($cd.preferredStarterIds)
        benchPlayerIds = @($cd.preferredBenchIds)
        idempotencyKey = "mp-tac-$n-$i-r$mr"
        expectedRoundVersion = [int]$curSnap.roundVersion
        saveAsPreferred = $true
      } | ConvertTo-Json)
      if ($tRes.roundStarted -eq $true) {
        $startedOnCoach = $i + 1
      }
    }
    if ($mr -lt 3) {
      $pre = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$mpWid/snapshot" -Method Get
      $g = 0
      while ($pre.roundState -ne "RESULTS" -and $g -lt 25) {
        $g++
        if ($pre.roundState -like "*PAUSED*" -or $pre.roundState -like "*BREAK*" -or $pre.roundState -like "*HALFTIME*") {
          foreach ($c in $coaches) {
            $cRes = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$mpWid/continue" -Method Post -ContentType "application/json" -Body (@{
              gameWorldId = $mpWid
              coachId = $c.coachId
              idempotencyKey = "mp-pre-$n-$mr-$g-$([guid]::NewGuid().ToString('N'))"
            } | ConvertTo-Json)
            $pre = $cRes.snapshot
          }
        } else {
          $pre = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$mpWid/tick?ticks=45" -Method Post
        }
      }
      Invoke-RestMethod -Uri "$baseUrl/api/worlds/$mpWid/next-round" -Method Post | Out-Null
    }
  }

  # Tick to minute 30 and request global pause by Coach 1
  Invoke-RestMethod -Uri "$baseUrl/api/worlds/$mpWid/tick?ticks=30" -Method Post | Out-Null
  Invoke-RestMethod -Uri "$baseUrl/api/worlds/$mpWid/pause" -Method Post -ContentType "application/json" -Body (@{
    gameWorldId = $mpWid
    coachId = $coaches[0].coachId
    idempotencyKey = "mp-pause-$n-c1"
  } | ConvertTo-Json) | Out-Null
  $pauseRound = $mpSnap0.roundNumber
  $mpSnapPaused = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$mpWid/snapshot" -Method Get

  # Coach 1 makes a substitution during global pause
  $c1Match = $mpSnapPaused.matches | Where-Object { $_.homeClubId -eq $coaches[0].clubId -or $_.awayClubId -eq $coaches[0].clubId } | Select-Object -First 1
  $subApplied = $false
  if ($null -ne $c1Match) {
    $isH = ($c1Match.homeClubId -eq $coaches[0].clubId)
    $stList = if ($isH) { $c1Match.homeStarters } else { $c1Match.awayStarters }
    $bnList = if ($isH) { $c1Match.homeBench } else { $c1Match.awayBench }
    $pOut = $stList | Where-Object { $_.position -ne "G" } | Select-Object -First 1
    $pIn = $bnList | Where-Object { $_.position -ne "G" } | Select-Object -First 1
    if ($null -ne $pOut -and $null -ne $pIn) {
      Invoke-RestMethod -Uri "$baseUrl/api/worlds/$mpWid/in-match-adjust" -Method Post -ContentType "application/json" -Body (@{
        gameWorldId = $mpWid
        coachId = $coaches[0].coachId
        matchId = $c1Match.matchId
        newFormation = "4-3-3"
        substitutions = @(@{ playerOutId = $pOut.playerId; playerInId = $pIn.playerId })
        idempotencyKey = "mp-adj-$n-c1"
      } | ConvertTo-Json -Depth 5) | Out-Null
      $subApplied = $true
    }
  }

  # All N coaches confirm continue
  $resumedOnCoach = 0
  for ($i = 0; $i -lt $n; $i++) {
    $c = $coaches[$i]
    $cRes = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$mpWid/continue" -Method Post -ContentType "application/json" -Body (@{
      gameWorldId = $mpWid
      coachId = $c.coachId
      idempotencyKey = "mp-cont-$n-c$i"
    } | ConvertTo-Json)
    if ($cRes.resumed -eq $true) {
      $resumedOnCoach = $i + 1
    }
  }

  # Finish round and advance to Round 2
  $mpSnap = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$mpWid/snapshot" -Method Get
  $guard = 0
  while ($mpSnap.roundState -ne "RESULTS" -and $guard -lt 25) {
    $guard++
    if ($mpSnap.roundState -like "*PAUSED*" -or $mpSnap.roundState -like "*BREAK*" -or $mpSnap.roundState -like "*HALFTIME*") {
      foreach ($c in $coaches) {
        $cRes = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$mpWid/continue" -Method Post -ContentType "application/json" -Body (@{
          gameWorldId = $mpWid
          coachId = $c.coachId
          idempotencyKey = "mp-cont-h-$n-$($c.coachId)-$guard-$([guid]::NewGuid().ToString('N'))"
        } | ConvertTo-Json)
        $mpSnap = $cRes.snapshot
      }
    } else {
      $mpSnap = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$mpWid/tick?ticks=45" -Method Post
    }
  }
  $mpSnapR2 = Invoke-RestMethod -Uri "$baseUrl/api/worlds/$mpWid/next-round" -Method Post

  $mpResults += [ordered]@{
    humanCoaches = $n
    worldId = $mpWid
    validatedRound = $pauseRound
    initialCounterLabel = $initialLabel
    roundStartedOnSubmission = "$startedOnCoach/$n"
    globalPauseState = $mpSnapPaused.roundState
    globalPauseRequestedBy = $mpSnapPaused.activePauseWindow.requestedByCoachName
    substitutionAppliedDuringPause = $subApplied
    resumedOnConfirmation = "$resumedOnCoach/$n"
    advancedToRound = $mpSnapR2.roundNumber
  }
  Write-Host "Validated Multiplayer N=${n}: started on $startedOnCoach/$n, paused=$($mpSnapPaused.roundState), resumed on $resumedOnCoach/$n, advanced to R$($mpSnapR2.roundNumber)"
}

# Save complete playtest + multiplayer bundle for visual QA and documentation
$worldsList = Invoke-RestMethod -Uri "$baseUrl/api/worlds" -Method Get
$bundle = [ordered]@{
  generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
  apiUrl = $baseUrl
  mainPlaytest = [ordered]@{
    coachId = $coachId
    worldId = $worldId
    myClubId = $myClubId
    myClubName = $myClub.name
    marketOperations = $marketOpsLog
    roundsPlayed = 22
    rolledOverToSeason = $snapSeason2R1.seasonNumber
    cupTotalFixtures = $cupEndS1.Count
    roundSummaries = $roundSummaries
  }
  multiplayerValidation = $mpResults
  snapshots = [ordered]@{
    auth = $auth
    worlds = $worldsList
    clubs = $clubs
    clubDetailR1 = $clubDetailAfterOps
    marketR1 = $marketAfterOps
    financesR1 = $financesAfterOps
    snapPrepR1 = $snapPrepR1
    snapRunningR1 = $snapRunningR1
    snapPausedR1 = $snapPausedR1
    snapAfterSubR1 = $snapAfterSubR1
    snapResultsR1 = $snapResultsR1
    standingsDiv1 = $standingsDiv1
    standingsDiv2 = $standingsDiv2
    standingsDiv3 = $standingsDiv3
    standingsDiv4 = $standingsDiv4
    cupEndS1 = $cupEndS1
    calendarEndS1 = $calendarEndS1
    scorersEndS1 = $scorersEndS1
    rankingAfterS1 = $rankingAfterS1
    invitationsAfterS1 = $invitationsAfterS1
    awardsSeason1 = $awardsSeason1
    playerRankingsGeneral = $playerRankingsGeneral
    topPlayerHistory = $topPlayerHistory
    snapSeason2R1 = $snapSeason2R1
    unemployedSnap = $unempSnap
    unemployedInvites = $unempInvites
  }
}

$bundle | ConvertTo-Json -Depth 12 | Set-Content -Path "$outDir\live-playtest-bundle-011.json" -Encoding UTF8
Write-Host "=== SAVED live-playtest-bundle-011.json SUCCESSFULLY ==="
