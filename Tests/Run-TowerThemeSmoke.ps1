param([string]$Executable, [ValidateRange(10, 300)][int]$TimeoutSeconds = 210, [string]$ReportDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if (!$Executable) { $Executable = Join-Path $projectRoot 'Build/TowerDemo/TowerDemo.exe' }
$Executable = [IO.Path]::GetFullPath($Executable)
if (!(Test-Path -LiteralPath $Executable)) { throw "Build the tower demo first: $Executable" }
$reportRoot = if ($ReportDirectory) { [IO.Path]::GetFullPath($ReportDirectory) } else { Join-Path $projectRoot 'Artifacts/TowerEditor/ThemeArt' }
[IO.Directory]::CreateDirectory($reportRoot) | Out-Null
$prefix = Join-Path $reportRoot 'themes'
$reportPath = Join-Path $reportRoot 'themes-report.json'
$logPath = Join-Path $reportRoot 'themes-player.log'
$arguments = @('-force-d3d11', '-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '900', '-logFile', ('"' + $logPath + '"'), '-towerThemeSmoke', '-towerThemeCapture', ('"' + $prefix + '"'), '-towerThemeReport', ('"' + $reportPath + '"'))
$startedAt = Get-Date
$process = Start-Process -FilePath $Executable -ArgumentList $arguments -WorkingDirectory (Split-Path -Parent $Executable) -WindowStyle Hidden -PassThru
Write-Host "Tower theme smoke PID $($process.Id); log: $logPath"
if (!$process.WaitForExit($TimeoutSeconds * 1000)) { Stop-Process -Id $process.Id -Force; throw "Theme smoke exceeded $TimeoutSeconds seconds. See $logPath" }
$process.Refresh()
if (!(Test-Path -LiteralPath $reportPath) -or (Get-Item -LiteralPath $reportPath).LastWriteTime -lt $startedAt) { throw "Theme smoke did not write a fresh report. See $logPath" }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if ($process.ExitCode -ne 0 -or !$report.success) { $report.errors | Write-Host; throw "Theme smoke failed (exit $($process.ExitCode)). See $reportPath" }
if ($report.sampledThemes -ne 7 -or $report.sampledSpecies -ne 49 -or $report.distinctPixelHashes -ne 49 -or $report.bitmapAtlases -ne 7 -or $report.screenshots.Count -ne 14) { throw 'Theme smoke did not cover all seven bitmap maps, forty-nine creatures, and fourteen gameplay/roster captures.' }
foreach ($check in @('bitmapCreaturesVerified', 'riggedCreaturesVerified', 'bitmapBackgroundsVerified', 'platformEdgesPreserved', 'noPreparationOverlays', 'rosterGalleryRendered', 'galleryFramingVerified')) {
    if (!$report.$check) { throw "Theme art verification is missing: $check" }
}
Add-Type -AssemblyName System.Drawing
foreach ($screenshot in $report.screenshots) {
    if (!(Test-Path -LiteralPath $screenshot) -or (Get-Item -LiteralPath $screenshot).LastWriteTime -lt $startedAt) { throw "Missing fresh screenshot: $screenshot" }
    $bitmap = [Drawing.Bitmap]::new($screenshot)
    try {
        $colors = [Collections.Generic.HashSet[int]]::new()
        for ($x = 1; $x -lt 80; $x++) { for ($y = 1; $y -lt 45; $y++) { $null = $colors.Add($bitmap.GetPixel([int]($bitmap.Width * $x / 80), [int]($bitmap.Height * $y / 45)).ToArgb()) } }
        if ($colors.Count -lt 20) { throw "Theme screenshot is blank or uniform: $screenshot" }
    } finally { $bitmap.Dispose() }
}
Write-Host "TOWER_THEME_SMOKE_OK $reportPath"
