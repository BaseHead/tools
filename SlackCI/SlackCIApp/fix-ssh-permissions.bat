@echo off
echo Fixing SSH Key Permissions for SlackCI Service...

REM Check if running as administrator
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo This script must be run as Administrator!
    echo Right-click and select "Run as administrator"
    pause
    exit /b 1
)

set "SERVICE_SSH_DIR=C:\ProgramData\SlackCI\.ssh"
set "SERVICE_KEY_PATH=%SERVICE_SSH_DIR%\id_rsa"

echo.
echo Fixing permissions for SSH keys...
echo SSH Directory: %SERVICE_SSH_DIR%
echo Private Key: %SERVICE_KEY_PATH%
echo.

REM Check if files exist
if not exist "%SERVICE_KEY_PATH%" (
    echo ERROR: SSH private key not found at %SERVICE_KEY_PATH%
    echo Please run generate-service-ssh.bat first
    pause
    exit /b 1
)

echo Current permissions before fix:
icacls "%SERVICE_KEY_PATH%"
echo.

echo Removing all permissions and setting strict access...

REM Remove inheritance and all existing permissions
icacls "%SERVICE_KEY_PATH%" /inheritance:d /remove "Everyone" /remove "Users" /remove "Authenticated Users" >nul 2>&1

REM Remove all users except essential ones
icacls "%SERVICE_KEY_PATH%" /remove:g "BUILTIN\Users" >nul 2>&1
icacls "%SERVICE_KEY_PATH%" /remove:g "NT AUTHORITY\Authenticated Users" >nul 2>&1
icacls "%SERVICE_KEY_PATH%" /remove:g "BUILTIN\Everyone" >nul 2>&1

REM Set minimal required permissions
icacls "%SERVICE_KEY_PATH%" /grant:r "SYSTEM:F" >nul 2>&1
icacls "%SERVICE_KEY_PATH%" /grant:r "Administrators:F" >nul 2>&1

REM For service accounts, we need to be more specific
icacls "%SERVICE_KEY_PATH%" /grant:r "NT AUTHORITY\LOCAL SERVICE:R" >nul 2>&1
icacls "%SERVICE_KEY_PATH%" /grant:r "NT AUTHORITY\NETWORK SERVICE:R" >nul 2>&1

echo.
echo New permissions after fix:
icacls "%SERVICE_KEY_PATH%"
echo.

REM Also fix the SSH directory permissions
echo Fixing SSH directory permissions...
icacls "%SERVICE_SSH_DIR%" /inheritance:d >nul 2>&1
icacls "%SERVICE_SSH_DIR%" /grant:r "SYSTEM:(OI)(CI)F" >nul 2>&1
icacls "%SERVICE_SSH_DIR%" /grant:r "Administrators:(OI)(CI)F" >nul 2>&1
icacls "%SERVICE_SSH_DIR%" /grant:r "NT AUTHORITY\LOCAL SERVICE:(OI)(CI)RX" >nul 2>&1
icacls "%SERVICE_SSH_DIR%" /grant:r "NT AUTHORITY\NETWORK SERVICE:(OI)(CI)RX" >nul 2>&1

echo SSH directory permissions updated.
echo.

REM Test SSH connection
echo Testing SSH connection...
echo Command: ssh -T -i "%SERVICE_KEY_PATH%" -o StrictHostKeyChecking=no git@bitbucket.org
echo.

ssh -T -i "%SERVICE_KEY_PATH%" -o StrictHostKeyChecking=no git@bitbucket.org
set SSH_RESULT=%errorlevel%

echo.
if %SSH_RESULT% equ 1 (
    echo SSH connection test passed! ^(Exit code 1 is normal for Bitbucket^)
    echo The SSH key should now work for Git operations.
) else if %SSH_RESULT% equ 255 (
    echo SSH connection failed. Please check:
    echo 1. The public key is added to your Bitbucket account
    echo 2. The key file permissions ^(should be fixed now^)
    echo 3. Network connectivity to bitbucket.org
) else (
    echo SSH test completed with exit code: %SSH_RESULT%
)

echo.
echo Permissions fix completed!
echo.
echo Next steps:
echo 1. Make sure your public key is added to Bitbucket
echo 2. Restart the SlackCI service: net stop SlackCIBuildServer ^& net start SlackCIBuildServer
echo 3. Test a build to verify Git pull works
echo.
pause
