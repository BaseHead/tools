@echo off
echo Testing Git Pull Functionality for SlackCI...

REM Find the executable in the correct location
if exist "publish\SlackCIApp.exe" (
    set "EXE_PATH=publish\SlackCIApp.exe"
    echo Using published version: %EXE_PATH%
) else if exist "bin\Release\net8.0\SlackCIApp.exe" (
    set "EXE_PATH=bin\Release\net8.0\SlackCIApp.exe"
    echo Using release build: %EXE_PATH%
) else if exist "bin\Debug\net8.0\SlackCIApp.exe" (
    set "EXE_PATH=bin\Debug\net8.0\SlackCIApp.exe"
    echo Using debug build: %EXE_PATH%
) else (
    echo ERROR: SlackCIApp.exe not found!
    echo Please build the project first: dotnet build
    echo Or publish it: dotnet publish -c Release -o publish
    pause
    exit /b 1
)

echo.
echo Testing console mode (should work with user SSH keys):
echo =====================================================
echo Running a Windows build test in console mode...
echo Command: %EXE_PATH%
echo.

REM Run in console mode - this will show the interactive menu
"%EXE_PATH%"

echo.
echo Console mode test completed.
echo.
echo If you saw Git pull messages above, the functionality is working!
echo.
echo To test service mode:
echo 1. Make sure the service is running: net start SlackCIBuildServer
echo 2. Send "build bh-pc" to your Slack channel
echo 3. Check the logs for Git pull messages
echo.
pause
