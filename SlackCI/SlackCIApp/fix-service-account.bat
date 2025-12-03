@echo off
REM Advanced Service Account Configuration Script
REM This script properly configures the SlackCI service to run as the current user

echo ==================================================
echo SlackCI Service Account Configuration
echo ==================================================
echo.

REM Check if running as administrator
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: This script must be run as Administrator!
    echo Right-click and select "Run as administrator"
    pause
    exit /b 1
)

REM Get current user information
set CURRENT_USER=%USERNAME%
set COMPUTER_NAME=%COMPUTERNAME%
set DOMAIN=%USERDOMAIN%

echo Current user: %CURRENT_USER%
echo Computer name: %COMPUTER_NAME%
echo Domain: %DOMAIN%
echo.

REM Determine the correct user format
if "%DOMAIN%"=="%COMPUTER_NAME%" (
    set SERVICE_USER=%COMPUTER_NAME%\%CURRENT_USER%
    echo Using local account format: %SERVICE_USER%
) else (
    set SERVICE_USER=%DOMAIN%\%CURRENT_USER%
    echo Using domain account format: %SERVICE_USER%
)

echo.
echo This will configure the SlackCI service to run as: %SERVICE_USER%
echo.
set /p CONFIRM=Continue? (y/n): 
if /i "%CONFIRM%" neq "y" (
    echo Operation cancelled.
    pause
    exit /b 0
)

echo.
echo Please enter the password for %CURRENT_USER%:
set /p SERVICE_PASSWORD=Password: 

echo.
echo Stopping SlackCI service...
sc stop SlackCI

echo Waiting for service to stop...
timeout /t 3 /nobreak >nul

echo.
echo Configuring service to run as %SERVICE_USER%...
sc config SlackCI obj= "%SERVICE_USER%" password= "%SERVICE_PASSWORD%"

if %errorLevel% neq 0 (
    echo.
    echo ERROR: Failed to configure service account.
    echo.
    echo Possible solutions:
    echo 1. Verify the password is correct
    echo 2. Try using the full domain format: %DOMAIN%\%CURRENT_USER%
    echo 3. Try using the local format: .\%CURRENT_USER%
    echo 4. Ensure the account has "Log on as a service" privilege
    echo.
    echo Attempting to grant "Log on as a service" privilege...
    
    REM Grant log on as service privilege
    echo Granting log on as service privilege to %SERVICE_USER%...
    secedit /export /cfg "%temp%\secpol.cfg"
    
    REM Check if user already has the privilege
    findstr /C:"%SERVICE_USER%" "%temp%\secpol.cfg" >nul
    if %errorLevel% neq 0 (
        REM Add the privilege
        powershell -Command "& {$secpol = Get-Content '%temp%\secpol.cfg'; $secpol = $secpol -replace 'SeServiceLogonRight = (.*)','SeServiceLogonRight = $1,%SERVICE_USER%'; $secpol | Set-Content '%temp%\secpol_new.cfg'}"
        secedit /configure /db "%temp%\secedit.sdb" /cfg "%temp%\secpol_new.cfg"
        
        echo Privilege granted. Retrying service configuration...
        sc config SlackCI obj= "%SERVICE_USER%" password= "%SERVICE_PASSWORD%"
    )
    
    if %errorLevel% neq 0 (
        echo.
        echo Still failed. Let's try alternative formats:
        echo.
        echo Trying local account format: .\%CURRENT_USER%
        sc config SlackCI obj= ".\%CURRENT_USER%" password= "%SERVICE_PASSWORD%"
        
        if %errorLevel% neq 0 (
            echo.
            echo All automatic attempts failed. Manual steps:
            echo.
            echo 1. Open Services.msc as Administrator
            echo 2. Find "SlackCI" service
            echo 3. Right-click → Properties → Log On tab
            echo 4. Select "This account" and enter: %SERVICE_USER%
            echo 5. Enter your password
            echo 6. Click OK
            echo.
            pause
            exit /b 1
        )
    )
)

echo.
echo Service account configured successfully!
echo.

echo Testing SSH access...
if exist "%USERPROFILE%\.ssh\id_rsa" (
    echo SSH key found at: %USERPROFILE%\.ssh\id_rsa
    echo.
    echo Starting SlackCI service...
    sc start SlackCI
    
    if %errorLevel% equ 0 (
        echo.
        echo SUCCESS! Service started with user account.
        echo.
        echo The service should now have access to your SSH keys.
        echo You can test this by triggering a build that requires Git pull.
        echo.
    ) else (
        echo.
        echo Service configuration succeeded but failed to start.
        echo Check the Windows Event Log for details.
        echo.
    )
) else (
    echo.
    echo WARNING: No SSH key found at %USERPROFILE%\.ssh\id_rsa
    echo You may need to generate SSH keys for this user account.
    echo.
)

echo.
echo Configuration complete!
echo.
echo Next steps:
echo 1. Test the service by triggering a build
echo 2. Check logs in the logs\ folder for any errors
echo 3. If Git pull still fails, check SSH key configuration
echo.

pause
