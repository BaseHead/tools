using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Linq;
using SlackCIApp.Config;
using Slack.Webhooks;
using System.Text.Json;
using Serilog;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace SlackCIApp
{
    public class SlackCIWorker : BackgroundService
    {
        private readonly ILogger<SlackCIWorker> _logger;
        private readonly IConfiguration _configuration;
        private readonly SlackCISettings _settings;
        private readonly HttpClient _httpClient;
        private DateTime _lastCheckTime = DateTime.MinValue;

        public SlackCIWorker(ILogger<SlackCIWorker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _settings = _configuration.GetSection("SlackCISettings").Get<SlackCISettings>() 
                ?? throw new InvalidOperationException("Failed to load SlackCISettings from configuration");
            _httpClient = new HttpClient();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("SlackCI Windows Service starting up");
                
                // Initialize Serilog if not already configured
                if (Log.Logger == Serilog.Core.Logger.None)
                {
                    var logDirectory = Path.GetDirectoryName(_settings.LogFilePath);
                    if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
                    {
                        Directory.CreateDirectory(logDirectory);
                    }
                    
                    Log.Logger = new LoggerConfiguration()
                        .MinimumLevel.Debug()
                        .WriteTo.Console(restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information)
                        .WriteTo.File(
                            _settings.LogFilePath, 
                            rollingInterval: RollingInterval.Day,
                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                        .CreateLogger();
                }

                Log.Information("SlackCI service started");
                SendSlackMessage(":wave: BaseHead CI Bot service is online and listening for build commands!");

                // Start polling for Slack messages if configured
                if (!string.IsNullOrEmpty(_settings.SlackBotToken) && !string.IsNullOrEmpty(_settings.ChannelId))
                {
                    _ = Task.Run(() => PollSlackMessages(stoppingToken), stoppingToken);
                    _logger.LogInformation("Slack message polling enabled");
                }
                else
                {
                    _logger.LogWarning("Slack bot token or channel ID not configured. Message polling disabled.");
                }

                // Keep the service running
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SlackCI service");
                Log.Error(ex, "Error in SlackCI service");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SlackCI service is stopping");
            SendSlackMessage(":wave: BaseHead CI Bot service is shutting down.");
            Log.Information("SlackCI service stopped");
            
            await base.StopAsync(cancellationToken);
        }

        private async Task PollSlackMessages(CancellationToken cancellationToken)
        {
            Log.Information("Now listening for Slack messages");
            _lastCheckTime = DateTime.UtcNow;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(10000, cancellationToken);

                    var timestamp = DateTimeToUnixTimestamp(_lastCheckTime);
                    var url = $"https://slack.com/api/conversations.history?channel={_settings.ChannelId}&oldest={timestamp}";

                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("Authorization", $"Bearer {_settings.SlackBotToken}");

                    using var response = await _httpClient.SendAsync(request, cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        Log.Warning("Failed to get messages from Slack: {StatusCode}", response.StatusCode);
                        continue;
                    }

                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    var json = JsonDocument.Parse(content);

                    if (!json.RootElement.GetProperty("ok").GetBoolean())
                    {
                        var error = json.RootElement.GetProperty("error").GetString();
                        Log.Warning("Slack API error: {Error}", error);
                        continue;
                    }

                    var messages = json.RootElement.GetProperty("messages");
                    foreach (var message in messages.EnumerateArray())
                    {
                        if (message.TryGetProperty("subtype", out var subtype) && subtype.GetString() == "bot_message")
                            continue;

                        var text = message.GetProperty("text").GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            await ProcessCommand(text);
                        }
                    }

                    _lastCheckTime = DateTime.UtcNow;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error polling Slack messages: {Error}", ex.Message);
                    await Task.Delay(30000, cancellationToken);
                }
            }
        }

        private async Task ProcessCommand(string input)
        {
            string inputLower = input.ToLower().Trim();
            
            if (inputLower == "1" || inputLower == _settings.TriggerBothCommand?.ToLower())
            {
                Log.Information("Starting build for both Windows and Mac in parallel...");
                SendSlackMessage(":rocket: Starting build for both Windows and Mac in parallel...");
                
                // Run both builds in parallel
                var windowsBuildTask = TriggerWindowsBuild();
                var macBuildTask = TriggerMacBuild();
                
                // Wait for both builds to complete
                await Task.WhenAll(windowsBuildTask, macBuildTask);
                
                Log.Information("Both build processes have completed");
                SendSlackMessage(":checkered_flag: Both build processes have completed");
            }
            else if (inputLower == "2" || inputLower == _settings.TriggerWindowsCommand?.ToLower())
            {
                Log.Information("Starting Windows build...");
                SendSlackMessage(":rocket: Starting Windows build...");
                
                await TriggerWindowsBuild();
            }
            else if (inputLower == "3" || inputLower == _settings.TriggerMacCommand?.ToLower())
            {
                Log.Information("Starting Mac build...");
                SendSlackMessage(":rocket: Starting Mac build...");
                
                await TriggerMacBuild();
            }
            else if (inputLower == "4" || inputLower == _settings.TriggerLLSCommand?.ToLower())
            {
                Log.Information("Starting LLS build...");
                SendSlackMessage(":rocket: Starting LLS build...");
                
                await TriggerLLSBuild();
            }
            else if (!string.IsNullOrWhiteSpace(input))
            {
                Log.Information("Unknown command: {Command}", input);
                SendSlackMessage($":question: Unknown command: {input}");
            }
        }

        #region Build Methods
        
        private async Task TriggerWindowsBuild()
        {
            try
            {
                // Clean up the basehead-pc folder before starting the build
                var baseheadPcDir = Path.Combine(_settings.WindowsRepoPath, "basehead", "basehead-pc");
                if (Directory.Exists(baseheadPcDir))
                {
                    try
                    {
                        Log.Information("Cleaning up previous build output at: {Dir}", baseheadPcDir);
                        SendSlackMessage(":broom: Cleaning up previous PC build output...");
                        Directory.Delete(baseheadPcDir, recursive: true);
                        
                        // Wait a moment to ensure all file handles are released
                        await Task.Delay(2000);
                        Log.Information("Successfully deleted previous build output directory");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to delete basehead-pc directory: {Message}", ex.Message);
                        SendSlackMessage(":warning: Failed to clean up previous build output. This might cause issues with the build.");
                    }
                }

                if (string.IsNullOrEmpty(_settings.WindowsBuildScriptPath) || !File.Exists(_settings.WindowsBuildScriptPath))
                {
                    Log.Warning("Windows build script not found. Running in simulation mode.");
                    await Task.Delay(3000);
                    
                    Log.Information("Windows build completed successfully (simulated)");
                    SendSlackMessage(":white_check_mark: Windows build completed successfully (simulated)!");
                    SendSlackMessage(":information_source: To enable actual builds, please configure a valid build script path in appsettings.json");
                    return;
                }

                // Attempt to pull latest changes if repo path is configured and Git pull is enabled for service
                if (!string.IsNullOrEmpty(_settings.WindowsRepoPath) && _settings.EnableGitPullForService)
                {
                    SendSlackMessage(":arrow_down: Pulling latest changes from Git for PC...");
                    Log.Information("Pulling latest changes from Git repository at {RepoPath}", _settings.WindowsRepoPath);
                    
                    var sshService = new SshService(_settings, Log.Logger);
                    var gitService = new GitService(_settings, Log.Logger, sshService);
                    var pullResult = await gitService.PullLatestChangesWindowsAsync();
                    
                    if (pullResult.Success)
                    {
                        Log.Information("Git pull successful: {Message}", pullResult.Message);
                        SendSlackMessage($":white_check_mark: Git pull successful: {pullResult.Message}");
                    }
                    else
                    {
                        Log.Error("Git pull failed: {Message}", pullResult.Message);
                        SendSlackMessage($":x: Build aborted: Git pull failed: {pullResult.Message}");
                        return; // Exit without running build script
                    }
                }
                else if (!string.IsNullOrEmpty(_settings.WindowsRepoPath) && !_settings.EnableGitPullForService)
                {
                    Log.Information("Git pull skipped (disabled for service mode)");
                    SendSlackMessage(":information_source: Git pull skipped (disabled for service mode)");
                }

                // Update version in csproj to today's date before building
                var csprojUpdate = UpdateCsprojVersionToToday();
                if (csprojUpdate.Success)
                {
                    SendSlackMessage($":calendar: {csprojUpdate.Message}");
                }
                else
                {
                    SendSlackMessage($":warning: Version update warning: {csprojUpdate.Message}");
                }

                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = _settings.WindowsBuildScriptPath,
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WorkingDirectory = Path.GetDirectoryName(_settings.WindowsBuildScriptPath)
                };

                Log.Information("Executing Windows build script: {ScriptPath}", _settings.WindowsBuildScriptPath);
                SendSlackMessage($":arrow_forward: Windows build v{csprojUpdate.Version} starting...");
                process.Start();
                await Task.Run(() => process.WaitForExit());

                if (process.ExitCode == 0)
                {
                    Log.Information("Windows build completed successfully");
                    SendSlackMessage(":white_check_mark: Windows build completed successfully!");

                    // Sync version from csproj to Advanced Installer before building
                    var versionSync = SyncVersionToAdvancedInstaller();
                    if (versionSync.Success)
                    {
                        SendSlackMessage($":label: {versionSync.Message}");
                    }
                    else
                    {
                        SendSlackMessage($":warning: Version sync warning: {versionSync.Message}");
                    }

                    // Run Advanced Installer to build the PC installer
                    var advancedInstallerPath = _settings.AdvancedInstallerProjectPath;
                    if (File.Exists(advancedInstallerPath))
                    {
                        await RunAdvancedInstaller(advancedInstallerPath, "PC", _settings.WindowsInstallerPath, versionSync.Version);
                    }
                    else
                    {
                        Log.Warning("Advanced Installer project not found at: {Path}", advancedInstallerPath);
                        SendSlackMessage($":warning: Advanced Installer project not found at: {advancedInstallerPath}");
                    }
                }
                else
                {
                    Log.Error("Windows build failed with exit code {ExitCode}", process.ExitCode);
                    SendSlackMessage($":x: Windows build failed with exit code {process.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during Windows build: {ErrorMessage}", ex.Message);
                SendSlackMessage($":x: Error during Windows build: {ex.Message}");
            }
        }
        
        private async Task TriggerMacBuild()
        {
            try
            {
                var sshService = new SlackCIApp.SshService(_settings, Log.Logger);
                
                // Attempt to pull latest changes if repo path is configured and Git pull is enabled for service
                if (!string.IsNullOrEmpty(_settings.MacRepoPath) && _settings.EnableGitPullForService)
                {
                    SendSlackMessage(":arrow_down: Pulling latest changes from Git on Mac...");
                    Log.Information("Pulling latest changes from Git repository at {RepoPath}", _settings.MacRepoPath);
                    
                    var gitService = new GitService(_settings, Log.Logger, sshService);
                    var pullResult = await gitService.PullLatestChangesMacAsync();
                    
                    if (pullResult.Success)
                    {
                        Log.Information("Git pull on Mac successful: {Message}", pullResult.Message);
                        SendSlackMessage($":white_check_mark: Git pull on Mac successful: {pullResult.Message}");
                    }
                    else
                    {
                        Log.Error("Git pull on Mac failed: {Message}", pullResult.Message);
                        SendSlackMessage($":x: Git pull on Mac failed: {pullResult.Message}");
                        
                        // Check if this is a publickey error and try to help
                        if (pullResult.Message.Contains("Permission denied (publickey)"))
                        {
                            SendSlackMessage(":key: Attempting to generate and configure SSH key for GitHub...");
                            var (keyGenSuccess, sshPublicKey, keyGenMessage) = await sshService.GenerateMacSshKeyAsync();
                            
                            if (keyGenSuccess)
                            {
                                SendSlackMessage(":white_check_mark: SSH key generated successfully!");
                                SendSlackMessage($":information_source: Please add this public key to your GitHub repository:\n```\n{sshPublicKey}\n```");
                                SendSlackMessage("Once you've added the key, try the build again.");
                            }
                            else
                            {
                                SendSlackMessage($":x: Failed to generate SSH key: {keyGenMessage}");
                            }
                        }
                        return; // Exit if Git pull fails
                    }
                }
                else if (!string.IsNullOrEmpty(_settings.MacRepoPath) && !_settings.EnableGitPullForService)
                {
                    Log.Information("Git pull skipped for Mac (disabled for service mode)");
                    SendSlackMessage(":information_source: Git pull skipped for Mac (disabled for service mode)");
                }
                
                Log.Information("Starting Mac build via SSH");
                SendSlackMessage(":arrow_forward: Mac build starting...");
                
                var (success, output) = await sshService.ExecuteMacBuildAsync();

                if (success)
                {
                    Log.Information("Mac build completed successfully");
                    SendSlackMessage(":white_check_mark: Mac build completed successfully!");

                    // Get version from csproj for Mac installer filename
                    var macVersionSync = SyncVersionToAdvancedInstaller();
                    string macVersion = macVersionSync.Version;
                    string macInstallerFileName = string.IsNullOrEmpty(macVersion)
                        ? "Install basehead v2026.pkg"
                        : $"Install basehead v{macVersion}.pkg";

                    // Set Mac installer path based on convention with version
                    _settings.MacInstallerPath = $"/Users/steve/Desktop/GitHub/basehead/build/{macInstallerFileName}";
                    if (!string.IsNullOrEmpty(_settings.MacInstallerPath))
                    {
                        // Copy Mac installer directly to BeeStation from Mac
                        SendSlackMessage($":arrow_down: Copying Mac installer to BeeStation...");

                        var (copySuccess, fileSizeMB) = await sshService.CopyInstallerToNetworkAsync(_settings.MacInstallerPath, macInstallerFileName);
                        if (copySuccess)
                        {
                            SendSlackMessage($":file_folder: Mac installer copied to network: {macInstallerFileName} ({fileSizeMB:F2} MB)");

                            // Add web download link
                            string webDownloadUrl = "http://j8cd6qcvrjt956vxvyixiuoss2n6mes.quickconnect.to/sharing/fcRcMqktX";
                            SendSlackMessage($":globe_with_meridians: Web download link: {webDownloadUrl}");
                        }
                        else
                        {
                            Log.Warning("Failed to copy Mac installer to BeeStation");
                            SendSlackMessage($":warning: Failed to copy Mac installer to BeeStation. File may be on Mac at: {_settings.MacInstallerPath}");

                            // Still show web download link
                            string webDownloadUrl = "http://j8cd6qcvrjt956vxvyixiuoss2n6mes.quickconnect.to/sharing/fcRcMqktX";
                            SendSlackMessage($":globe_with_meridians: Web download link: {webDownloadUrl}");
                        }
                    }
                }
                else
                {
                    Log.Error("Mac build failed: {Error}", output);
                    SendSlackMessage($":x: Mac build failed: {output}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during Mac build: {ErrorMessage}", ex.Message);
                SendSlackMessage($":x: Error during Mac build: {ex.Message}");
            }
        }

        private async Task TriggerLLSBuild()
        {
            try
            {
                if (string.IsNullOrEmpty(_settings.LLSBuildScriptPath) || !File.Exists(_settings.LLSBuildScriptPath))
                {
                    Log.Warning("LLS build script not found. Running in simulation mode.");
                    await Task.Delay(3000);
                    
                    Log.Information("LLS build completed successfully (simulated)");
                    SendSlackMessage(":white_check_mark: LLS build completed successfully (simulated)!");
                    SendSlackMessage(":information_source: To enable actual builds, please configure a valid LLS build script path in appsettings.json");
                    return;
                }

                // LLS is part of the same repository as Windows builds
                // So we skip Git pull for LLS builds - it should be up to date already
                // If you need to pull for LLS specifically, run Windows build first or manually pull
                Log.Information("Skipping Git pull for LLS build (LLS is part of the main basehead repository)");
                SendSlackMessage(":information_source: Skipping Git pull for LLS (part of main repository)");

                // Update LLS version in csproj to today's date before building
                var llsCsprojUpdate = UpdateLLSCsprojVersionToToday();
                if (llsCsprojUpdate.Success)
                {
                    SendSlackMessage($":calendar: {llsCsprojUpdate.Message}");
                }
                else
                {
                    SendSlackMessage($":warning: LLS version update warning: {llsCsprojUpdate.Message}");
                }

                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = _settings.LLSBuildScriptPath,
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WorkingDirectory = Path.GetDirectoryName(_settings.LLSBuildScriptPath)
                };

                Log.Information("Executing LLS build script: {ScriptPath}", _settings.LLSBuildScriptPath);
                SendSlackMessage($":arrow_forward: LLS build v{llsCsprojUpdate.Version} starting...");
                process.Start();
                await Task.Run(() => process.WaitForExit());

                if (process.ExitCode == 0)
                {
                    Log.Information("LLS build completed successfully");
                    SendSlackMessage(":white_check_mark: LLS build completed successfully!");

                    // Sync LLS version to Advanced Installer before building installer
                    var llsVersionSync = SyncLLSVersionToAdvancedInstaller();
                    if (llsVersionSync.Success)
                    {
                        SendSlackMessage($":label: {llsVersionSync.Message}");
                    }
                    else
                    {
                        SendSlackMessage($":warning: LLS version sync warning: {llsVersionSync.Message}");
                    }

                    // Run Advanced Installer to build the LLS installer
                    var advancedInstallerPath = _settings.LLSAdvancedInstallerProjectPath;
                    if (File.Exists(advancedInstallerPath))
                    {
                        await RunAdvancedInstaller(advancedInstallerPath, "LLS", _settings.LLSInstallerPath, llsVersionSync.Version);
                    }
                    else
                    {
                        Log.Warning("Advanced Installer LLS project not found at: {Path}", advancedInstallerPath);
                        SendSlackMessage($":warning: Advanced Installer LLS project not found at: {advancedInstallerPath}");
                    }

                    // Build the Linux .deb package via WSL
                    await BuildLLSLinuxPackage(llsVersionSync.Version);

                    // Build macOS via SSH (includes signing and notarization)
                    await BuildLLSMacPackage(llsVersionSync.Version);
                }
                else
                {
                    Log.Error("LLS build failed with exit code {ExitCode}", process.ExitCode);
                    SendSlackMessage($":x: LLS build failed with exit code {process.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during LLS build: {ErrorMessage}", ex.Message);
                SendSlackMessage($":x: Error during LLS build: {ex.Message}");
            }
        }

        /// <summary>
        /// Builds the macOS packages for LLS via SSH (includes signing and notarization)
        /// </summary>
        private async Task BuildLLSMacPackage(string version)
        {
            try
            {
                Log.Information("Starting macOS LLS build via SSH...");
                SendSlackMessage(":apple: Building macOS LLS package via SSH (includes signing & notarization)...");

                var sshService = new SshService(_settings, Log.Logger);

                // First, pull latest changes on Mac
                SendSlackMessage(":arrow_down: Pulling latest changes on Mac...");
                var (pullSuccess, pullError) = await sshService.ExecuteCommandAsync($"cd {_settings.LLSMacRepoPath} && git pull");
                if (!pullSuccess)
                {
                    Log.Warning("Git pull on Mac failed: {Error}", pullError);
                    SendSlackMessage($":warning: Git pull on Mac failed: {pullError}");
                    // Continue anyway - might just be up to date
                }

                // Execute the Mac build script
                var (success, output) = await sshService.ExecuteLLSMacBuildAsync();

                if (success)
                {
                    Log.Information("macOS LLS build completed successfully");
                    SendSlackMessage(":white_check_mark: macOS LLS build completed successfully!");

                    // Copy Mac installers directly to BeeStation from Mac
                    try
                    {
                        // Copy ARM64 installer
                        string arm64RemotePath = $"{_settings.LLSMacRepoPath}/Installer/macOS/Install basehead.LLS v{version} (ARM64).pkg";
                        string arm64FileName = $"Install basehead.LLS v{version} (ARM64).pkg";

                        var (arm64Success, arm64Size) = await sshService.CopyInstallerToNetworkAsync(arm64RemotePath, arm64FileName, "LLS");
                        if (arm64Success)
                        {
                            SendSlackMessage($":file_folder: macOS ARM64 installer ({arm64Size:F2} MB) copied to network");
                        }
                        else
                        {
                            Log.Warning("Failed to copy ARM64 installer to BeeStation");
                            SendSlackMessage(":warning: Failed to copy ARM64 installer to BeeStation");
                        }

                        // Copy Intel installer
                        string x64RemotePath = $"{_settings.LLSMacRepoPath}/Installer/macOS/Install basehead.LLS v{version} (Intel).pkg";
                        string x64FileName = $"Install basehead.LLS v{version} (Intel).pkg";

                        var (x64Success, x64Size) = await sshService.CopyInstallerToNetworkAsync(x64RemotePath, x64FileName, "LLS");
                        if (x64Success)
                        {
                            SendSlackMessage($":file_folder: macOS Intel installer ({x64Size:F2} MB) copied to network");
                        }
                        else
                        {
                            Log.Warning("Failed to copy Intel installer to BeeStation");
                            SendSlackMessage(":warning: Failed to copy Intel installer to BeeStation");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error copying Mac installers to BeeStation: {Error}", ex.Message);
                        SendSlackMessage($":warning: Error handling Mac installers: {ex.Message}");
                    }
                }
                else
                {
                    Log.Error("macOS LLS build failed: {Output}", output);
                    SendSlackMessage($":x: macOS LLS build failed");
                    if (output.Length > 500)
                    {
                        output = "..." + output.Substring(Math.Max(0, output.Length - 500));
                    }
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        SendSlackMessage($"```\n{output}\n```");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during macOS LLS build: {ErrorMessage}", ex.Message);
                SendSlackMessage($":x: Error during macOS LLS build: {ex.Message}");
            }
        }

        /// <summary>
        /// Builds the Linux .deb package for LLS via WSL
        /// </summary>
        private async Task BuildLLSLinuxPackage(string version)
        {
            try
            {
                Log.Information("Starting Linux LLS build via WSL...");
                SendSlackMessage(":penguin: Building Linux .deb package via WSL...");

                // Convert Windows path to WSL path
                string llsRepoPath = _settings.LLSRepoPath;
                string wslPath = llsRepoPath.Replace("C:", "/mnt/c").Replace("\\", "/");
                string wslCommand = $"cd {wslPath} && bash publish-linux-x64.sh";

                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "wsl",
                    Arguments = $"bash -c \"{wslCommand}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = false
                };

                var outputBuilder = new StringBuilder();
                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                        Log.Information("WSL LLS: {Output}", e.Data);
                    }
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                        Log.Warning("WSL LLS Error: {Output}", e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                bool completed = await Task.Run(() => process.WaitForExit(600000)); // 10 minute timeout

                if (!completed)
                {
                    Log.Error("Linux LLS build timed out after 10 minutes");
                    SendSlackMessage(":x: Linux LLS build timed out after 10 minutes");
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error killing WSL process");
                    }
                    return;
                }

                if (process.ExitCode == 0)
                {
                    Log.Information("Linux LLS .deb package built successfully");
                    SendSlackMessage(":white_check_mark: Linux LLS .deb package built successfully!");

                    // Look for the .deb file and copy to network
                    try
                    {
                        string debOutputDir = Path.Combine(_settings.LLSRepoPath, "publish-linux-x64");
                        if (Directory.Exists(debOutputDir))
                        {
                            string[] debFiles = Directory.GetFiles(debOutputDir, "*.deb");
                            if (debFiles.Length > 0)
                            {
                                string debFile = debFiles.OrderByDescending(f => new FileInfo(f).CreationTime).First();
                                string fileName = Path.GetFileName(debFile);
                                var fileSize = new FileInfo(debFile).Length / (1024.0 * 1024.0);
                                SendSlackMessage($":package: Linux package built: {fileName} ({fileSize:F2} MB)");

                                // Copy to network path
                                string networkPath = @"\\BeeStation\home\Files\build-server\LLS";
                                string versionedFileName = GetVersionedFileName(fileName, version);
                                string networkFilePath = Path.Combine(networkPath, versionedFileName);

                                if (!Directory.Exists(networkPath))
                                {
                                    Directory.CreateDirectory(networkPath);
                                }

                                File.Copy(debFile, networkFilePath, true);
                                SendSlackMessage($":file_folder: Linux package copied to network: {versionedFileName}");
                            }
                            else
                            {
                                Log.Warning("No .deb files found in output directory: {Dir}", debOutputDir);
                                SendSlackMessage($":warning: No .deb files found in output directory");
                            }
                        }
                        else
                        {
                            Log.Warning("Linux output directory not found: {Dir}", debOutputDir);
                            SendSlackMessage($":warning: Linux output directory not found: {debOutputDir}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error copying Linux .deb to network: {Error}", ex.Message);
                        SendSlackMessage($":warning: Failed to copy Linux .deb to network: {ex.Message}");
                    }
                }
                else
                {
                    Log.Error("Linux LLS build failed with exit code {ExitCode}", process.ExitCode);
                    SendSlackMessage($":x: Linux LLS build failed with exit code {process.ExitCode}");
                    var output = outputBuilder.ToString().Trim();
                    if (output.Length > 500)
                    {
                        output = "..." + output.Substring(Math.Max(0, output.Length - 500));
                    }
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        SendSlackMessage($"```\n{output}\n```");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during Linux LLS build: {ErrorMessage}", ex.Message);
                SendSlackMessage($":x: Error during Linux LLS build: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        private async Task RunAdvancedInstaller(string projectPath, string buildType, string outputPath, string version = "")
        {
            try
            {
                Log.Information($"Running Advanced Installer to build {buildType} installer: {{Path}}", projectPath);
                string versionDisplay = string.IsNullOrEmpty(version) ? "" : $" v{version}";
                SendSlackMessage($":gear: Creating {buildType} installer{versionDisplay} with Advanced Installer...");
                var outputBuilder = new StringBuilder();

                var appDir = Directory.GetCurrentDirectory();
                var batchFile = Path.Combine(appDir, $"RunAdvancedInstaller_{buildType}.cmd");
                string batchContent = $"@echo off\necho Running Advanced Installer for {buildType} with elevated privileges...\nset AI_LOG=\"%~dp0AdvancedInstaller_{buildType}.log\"\necho Starting {buildType} build at %DATE% %TIME% > %AI_LOG%\n\"{_settings.AdvancedInstallerExePath}\" /build \"{projectPath}\" >> %AI_LOG% 2>&1\nset EXIT_CODE=%errorlevel%\necho {buildType} build finished with exit code %EXIT_CODE% at %DATE% %TIME% >> %AI_LOG%\nexit /b %EXIT_CODE%";
                await File.WriteAllTextAsync(batchFile, batchContent);
                
                using var aiProcess = new Process();
                aiProcess.StartInfo = new ProcessStartInfo
                {
                    FileName = batchFile,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = false,
                    WorkingDirectory = Path.GetDirectoryName(projectPath)
                };

                if (!File.Exists(_settings.AdvancedInstallerExePath))
                {
                    Log.Error("Advanced Installer executable not found at: {Path}", _settings.AdvancedInstallerExePath);
                    SendSlackMessage($":x: Advanced Installer executable not found at: {_settings.AdvancedInstallerExePath}");
                    return;
                }
                
                try
                {
                    aiProcess.Start();
                    Log.Information($"Advanced Installer process started for {buildType}");
                    aiProcess.OutputDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            outputBuilder.AppendLine(e.Data);
                            Log.Information($"AI {buildType}: {{Output}}", e.Data);
                        }
                    };
                    aiProcess.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            outputBuilder.AppendLine(e.Data);
                            Log.Error($"AI {buildType} Error: {{Output}}", e.Data);
                        }
                    };
                    aiProcess.BeginOutputReadLine();
                    aiProcess.BeginErrorReadLine();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to start Advanced Installer process for {buildType}");
                    SendSlackMessage($":x: Failed to start Advanced Installer for {buildType}: {ex.Message}");
                    return;
                }
                
                bool completed = await Task.Run(() =>
                {
                    try
                    {
                        return aiProcess.WaitForExit(600000);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Error waiting for Advanced Installer {buildType} process");
                        return false;
                    }
                });
                
                if (!completed)
                {
                    Log.Error($"Advanced Installer {buildType} build timed out after 10 minutes");
                    SendSlackMessage($":x: Advanced Installer {buildType} build timed out after 10 minutes");
                    try
                    {
                        if (!aiProcess.HasExited)
                        {
                            aiProcess.Kill();
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "taskkill",
                                Arguments = "/F /IM \"AdvancedInstaller.com\" /T",
                                UseShellExecute = true,
                                Verb = "runas",
                                CreateNoWindow = true
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Error killing Advanced Installer {buildType} process");
                    }
                    return;
                }
                
                var exitCode = aiProcess.ExitCode;
                Log.Information($"Advanced Installer {buildType} process completed with exit code: {{ExitCode}}", exitCode);
                
                if (exitCode == 0)
                {
                    Log.Information($"{buildType} installer built successfully");
                    SendSlackMessage($":white_check_mark: {buildType} installer built successfully!");
                    
                    await HandleInstallerOutput(projectPath, buildType, outputPath, version);
                }
                else
                {
                    Log.Error($"Advanced Installer {buildType} failed with exit code {{ExitCode}}", aiProcess.ExitCode);
                    SendSlackMessage($":x: {buildType} installer build failed with exit code {aiProcess.ExitCode}");
                    
                    var output = outputBuilder.ToString().Trim();
                    if (output.Length > 500)
                    {
                        output = "..." + output.Substring(Math.Max(0, output.Length - 500));
                    }
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        SendSlackMessage($"```\n{output}\n```");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error running Advanced Installer for {buildType}: {{ErrorMessage}}", ex.Message);
                SendSlackMessage($":x: Error Creating {buildType} installer: {ex.Message}");
            }
        }

        private Task HandleInstallerOutput(string projectPath, string buildType, string settingsInstallerPath, string version = "")
        {
            try
            {
                string installerDir = Path.GetDirectoryName(projectPath) ?? Directory.GetCurrentDirectory();
                Log.Information($"HandleInstallerOutput called for {buildType}. Project: {{ProjectPath}}, InstallerDir: {{InstallerDir}}", projectPath, installerDir);

                string[] potentialOutputDirs = new[]
                {
                    Path.Combine(installerDir, "Output"),
                    Path.Combine(installerDir, "Builds"),
                    Path.Combine(installerDir, "Setup"),
                    installerDir
                };

                // Log which directories exist
                foreach (var dir in potentialOutputDirs)
                {
                    Log.Information($"Checking if directory exists: {{Dir}} = {{Exists}}", dir, Directory.Exists(dir));
                }

                string outputDir = potentialOutputDirs.FirstOrDefault(dir => Directory.Exists(dir)) ?? installerDir;
                Log.Information($"Checking for {buildType} installer files in: {{Dir}}", outputDir);

                if (Directory.Exists(outputDir))
                {
                    string[] installerFiles = Directory.GetFiles(outputDir, "*.exe")
                        .Concat(Directory.GetFiles(outputDir, "*.msi"))
                        .ToArray();

                    Log.Information($"Found {{Count}} installer files in {{Dir}}", installerFiles.Length, outputDir);
                    foreach (var file in installerFiles)
                    {
                        Log.Information($"  - {{File}}", file);
                    }
                if (installerFiles.Length > 0)
                {
                    var latestInstaller = installerFiles.OrderByDescending(f => new FileInfo(f).CreationTime).First();
                    var fileName = Path.GetFileName(latestInstaller);
                    var fileSize = new FileInfo(latestInstaller).Length / (1024.0 * 1024.0);
                    
                    // Update settings based on build type
                    if (buildType == "PC")
                        _settings.WindowsInstallerPath = latestInstaller;
                    else if (buildType == "LLS")
                        _settings.LLSInstallerPath = latestInstaller;
                    
                    SaveSettings();
                    SendSlackMessage($":package: {buildType} installer built: {fileName} ({fileSize:F2} MB)");

                    // Copy to network path with versioned filename
                    try
                    {
                        string networkPath = @"\\BeeStation\home\Files\build-server\LLS";
                        string versionedFileName = GetVersionedFileName(fileName, version);
                        string networkFilePath = Path.Combine(networkPath, versionedFileName);
                        Log.Information($"Copying {buildType} installer to network path: {{NetworkPath}}", networkFilePath);

                        if (!Directory.Exists(networkPath))
                        {
                            Directory.CreateDirectory(networkPath);
                        }

                        File.Copy(latestInstaller, networkFilePath, true);
                        SendSlackMessage($":file_folder: {buildType} installer copied to network: {versionedFileName}");

                        // Add web download link
                        string webDownloadUrl = "http://j8cd6qcvrjt956vxvyixiuoss2n6mes.quickconnect.to/sharing/fcRcMqktX";
                        SendSlackMessage($":globe_with_meridians: Web download link: {webDownloadUrl}");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Failed to copy {buildType} installer to network path: {{Error}}", ex.Message);
                        SendSlackMessage($":warning: Failed to copy {buildType} installer to network path: {ex.Message}");
                    }
                }
                else
                {
                    Log.Warning($"No {buildType} installer files found in output directory: {{Dir}}", installerDir);
                }
            }
            else
            {
                Log.Warning($"{buildType} output directory not found: {{Dir}}", installerDir);
            }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error in HandleInstallerOutput for {buildType}: {{ErrorMessage}}", ex.Message);
                SendSlackMessage($":warning: Error processing {buildType} installer output: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        private void SendSlackMessage(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(_settings.SlackWebhookUrl))
                {
                    Log.Warning("Slack webhook URL not configured. Message not sent: {Message}", message);
                    return;
                }
                
                var webhookClient = new SlackClient(_settings.SlackWebhookUrl);
                var slackMessage = new SlackMessage
                {
                    Text = message,
                    Channel = _settings.ChannelName,
                    Username = "BaseHead CI",
                    IconEmoji = ":robot_face:"
                };
                
                webhookClient.Post(slackMessage);
                Log.Information("Sent message to Slack: {Message}", message);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send message to Slack: {ErrorMessage}", ex.Message);
            }
        }

        private static double DateTimeToUnixTimestamp(DateTime dateTime)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            if (dateTime == DateTime.MinValue)
            {
                return Math.Round((DateTime.UtcNow.AddMinutes(-5) - epoch).TotalSeconds, 3);
            }
            return Math.Round((dateTime - epoch).TotalSeconds, 3);
        }

        private void SaveSettings()
        {
            try
            {
                string appSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                if (File.Exists(appSettingsPath))
                {
                    // Read existing JSON
                    string json = File.ReadAllText(appSettingsPath);
                    using JsonDocument document = JsonDocument.Parse(json);
                    
                    // Create new JSON with updated settings
                    using var stream = new MemoryStream();
                    using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
                    
                    writer.WriteStartObject();
                    
                    // Copy all root properties
                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        if (property.Name == "SlackCISettings")
                        {
                            // Write updated SlackCISettings
                            writer.WritePropertyName("SlackCISettings");
                            JsonSerializer.Serialize(writer, _settings);
                        }
                        else
                        {
                            // Copy other properties as-is
                            property.WriteTo(writer);
                        }
                    }
                    
                    writer.WriteEndObject();
                    writer.Flush();
                    
                    // Write back to file
                    File.WriteAllBytes(appSettingsPath, stream.ToArray());
                    Log.Information("Settings saved successfully");
                }
                else
                {
                    Log.Warning("appsettings.json not found, cannot save settings");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving settings: {Message}", ex.Message);
            }
        }

        #endregion

        public override void Dispose()
        {
            _httpClient?.Dispose();
            base.Dispose();
        }

        /// <summary>
        /// Syncs the version from the csproj file to the Advanced Installer .aip file
        /// Uses Advanced Installer command line to properly regenerate ProductCode and component GUIDs
        /// </summary>
        private (bool Success, string Version, string Message) SyncVersionToAdvancedInstaller()
        {
            try
            {
                // Find the csproj file in the repo
                string repoPath = _settings.WindowsRepoPath;
                string csprojPath = Path.Combine(repoPath, "basehead", "basehead.csproj");

                if (!File.Exists(csprojPath))
                {
                    Log.Warning("Could not find csproj file at: {Path}", csprojPath);
                    return (false, "", $"Could not find csproj file at: {csprojPath}");
                }

                // Read version from csproj
                var csprojDoc = XDocument.Load(csprojPath);
                var versionElement = csprojDoc.Descendants("Version").FirstOrDefault();

                if (versionElement == null)
                {
                    Log.Warning("No <Version> element found in csproj file");
                    return (false, "", "No <Version> element found in csproj file");
                }

                string version = versionElement.Value;
                Log.Information("Found version in csproj: {Version}", version);

                // Update Advanced Installer .aip file
                string aipPath = _settings.AdvancedInstallerProjectPath;
                if (!File.Exists(aipPath))
                {
                    Log.Warning("Could not find Advanced Installer project at: {Path}", aipPath);
                    return (false, version, $"Could not find Advanced Installer project at: {aipPath}");
                }

                // Check current version in .aip file
                string aipContent = File.ReadAllText(aipPath);
                var versionRegex = new Regex(@"<ROW Property=""ProductVersion"" Value=""([^""]+)""");
                var versionMatch = versionRegex.Match(aipContent);
                string oldVersion = versionMatch.Success ? versionMatch.Groups[1].Value : "";

                if (oldVersion == version)
                {
                    Log.Information("Version already matches: {Version}", version);
                    return (true, version, $"Version already up to date: {version}");
                }

                // Ensure AI_UPGRADE is set to "Yes" for major upgrade support (do this before AI command line)
                if (aipContent.Contains(@"<ROW Property=""AI_UPGRADE"" Value=""No""/>"))
                {
                    aipContent = aipContent.Replace(@"<ROW Property=""AI_UPGRADE"" Value=""No""/>", @"<ROW Property=""AI_UPGRADE"" Value=""Yes""/>");
                    File.WriteAllText(aipPath, aipContent);
                    Log.Information("Set AI_UPGRADE to Yes");
                }

                // Use Advanced Installer command line to set version and regenerate ProductCode + component GUIDs
                string aiExePath = _settings.AdvancedInstallerExePath;
                if (!File.Exists(aiExePath))
                {
                    Log.Warning("Advanced Installer not found at: {Path}, falling back to manual edit", aiExePath);
                    return FallbackManualVersionUpdate(aipPath, version, oldVersion);
                }

                Log.Information("Using Advanced Installer CLI to update version and regenerate GUIDs...");

                // First set the version (this triggers GUID regeneration in AI)
                var setVersionProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = aiExePath,
                        Arguments = $"/edit \"{aipPath}\" /SetVersion {version}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                setVersionProcess.Start();
                string versionOutput = setVersionProcess.StandardOutput.ReadToEnd();
                string versionError = setVersionProcess.StandardError.ReadToEnd();
                setVersionProcess.WaitForExit();

                if (setVersionProcess.ExitCode != 0)
                {
                    Log.Warning("SetVersion command failed: {Error}, falling back to manual edit", versionError);
                    return FallbackManualVersionUpdate(aipPath, version, oldVersion);
                }

                Log.Information("SetVersion output: {Output}", versionOutput);

                // Then set new ProductCode (auto-generates if no GUID specified)
                var setProductCodeProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = aiExePath,
                        Arguments = $"/edit \"{aipPath}\" /SetProductCode -langid 1033",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                setProductCodeProcess.Start();
                string productCodeOutput = setProductCodeProcess.StandardOutput.ReadToEnd();
                string productCodeError = setProductCodeProcess.StandardError.ReadToEnd();
                setProductCodeProcess.WaitForExit();

                if (setProductCodeProcess.ExitCode != 0)
                {
                    Log.Warning("SetProductCode command returned: {Error}", productCodeError);
                }
                else
                {
                    Log.Information("SetProductCode output: {Output}", productCodeOutput);
                }

                Log.Information("Updated Advanced Installer version from {OldVersion} to {NewVersion} using CLI", oldVersion, version);
                return (true, version, $"Updated version from {oldVersion} to {version}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error syncing version to Advanced Installer");
                return (false, "", $"Error syncing version: {ex.Message}");
            }
        }

        /// <summary>
        /// Fallback method to manually update version and ProductCode when AI CLI is not available
        /// </summary>
        private (bool Success, string Version, string Message) FallbackManualVersionUpdate(string aipPath, string version, string oldVersion)
        {
            try
            {
                string aipContent = File.ReadAllText(aipPath);

                // Update ProductVersion
                var regex = new Regex(@"(<ROW Property=""ProductVersion"" Value="")([^""]+)("")");
                string newContent = regex.Replace(aipContent, $"${{1}}{version}${{3}}");

                // Generate new ProductCode GUID
                var productCodeRegex = new Regex(@"(<ROW Property=""ProductCode"" Value=""1033:)(\{[A-F0-9\-]+\})("" Type=""16""/>)");
                var productCodeMatch = productCodeRegex.Match(newContent);
                if (productCodeMatch.Success)
                {
                    string newGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
                    newContent = productCodeRegex.Replace(newContent, $"${{1}}{newGuid}${{3}}");
                    Log.Information("Generated new ProductCode (fallback): {ProductCode}", newGuid);
                }

                // Ensure AI_UPGRADE is Yes
                newContent = newContent.Replace(@"<ROW Property=""AI_UPGRADE"" Value=""No""/>", @"<ROW Property=""AI_UPGRADE"" Value=""Yes""/>");

                File.WriteAllText(aipPath, newContent);

                Log.Warning("Used fallback manual version update - component GUIDs were NOT regenerated");
                Log.Warning("For proper major upgrade support, ensure Advanced Installer CLI is available");
                return (true, version, $"Updated version from {oldVersion} to {version} (fallback mode - component GUIDs not updated)");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fallback version update failed");
                return (false, version, $"Fallback update failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the version in basehead.csproj to today's date (YYYY.MM.DD format)
        /// </summary>
        private (bool Success, string Version, string Message) UpdateCsprojVersionToToday()
        {
            try
            {
                string repoPath = _settings.WindowsRepoPath;
                string csprojPath = Path.Combine(repoPath, "basehead", "basehead.csproj");

                if (!File.Exists(csprojPath))
                {
                    Log.Warning("Could not find csproj file at: {Path}", csprojPath);
                    return (false, "", $"Could not find csproj file at: {csprojPath}");
                }

                // Generate today's date as version
                string todayVersion = DateTime.Now.ToString("yyyy.MM.dd");

                // Read and parse csproj
                var csprojDoc = XDocument.Load(csprojPath);
                var versionElement = csprojDoc.Descendants("Version").FirstOrDefault();

                if (versionElement == null)
                {
                    Log.Warning("No <Version> element found in csproj file");
                    return (false, "", "No <Version> element found in csproj file");
                }

                string oldVersion = versionElement.Value;
                if (oldVersion == todayVersion)
                {
                    Log.Information("Version already set to today's date: {Version}", todayVersion);
                    return (true, todayVersion, $"Version already up to date: {todayVersion}");
                }

                // Update the version
                versionElement.Value = todayVersion;
                csprojDoc.Save(csprojPath);

                Log.Information("Updated csproj version from {OldVersion} to {NewVersion}", oldVersion, todayVersion);
                return (true, todayVersion, $"Updated version from {oldVersion} to {todayVersion}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating csproj version");
                return (false, "", $"Error updating csproj version: {ex.Message}");
            }
        }

        /// <summary>
        /// xe"
        /// </summary>
        private string GetVersionedFileName(string originalFileName, string version)
        {
            if (string.IsNullOrEmpty(version))
                return originalFileName;

            string extension = Path.GetExtension(originalFileName);
            string nameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);

            // Pattern: "Install basehead v2026" or similar - replace the version part
            var versionPattern = new Regex(@"(.*\s+v?)(\d{4})(\.\d+)?(\.\d+)?$", RegexOptions.IgnoreCase);
            var match = versionPattern.Match(nameWithoutExt);

            if (match.Success)
            {
                // Replace with full version
                return $"{match.Groups[1].Value}{version}{extension}";
            }

            // If no version pattern found, just append version before extension
            return $"{nameWithoutExt} v{version}{extension}";
        }

        /// <summary>
        /// Updates the version in LLS csproj to today's date (YYYY.MM.DD format)
        /// </summary>
        private (bool Success, string Version, string Message) UpdateLLSCsprojVersionToToday()
        {
            try
            {
                string csprojPath = Path.Combine(_settings.LLSRepoPath, "basehead.LLS.csproj");

                if (!File.Exists(csprojPath))
                {
                    Log.Warning("Could not find LLS csproj file at: {Path}", csprojPath);
                    return (false, "", $"Could not find LLS csproj file at: {csprojPath}");
                }

                // Generate today's date as version
                string todayVersion = DateTime.Now.ToString("yyyy.MM.dd");

                // Read and parse csproj
                var csprojDoc = XDocument.Load(csprojPath);
                var versionElement = csprojDoc.Descendants("Version").FirstOrDefault();

                if (versionElement == null)
                {
                    Log.Warning("No <Version> element found in LLS csproj file");
                    return (false, "", "No <Version> element found in LLS csproj file");
                }

                string oldVersion = versionElement.Value;
                if (oldVersion == todayVersion)
                {
                    Log.Information("LLS version already set to today's date: {Version}", todayVersion);
                    return (true, todayVersion, $"LLS version already up to date: {todayVersion}");
                }

                // Update the version
                versionElement.Value = todayVersion;
                csprojDoc.Save(csprojPath);

                Log.Information("Updated LLS csproj version from {OldVersion} to {NewVersion}", oldVersion, todayVersion);
                return (true, todayVersion, $"Updated LLS version from {oldVersion} to {todayVersion}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating LLS csproj version");
                return (false, "", $"Error updating LLS csproj version: {ex.Message}");
            }
        }

        /// <summary>
        /// Syncs the version from LLS csproj to the LLS Advanced Installer .aip file
        /// Uses Advanced Installer command line to properly regenerate ProductCode and component GUIDs
        /// </summary>
        private (bool Success, string Version, string Message) SyncLLSVersionToAdvancedInstaller()
        {
            try
            {
                // Read version from LLS csproj
                string csprojPath = Path.Combine(_settings.LLSRepoPath, "basehead.LLS.csproj");

                if (!File.Exists(csprojPath))
                {
                    Log.Warning("Could not find LLS csproj file at: {Path}", csprojPath);
                    return (false, "", $"Could not find LLS csproj file at: {csprojPath}");
                }

                var csprojDoc = XDocument.Load(csprojPath);
                var versionElement = csprojDoc.Descendants("Version").FirstOrDefault();

                if (versionElement == null)
                {
                    Log.Warning("No <Version> element found in LLS csproj file");
                    return (false, "", "No <Version> element found in LLS csproj file");
                }

                string version = versionElement.Value;
                Log.Information("Found version in LLS csproj: {Version}", version);

                // Update LLS Advanced Installer .aip file
                string aipPath = _settings.LLSAdvancedInstallerProjectPath;
                if (!File.Exists(aipPath))
                {
                    Log.Warning("Could not find LLS Advanced Installer project at: {Path}", aipPath);
                    return (false, version, $"Could not find LLS Advanced Installer project at: {aipPath}");
                }

                string aipContent = File.ReadAllText(aipPath);

                // Check current version in .aip file
                var versionRegex = new Regex(@"<ROW Property=""ProductVersion"" Value=""([^""]+)""");
                var versionMatch = versionRegex.Match(aipContent);
                string oldVersion = versionMatch.Success ? versionMatch.Groups[1].Value : "";

                if (oldVersion == version)
                {
                    Log.Information("LLS Advanced Installer version already matches: {Version}", version);
                    return (true, version, $"LLS AI version already up to date: {version}");
                }

                // Ensure AI_UPGRADE is set to "Yes" for major upgrade support (do this before AI command line)
                bool aipModified = false;
                if (aipContent.Contains(@"<ROW Property=""AI_UPGRADE"" Value=""No""/>"))
                {
                    aipContent = aipContent.Replace(@"<ROW Property=""AI_UPGRADE"" Value=""No""/>", @"<ROW Property=""AI_UPGRADE"" Value=""Yes""/>");
                    aipModified = true;
                    Log.Information("Changed AI_UPGRADE from No to Yes for LLS installer");
                }
                else if (!aipContent.Contains(@"<ROW Property=""AI_UPGRADE"""))
                {
                    // AI_UPGRADE property doesn't exist at all - need to add it after UpgradeCode
                    var upgradeCodePattern = @"(<ROW Property=""UpgradeCode"" Value=""[^""]+""[^/]*/\s*>)";
                    var upgradeCodeRegex = new Regex(upgradeCodePattern);
                    if (upgradeCodeRegex.IsMatch(aipContent))
                    {
                        aipContent = upgradeCodeRegex.Replace(aipContent, "$1\n    <ROW Property=\"AI_UPGRADE\" Value=\"Yes\"/>");
                        aipModified = true;
                        Log.Information("Added AI_UPGRADE=Yes property for LLS installer");
                    }
                }

                if (aipModified)
                {
                    File.WriteAllText(aipPath, aipContent);
                }

                // Use Advanced Installer command line to set version and regenerate ProductCode + component GUIDs
                // This is the proper way to handle major upgrades
                string aiExePath = _settings.AdvancedInstallerExePath;
                if (!File.Exists(aiExePath))
                {
                    Log.Warning("Advanced Installer not found at: {Path}, falling back to manual edit", aiExePath);
                    return FallbackLLSManualVersionUpdate(aipPath, version, oldVersion);
                }

                Log.Information("Using Advanced Installer CLI to update LLS version and regenerate GUIDs...");

                // First set the version (this triggers GUID regeneration in AI)
                var setVersionProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = aiExePath,
                        Arguments = $"/edit \"{aipPath}\" /SetVersion {version}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                setVersionProcess.Start();
                string versionOutput = setVersionProcess.StandardOutput.ReadToEnd();
                string versionError = setVersionProcess.StandardError.ReadToEnd();
                setVersionProcess.WaitForExit();

                if (setVersionProcess.ExitCode != 0)
                {
                    Log.Warning("SetVersion command failed for LLS: {Error}, falling back to manual edit", versionError);
                    return FallbackLLSManualVersionUpdate(aipPath, version, oldVersion);
                }

                Log.Information("LLS SetVersion output: {Output}", versionOutput);

                // Then set new ProductCode (auto-generates if no GUID specified)
                var setProductCodeProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = aiExePath,
                        Arguments = $"/edit \"{aipPath}\" /SetProductCode -langid 1033",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                setProductCodeProcess.Start();
                string productCodeOutput = setProductCodeProcess.StandardOutput.ReadToEnd();
                string productCodeError = setProductCodeProcess.StandardError.ReadToEnd();
                setProductCodeProcess.WaitForExit();

                if (setProductCodeProcess.ExitCode != 0)
                {
                    Log.Warning("SetProductCode command returned for LLS: {Error}", productCodeError);
                }
                else
                {
                    Log.Information("LLS SetProductCode output: {Output}", productCodeOutput);
                }

                Log.Information("Updated LLS Advanced Installer version from {OldVersion} to {NewVersion} using CLI", oldVersion, version);
                return (true, version, $"Updated LLS AI version from {oldVersion} to {version}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error syncing LLS version to Advanced Installer");
                return (false, "", $"Error syncing LLS version: {ex.Message}");
            }
        }

        /// <summary>
        /// Fallback method to manually update LLS version and ProductCode when AI CLI is not available
        /// </summary>
        private (bool Success, string Version, string Message) FallbackLLSManualVersionUpdate(string aipPath, string version, string oldVersion)
        {
            try
            {
                string aipContent = File.ReadAllText(aipPath);

                // Update ProductVersion
                var regex = new Regex(@"(<ROW Property=""ProductVersion"" Value="")([^""]+)("")");
                string newContent = regex.Replace(aipContent, $"${{1}}{version}${{3}}");

                // Generate new ProductCode GUID
                var productCodeRegex = new Regex(@"(<ROW Property=""ProductCode"" Value=""1033:)(\{[A-F0-9\-]+\})("" Type=""16""/>)");
                var productCodeMatch = productCodeRegex.Match(newContent);
                if (productCodeMatch.Success)
                {
                    string newGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
                    newContent = productCodeRegex.Replace(newContent, $"${{1}}{newGuid}${{3}}");
                    Log.Information("Generated new ProductCode for LLS (fallback): {ProductCode}", newGuid);
                }

                // Ensure AI_UPGRADE is Yes - either replace existing or add if missing
                if (newContent.Contains(@"<ROW Property=""AI_UPGRADE"" Value=""No""/>"))
                {
                    newContent = newContent.Replace(@"<ROW Property=""AI_UPGRADE"" Value=""No""/>", @"<ROW Property=""AI_UPGRADE"" Value=""Yes""/>");
                }
                else if (!newContent.Contains(@"<ROW Property=""AI_UPGRADE"""))
                {
                    // Add AI_UPGRADE property after UpgradeCode
                    var upgradeCodePattern = @"(<ROW Property=""UpgradeCode"" Value=""[^""]+""[^/]*/\s*>)";
                    var upgradeCodeRegex = new Regex(upgradeCodePattern);
                    if (upgradeCodeRegex.IsMatch(newContent))
                    {
                        newContent = upgradeCodeRegex.Replace(newContent, "$1\n    <ROW Property=\"AI_UPGRADE\" Value=\"Yes\"/>");
                        Log.Information("Added AI_UPGRADE=Yes property for LLS installer (fallback)");
                    }
                }

                File.WriteAllText(aipPath, newContent);

                Log.Warning("Used fallback manual version update for LLS - component GUIDs were NOT regenerated");
                Log.Warning("For proper major upgrade support, ensure Advanced Installer CLI is available");
                return (true, version, $"Updated LLS AI version from {oldVersion} to {version} (fallback mode - component GUIDs not updated)");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fallback LLS version update failed");
                return (false, "", $"Fallback LLS version update failed: {ex.Message}");
            }
        }
    }
}
