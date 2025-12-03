@echo off
echo Run these commands in an Administrator Command Prompt:
echo.
echo 1. Stop and remove the old service:
echo    sc stop "SlackCIBuildServer"
echo    sc delete "SlackCIBuildServer"
echo.
echo 2. Create the new service with correct path:
echo    sc create "SlackCIBuildServer" binPath= "\"C:\Users\carbo\Desktop\BitBucket\tools\SlackCI\SlackCIApp\publish\SlackCIApp.exe\" --service" DisplayName= "SlackCI Build Server" start= auto
echo.
echo 3. Set service description:
echo    sc description "SlackCIBuildServer" "Automated build server that listens for Slack commands to trigger builds"
echo.
echo 4. Configure service recovery:
echo    sc failure "SlackCIBuildServer" reset= 3600 actions= restart/5000/restart/10000/restart/30000
echo.
echo 5. Start the service:
echo    sc start "SlackCIBuildServer"
echo.
echo 6. Check service status:
echo    sc query "SlackCIBuildServer"
echo.
pause
