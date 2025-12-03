# BaseHead Slack CI

This application allows you to trigger build scripts for BaseHead using commands from a Slack channel or through a command-line interface. Build notifications are sent to your Slack channel.

## Features

- Send build notifications to a Slack channel via webhooks
- Trigger builds through the command-line interface
- Optionally read messages from Slack to trigger builds (requires Bot Token)
- Support for both Windows and Mac builds (Mac builds via SSH)
- Notify about installer files when builds complete
- Simulation mode if build scripts or SSH access aren't available
- Detailed logging for troubleshooting

## Requirements

- .NET 8.0 runtime
- Slack workspace with admin privileges
- Incoming Webhook URL from Slack
- (Optional) Bot User OAuth Token for reading messages from Slack

## Setup Instructions

### 1. Create a Slack App with Incoming Webhook

1. Go to https://api.slack.com/apps and click "Create New App"
2. Choose "From scratch" and provide a name (e.g., "BaseHead CI")
3. Select your workspace and click "Create App"
4. Go to "Incoming Webhooks" in the sidebar and enable it
5. Click "Add New Webhook to Workspace" and select a channel (e.g., #builds)
6. Copy the Webhook URL that is generated - you'll need this for the app configuration

### 2. (Optional) Set Up the App for Reading Messages from Slack

If you want to trigger builds by typing commands in your Slack channel:

1. Go to your app settings at https://api.slack.com/apps
2. Click on "OAuth & Permissions" in the sidebar
3. Under "Scopes" > "Bot Token Scopes", add the following permissions:
   - `channels:history` (to read messages)
   - `chat:write` (to send messages)
4. Click "Install to Workspace" at the top of the page
5. Copy the "Bot User OAuth Token" (starts with `xoxb-`) - you'll need this for the app configuration
6. Get your channel ID by right-clicking on the channel in Slack and selecting "Copy Link" - the ID is the part after the last slash (e.g., C01234ABCDE)

### 3. Configure the Application

1. Update the `appsettings.json` file with your webhook URL:
   ```json
   "SlackWebhookUrl": "https://hooks.slack.com/services/YOUR/WEBHOOK/URL",
   "ChannelName": "#builds"
   ```

2. If you want to enable reading messages from Slack, also set:
   ```json
   "SlackBotToken": "xoxb-your-bot-token",
   "ChannelId": "C01234ABCDE"
   ```

2. Verify the build script paths are correct:
   ```json
   "WindowsBuildScriptPath": "C:\\Users\\carbo\\Desktop\\GitHub\\basehead\\build.bat",
   "MacBuildScriptPath": "/Users/steve/Desktop/GitHub/basehead/build.sh"
   ```

3. For Mac builds via SSH, configure the connection settings:
   ```json
   "MacHostname": "mac.example.com",
   "MacUsername": "username",
   "MacKeyPath": "C:\\path\\to\\private_key.pem"
   ```

4. If you want to notify about installers, set the paths:
   ```json
   "WindowsInstallerPath": "path/to/windows/installer.exe",
   "MacInstallerPath": "/path/on/mac/to/installer.pkg"
   ```

## Usage

Run the application using:
```
dotnet run
```

Once running, the application provides a command-line interface for testing. You can enter:

1. `build bh` - Triggers builds on both Windows and Mac
2. `build bh-pc` - Triggers build only on Windows
3. `build bh-mac` - Triggers build only on Mac

In your Slack #builds channel, you can use the same commands to trigger builds.

## How It Works

1. The application sends a "bot is online" message to the Slack channel when it starts
2. You can trigger builds in two ways:
   
   a) Through the console:
   - Enter the command number (1, 2, or 3) or the full command text
   - The app sends a notification to Slack that the build is starting
   - Build scripts are executed locally or via SSH
   - Success/failure notifications are sent to Slack
   
   b) Through Slack (if you've configured the Bot Token and Channel ID):   - Type the command in your Slack channel (e.g., `build bh-pc`)
   - The app polls Slack every 10 seconds for new messages
   - When a command is detected, the build process starts
   - All notifications are sent back to the Slack channel

## Troubleshooting

### Windows Builds
- Ensure the Windows build script path is correct and points to a valid batch file
- The script should return exit code 0 for success, any other code is considered a build failure
- If the script path isn't valid, the app will run in simulation mode

### Mac Builds
- Mac builds require SSH access to the Mac machine
- The private key must be in PEM format and accessible to the application
- Ensure the Mac user has permission to run the build script
- The SSH connection has a 15-second timeout to avoid hanging if the Mac is unreachable

### Slack Integration
- If Slack notifications aren't working, verify the webhook URL is correct
- For reading messages from Slack, ensure the bot has been invited to the channel
- If the Bot Token is invalid or doesn't have required permissions, the app will fall back to console-only mode

### Logs
- Check the logs folder for detailed information about any errors
- The log files are named according to the date (e.g., `slackci20250430.log`)

## Mac Build Configuration

For the Mac build to work, you'll need to set up SSH key-based authentication. Update the following settings:

```json
"MacHostname": "your-mac-hostname",
"MacUsername": "username",
"MacKeyPath": "path/to/private/key"
```

The application will use SSH to:
1. Connect to the Mac machine
2. Make the build script executable
3. Run the build script
4. Download the installer file (if configured)

## Extending the Application

To create a fully automated solution that listens to Slack in real-time, you would need to:

1. Create a Slack app with Bot User OAuth Tokens
2. Set up Event Subscriptions with URL verification
3. Use the Slack Events API to receive messages in real-time
4. Deploy the application as a web service with a public endpoint

For simplicity, this application uses a local command line interface for triggering builds, while still sending notifications to Slack.

## Running as a Service on Windows

To run SlackCI as a Windows Service so it's always monitoring for build commands:

1. Install the Windows Service Wrapper (NSSM):
   - Download from https://nssm.cc/download
   - Extract to a known location

2. Register the service:
   ```powershell
   # Navigate to the NSSM folder
   cd C:\path\to\nssm\win64

   # Install the service
   ./nssm.exe install BaseHeadSlackCI

   # This will open a GUI where you configure:
   # - Path: [path to dotnet.exe] (typically C:\Program Files\dotnet\dotnet.exe)
   # - Startup directory: C:\Users\carbo\Desktop\GitHub\tools\SlackCI\SlackCIApp
   # - Arguments: run
   ```

3. Start the service:
   ```powershell
   ./nssm.exe start BaseHeadSlackCI
   ```

4. To stop or remove the service:
   ```powershell
   ./nssm.exe stop BaseHeadSlackCI
   ./nssm.exe remove BaseHeadSlackCI
   ```

## Triggering Builds from a Remote Computer

To trigger builds from any computer with access to your Slack workspace:

1. Ensure SlackCI is running (either as a console app or as a Windows Service)
2. Open the configured Slack channel
3. Type one of the build commands:   - `build bh` (for both platforms)
   - `build bh-pc` (Windows only)
   - `build bh-mac` (Mac only)
4. The SlackCI bot will:
   - Acknowledge your command
   - Execute the appropriate build script
   - Post the build status back to the Slack channel
   - Notify when installation packages are ready (if configured)
