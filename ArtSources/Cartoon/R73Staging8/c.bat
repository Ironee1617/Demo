@echo off
cd /d D:\Demo
"C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Data/NetCoreRuntime/dotnet.exe" exec "C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig @"D:/Demo/ArtSources/Cartoon/R73Staging8/Assembly-CSharp.rsp" > "D:/Demo/ArtSources/Cartoon/R73Staging8/log1.txt" 2>&1
echo !errorlevel! > "D:/Demo/ArtSources/Cartoon/R73Staging8/done1.txt"
