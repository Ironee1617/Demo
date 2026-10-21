param([string]$Executable, [switch]$ArtOnly, [switch]$CombatOnly, [switch]$BossOnly, [switch]$MythOnly, [switch]$CompanionsOnly, [switch]$PolishOnly, [int]$TimeoutSeconds = 300)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if (!$Executable) { $Executable = Join-Path $projectRoot 'Build/TowerDemo/TowerDemo.exe' }
$Executable = [IO.Path]::GetFullPath($Executable)
if (!(Test-Path -LiteralPath $Executable)) { throw "Build the tower demo first: $Executable" }
$reportRoot = Join-Path $projectRoot 'Artifacts/TowerDemo'
[IO.Directory]::CreateDirectory($reportRoot) | Out-Null
if (@(@($ArtOnly, $CombatOnly, $BossOnly, $MythOnly, $CompanionsOnly, $PolishOnly) | Where-Object { $_ }).Count -gt 1) { throw 'Choose one isolated smoke suite per player process.' }
$mode = if ($ArtOnly) { 'art' } elseif ($CombatOnly) { 'combat' } elseif ($BossOnly) { 'boss' } elseif ($MythOnly) { 'myth' } elseif ($CompanionsOnly) { 'companions' } elseif ($PolishOnly) { 'polish' } else { 'playtest' }
$prefix = Join-Path $reportRoot $mode
$reportPath = Join-Path $reportRoot ($mode + '-report.json')
$logName = if ($ArtOnly) { 'art-player.log' } elseif ($CombatOnly) { 'combat-player.log' } elseif ($BossOnly) { 'boss-player.log' } elseif ($MythOnly) { 'myth-player.log' } elseif ($CompanionsOnly) { 'companions-player.log' } elseif ($PolishOnly) { 'polish-player.log' } else { 'player.log' }
$logPath = Join-Path $reportRoot $logName
if ($ArtOnly) {
    $pairs = [Collections.Generic.List[object]]::new()
    $runtimeRoot = Join-Path $projectRoot 'Assets/Demos/Roguelike/Resources/Tower/Actors'
    $sourceRoot = Join-Path $projectRoot 'Assets/Demos/Roguelike/RPGAsset/spine'
    foreach ($png in Get-ChildItem -LiteralPath $runtimeRoot -Recurse -File -Filter '*.png') {
        $relative = $png.FullName.Substring($runtimeRoot.Length).TrimStart([char[]]'\/')
        $pairs.Add(@{ resource = $png.FullName; source = (Join-Path $sourceRoot $relative) })
    }
    for ($frame = 0; $frame -lt 7; $frame++) {
        $pairs.Add(@{ resource = (Join-Path $projectRoot "Assets/Demos/Roguelike/Resources/Tower/Effects/slash_$frame.png"); source = (Join-Path $projectRoot "Assets/Demos/Roguelike/RPGAsset/cha/하뚝/eff/skill$frame.png") })
    }
    $pairs.Add(@{ resource = (Join-Path $projectRoot 'Assets/Demos/Roguelike/Resources/Tower/Effects/swirl.png'); source = (Join-Path $projectRoot 'Assets/Demos/Roguelike/RPGAsset/cha/하뚝/images/eff0.png') })
    $pairs.Add(@{ resource = (Join-Path $projectRoot 'Assets/Demos/Roguelike/Resources/Tower/Effects/arrow.png'); source = (Join-Path $projectRoot 'Assets/Demos/Roguelike/RPGAsset/spine/cha_014/arrow.png') })
    $monsterSources = @('Stage_0_0.png', 'Stage_0_1.png', 'Stage_0_2.png', 'Stage_1_Boss.png', 'Stage_0_Boss.png')
    for ($monster = 0; $monster -lt $monsterSources.Count; $monster++) {
        $pairs.Add(@{ resource = (Join-Path $projectRoot "Assets/Demos/Roguelike/Resources/Tower/Monsters/monster_$monster.png"); source = (Join-Path $projectRoot ("Assets/Demos/Roguelike/RPGAsset/mobIcon/" + $monsterSources[$monster])) })
    }
    $hashes = foreach ($pair in $pairs) {
        if (!(Test-Path -LiteralPath $pair.source) -or !(Test-Path -LiteralPath $pair.resource)) { throw "Original art mapping missing: $($pair.resource)" }
        $sourceHash = (Get-FileHash -LiteralPath $pair.source).Hash
        $resourceHash = (Get-FileHash -LiteralPath $pair.resource).Hash
        if ($sourceHash -ne $resourceHash) { throw "Original art changed: $($pair.resource)" }
        [ordered]@{ source = $pair.source; resource = $pair.resource; sha256 = $sourceHash }
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zipPath = Join-Path $projectRoot 'Assets/Demos/Roguelike/RPGAsset/RPGAsset/RPG_Buildings_CASTLE_v1.0.zip'
    $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $environment = @{ CastleInterior = 'castle_interriors.png'; CastleDecor = 'castle_decoratives.png'; Torch = 'Anim/torch.png' }
        foreach ($name in $environment.Keys) {
            $entries = @($zip.Entries | Where-Object { $_.FullName.EndsWith($environment[$name], [StringComparison]::OrdinalIgnoreCase) })
            if ($entries.Count -ne 1) { throw "Castle source ZIP entry is ambiguous or missing: $name" }
            $stream = $entries[0].Open(); $sha = [Security.Cryptography.SHA256]::Create()
            try { $sourceHash = -join ($sha.ComputeHash($stream) | ForEach-Object { $_.ToString('X2') }) }
            finally { $stream.Dispose(); $sha.Dispose() }
            $resource = Join-Path $projectRoot ("Assets/Demos/Roguelike/Resources/Tower/Environment/$name.png")
            if ($sourceHash -ne (Get-FileHash -LiteralPath $resource).Hash) { throw "Original castle pixels changed: $name" }
            $hashes += [ordered]@{ source = ($zipPath + ':' + $entries[0].FullName); resource = $resource; sha256 = $sourceHash }
        }
    } finally { $zip.Dispose() }
    $artSourceReport = [ordered]@{ checkedAt = (Get-Date).ToString('o'); success = $true; preservedPixelFiles = @($hashes).Count; files = @($hashes) }
    [IO.File]::WriteAllText((Join-Path $reportRoot 'source-art-report.json'), ($artSourceReport | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
}
$arguments = @('-force-d3d11', '-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '900', '-logFile', ('"' + $logPath + '"'))
if ($ArtOnly) { $arguments += @('-towerArtSmoke', '-towerArtCapture', ('"' + $prefix + '"'), '-towerArtReport', ('"' + $reportPath + '"')) }
elseif ($CombatOnly) { $arguments += @('-towerCombatSmoke', '-towerCombatCapture', ('"' + $prefix + '"'), '-towerCombatReport', ('"' + $reportPath + '"')) }
elseif ($BossOnly) { $arguments += @('-towerBossSmoke', '-towerBossCapture', ('"' + $prefix + '"'), '-towerBossReport', ('"' + $reportPath + '"')) }
elseif ($MythOnly) { $arguments += @('-towerMythSmoke', '-towerMythCapture', ('"' + $prefix + '"'), '-towerMythReport', ('"' + $reportPath + '"')) }
elseif ($CompanionsOnly) { $arguments += @('-towerCompanionSmoke', '-towerCompanionCapture', ('"' + $prefix + '"'), '-towerCompanionReport', ('"' + $reportPath + '"')) }
elseif ($PolishOnly) { $arguments += @('-towerPolishSmoke', '-towerPolishCapture', ('"' + $prefix + '"'), '-towerPolishReport', ('"' + $reportPath + '"')) }
else { $arguments += @('-towerSmoke', '-towerCapture', ('"' + $prefix + '"'), '-towerReport', ('"' + $reportPath + '"')) }
$startedAt = Get-Date
$process = Start-Process -FilePath $Executable -ArgumentList $arguments -WorkingDirectory (Split-Path -Parent $Executable) -WindowStyle Hidden -PassThru
Write-Host "Tower $mode smoke PID $($process.Id); log: $logPath"
if (!$process.WaitForExit($TimeoutSeconds * 1000)) { Stop-Process -Id $process.Id -Force; throw "Tower smoke exceeded $TimeoutSeconds seconds. See $logPath" }
$process.Refresh()
if (!(Test-Path -LiteralPath $reportPath) -or (Get-Item -LiteralPath $reportPath).LastWriteTime -lt $startedAt) { throw "Tower smoke did not write a fresh report. See $logPath" }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if ($process.ExitCode -ne 0 -or !$report.success) {
    if ($report.errors) { $report.errors | Write-Host }
    throw "Tower $mode smoke failed (exit $($process.ExitCode)). See $reportPath"
}
if ($CombatOnly) {
    foreach ($check in @('companionTargetExcluded', 'companionMeleeImmune', 'companionProjectilePassThrough', 'companionBossAreaImmune', 'companionStatusImmune', 'companionsStillAttack')) {
        if (!$report.$check) { throw "Companion hit immunity check was not verified: $check" }
    }
}
$expectedCaptures = if ($ArtOnly) { 15 } elseif ($CompanionsOnly) { 7 } elseif ($PolishOnly) { 3 } else { 6 }
if ($report.screenshots.Count -ne $expectedCaptures) { throw "Tower smoke must capture all $expectedCaptures required screens." }
Add-Type -AssemblyName System.Drawing
foreach ($screenshot in $report.screenshots) {
    if (!(Test-Path -LiteralPath $screenshot) -or (Get-Item -LiteralPath $screenshot).Length -le 0) { throw "Missing screenshot: $screenshot" }
    $bitmap = [Drawing.Bitmap]::new($screenshot)
    try {
        $colors = [Collections.Generic.HashSet[int]]::new()
        for ($x = 1; $x -lt 100; $x++) { for ($y = 1; $y -lt 60; $y++) { $null = $colors.Add($bitmap.GetPixel([int]($bitmap.Width * $x / 100), [int]($bitmap.Height * $y / 60)).ToArgb()) } }
        if ($colors.Count -lt 8) { throw "Screenshot is blank or uniform: $screenshot" }
    } finally { $bitmap.Dispose() }
}
Write-Host "TOWER_SMOKE_OK $reportPath"
if ($ArtOnly) { Write-Host 'Verified original pixel copies,51 character profiles,357 motion samples,16 VFX playbacks and cleanup.' }
elseif ($CombatOnly) { Write-Host 'Verified formation, companion hit immunity and projectile pass-through, all-role flying targets, melee platform climbing, solo flyers, locked-aim patterns, actual jump/dash evasion, swept shots and stable camera.' }
elseif ($BossOnly) { Write-Host 'Verified floor scaling, boss cadence, exclusive patterns, original motions, actual control combat and one reward after every enemy dies.' }
elseif ($MythOnly) { Write-Host 'Verified selection guards,10 atlases,50 portraits,300 visible state frames, actual floor points, purchases and untouched saved account.' }
elseif ($CompanionsOnly) { Write-Host 'Verified all50 companion basic attacks and200 owned skills through actual Update, timing, flying-target reach, upper-platform formation and dismissal.' }
elseif ($PolishOnly) { Write-Host 'Verified dodge counters, pause/expiry, flawless records, clear safety and run resets.' }
else { Write-Host 'Verified combat, mixed rewards, learned skills, contact/release timing, hero stats, dismissal and completion.' }
