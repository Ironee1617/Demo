$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$version = ((Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'))[0] -split ': ')[1]
$monoRoot = "C:/Program Files/Unity/Hub/Editor/$version/Editor/Data/MonoBleedingEdge"
$testRoot = Join-Path $projectRoot '.validation/TowerModelTests'
[IO.Directory]::CreateDirectory($testRoot) | Out-Null
$binary = Join-Path $testRoot 'TowerRunTests.exe'
& (Join-Path $monoRoot 'bin/mono.exe') (Join-Path $monoRoot 'lib/mono/4.5/csc.exe') /nologo /target:exe "/out:$binary" (Join-Path $projectRoot 'Assets/Demos/Roguelike/Scripts/TowerRun.cs') (Join-Path $projectRoot 'Assets/Demos/Roguelike/Scripts/TowerAugmentCatalog.cs') (Join-Path $PSScriptRoot 'TowerRunTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Tower model tests failed to compile.' }
& (Join-Path $monoRoot 'bin/mono.exe') $binary
if ($LASTEXITCODE -ne 0) { throw 'Tower model tests failed.' }
