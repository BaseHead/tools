@echo off
echo Checking SlackCI Build Server Service Status...

set "SERVICE_NAME=SlackCIBuildServer"

echo.
echo Service Name: %SERVICE_NAME%
echo.

REM Check if service exists
sc query "%SERVICE_NAME%" >nul 2>&1
if %errorlevel% neq 0 (
    echo Service "%SERVICE_NAME%" does not exist!
    echo Please run install-service.bat first.
    echo.
    pause
    exit /b 1
)

REM Get detailed service information
echo Detailed Service Information:
echo ==============================
sc qc "%SERVICE_NAME%"

echo.
echo Current Service Status:
echo ======================
sc query "%SERVICE_NAME%"

echo.
echo Recent service events can be found in Windows Event Viewer under:
echo Windows Logs ^> Application
echo Look for events from source "SlackCIBuildServer"
echo.
pause
