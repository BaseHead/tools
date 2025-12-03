@echo off
echo Stopping SlackCI Build Server Service...

REM Check if running as administrator
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo This script must be run as Administrator!
    echo Right-click and select "Run as administrator"
    pause
    exit /b 1
)

set "SERVICE_NAME=SlackCIBuildServer"

REM Check if service exists
sc query "%SERVICE_NAME%" >nul 2>&1
if %errorlevel% neq 0 (
    echo Service "%SERVICE_NAME%" does not exist!
    pause
    exit /b 1
)

echo Stopping service...
net stop "%SERVICE_NAME%"
if %errorlevel% equ 0 (
    echo Service stopped successfully!
) else (
    echo Service may already be stopped or failed to stop.
)

echo.
pause
