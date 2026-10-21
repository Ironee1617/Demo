param([string]$Executable, [string]$ReportDirectory, [switch]$Baseline, [ValidateRange(10, 600)][int]$TimeoutSeconds = 300)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if (!$Executable) { $Executable = Join-Path $projectRoot 'Build/TowerMyths/TowerMyths.exe' }
$Executable = [IO.Path]::GetFullPath($Executable)
if (!(Test-Path -LiteralPath $Executable)) { throw "Build the tower game first: $Executable" }
$reportRoot = if ($ReportDirectory) { [IO.Path]::GetFullPath($ReportDirectory) } else { Join-Path $projectRoot '.validation/AnimationQA' }
[IO.Directory]::CreateDirectory($reportRoot) | Out-Null
$reportPath = Join-Path $reportRoot 'animation-report.json'
$logPath = Join-Path $reportRoot 'animation-player.log'
$arguments = @('-force-d3d11', '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720', '-logFile', ('"' + $logPath + '"'), '-towerAnimationSmoke', '-towerAnimationCapture', ('"' + $reportRoot + '"'), '-towerAnimationReport', ('"' + $reportPath + '"'))
if ($Baseline) { $arguments += '-towerAnimationBaseline' }
$startedAt = Get-Date
$process = Start-Process -FilePath $Executable -ArgumentList $arguments -WorkingDirectory (Split-Path -Parent $Executable) -WindowStyle Hidden -PassThru
Write-Host "Tower animation smoke PID $($process.Id); log: $logPath"
if (!$process.WaitForExit($TimeoutSeconds * 1000)) { Stop-Process -Id $process.Id -Force; throw "Animation smoke exceeded $TimeoutSeconds seconds. See $logPath" }
$process.Refresh()
if (!(Test-Path -LiteralPath $reportPath) -or (Get-Item -LiteralPath $reportPath).LastWriteTime -lt $startedAt) { throw "Animation smoke did not write a fresh report. See $logPath" }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if ($process.ExitCode -ne 0 -or !$report.success) { $report.failures | Write-Host; $report.errors | Write-Host; throw "Animation smoke failed (exit $($process.ExitCode)). See $reportPath" }
if ($report.probeContract -eq 'non-eden-rendered-foot-surfaces-v4') {
    if ($report.characters.Count -ne 7 -or $report.sampledFrames -ne 240 -or $report.screenshots.Count -ne 240 -or $report.previewFrames -ne 180 -or $report.runScreenshots.Count -ne 180) { throw 'Animation smoke did not render all seven actors across the four-second action and three-second speed clips.' }
    if (!$Baseline -and ($report.catalogCharacters -ne 45 -or $report.catalogMotionSamples -ne 30780 -or $report.groundedCharacters -ne 44 -or $report.hoveringCharacters -ne 1 -or $report.gaitWarmupFrames -ne 45 -or $report.gaitMeasuredFrames -ne 120)) { throw 'Animation smoke did not validate all 45 mesh companions. Run -towerEdenReview separately for the five Eden pixel actors.' }
    if (!$Baseline) {
        foreach ($actor in $report.catalog) {
            if ($actor.gaits.Count -ne 3 -or $actor.motionSamples -ne 684) { throw "Incomplete behavioral coverage: $($actor.name)" }
            if (!$actor.hovering) {
                if (!$actor.rootMatchedGait -or !$actor.groundedFeet -or !$actor.continuousActionSteps -or !$actor.rigidShoes -or $actor.leftFootProbeCount -lt 4 -or $actor.rightFootProbeCount -lt 4) { throw "Actual rendered shoe/contact regression: $($actor.name)" }
                foreach ($gait in $actor.gaits) {
                    if ($gait.measuredFrames -ne 120 -or !$gait.groundedContacts -or !$gait.walkingSupport -or !$gait.alternatingStride -or !$gait.rigidShoes) { throw "Incomplete rendered foot-surface coverage: $($actor.name), speed $($gait.worldSpeed)" }
                    foreach ($foot in @($gait.leftFoot, $gait.rightFoot)) {
                        if ($foot.sampledFrames -ne 120 -or $foot.stanceFrames -lt 12 -or $foot.stancePairs -lt 8 -or !$foot.contactMaintained -or !$foot.rootMatched -or !$foot.rigidShoe) { throw "Per-foot stance or shoe-shape regression: $($actor.name), speed $($gait.worldSpeed)" }
                    }
                }
                foreach ($foot in @($actor.leftActionFoot, $actor.rightActionFoot)) {
                    if (!$foot.contactMaintained -or !$foot.rootMatched -or !$foot.rigidShoe) { throw "Per-foot action contact regression: $($actor.name)" }
                }
            }
        }
        if ($report.stopProbeContract -ne 'planted-stop-surfaces-v1' -or $report.stopCharacters -ne 45 -or $report.stopSamples -ne 3240 -or $report.stopWarmupFrames -ne 45 -or $report.stopMeasuredFrames -ne 24 -or $report.stopping.Count -ne 45) { throw 'Animation smoke did not validate stop frames at three speeds for all 45 mesh companions.' }
        $stopIds = @($report.stopping | ForEach-Object { [int]$_.artIndex } | Sort-Object -Unique)
        if (($stopIds -join ',') -ne ((105..149) -join ',')) { throw 'Stop-contact coverage has missing or duplicate mesh companion identities.' }
        foreach ($actor in $report.stopping) {
            if (!$actor.passed -or $actor.stopSamples -ne 72 -or $actor.speeds.Count -ne 3) { throw "Incomplete stopped-foot coverage: $($actor.name)" }
            if ((@($actor.speeds | ForEach-Object { [double]$_.worldSpeed } | Sort-Object) -join ',') -ne '1.5,3,5') { throw "Missing stop approach speed: $($actor.name)" }
            foreach ($stop in $actor.speeds) {
                if (!$stop.passed -or !$stop.finiteGeometry -or $stop.sampledFrames -ne 24) { throw "Stopping regression: $($actor.name), speed $($stop.worldSpeed)" }
                if (!$actor.hovering) {
                    foreach ($foot in @($stop.leftFoot, $stop.rightFoot)) {
                        if ($foot.sampledFrames -ne 24 -or !$foot.settled -or !$foot.stationary -or !$foot.rigidShoe -or $foot.landingFrame -lt 0 -or $foot.landingFrame -gt 15) { throw "Painted shoe slides, floats, or fails to settle after stopping: $($actor.name), speed $($stop.worldSpeed)" }
                        if ($foot.initiallyPlanted -and ($foot.landingFrame -ne 0 -or $foot.lockedFrames -ne 24)) { throw "Initially planted shoe was not checked from the first stop frame: $($actor.name)" }
                    }
                }
            }
        }
    }
    if ($report.transitions.Count -ne 61) { throw 'Animation smoke did not exercise all sixteen original rigs and 45 companion rigs across locomotion transitions.' }
    foreach ($actor in $report.transitions) {
        if ($actor.sampledTransitions -ne 9) { throw "Incomplete start/stop/jump/apex/landing/dash/hurt/turn coverage: $($actor.name)" }
        if (!$Baseline -and (!$actor.continuous -or !$actor.pausedPose)) { throw "Locomotion transition regression: $($actor.name), $($actor.largestTransition)" }
    }
} elseif (!$Baseline) { throw 'The player lacks the common-floor heel/toe, per-foot stance and rigid-shoe tests; rebuild before validating the new models.' }
foreach ($screenshot in @($report.screenshots) + @($report.runScreenshots)) {
    if (!(Test-Path -LiteralPath $screenshot) -or (Get-Item -LiteralPath $screenshot).LastWriteTime -lt $startedAt -or (Get-Item -LiteralPath $screenshot).Length -le 0) { throw "Missing fresh frame: $screenshot" }
}
if ($Baseline) { $report.failures | Write-Host; Write-Host "TOWER_ANIMATION_BASELINE_OK $reportPath" }
else { Write-Host "TOWER_ANIMATION_SMOKE_OK $reportPath" }
