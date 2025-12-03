@echo off
echo Fixing SlackCI Service Installation...

REM Check if running as administrator
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo This script must be run as Administrator!
    echo Right-click and select "Run as administrator"
    pause
    exit /b 1
)

set "SERVICE_NAME=SlackCIBuildServer"

REM Try to find the executable in order of preference
if exist "%~dp0publish\SlackCIApp.exe" (
    set "CORRECT_SERVICE_PATH=%~dp0publish\SlackCIApp.exe"
    echo Using published version
) else if exist "%~dp0bin\Release\net8.0\SlackCIApp.exe" (
    set "CORRECT_SERVICE_PATH=%~dp0bin\Release\net8.0\SlackCIApp.exe"
    echo Using release build
) else if exist "%~dp0bin\Debug\net8.0\SlackCIApp.exe" (
    set "CORRECT_SERVICE_PATH=%~dp0bin\Debug\net8.0\SlackCIApp.exe"
    echo Using debug build
) else (
    echo ERROR: SlackCIApp.exe not found in any expected location!
    echo Please build the project first: dotnet build
    echo Or publish it: dotnet publish -c Release -o publish
    pause
    exit /b 1
)

set "SERVICE_DISPLAY_NAME=SlackCI Build Server"
set "SERVICE_DESCRIPTION=Automated build server that listens for Slack commands to trigger builds"

echo Correct Service Path: %CORRECT_SERVICE_PATH%

REM Check if the executable exists
if not exist "%CORRECT_SERVICE_PATH%" (
    echo ERROR: Service executable not found at: %CORRECT_SERVICE_PATH%
    echo Please make sure the project is built first by running: dotnet build
    pause
    exit /b 1
)

echo Removing existing service if it exists...
sc query "%SERVICE_NAME%" >nul 2>&1
if %errorlevel% equ 0 (
    echo Stopping existing service...
    sc stop "%SERVICE_NAME%" >nul 2>&1
    timeout /t 5 /nobreak >nul
    echo Removing existing service...
    sc delete "%SERVICE_NAME%"
    timeout /t 5 /nobreak >nul
    echo Existing service removed.
) else (
    echo No existing service found.
)

echo Creating service with correct path...
sc create "%SERVICE_NAME%" binPath= "\"%CORRECT_SERVICE_PATH%\" --service" DisplayName= "%SERVICE_DISPLAY_NAME%" start= auto
if %errorlevel% neq 0 (
    echo Failed to create service!
    pause
    exit /b 1
)

REM Set service description
sc description "%SERVICE_NAME%" "%SERVICE_DESCRIPTION%"

REM Configure service to restart on failure
sc failure "%SERVICE_NAME%" reset= 3600 actions= restart/5000/restart/10000/restart/30000

echo Service installed successfully with correct path!
echo.
echo Service Name: %SERVICE_NAME%
echo Display Name: %SERVICE_DISPLAY_NAME%
echo Executable Path: %CORRECT_SERVICE_PATH%
echo.
echo To start the service now, run: start-service.bat
echo.
pause
