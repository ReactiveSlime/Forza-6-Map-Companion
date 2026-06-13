@echo off
setlocal
set PROJ=Forza6Client
set OUT=%~dp0publish
set DST=%~dp0

echo Building %PROJ% - Windows Release Single File...
dotnet publish "%~dp0%PROJ%.csproj" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=true /p:IncludeNativeLibrariesForSelfExtract=true -o "%OUT%\win-x64"
if %ERRORLEVEL% neq 0 (
  echo Windows build failed.
  exit /b %ERRORLEVEL%
)
move /y "%OUT%\win-x64\%PROJ%.exe" "%DST%\%PROJ%-win-x64.exe" >nul

echo Building %PROJ% — Linux Release Single File...
dotnet publish "%~dp0%PROJ%.csproj" -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=true /p:IncludeNativeLibrariesForSelfExtract=true /p:DebugType=embedded -o "%OUT%\linux-x64"
if %ERRORLEVEL% neq 0 (
  echo Linux build failed.
  exit /b %ERRORLEVEL%
)
move /y "%OUT%\linux-x64\%PROJ%" "%DST%\%PROJ%-linux-x64" >nul

echo Cleaning up...
rmdir /s /q "%OUT%" 2>nul

echo Done.
echo   %DST%%PROJ%-win-x64.exe
echo   %DST%%PROJ%-linux-x64
pause