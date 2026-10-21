param(
    [string]$Executable,
    [int]$Width = 1600,
    [int]$Height = 900
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (!$Executable) { $Executable = Join-Path $projectRoot 'Build/TowerUIRefresh/TowerUIRefresh.exe' }
$prefix = Join-Path $projectRoot ("Artifacts/TowerUIRefresh/ui-${Width}x${Height}-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
[IO.Directory]::CreateDirectory((Split-Path -Parent $prefix)) | Out-Null
$arguments = @('-screen-width', $Width, '-screen-height', $Height, '-screen-fullscreen', '0',
    '-towerUIReview', '-towerUICapture', ('"' + $prefix + '"'), '-logFile', ('"' + $prefix + '.log"'))
$reviewProcess = Start-Process -FilePath $Executable -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Host "UI capture: $prefix"
if (!$reviewProcess.WaitForExit(180000)) {
    Stop-Process -Id $reviewProcess.Id -Force
    throw 'UI capture exceeded 180 seconds.'
}
$reviewProcess.Refresh()
$report = Get-Content -LiteralPath ($prefix + '-report.json') -Raw | ConvertFrom-Json
if ($reviewProcess.ExitCode -ne 0 -or !$report.success) {
    $report.errors | ForEach-Object { Write-Host $_ }
    throw 'UI review failed. Inspect the capture log.'
}
Write-Host ("PASS UI review; " + $report.screenshots.Count + " screens; errors=0")
