param([switch]$BoundsOnly)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$version = ((Get-Content (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'))[0] -split ': ')[1]
$monoRoot = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Data\MonoBleedingEdge"
$compiler = Join-Path $monoRoot 'lib/mono/4.5/csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw "Unity C# compiler missing: $compiler" }
$testRoot = Join-Path $projectRoot 'Temp/AquariumTests'
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$binary = Join-Path $testRoot 'GeometryPerformance.exe'
$resultRoot = Join-Path $projectRoot 'Artifacts/GeometryQA'
New-Item -ItemType Directory -Path $resultRoot -Force | Out-Null
$resultPath = Join-Path $resultRoot $(if ($BoundsOnly) { 'bounds-audit.txt' } else { 'geometry-checks.txt' })
$sources = @(
    'Assets/Demos/HideAndSeek/Scripts/AquariumModel.cs',
    'Assets/Demos/HideAndSeek/Scripts/AquariumClearanceGrid.cs',
    'Assets/Demos/HideAndSeek/Scripts/AquariumBodyCollision.cs',
    'Assets/Demos/HideAndSeek/Environment/Runtime/AquariumMapOutline.cs',
    'Assets/Demos/HideAndSeek/Environment/Runtime/AquariumWalkwayLayout.cs',
    'Assets/Demos/HideAndSeek/Environment/Runtime/AquariumMonolithLayout.cs',
    'Assets/Demos/HideAndSeek/Environment/Runtime/AquariumSeabed.cs',
    'Tests/GeometryPerformance.cs'
) | ForEach-Object { Join-Path $projectRoot $_ }
& (Join-Path $monoRoot 'bin/mono.exe') $compiler /nologo /target:exe "/out:$binary" $sources
if ($LASTEXITCODE -ne 0) { throw 'Geometry performance failed to compile.' }
if ($BoundsOnly) { & $binary --bounds-only | Tee-Object -FilePath $resultPath } else { & $binary | Tee-Object -FilePath $resultPath }
if ($LASTEXITCODE -ne 0) { throw 'Geometry performance failed.' }
