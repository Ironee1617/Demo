@echo off
cd /d D:\Demo
"C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Data/NetCoreRuntime/dotnet.exe" exec "C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig @"D:/Demo/ArtSources/Cartoon/R73Staging2/Assembly-CSharp.rsp" > "D:/Demo/ArtSources/Cartoon/R73Staging2/log1.txt" 2>&1
echo !errorlevel! > "D:/Demo/ArtSources/Cartoon/R73Staging2/done1.txt"
"C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Data/NetCoreRuntime/dotnet.exe" exec "C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig @"D:/Demo/ArtSources/Cartoon/R73Staging2/Assembly-CSharp-Editor.rsp" > "D:/Demo/ArtSources/Cartoon/R73Staging2/log2.txt" 2>&1
echo !errorlevel! > "D:/Demo/ArtSources/Cartoon/R73Staging2/done2.txt"
