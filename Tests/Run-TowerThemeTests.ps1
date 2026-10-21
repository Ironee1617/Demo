$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$version = ((Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'))[0] -split ': ')[1]
$monoRoot = "C:/Program Files/Unity/Hub/Editor/$version/Editor/Data/MonoBleedingEdge"
$testRoot = Join-Path $projectRoot '.validation/TowerModelTests'
[IO.Directory]::CreateDirectory($testRoot) | Out-Null
$binary = Join-Path $testRoot 'TowerThemeTests.exe'
$sources = @(
    'Assets/Demos/Roguelike/Scripts/TowerRun.cs',
    'Assets/Demos/Roguelike/Scripts/TowerOath.cs',
    'Assets/Demos/Roguelike/Scripts/TowerAugmentCatalog.cs',
    'Assets/Demos/Roguelike/Scripts/TowerMythCatalog.cs',
    'Assets/Demos/Roguelike/Scripts/TowerProgress.cs',
    'Assets/Demos/Roguelike/Scripts/TowerDifficulty.cs',
    'Assets/Demos/Roguelike/Scripts/TowerThemeCatalog.cs',
    'Tests/TowerThemeTests.cs'
) | ForEach-Object { Join-Path $projectRoot $_ }
& (Join-Path $monoRoot 'bin/mono.exe') (Join-Path $monoRoot 'lib/mono/4.5/csc.exe') /nologo /target:exe "/out:$binary" @sources
if ($LASTEXITCODE -ne 0) { throw 'Theme tests failed to compile.' }
& (Join-Path $monoRoot 'bin/mono.exe') $binary
if ($LASTEXITCODE -ne 0) { throw 'Theme tests failed.' }
