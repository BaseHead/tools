@echo off
echo Generating SSH Key for SlackCI Service (Simple Version)...

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
echo Service SSH Directory: %SERVICE_SSH_DIR%
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

REM Check where ssh-keygen might be located
echo Checking for ssh-keygen...
where ssh-keygen >nul 2>&1
if %errorlevel% neq 0 (
    echo ssh-keygen not found in PATH. Checking common locations...
    
    REM Check Git for Windows location
    if exist "C:\Program Files\Git\usr\bin\ssh-keygen.exe" (
        set "SSH_KEYGEN=C:\Program Files\Git\usr\bin\ssh-keygen.exe"
        echo Found ssh-keygen in Git for Windows
    ) else if exist "C:\Windows\System32\OpenSSH\ssh-keygen.exe" (
        set "SSH_KEYGEN=C:\Windows\System32\OpenSSH\ssh-keygen.exe"
        echo Found ssh-keygen in Windows OpenSSH
    ) else (
        echo ERROR: ssh-keygen not found!
        echo.
        echo Please install one of these:
        echo 1. Git for Windows: https://git-scm.com/download/win
        echo 2. Windows OpenSSH feature
        echo.
        echo Or generate the key manually:
        echo ssh-keygen -t ed25519 -C "slackci-service@baseheadinc.com" -f "%SERVICE_KEY_PATH%" -N ""
        pause
        exit /b 1
    )
) else (
    set "SSH_KEYGEN=ssh-keygen"
    echo ssh-keygen found in PATH
)

REM Remove old service keys if they exist
if exist "%SERVICE_KEY_PATH%" (
    echo Removing old service SSH keys...
    del "%SERVICE_KEY_PATH%" >nul 2>&1
    del "%SERVICE_KEY_PATH%.pub" >nul 2>&1
)

REM Generate new SSH key
echo.
echo Generating new SSH key...
echo Command: "%SSH_KEYGEN%" -t ed25519 -C "slackci-service@baseheadinc.com" -f "%SERVICE_KEY_PATH%" -N ""
echo.

"%SSH_KEYGEN%" -t ed25519 -C "slackci-service@baseheadinc.com" -f "%SERVICE_KEY_PATH%" -N ""

if %errorlevel% neq 0 (
    echo Failed to generate SSH key!
    echo Trying with RSA instead...
    "%SSH_KEYGEN%" -t rsa -b 4096 -C "slackci-service@baseheadinc.com" -f "%SERVICE_KEY_PATH%" -N ""
    if %errorlevel% neq 0 (
        echo Failed to generate RSA key as well!
        pause
        exit /b 1
    )
)

echo SSH key generated successfully!

REM Check if the key files were created
if not exist "%SERVICE_KEY_PATH%" (
    echo ERROR: Private key file was not created!
    pause
    exit /b 1
)

if not exist "%SERVICE_KEY_PATH%.pub" (
    echo ERROR: Public key file was not created!
    pause
    exit /b 1
)

REM Create known_hosts file
echo Creating known_hosts file...
echo github.com ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIIazEu89wgQZ4bqs3d63QSMzYVa0MuJ2e2KsTBuMioDO > "%SERVICE_SSH_DIR%\known_hosts"
echo github.com ssh-rsa AAAAB3NzaC1yc2EAAAABIwAAAQEAubiN81eDcafrgMeLzaFPsw2kNvEcqTKl/VqLat/MaB33pZy0y3rJZtnqwR2qOOvbwKZYKiEO1O6VqNEBxKvJJelCq0dTXWT5pbO2gDXC6h6QDXCaHo6pOHGPUy+YBaGQRGuSusMEASYiWunYN0vCAI8QaXnWMXNMdFP3jHAJH0eDsoiGnLPBlBp4TN >> "%SERVICE_SSH_DIR%\known_hosts"

REM Set permissions
echo Setting permissions...
icacls "%SERVICE_SSH_DIR%" /inheritance:d >nul 2>&1
icacls "%SERVICE_SSH_DIR%" /grant:r "SYSTEM:(OI)(CI)F" >nul 2>&1
icacls "%SERVICE_SSH_DIR%" /grant:r "Administrators:(OI)(CI)F" >nul 2>&1
icacls "%SERVICE_SSH_DIR%" /grant:r "LOCAL SERVICE:(OI)(CI)R" >nul 2>&1
icacls "%SERVICE_SSH_DIR%" /grant:r "NETWORK SERVICE:(OI)(CI)R" >nul 2>&1

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
echo Files created:
dir "%SERVICE_SSH_DIR%"
echo.
echo ================================
echo PUBLIC KEY TO ADD TO GITHUB:
echo ================================
echo.
type "%SERVICE_KEY_PATH%.pub"
echo.
echo ================================
echo.
echo NEXT STEPS:
echo 1. Copy the public key above
echo 2. Go to GitHub.com ^> Settings ^> SSH and GPG keys ^> Add SSH key
echo 3. Paste the public key and save
echo 4. Run update-service.bat as Administrator
echo.
pause
