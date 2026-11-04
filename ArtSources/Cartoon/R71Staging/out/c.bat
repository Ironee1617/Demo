@echo off
cd /d D:\Demo
"C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Data\NetCoreRuntime\dotnet.exe" exec "C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Data\DotNetSdkRoslyn\csc.dll" /shared @"D:\Demo\ArtSources\Cartoon\R71Staging\out\r.rsp" > "D:\Demo\ArtSources\Cartoon\R71Staging\out\log.txt" 2>&1
echo !errorlevel! > "D:\Demo\ArtSources\Cartoon\R71Staging\out\done.txt"
