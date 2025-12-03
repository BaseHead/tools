@echo off
echo Fixing SSH permissions for Windows Service Account...

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
set "SERVICE_NAME=SlackCIBuildServer"

echo.
echo Fixing SSH permissions for service: %SERVICE_NAME%
echo.

REM Get the service account that the service is running under
echo Checking service account...
for /f "tokens=2 delims= " %%a in ('sc qc "%SERVICE_NAME%" ^| findstr "SERVICE_START_NAME"') do set SERVICE_ACCOUNT=%%a
echo Service is running as: %SERVICE_ACCOUNT%

if not exist "%SERVICE_KEY_PATH%" (
    echo ERROR: SSH private key not found at %SERVICE_KEY_PATH%
    pause
    exit /b 1
)

echo.
echo Step 1: Removing all existing permissions...
REM Take ownership and remove all permissions
takeown /f "%SERVICE_KEY_PATH%" /a >nul 2>&1
icacls "%SERVICE_KEY_PATH%" /reset >nul 2>&1
icacls "%SERVICE_KEY_PATH%" /inheritance:d >nul 2>&1

echo Step 2: Setting minimal permissions for SSH...
REM Set very restrictive permissions - only SYSTEM and Administrators
icacls "%SERVICE_KEY_PATH%" /grant:r "NT AUTHORITY\SYSTEM:(F)" >nul 2>&1
icacls "%SERVICE_KEY_PATH%" /grant:r "BUILTIN\Administrators:(F)" >nul 2>&1

REM If service is running as LOCAL SYSTEM, it should already have access
REM If running as a specific user, grant that user access
if not "%SERVICE_ACCOUNT%"=="LocalSystem" (
    echo Step 3: Granting access to service account: %SERVICE_ACCOUNT%
    icacls "%SERVICE_KEY_PATH%" /grant:r "%SERVICE_ACCOUNT%:(R)" >nul 2>&1
)

echo Step 4: Setting directory permissions...
icacls "%SERVICE_SSH_DIR%" /reset >nul 2>&1
icacls "%SERVICE_SSH_DIR%" /inheritance:d >nul 2>&1
icacls "%SERVICE_SSH_DIR%" /grant:r "NT AUTHORITY\SYSTEM:(OI)(CI)(F)" >nul 2>&1
icacls "%SERVICE_SSH_DIR%" /grant:r "BUILTIN\Administrators:(OI)(CI)(F)" >nul 2>&1

if not "%SERVICE_ACCOUNT%"=="LocalSystem" (
    icacls "%SERVICE_SSH_DIR%" /grant:r "%SERVICE_ACCOUNT%:(OI)(CI)(RX)" >nul 2>&1
)

echo.
echo Current permissions:
icacls "%SERVICE_KEY_PATH%"
echo.

REM Test as current user
echo Testing SSH as current user...
ssh -T -i "%SERVICE_KEY_PATH%" -o StrictHostKeyChecking=no -o UserKnownHostsFile="%SERVICE_SSH_DIR%\known_hosts" git@github.com 2>&1
echo.

REM Now let's configure the service to run as a specific user that we know works
echo.
echo OPTION 1: Configure service to run as current user
echo This ensures the service has access to SSH keys that work.
echo.
set /p "USE_CURRENT_USER=Do you want to configure the service to run as the current user? (y/n): "

if /i "%USE_CURRENT_USER%"=="y" (
    echo.
    echo Configuring service to run as current user...
    set /p "USER_PASSWORD=Enter password for %USERNAME%: "
    
    sc config "%SERVICE_NAME%" obj= "%USERDOMAIN%\%USERNAME%" password= "!USER_PASSWORD!"
    if !errorlevel! equ 0 (
        echo Service configured to run as %USERDOMAIN%\%USERNAME%
        
        REM Now set permissions for this specific user
        icacls "%SERVICE_KEY_PATH%" /grant:r "%USERDOMAIN%\%USERNAME%:(R)" >nul 2>&1
        icacls "%SERVICE_SSH_DIR%" /grant:r "%USERDOMAIN%\%USERNAME%:(OI)(CI)(RX)" >nul 2>&1
        
        echo Restarting service...
        net stop "%SERVICE_NAME%" >nul 2>&1
        timeout /t 3 /nobreak >nul
        net start "%SERVICE_NAME%"
        
        echo.
        echo Service should now run with proper SSH access!
    ) else (
        echo Failed to configure service account. Please check the password.
    )
) else (
    echo.
    echo OPTION 2: Alternative - Copy user SSH keys to service location
    echo.
    set /p "COPY_USER_KEYS=Copy your working SSH keys to service location? (y/n): "
    
    if /i "!COPY_USER_KEYS!"=="y" (
        set "USER_SSH_DIR=%USERPROFILE%\.ssh"
        if exist "!USER_SSH_DIR!\id_rsa" (
            echo Copying working SSH keys...
            copy "!USER_SSH_DIR!\id_rsa" "%SERVICE_KEY_PATH%" >nul
            copy "!USER_SSH_DIR!\id_rsa.pub" "%SERVICE_KEY_PATH%.pub" >nul
            copy "!USER_SSH_DIR!\known_hosts" "%SERVICE_SSH_DIR%\known_hosts" >nul 2>&1
            
            REM Reset permissions on copied keys
            icacls "%SERVICE_KEY_PATH%" /reset >nul 2>&1
            icacls "%SERVICE_KEY_PATH%" /inheritance:d >nul 2>&1
            icacls "%SERVICE_KEY_PATH%" /grant:r "NT AUTHORITY\SYSTEM:(F)" >nul 2>&1
            icacls "%SERVICE_KEY_PATH%" /grant:r "BUILTIN\Administrators:(F)" >nul 2>&1
            
            echo SSH keys copied and permissions set.
            echo Restarting service...
            net stop "%SERVICE_NAME%" >nul 2>&1
            timeout /t 3 /nobreak >nul
            net start "%SERVICE_NAME%"
        ) else (
            echo No user SSH keys found at !USER_SSH_DIR!
        )
    )
)

echo.
echo Permission fix completed!
echo.
pause
