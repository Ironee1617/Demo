param(
    [string]$Executable,
    [string]$ReportDirectory,
    [int]$TimeoutSeconds = 120
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if (!$Executable) { $Executable = Join-Path $projectRoot 'Build/TowerEdenBalanced/TowerEdenBalanced.exe' }
if (!$ReportDirectory) { $ReportDirectory = Join-Path $projectRoot ('Artifacts/TowerEdenBalanced/review-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$Executable = [IO.Path]::GetFullPath($Executable)
$ReportDirectory = [IO.Path]::GetFullPath($ReportDirectory)
[IO.Directory]::CreateDirectory($ReportDirectory) | Out-Null
$log = Join-Path $ReportDirectory 'player.log'
$startedAt = Get-Date
$arguments = @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-towerEdenReview',
    '-towerEdenCapture',('"' + $ReportDirectory + '"'),'-logFile',('"' + $log + '"'))
$process = Start-Process -FilePath $Executable -ArgumentList $arguments -WorkingDirectory (Split-Path -Parent $Executable) -WindowStyle Hidden -PassThru
Write-Host "Eden review PID $($process.Id): $ReportDirectory"
if (!$process.WaitForExit($TimeoutSeconds * 1000)) { Stop-Process -Id $process.Id -Force; throw "Eden review exceeded $TimeoutSeconds seconds. See $log" }
$process.Refresh()
$reportPath = Join-Path $ReportDirectory 'eden-report.json'
if (!(Test-Path -LiteralPath $reportPath) -or (Get-Item -LiteralPath $reportPath).LastWriteTime -lt $startedAt) { throw "Eden review did not write a fresh report. See $log" }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if ($process.ExitCode -ne 0 -or !$report.success -or $report.characters -ne 50 -or $report.distinctPoses -ne 550 -or $report.animationFrames -ne 144) {
    $report.errors | Write-Host
    throw "Eden review failed. See $reportPath"
}
foreach ($image in $report.screenshots) {
    if (!(Test-Path -LiteralPath $image) -or (Get-Item -LiteralPath $image).Length -eq 0) { throw "Missing animation capture: $image" }
}
Write-Host "TOWER_EDEN_REVIEW_OK $reportPath"
