@echo off
echo Installing SlackCI as Windows Service...

REM Check if running as administrator
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo This script must be run as Administrator!
    echo Right-click and select "Run as administrator"
    pause
    exit /b 1
)

REM Get the current directory where the batch file is located
REM Try to use the published version first, then fall back to debug build
if exist "%~dp0publish\SlackCIApp.exe" (
    set "SERVICE_PATH=%~dp0publish\SlackCIApp.exe"
    echo Using published version
) else if exist "%~dp0bin\Release\net8.0\SlackCIApp.exe" (
    set "SERVICE_PATH=%~dp0bin\Release\net8.0\SlackCIApp.exe"
    echo Using release build
) else if exist "%~dp0bin\Debug\net8.0\SlackCIApp.exe" (
    set "SERVICE_PATH=%~dp0bin\Debug\net8.0\SlackCIApp.exe"
    echo Using debug build
) else (
    echo ERROR: SlackCIApp.exe not found in any expected location!
    echo Please build the project first: dotnet build
    echo Or publish it: dotnet publish -c Release -o publish
    pause
    exit /b 1
)

set "SERVICE_NAME=SlackCIBuildServer"
set "SERVICE_DISPLAY_NAME=SlackCI Build Server"
set "SERVICE_DESCRIPTION=Automated build server that listens for Slack commands to trigger builds"

echo Service Path: %SERVICE_PATH%

REM Check if service already exists
sc query "%SERVICE_NAME%" >nul 2>&1
if %errorlevel% equ 0 (
    echo Service already exists. Stopping and removing existing service...
    sc stop "%SERVICE_NAME%"
    timeout /t 5 /nobreak >nul
    sc delete "%SERVICE_NAME%"
    timeout /t 5 /nobreak >nul
)

REM Create the service
echo Creating service...
sc create "%SERVICE_NAME%" binPath= "\"%SERVICE_PATH%\" --service" DisplayName= "%SERVICE_DISPLAY_NAME%" start= auto
if %errorlevel% neq 0 (
    echo Failed to create service!
    pause
    exit /b 1
)

REM Set service description
sc description "%SERVICE_NAME%" "%SERVICE_DESCRIPTION%"

REM Configure service to restart on failure
sc failure "%SERVICE_NAME%" reset= 3600 actions= restart/5000/restart/10000/restart/30000

echo Service installed successfully!
echo.
echo Service Name: %SERVICE_NAME%
echo Display Name: %SERVICE_DISPLAY_NAME%
echo.
echo To start the service now, run: net start "%SERVICE_NAME%"
echo To start automatically at boot, the service is already configured for automatic startup.
echo.
echo You can also use the Services management console (services.msc) to manage the service.
echo.
pause
