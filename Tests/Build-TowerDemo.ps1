param(
    [switch]$PrepareOnly,
    [switch]$ValidateOnly,
    [string]$UnityPath,
    [int]$TimeoutSeconds = 900
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$validationRoot = Join-Path $projectRoot '.validation/TowerDemoProject'
$sourceRoot = Join-Path $projectRoot 'Assets/Demos/Roguelike'
$reportRoot = Join-Path $projectRoot 'Artifacts/TowerDemo'
$buildPath = Join-Path $projectRoot 'Build/TowerDemo/TowerDemo.exe'

function Assert-InWorkspace([string]$Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    if (!$resolved.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside this workspace: $resolved"
    }
    return $resolved
}

function Assert-NoLinks([string]$Path) {
    # Walk explicitly so a reparse-point directory is inspected before descent.
    if (!(Test-Path -LiteralPath $Path)) { return }
    $pending = [Collections.Generic.Stack[IO.FileSystemInfo]]::new()
    $pending.Push((Get-Item -LiteralPath ([IO.Path]::GetFullPath($Path)) -Force))
    while ($pending.Count -gt 0) {
        $item = $pending.Pop()
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or $item.LinkType) {
            throw "Linked source or validation path is forbidden: $($item.FullName) -> $($item.Target)"
        }
        if ($item.PSIsContainer) {
            foreach ($child in Get-ChildItem -LiteralPath $item.FullName -Force) {
                $pending.Push($child)
            }
        }
    }
}

function Assert-AncestorsHaveNoLinks([string]$Path) {
    $ancestor = [IO.Path]::GetFullPath($Path)
    while ($ancestor.Length -ge $projectRoot.Length) {
        if (Test-Path -LiteralPath $ancestor) {
            $item = Get-Item -LiteralPath $ancestor -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or $item.LinkType) {
                throw "Linked ancestor is forbidden: $ancestor"
            }
        }
        if ($ancestor -eq $projectRoot) { break }
        $ancestor = Split-Path -Parent $ancestor
    }
}

function Copy-IndependentTree([string]$Source, [string]$Destination) {
    Assert-NoLinks $Source
    Assert-NoLinks $Destination
    [IO.Directory]::CreateDirectory((Assert-InWorkspace $Destination)) | Out-Null
    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push($Source)
    while ($pending.Count -gt 0) {
        $directory = $pending.Pop()
        $relative = $directory.Substring($Source.Length).TrimStart([char[]]'\/')
        $target = if ($relative) { Join-Path $Destination $relative } else { $Destination }
        [IO.Directory]::CreateDirectory($target) | Out-Null
        foreach ($child in Get-ChildItem -LiteralPath $directory -Force) {
            if ($child.PSIsContainer) {
                if ($directory -eq $Source -and $child.Name -notin @('Scripts', 'Resources', 'Editor')) { continue }
                $pending.Push($child.FullName)
            } elseif ($directory -ne $Source -or $child.Name -in @('Scripts.meta', 'Resources.meta', 'Editor.meta', '20_Roguelike.unity', '20_Roguelike.unity.meta')) {
                # File.Copy always creates independent bytes, preserving Unity .meta companions.
                [IO.File]::Copy($child.FullName, (Join-Path $target $child.Name), $true)
            }
        }
    }
}

