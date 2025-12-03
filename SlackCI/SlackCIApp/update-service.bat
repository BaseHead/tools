@echo off
echo Updating SlackCI Service with Git Pull Support...

REM Check if running as administrator
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo This script must be run as Administrator!
    echo Right-click and select "Run as administrator"
    pause
    exit /b 1
)

REM Change to the script's directory
cd /d "%~dp0"
echo Working directory: %CD%

set "SERVICE_NAME=SlackCIBuildServer"

echo Stopping service to allow file updates...
sc query "%SERVICE_NAME%" >nul 2>&1
if %errorlevel% equ 0 (
    net stop "%SERVICE_NAME%"
    if %errorlevel% equ 0 (
        echo Service stopped successfully.
    ) else (
        echo Service may already be stopped or failed to stop.
    )
) else (
    echo Service does not exist or is not installed.
    echo You may need to run install-service.bat or fix-service.bat first.
)

echo Waiting for files to be released...
timeout /t 3 /nobreak >nul

echo Building and publishing updated version...
if exist "SlackCIApp.csproj" (
    dotnet publish -c Release -o publish
    if %errorlevel% neq 0 (
        echo Failed to build and publish the application!
        echo Please check for compilation errors.
        pause
        exit /b 1
    )
    echo Build completed successfully.
) else (
    echo ERROR: SlackCIApp.csproj not found in current directory!
    echo Current directory: %CD%
    echo Please run this script from the SlackCIApp project directory.
    pause
    exit /b 1
)

echo.
echo Setting up SSH keys for service...
if exist "setup-service-ssh.bat" (
    call setup-service-ssh.bat
    if %errorlevel% neq 0 (
        echo SSH setup failed! Git pull may not work.
        echo You can run setup-service-ssh.bat manually later.
        echo Continuing with service update...
        timeout /t 3 /nobreak >nul
    )
) else (
    echo setup-service-ssh.bat not found, skipping SSH setup.
    echo You may need to set up SSH keys manually for Git pull to work.
)

echo.
echo Checking if service exists...
sc query "%SERVICE_NAME%" >nul 2>&1
if %errorlevel% neq 0 (
    echo Service does not exist. Installing service...
    call install-service.bat
    if %errorlevel% neq 0 (
        echo Failed to install service!
        pause
        exit /b 1
    )
) else (
    echo Starting service with updated version...
    net start "%SERVICE_NAME%"
    if %errorlevel% equ 0 (
        echo Service started successfully!
    ) else (
        echo Failed to start service!
        echo Check the Windows Event Log for details.
        echo You may need to run fix-service.bat to fix the service configuration.
    )
)

echo.
echo ✓ Git pull is now ENABLED for basehead builds (options 1-3)
echo ✓ Git pull is DISABLED for LLS builds (option 4) - not needed
echo ✓ SSH keys configured for service account
echo.
echo The service should now be able to pull from Git before builds!

echo.
echo Checking service status:
sc query "%SERVICE_NAME%"

echo.
echo Testing Git pull functionality by triggering a test build...
echo You can now test by sending "build bh-pc" to your Slack channel
echo or running the application in console mode to test Git pull.
echo.
pause
