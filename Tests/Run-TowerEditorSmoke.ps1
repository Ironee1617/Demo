param(
    [ValidateSet('All', 'Companions', 'Combat', 'Boss')][string]$Suite = 'All',
    [string]$UnityCli,
    [int]$TimeoutSeconds = 300
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if (!$UnityCli) { $UnityCli = (Get-Command unity -ErrorAction Stop).Source }
$reportRoot = Join-Path $projectRoot 'Artifacts/TowerEditor'
[IO.Directory]::CreateDirectory($reportRoot) | Out-Null
function Invoke-Cli([string[]]$Arguments) {
    $output = & $UnityCli @Arguments
    $exitCode = $LASTEXITCODE
    $response = $output | ConvertFrom-Json
    if ($exitCode -ne 0 -or !$response.success) { throw "Unity CLI failed: $($Arguments[0]): $($response.errors.message -join '; ')" }
    return $response.data
}
function Invoke-Editor([string]$Command, [string[]]$Arguments = @()) {
    if ($Command -eq 'eval') { $Arguments += @('--', '--timeout', '60000') }
    $requestArguments = @('command', '--project-path', $projectRoot, '--timeout', '90', '--json', $Command) + $Arguments
    return (Invoke-Cli $requestArguments).result
}
function Wait-Ready {
    Invoke-Cli @('status', '--project-path', $projectRoot, '--until-ready', '--timeout', '60', '--json') | Out-Null
}
function CSharp-Path([string]$Path) { return '@"' + $Path.Replace('"', '""') + '"' }
Wait-Ready
$initial = Invoke-Editor 'editor_status'
if ($initial.playMode -ne 'stopped') { throw 'Stop the current play session before running isolated tower scenarios.' }
$initialScenes = Invoke-Editor 'list_open_scenes'
if (@($initialScenes.scenes | Where-Object { $_.isActive -and $_.path -eq 'Assets/Demos/Roguelike/20_Roguelike.unity' }).Count -ne 1) {
    throw 'Open the 20_Roguelike scene before running these scenarios.'
}
$suites = if ($Suite -eq 'All') { @('Companions', 'Combat', 'Boss') } else { @($Suite) }
foreach ($mode in $suites) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $prefix = Join-Path $reportRoot ($mode.ToLowerInvariant() + '-' + $stamp)
    $reportFile = $prefix + '-report.json'
    $className = 'Tower' + $(if ($mode -eq 'Companions') { 'Companion' } else { $mode }) + 'SmokeDriver'
    try {
        Invoke-Editor 'editor_play' | Out-Null
        Wait-Ready
        $extra = if ($mode -eq 'Companions') { '' } else { ', false' }
        $code = 'var game = UnityEngine.Object.FindFirstObjectByType<PrototypeLab.Roguelike.TowerGame>(); if (game == null) throw new System.InvalidOperationException("TowerGame is missing."); var capture = game.GetComponent<PrototypeLab.Roguelike.TowerSmokeDriver>(); var driver = game.gameObject.AddComponent<PrototypeLab.Roguelike.' + $className + '>(); driver.Initialize(game, capture, ' + (CSharp-Path $prefix) + ', ' + (CSharp-Path $reportFile) + $extra + '); return true;'
        $started = Invoke-Editor 'eval' @($code)
        if (!$started.success) { throw "Could not start $mode scenario." }
        Write-Host "Started $mode via Unity CLI; report: $reportFile"
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        while (!(Test-Path -LiteralPath $reportFile)) {
            if ((Get-Date) -ge $deadline) { throw "$mode scenario exceeded $TimeoutSeconds seconds." }
            Start-Sleep -Milliseconds 500
        }
        # The editor writes the file synchronously; allow the handle to close before reading.
        Start-Sleep -Milliseconds 100
        $report = Get-Content -LiteralPath $reportFile -Raw | ConvertFrom-Json
        if (!$report.success -or @($report.errors).Count -gt 0) {
            $report.errors | ForEach-Object { Write-Host $_ }
            throw "$mode failed. See $reportFile"
        }
        Write-Host "PASS $mode; errors=0"
    } finally {
        Invoke-Editor 'editor_stop' | Out-Null
        Wait-Ready
    }
}
$finalScenes = Invoke-Editor 'list_open_scenes'
if (($initialScenes | ConvertTo-Json -Depth 5 -Compress) -ne ($finalScenes | ConvertTo-Json -Depth 5 -Compress)) {
    throw 'Authoring scene state changed during the scenarios.'
}
Write-Host 'TOWER_EDITOR_SMOKE_OK original authoring scene restored'