Assert-InWorkspace $validationRoot | Out-Null
Assert-AncestorsHaveNoLinks $validationRoot
Assert-NoLinks $validationRoot
Assert-AncestorsHaveNoLinks $buildPath
Assert-NoLinks (Split-Path -Parent $buildPath)
Assert-AncestorsHaveNoLinks $reportRoot
Assert-NoLinks $reportRoot
$alreadyRunning = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.IndexOf($validationRoot, [StringComparison]::OrdinalIgnoreCase) -ge 0
}
if ($alreadyRunning) { throw 'The isolated validation project is already open in Unity.' }
Assert-NoLinks $sourceRoot
[IO.Directory]::CreateDirectory($reportRoot) | Out-Null
Copy-IndependentTree $sourceRoot (Join-Path $validationRoot 'Assets/Demos/Roguelike')
foreach ($metadata in Get-ChildItem -LiteralPath (Join-Path $validationRoot 'Assets/Demos/Roguelike') -Recurse -File -Filter '*.meta') {
    $guidLine = Select-String -LiteralPath $metadata.FullName -Pattern '^guid:'
    if ($guidLine.Count -ne 1 -or $guidLine.Line -notmatch '^guid: [0-9a-fA-F]{32}$') {
        throw "Malformed Unity metadata GUID: $($metadata.FullName)"
    }
}
foreach ($folder in @('Assets/Demos', 'Assets/Demos/Roguelike')) {
    $metaSource = Join-Path $projectRoot ($folder + '.meta')
    if (Test-Path -LiteralPath $metaSource) {
        [IO.File]::Copy($metaSource, (Join-Path $validationRoot ($folder + '.meta')), $true)
    }
}
[IO.Directory]::CreateDirectory((Join-Path $validationRoot 'ProjectSettings')) | Out-Null
[IO.Directory]::CreateDirectory((Join-Path $validationRoot 'Packages')) | Out-Null
[IO.File]::Copy((Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'), (Join-Path $validationRoot 'ProjectSettings/ProjectVersion.txt'), $true)
$utf8 = [Text.UTF8Encoding]::new($false)
$manifest = @'
{
  "dependencies": {
    "com.unity.modules.imgui": "1.0.0",
    "com.unity.modules.physics": "1.0.0",
    "com.unity.modules.audio": "1.0.0",
    "com.unity.modules.jsonserialize": "1.0.0",
    "com.unity.modules.screencapture": "1.0.0",
    "com.unity.modules.animation": "1.0.0"
  }
}
'@
[IO.File]::WriteAllText((Join-Path $validationRoot 'Packages/manifest.json'), $manifest, $utf8)
Write-Host "Prepared independent validation project: $validationRoot"
if ($PrepareOnly) { return }

if (!$UnityPath) {
    $version = ((Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'))[0] -split ': ')[1]
    $UnityPath = "C:/Program Files/Unity/Hub/Editor/$version/Editor/Unity.exe"
}
if (!(Test-Path -LiteralPath $UnityPath)) { throw "Unity executable missing: $UnityPath" }
$method = if ($ValidateOnly) { 'Validate' } else { 'Windows' }
$logPath = Join-Path $reportRoot ("unity-$($method.ToLowerInvariant()).log")
$previousBuildPath = $env:TOWER_BUILD_PATH
$env:TOWER_BUILD_PATH = $buildPath
$startedAt = Get-Date
try {
    $arguments = @('-batchmode', '-nographics', '-quit', '-projectPath', "`"$validationRoot`"", '-executeMethod', "PrototypeLab.Roguelike.Editor.TowerBuild.$method", '-logFile', "`"$logPath`"")
    $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Write-Host "Unity PID $($process.Id); log: $logPath"
    if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
        Stop-Process -Id $process.Id -Force
        throw "Unity exceeded $TimeoutSeconds seconds. See $logPath"
    }
    $process.Refresh()
    $log = if (Test-Path -LiteralPath $logPath) { [IO.File]::ReadAllText($logPath) } else { '' }
    $marker = if ($ValidateOnly) { 'TOWER_VALIDATE_OK' } else { 'TOWER_BUILD_OK' }
    $succeeded = $process.ExitCode -eq 0 -and $log.Contains($marker)
    $report = [ordered]@{
        startedAt = $startedAt.ToString('o')
        finishedAt = (Get-Date).ToString('o')
        validationProject = $validationRoot
        sourceProject = $projectRoot
        method = $method
        exitCode = $process.ExitCode
        succeeded = $succeeded
        log = $logPath
        executable = if ($ValidateOnly) { $null } else { $buildPath }
    }
    [IO.File]::WriteAllText((Join-Path $reportRoot 'build-report.json'), ($report | ConvertTo-Json), $utf8)
    if (!$succeeded) { throw "Tower $method failed (exit $($process.ExitCode)). See $logPath" }
    Write-Host "TOWER_$($method.ToUpperInvariant())_OK $buildPath"
} finally {
    $env:TOWER_BUILD_PATH = $previousBuildPath
}
