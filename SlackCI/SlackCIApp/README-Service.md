# SlackCI Build Server - Windows Service

This application can now run both as a console application and as a Windows service.

## Running as Console Application (Original Mode)

To run in console mode (as before), simply run:
```
SlackCIApp.exe
```

Or with test upload:
```
SlackCIApp.exe test-upload
```

## Running as Windows Service

### Installation

1. **Run as Administrator** - Right-click on `install-service.bat` and select "Run as administrator"
2. The script will install the service with the name "SlackCIBuildServer"
3. The service will be configured to start automatically when Windows boots

### Service Management

Use the provided batch files (all must be run as Administrator):

- **install-service.bat** - Installs the service
- **start-service.bat** - Starts the service
- **stop-service.bat** - Stops the service  
- **uninstall-service.bat** - Removes the service
- **service-status.bat** - Shows service status and configuration

### Alternative Service Management

You can also use Windows built-in tools:

#### Command Line:
```cmd
# Start service
net start SlackCIBuildServer

# Stop service  
net stop SlackCIBuildServer

# Check status
sc query SlackCIBuildServer
```

#### Services Management Console:
1. Press `Win + R`, type `services.msc`, press Enter
2. Find "SlackCI Build Server" in the list
3. Right-click to start, stop, restart, or configure the service

## Service Benefits

Running as a Windows service provides several advantages:

1. **Automatic Startup** - Starts automatically when Windows boots
2. **Background Operation** - Runs without needing a logged-in user
3. **System Integration** - Properly integrated with Windows service management
4. **Reliability** - Configured to restart automatically on failure
5. **Security** - Can run under a specific service account if needed

## Logging

When running as a service, logs are still written to the configured log file path in `appsettings.json`. You can also check Windows Event Viewer for service-related events:

1. Open Event Viewer (`eventvwr.msc`)
2. Navigate to Windows Logs > Application
3. Look for events from source "SlackCIBuildServer"

## Configuration

The service uses the same `appsettings.json` configuration file as the console application. Make sure this file is in the same directory as the executable.

## Troubleshooting

### Service Won't Start
1. Check that `appsettings.json` is in the same directory as `SlackCIApp.exe`
2. Verify that all file paths in the configuration are accessible to the service
3. Check Windows Event Viewer for error messages
4. Ensure the service has appropriate permissions to access network drives and execute build scripts

### Permission Issues
If you encounter permission issues:
1. Consider running the service under a specific user account with appropriate permissions
2. Use the Services management console to configure the service to run under a specific account
3. Ensure the account has "Log on as a service" permission

### Network Drive Access
If using network drives (like \\BeeStation), you may need to:
1. Configure the service to run under a domain account that has access to the network resources
2. Or map network drives using a startup script that runs before the service starts

## Build Types Available

The service supports all the same build commands:
1. Build both (Windows + Mac)
2. Build Windows only  
3. Build Mac only
4. Build LLS only

Commands can be triggered via Slack messages (if configured) or by using the console mode for testing.
