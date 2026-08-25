@echo off
setlocal EnableExtensions

cd /d "%~dp0"

set "SOLUTION=YtMusicController.sln"
set "APP_PROJECT=app\src\YtMusicController.App\YtMusicController.App.csproj"
set "CONFIGURATION=Release"
set "RUNTIME=win-x64"
set "PUBLISH_DIR=artifacts\win-x64"
set "PACKAGE_PATH=..\YtMusicController-win-x64.zip"
set "PAUSE_AT_END=1"

if /I "%~1"=="--no-pause" set "PAUSE_AT_END=0"

echo ============================================================
echo  YtMusicController - Build, Test, Publish, and Package
echo ============================================================

where dotnet >nul 2>nul
if errorlevel 1 (
    echo.
    echo ERROR: The .NET SDK was not found in PATH.
    echo Install the .NET 8 SDK or a newer SDK, then try again.
    goto :failure
)

echo.
echo Using .NET SDK:
dotnet --version
if errorlevel 1 goto :failure

call :run dotnet restore "%SOLUTION%" --locked-mode
if errorlevel 1 goto :failure

call :run dotnet build "%SOLUTION%" -c "%CONFIGURATION%" --no-restore
if errorlevel 1 goto :failure

call :run dotnet test "%SOLUTION%" -c "%CONFIGURATION%" --no-build --no-restore
if errorlevel 1 goto :failure

call :run dotnet restore "%APP_PROJECT%" -r "%RUNTIME%" --locked-mode
if errorlevel 1 goto :failure

if exist "%PUBLISH_DIR%" (
    echo.
    echo Removing previous publish output: %PUBLISH_DIR%
    rmdir /s /q "%PUBLISH_DIR%"
    if errorlevel 1 goto :failure
)

call :run dotnet publish "%APP_PROJECT%" -c "%CONFIGURATION%" -r "%RUNTIME%" --self-contained false --no-restore -o "%PUBLISH_DIR%"
if errorlevel 1 goto :failure

echo.
echo Creating package: %PACKAGE_PATH%
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%CD%\%PUBLISH_DIR%\*' -DestinationPath '%CD%\%PACKAGE_PATH%' -Force"
if errorlevel 1 goto :failure

echo.
echo ============================================================
echo  BUILD SUCCEEDED
echo  Application: %CD%\%PUBLISH_DIR%\YtMusicController.exe
echo  Package:     %CD%\%PACKAGE_PATH%
echo ============================================================
set "RESULT=0"
goto :finish

:run
echo.
echo ^> %*
%*
exit /b %ERRORLEVEL%

:failure
set "RESULT=%ERRORLEVEL%"
if "%RESULT%"=="0" set "RESULT=1"
echo.
echo ============================================================
echo  BUILD FAILED - review the error above
echo ============================================================

:finish
if "%PAUSE_AT_END%"=="1" (
    echo.
    pause
)
exit /b %RESULT%
