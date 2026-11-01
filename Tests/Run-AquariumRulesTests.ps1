param([string]$ProjectRoot=(Split-Path -Parent $PSScriptRoot),[string]$OutputDirectory=(Join-Path ([IO.Path]::GetTempPath()) 'AquariumRulesTests'))
$ErrorActionPreference='Stop'
$project=Join-Path $ProjectRoot 'Assets/Demos/HideAndSeek'
$version=((Get-Content -LiteralPath (Join-Path $ProjectRoot 'ProjectSettings/ProjectVersion.txt'))[0] -split ': ')[1]
$mono="C:/Program Files/Unity/Hub/Editor/$version/Editor/Data/MonoBleedingEdge"
$relative=@('Scripts/AquariumModel.cs','Scripts/AquariumBodyCollision.cs','Scripts/AquariumClearanceGrid.cs','Scripts/AquariumFishTraits.cs','Scripts/AquariumAnemoneLayout.cs','Scripts/AquariumCoralTable.cs','Scripts/AquariumDiveEntry.cs','Environment/Runtime/AquariumMapOutline.cs','Environment/Runtime/AquariumWalkwayLayout.cs','Environment/Runtime/AquariumMonolithLayout.cs','Environment/Runtime/AquariumSeabed.cs','Environment/Runtime/AquariumEntryLayout.cs','Drains/Runtime/AquariumDrainLayout.cs','Drains/Runtime/AquariumDrainSites.cs')
$sources=@();foreach($path in $relative){$sources+=Join-Path $project $path}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$binary=Join-Path $OutputDirectory 'AquariumRulesTests.exe'
& "$mono/bin/mono.exe" "$mono/lib/mono/4.5/csc.exe" /nologo /target:exe "/out:$binary" @sources "$PSScriptRoot/AquariumRulesTests.cs"
if($LASTEXITCODE -ne 0){throw 'Rule tests did not compile.'}
& $binary
if($LASTEXITCODE -ne 0){throw 'Rule tests failed.'}
