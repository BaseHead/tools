@echo off
echo Resolving "Service marked for deletion" issue...

REM Check if running as administrator
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo This script must be run as Administrator!
    echo Right-click and select "Run as administrator"
    pause
    exit /b 1
)

set "SERVICE_NAME=SlackCIBuildServer"

echo Attempting to resolve service deletion issue...
echo.

echo Method 1: Stopping Services Control Manager and waiting...
net stop "Services" >nul 2>&1
timeout /t 3 /nobreak >nul
net start "Services" >nul 2>&1
timeout /t 5 /nobreak >nul

echo Method 2: Checking if service still exists...
sc query "%SERVICE_NAME%" >nul 2>&1
if %errorlevel% equ 0 (
    echo Service still exists. Waiting longer for deletion...
    timeout /t 10 /nobreak >nul
    
    REM Try to query again
    sc query "%SERVICE_NAME%" >nul 2>&1
    if %errorlevel% equ 0 (
        echo Service deletion is taking longer than expected.
        echo You may need to restart the computer to complete the deletion.
        echo After restart, run install-service.bat to reinstall the service.
        pause
        exit /b 1
    )
)

echo Service appears to be deleted. Attempting to create new service...

REM Try to find the executable
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
    echo ERROR: SlackCIApp.exe not found!
    pause
    exit /b 1
)

set "SERVICE_DISPLAY_NAME=SlackCI Build Server"
set "SERVICE_DESCRIPTION=Automated build server that listens for Slack commands to trigger builds"

echo Creating service with path: %SERVICE_PATH%
sc create "%SERVICE_NAME%" binPath= "\"%SERVICE_PATH%\" --service" DisplayName= "%SERVICE_DISPLAY_NAME%" start= auto

if %errorlevel% equ 0 (
    echo Service created successfully!
    
    REM Set description
    sc description "%SERVICE_NAME%" "%SERVICE_DESCRIPTION%"
    
    REM Configure failure recovery
    sc failure "%SERVICE_NAME%" reset= 3600 actions= restart/5000/restart/10000/restart/30000
    
    echo Service configured successfully!
    echo To start the service, run: start-service.bat
) else (
    echo Failed to create service. Error code: %errorlevel%
    echo.
    echo If you continue to get "marked for deletion" errors, please:
    echo 1. Restart your computer
    echo 2. Run this script again after restart
    echo.
    echo Alternatively, you can manually check Services.msc to see if the service exists.
)

echo.
pause
