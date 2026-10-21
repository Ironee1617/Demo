param(
    [string]$Executable,
    [string]$ReportDirectory,
    [switch]$NoAnimation,
    [ValidateRange(10, 900)][int]$TimeoutSeconds = 360
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if (!$Executable) { $Executable = Join-Path $projectRoot 'Build/TowerSkyRig/TowerSkyRig.exe' }
$Executable = [IO.Path]::GetFullPath($Executable)
if (!(Test-Path -LiteralPath $Executable -PathType Leaf)) { throw "Build the tower game first: $Executable" }
$reportRoot = if ($ReportDirectory) { [IO.Path]::GetFullPath($ReportDirectory) } else { Join-Path $projectRoot '.validation/MonsterRigQA' }
[IO.Directory]::CreateDirectory($reportRoot) | Out-Null
$prefix = Join-Path $reportRoot 'monster-rigs'
$reportPath = Join-Path $reportRoot 'monster-rigs-report.json'
$logPath = Join-Path $reportRoot 'monster-rigs-player.log'
$arguments = @('-force-d3d11', '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720',
    '-logFile', ('"' + $logPath + '"'), '-towerMonsterRigSmoke',
    '-towerMonsterRigCapture', ('"' + $prefix + '"'), '-towerMonsterRigReport', ('"' + $reportPath + '"'))
if ($NoAnimation) { $arguments += '-towerMonsterRigNoAnimation' }
$startedAt = Get-Date
$process = Start-Process -FilePath $Executable -ArgumentList $arguments -WorkingDirectory (Split-Path -Parent $Executable) -WindowStyle Hidden -PassThru
Write-Host "Tower monster rig smoke PID $($process.Id); log: $logPath"
if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
    Stop-Process -Id $process.Id -Force
    throw "Monster rig smoke exceeded $TimeoutSeconds seconds. See $logPath"
}
$process.Refresh()
if (!(Test-Path -LiteralPath $reportPath) -or (Get-Item -LiteralPath $reportPath).LastWriteTime -lt $startedAt) {
    throw "Monster rig smoke did not write a fresh report. See $logPath"
}
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if ($process.ExitCode -ne 0 -or !$report.success -or $report.errors.Count -gt 0) {
    $report.errors | Write-Host
    throw "Monster rig smoke failed (exit $($process.ExitCode)). See $reportPath"
}
if ($report.species -ne 49 -or $report.skeletons -ne 49 -or $report.bonesPerSkeleton -ne 14 -or $report.weightedBones -lt 196 -or $report.samples.Count -ne 49) {
    throw 'Monster rig smoke did not inspect all forty-nine species with fourteen bones and at least four weighted bones per creature.'
}
if ($report.sampledFrames -lt 16226 -or $report.screenshots.Count -ne 3) {
    throw 'Monster rig smoke did not sample all standard and boss motions or capture all three full-catalog poses.'
}
$expectedAnimationFrames = if ($NoAnimation) { 0 } else { 72 }
if ($report.animationFrames.Count -ne $expectedAnimationFrames) {
    throw "Monster rig smoke expected $expectedAnimationFrames animation captures; found $($report.animationFrames.Count). Rebuild if the executable predates -towerMonsterRigNoAnimation."
}
Add-Type -AssemblyName System.Drawing
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($screenshot in @($report.screenshots) + @($report.animationFrames)) {
    if (![IO.Path]::IsPathRooted($screenshot) -or !$seen.Add([IO.Path]::GetFullPath($screenshot))) { throw "Nonabsolute or duplicated capture: $screenshot" }
    if (!(Test-Path -LiteralPath $screenshot -PathType Leaf)) { throw "Missing frame: $screenshot" }
    $file = Get-Item -LiteralPath $screenshot
    if ($file.LastWriteTime -lt $startedAt -or $file.Length -le 0) { throw "Missing fresh frame: $screenshot" }
    $bitmap = [Drawing.Bitmap]::new($screenshot)
    try {
        $colors = [Collections.Generic.HashSet[int]]::new()
        for ($x = 1; $x -lt 80; $x++) {
            for ($y = 1; $y -lt 45; $y++) {
                $null = $colors.Add($bitmap.GetPixel([int]($bitmap.Width * $x / 80), [int]($bitmap.Height * $y / 45)).ToArgb())
            }
        }
        if ($bitmap.Width -lt 1280 -or $bitmap.Height -lt 720 -or $colors.Count -lt 20) { throw "Blank or undersized rig capture: $screenshot" }
    } finally { $bitmap.Dispose() }
}
Write-Host "TOWER_MONSTER_RIG_SMOKE_OK $reportPath"
Write-Host "Verified 49 creatures, 14 bones each, $($report.sampledFrames) motion samples, 3 catalog captures and $expectedAnimationFrames animation frames."
