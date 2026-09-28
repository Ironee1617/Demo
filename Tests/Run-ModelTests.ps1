$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$version = ((Get-Content (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'))[0] -split ': ')[1]
$monoRoot = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Data\MonoBleedingEdge"
$compiler = Join-Path $monoRoot 'lib/mono/4.5/csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw "Unity C# compiler missing: $compiler" }
$testRoot = Join-Path $projectRoot 'Temp/AquariumTests'
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$binary = Join-Path $testRoot 'ModelTests.exe'
& (Join-Path $monoRoot 'bin/mono.exe') $compiler /nologo /target:exe "/out:$binary" (Join-Path $projectRoot 'Assets/Demos/HideAndSeek/Scripts/AquariumModel.cs') (Join-Path $PSScriptRoot 'ModelTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Model tests failed to compile.' }
& $binary
if ($LASTEXITCODE -ne 0) { throw 'Model tests failed.' }
