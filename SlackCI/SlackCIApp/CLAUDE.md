# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SlackCIApp is a CI/CD build server for the BaseHead audio application. It listens for build commands via Slack and triggers cross-platform builds (Windows and Mac). The app can run as a console application or as a Windows service.

## Build Commands

```bash
# Build the project
dotnet build

# Run in console mode
dotnet run

# Run with test upload
dotnet run -- test-upload

# Run as Windows service (use batch files)
install-service.bat    # Install service (run as admin)
start-service.bat      # Start service
stop-service.bat       # Stop service
uninstall-service.bat  # Remove service
```

## Architecture

### Entry Points
- **Program.cs** - Main entry point. Detects console vs service mode via `--service` flag. Console mode runs `MainAsync()`, service mode uses `IHostBuilder` with `SlackCIWorker`.
- **SlackCIWorker.cs** - `BackgroundService` implementation for Windows service mode. Contains duplicated build logic from Program.cs (refactored helper methods like `RunAdvancedInstaller`).

### Core Services
- **BuildManager.cs** - Orchestrates build workflow: git pull, build script execution, Advanced Installer packaging, Slack notifications. Uses dependency injection pattern.
- **GitService.cs** - Handles git operations on both Windows (local process) and Mac (via SSH). Includes SSH validation, retry logic, and HTTPS-to-SSH URL conversion.
- **SshService.cs** - SSH/SCP operations for Mac builds. Opens Terminal.app via AppleScript, monitors build progress via marker files.
- **SlackNotifier.cs** - Sends messages via webhook and uploads files via Slack API.

### Configuration
- **Config/SlackCISettings.cs** - All configuration options. Loaded from `appsettings.json` under `SlackCISettings` section.

### Build Types
Triggered via Slack messages or console input:
1. `build bh` - Build both Windows and Mac in parallel
2. `build bh-pc` - Windows only
3. `build bh-mac` - Mac only (via SSH)
4. `build lls` - License Server build

### Build Flow
1. Clean previous build output (`basehead-pc` directory)
2. Pull latest from Git (SSH authentication to Bitbucket)
3. Execute platform-specific build script
4. Run Advanced Installer to create installer package
5. Copy installer to network share (`\\BeeStation\home\Files\build-server`)
6. Send Slack notification with download link

## Key Dependencies
- **Renci.SshNet** - SSH/SCP operations to Mac
- **Slack.Webhooks** / **SlackAPI** - Slack integration
- **Serilog** - Logging (console + rolling file)
- **Microsoft.Extensions.Hosting.WindowsServices** - Windows service support

## Configuration Notes

The app requires `appsettings.json` with `SlackCISettings` section containing:
- Slack credentials (webhook URL, bot token, channel ID)
- Build script paths for Windows/Mac/LLS
- Git repository paths
- SSH key paths for Bitbucket access
- Advanced Installer paths

Settings are dynamically saved back to `appsettings.json` when installer paths are updated after builds.
