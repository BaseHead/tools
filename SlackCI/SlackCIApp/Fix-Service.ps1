# PowerShell script to fix SlackCI service
# Run this in PowerShell as Administrator

$serviceName = "SlackCIBuildServer"
$servicePath = "C:\Users\carbo\Desktop\BitBucket\tools\SlackCI\SlackCIApp\publish\SlackCIApp.exe"
$displayName = "SlackCI Build Server"
$description = "Automated build server that listens for Slack commands to trigger builds"

Write-Host "Fixing SlackCI Build Server service..." -ForegroundColor Yellow

# Stop and remove existing service
Write-Host "Stopping existing service..." -ForegroundColor Cyan
Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue

Write-Host "Removing existing service..." -ForegroundColor Cyan
sc.exe delete $serviceName

Start-Sleep -Seconds 2

# Check if executable exists
if (!(Test-Path $servicePath)) {
    Write-Host "ERROR: Service executable not found at: $servicePath" -ForegroundColor Red
    Write-Host "Please make sure the project is published first: dotnet publish -c Release -o publish" -ForegroundColor Red
    exit 1
}

# Create new service
Write-Host "Creating service with correct path..." -ForegroundColor Cyan
sc.exe create $serviceName binPath= "`"$servicePath`" --service" DisplayName= $displayName start= auto

# Set description
Write-Host "Setting service description..." -ForegroundColor Cyan
sc.exe description $serviceName $description

# Configure failure recovery
Write-Host "Configuring service recovery..." -ForegroundColor Cyan
sc.exe failure $serviceName reset= 3600 actions= restart/5000/restart/10000/restart/30000

# Start the service
Write-Host "Starting service..." -ForegroundColor Cyan
sc.exe start $serviceName

Start-Sleep -Seconds 3

# Check status
Write-Host "Service status:" -ForegroundColor Green
sc.exe query $serviceName

Write-Host "`nService installation completed!" -ForegroundColor Green
Write-Host "You can also manage the service through:" -ForegroundColor Yellow
Write-Host "- Services.msc (Services management console)" -ForegroundColor Yellow
Write-Host "- The provided batch files (start-service.bat, stop-service.bat, etc.)" -ForegroundColor Yellow
