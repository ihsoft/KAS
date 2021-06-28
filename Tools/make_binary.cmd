@echo off
REM Until new major version of C# is released the build number in the path will keep counting.
REM KAS *requires* .NET compiler version 4.0 or higher. Don't get confused with KSP run-time requirement of 3.5.
"C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe" ..\Source\KAS.csproj /t:Clean,Build /p:Configuration=Release
