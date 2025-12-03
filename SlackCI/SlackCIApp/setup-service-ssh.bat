@echo off
echo Setting up NEW SSH keys for SlackCI Service...

REM Check if running as administrator
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo This script must be run as Administrator!
    echo Right-click and select "Run as administrator"
    pause
    exit /b 1
)

set "SERVICE_SSH_DIR=C:\ProgramData\SlackCI\.ssh"
set "USER_SSH_DIR=%USERPROFILE%\.ssh"
set "SERVICE_KEY_PATH=%SERVICE_SSH_DIR%\id_rsa"
set "USER_KEY_PATH=%USER_SSH_DIR%\id_rsa"

echo.
echo Generating NEW SSH keys for SlackCI service...
echo Service SSH Directory: %SERVICE_SSH_DIR%
echo.
echo NOTE: This will generate a FRESH SSH key to avoid GitHub's "duplicate key" issue.
echo.

REM Create service SSH directory
if not exist "%SERVICE_SSH_DIR%" (
    echo Creating service SSH directory...
    mkdir "%SERVICE_SSH_DIR%"
    if %errorlevel% neq 0 (
        echo Failed to create service SSH directory!
        pause
        exit /b 1
    )
    echo Service SSH directory created successfully.
) else (
    echo Service SSH directory already exists.
)

REM Check if ssh-keygen is available
ssh-keygen -h >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: ssh-keygen command not found!
    echo Please install Git for Windows or OpenSSH to generate SSH keys.
    echo You can download Git from: https://git-scm.com/download/win
    pause
    exit /b 1
)

REM Generate a new SSH key specifically for the service
echo Generating new SSH key for SlackCI service...
echo This will create a unique key to avoid GitHub's duplicate key restriction.
echo.

REM Remove old service keys if they exist
if exist "%SERVICE_KEY_PATH%" (
    echo Removing old service SSH keys...
    del "%SERVICE_KEY_PATH%" >nul 2>&1
    del "%SERVICE_KEY_PATH%.pub" >nul 2>&1
)

REM Generate new SSH key
echo Generating new Ed25519 SSH key...
ssh-keygen -t ed25519 -C "slackci-service@baseheadinc.com" -f "%SERVICE_KEY_PATH%" -N ""
if %errorlevel% neq 0 (
    echo Failed to generate SSH key!
    pause
    exit /b 1
)

echo SSH key generated successfully!

REM Copy known_hosts from user if it exists, or create it
if exist "%USER_SSH_DIR%\known_hosts" (
    copy "%USER_SSH_DIR%\known_hosts" "%SERVICE_SSH_DIR%\known_hosts" >nul 2>&1
    echo known_hosts copied from user directory.
) else (
    echo Creating known_hosts file with GitHub host key...
    echo github.com ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIIazEu89wgQZ4bqs3d63QSMzYVa0MuJ2e2KsTBuMioDO > "%SERVICE_SSH_DIR%\known_hosts"
    echo github.com ssh-rsa AAAAB3NzaC1yc2EAAAABIwAAAQEAubiN81eDcafrgMeLzaFPsw2kNvEcqTKl/VqLat/MaB33pZy0y3rJZtnqwR2qOOvbwKZYKiEO1O6VqNEBxKvJJelCq0dTXWT5pbO2gDXC6h6QDXCaHo6pOHGPUy+YBaGQRGuSusMEASYiWunYN0vCAI8QaXnWMXNMdFP3jHAJH0eDsoiGnLPBlBp4TN >> "%SERVICE_SSH_DIR%\known_hosts"
)

REM Set appropriate permissions on the service SSH directory
echo Setting permissions on service SSH directory...
icacls "%SERVICE_SSH_DIR%" /inheritance:d >nul 2>&1
icacls "%SERVICE_SSH_DIR%" /grant:r "SYSTEM:(OI)(CI)F" >nul 2>&1
icacls "%SERVICE_SSH_DIR%" /grant:r "Administrators:(OI)(CI)F" >nul 2>&1
icacls "%SERVICE_SSH_DIR%" /grant:r "LOCAL SERVICE:(OI)(CI)R" >nul 2>&1
icacls "%SERVICE_SSH_DIR%" /grant:r "NETWORK SERVICE:(OI)(CI)R" >nul 2>&1

REM Set permissions on the private key
icacls "%SERVICE_KEY_PATH%" /inheritance:d >nul 2>&1
icacls "%SERVICE_KEY_PATH%" /grant:r "SYSTEM:F" >nul 2>&1
icacls "%SERVICE_KEY_PATH%" /grant:r "Administrators:F" >nul 2>&1
icacls "%SERVICE_KEY_PATH%" /grant:r "LOCAL SERVICE:R" >nul 2>&1
icacls "%SERVICE_KEY_PATH%" /grant:r "NETWORK SERVICE:R" >nul 2>&1

echo.
echo ================================
echo SSH KEY GENERATION COMPLETED!
echo ================================
echo.
echo Service SSH Key Path: %SERVICE_KEY_PATH%
echo Service SSH Directory: %SERVICE_SSH_DIR%
echo.
echo NEXT STEPS:
echo 1. Copy the PUBLIC key below and add it to your GitHub account
echo 2. Go to GitHub.org ^> Settings ^> SSH and GPG keys ^> Add SSH key
echo 3. Paste the public key and save
echo.
echo ================================
echo PUBLIC KEY TO ADD TO BITBUCKET:
echo ================================
echo.
type "%SERVICE_KEY_PATH%.pub"
echo.
echo ================================
echo.
echo After adding this key to GitHub:
echo - Update and restart the service using: update-service.bat
echo - Test Git pull functionality
echo.
echo This is a UNIQUE key that should not conflict with existing keys in GitHub.
echo.
pause
