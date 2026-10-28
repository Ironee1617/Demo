param([string]$UnityCli, [ValidateRange(30, 300)][int]$TimeoutSeconds = 180)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if (!$UnityCli) { $UnityCli = (Get-Command unity -ErrorAction Stop).Source }
# Windows PowerShell 5.1 (and 7.0-7.2) pass native arguments without escaping embedded quotes,
# which splits eval code at its string literals. PowerShell 7.3+ escapes them itself.
$legacyNativeArguments = (Get-Variable PSNativeCommandArgumentPassing -ValueOnly -ErrorAction SilentlyContinue) -in @($null, 'Legacy')
function Invoke-Cli([string[]]$Arguments) {
    if ($legacyNativeArguments) { $Arguments = @($Arguments | ForEach-Object { [regex]::Replace($_, '(\\*)"', '$1$1\"') }) }
    $output = & $UnityCli @Arguments
    $exitCode = $LASTEXITCODE
    $response = $output | ConvertFrom-Json
    if ($exitCode -ne 0 -or !$response.success) { throw "Unity CLI failed: $($Arguments[0]): $($response.errors.message -join '; ')" }
    return $response.data
}
function Invoke-Editor([string]$Command, [string[]]$Arguments = @()) {
    if ($Command -eq 'eval') { $Arguments += @('--', '--timeout', '60000') }
    return (Invoke-Cli (@('command', '--project-path', $projectRoot, '--timeout', '90', '--json', $Command) + $Arguments)).result
}
function Wait-Ready { Invoke-Cli @('status', '--project-path', $projectRoot, '--until-ready', '--timeout', '60', '--json') | Out-Null }
Wait-Ready
if ((Invoke-Editor 'editor_status').playMode -ne 'stopped') { throw 'Stop the current play session before running the music scenario.' }
$initialScenes = Invoke-Editor 'list_open_scenes'
if (@($initialScenes.scenes | Where-Object { $_.isActive -and $_.path -eq 'Assets/Demos/Roguelike/20_Roguelike.unity' }).Count -ne 1) {
    throw 'Open the 20_Roguelike scene before running the music scenario.'
}
$reportRoot = Join-Path $projectRoot 'Artifacts/TowerEditor/Music'
[IO.Directory]::CreateDirectory($reportRoot) | Out-Null
$reportFile = Join-Path $reportRoot ('music-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-report.json')
try {
    Invoke-Editor 'editor_play' | Out-Null
    $playDeadline = (Get-Date).AddSeconds(60)
    do {
        Start-Sleep -Milliseconds 500
        Wait-Ready
        $playing = (Invoke-Editor 'editor_status').playMode -eq 'playing'
        if ((Get-Date) -ge $playDeadline) { throw 'The editor did not finish entering Play mode.' }
    } until ($playing)
    $code = 'var game = UnityEngine.Object.FindFirstObjectByType<PrototypeLab.Roguelike.TowerGame>(); if (game == null) throw new System.InvalidOperationException("TowerGame is missing."); game.gameObject.AddComponent<PrototypeLab.Roguelike.TowerMusicSmokeDriver>().Initialize(game, @"' + $reportFile.Replace('"', '""') + '"); return true;'
    if (!(Invoke-Editor 'eval' @($code)).success) { throw 'Could not start the music scenario.' }
    Write-Host "Started music smoke; report: $reportFile"
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while (!(Test-Path -LiteralPath $reportFile)) {
        if ((Get-Date) -ge $deadline) { throw "Music scenario exceeded $TimeoutSeconds seconds." }
        Start-Sleep -Milliseconds 500
    }
    Start-Sleep -Milliseconds 100
    $report = Get-Content -LiteralPath $reportFile -Raw | ConvertFrom-Json
    if (!$report.success -or @($report.errors).Count -gt 0 -or $report.tracks -ne 10) {
        $report.errors | Write-Host
        throw "Music smoke failed. See $reportFile"
    }
    foreach ($check in @('stereoStreaming', 'playbackAdvances', 'sameThemeContinuous', 'themeBoundaryCrossfades', 'loopWraps', 'disablePauses', 'reenableResumes', 'lifecycleCleanup', 'soundEffectsIndependent')) {
        if (!$report.$check) { throw "Music smoke did not verify $check." }
    }
} finally {
    Invoke-Editor 'editor_stop' | Out-Null
    Wait-Ready
}
if (($initialScenes | ConvertTo-Json -Depth 5 -Compress) -ne ((Invoke-Editor 'list_open_scenes') | ConvertTo-Json -Depth 5 -Compress)) {
    throw 'Authoring scene state changed during music validation.'
}
Write-Host "TOWER_MUSIC_SMOKE_OK $reportFile"
