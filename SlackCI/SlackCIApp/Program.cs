using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SlackCIApp.Config;
using Slack.Webhooks;
using System.Text.Json;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace SlackCIApp
{
    class Program
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static CancellationTokenSource cts = new CancellationTokenSource();
        private static DateTime lastCheckTime = DateTime.MinValue;

        static int Main(string[] args)
        {
            try
            {
                // Check if running as Windows service
                if (args.Length > 0 && args[0].Equals("--service", StringComparison.OrdinalIgnoreCase))
                {
                    CreateHostBuilder(args).Build().Run();
                    return 0;
                }
                else
                {
                    // Run in console mode
                    MainAsync(args).GetAwaiter().GetResult();
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
                return 1;
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService(options =>
                {
                    options.ServiceName = "SlackCI Build Server";
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<SlackCIWorker>();
                });
        
        static async Task MainAsync(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var settings = configuration.GetSection("SlackCISettings").Get<SlackCISettings>();
            
            // Check if settings loaded successfully
            if (settings == null)
            {
                Console.WriteLine("Failed to load configuration settings. Please check your appsettings.json file.");
                return;
            }
            
            // Check for test-upload command
            if (args.Length > 0 && args[0].Equals("test-upload", StringComparison.OrdinalIgnoreCase))
            {
                await TestSlackUpload(settings);
                return;
            }
            
            // Check for test-upload command
            if (args.Length > 0 && args[0].Equals("test-upload", StringComparison.OrdinalIgnoreCase))
            {
                await TestSlackUpload(settings);
                return;
            }
            
            if (settings == null)
            {
                Console.WriteLine("Failed to load configuration settings. Please check your appsettings.json file.");
                return;
            }
            
            // Check for test upload command
            if (args.Length > 0 && args[0].Equals("test-upload", StringComparison.OrdinalIgnoreCase))
            {
                await TestSlackUpload(settings);
                return;
            }
            
            if (settings == null)
            {
                Console.WriteLine("Failed to load configuration settings. Please check your appsettings.json file.");
                return;
            }
            
            var logDirectory = Path.GetDirectoryName(settings.LogFilePath);
            if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }
            
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information)
                .WriteTo.File(
                    settings.LogFilePath, 
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            try
            {
                Log.Information("Starting SlackCI application");
                
                if (string.IsNullOrEmpty(settings.SlackWebhookUrl))
                {
                    Log.Warning("Slack Webhook URL is missing. Please update your configuration.");
                    Console.WriteLine("Please enter your Slack Webhook URL (or press Enter to skip): ");
                    settings.SlackWebhookUrl = Console.ReadLine() ?? string.Empty;
                }
                
                if (string.IsNullOrEmpty(settings.SlackBotToken) || string.IsNullOrEmpty(settings.ChannelId))
                {
                    if (!string.IsNullOrEmpty(settings.SlackBotToken) && !string.IsNullOrEmpty(settings.ChannelId))
                    {
                        Log.Information("Message polling will be enabled");
                    }
                    else
                    {
                        Log.Warning("Bot token or channel ID not provided. Message polling will be disabled.");
                    }
                }
                
                if (string.IsNullOrEmpty(settings.ChannelName))
                {
                    Console.WriteLine("Please enter the Slack channel name (e.g., #builds): ");
                    settings.ChannelName = Console.ReadLine() ?? "#builds";
                }
                
                if (!File.Exists(settings.WindowsBuildScriptPath))
                {
                    Log.Warning("Windows build script not found at: {Path}", settings.WindowsBuildScriptPath);
                    Console.WriteLine("Enter the full path to the Windows build script (or press Enter to use simulation mode): ");
                    settings.WindowsBuildScriptPath = Console.ReadLine() ?? string.Empty;
                }
                
                using (cts = new CancellationTokenSource())
                {
                    Console.WriteLine("Press Ctrl+C to exit");
                    Console.CancelKeyPress += (sender, e) =>
                    {
                        e.Cancel = true;
                        cts.Cancel();
                    };

                    try
                    {
                        SendSlackMessage(settings, ":wave: BaseHead CI Bot is online and listening for build commands!");
                        
                        if (!string.IsNullOrEmpty(settings.SlackBotToken) && !string.IsNullOrEmpty(settings.ChannelId))
                        {
                            _ = Task.Run(() => PollSlackMessages(settings), cts.Token);
                        }

                        Console.WriteLine("\nEnter build commands directly here:");
                        Console.WriteLine($"1) {settings.TriggerBothCommand}");
                        Console.WriteLine($"2) {settings.TriggerWindowsCommand}");
                        Console.WriteLine($"3) {settings.TriggerMacCommand}");
                        Console.WriteLine($"4) {settings.TriggerLLSCommand}");
                        Console.WriteLine("Type \"exit\" to quit");
                        
                        while (!cts.Token.IsCancellationRequested)
                        {
                            string input = Console.ReadLine() ?? string.Empty;
                            
                            if (input.ToLower() == "exit")
                                break;
                            
                            await ProcessCommand(settings, input);
                            await Task.Delay(100);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error in service: {ErrorMessage}", ex.Message);
                    }
                    finally
                    {
                        SendSlackMessage(settings, ":wave: BaseHead CI Bot is shutting down.");
                        Log.Information("Service stopped");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        static async Task ProcessCommand(SlackCISettings settings, string input)
        {
            string inputLower = input.ToLower().Trim();
              if (inputLower == "1" || inputLower == settings.TriggerBothCommand?.ToLower())
            {
                Log.Information("Starting build for both Windows and Mac in parallel...");
                SendSlackMessage(settings, ":rocket: Starting build for both Windows and Mac in parallel...");
                
                // Run both builds in parallel
                var windowsBuildTask = TriggerWindowsBuild(settings);
                var macBuildTask = TriggerMacBuild(settings);
                
                // Wait for both builds to complete
                await Task.WhenAll(windowsBuildTask, macBuildTask);
                
                Log.Information("Both build processes have completed");
                SendSlackMessage(settings, ":checkered_flag: Both build processes have completed");
            }
            else if (inputLower == "2" || inputLower == settings.TriggerWindowsCommand?.ToLower())
            {
                Log.Information("Starting Windows build...");
                SendSlackMessage(settings, ":rocket: Starting Windows build...");
                
                await TriggerWindowsBuild(settings);
            }
            else if (inputLower == "3" || inputLower == settings.TriggerMacCommand?.ToLower())
            {
                Log.Information("Starting Mac build...");
                SendSlackMessage(settings, ":rocket: Starting Mac build...");
                
                await TriggerMacBuild(settings);
            }
            else if (inputLower == "4" || inputLower == settings.TriggerLLSCommand?.ToLower())
            {
                Log.Information("Starting LLS build...");
                SendSlackMessage(settings, ":rocket: Starting LLS build...");
                
                await TriggerLLSBuild(settings);
            }
            else if (!string.IsNullOrWhiteSpace(input))
            {
                Log.Information("Unknown command: {Command}", input);
                SendSlackMessage(settings, $":question: Unknown command: {input}");
            }
        }        
          static async Task TriggerWindowsBuild(SlackCISettings settings)
        {
            try
            {
                // Clean up the basehead-pc folder before starting the build
                var baseheadPcDir = Path.Combine(settings.WindowsRepoPath, "basehead", "basehead-pc");
                if (Directory.Exists(baseheadPcDir))
                {
                    try
                    {
                        Log.Information("Cleaning up previous build output at: {Dir}", baseheadPcDir);
                        SendSlackMessage(settings, ":broom: Cleaning up previous PC build output...");
                        Directory.Delete(baseheadPcDir, recursive: true);
                        
                        // Wait a moment to ensure all file handles are released
                        await Task.Delay(2000);
                        Log.Information("Successfully deleted previous build output directory");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to delete basehead-pc directory: {Message}", ex.Message);
                        SendSlackMessage(settings, ":warning: Failed to clean up previous build output. This might cause issues with the build.");
                    }
                }

                if (string.IsNullOrEmpty(settings.WindowsBuildScriptPath) || !File.Exists(settings.WindowsBuildScriptPath))
                {
                    Log.Warning("Windows build script not found. Running in simulation mode.");
                    await Task.Delay(3000);
                    
                    Log.Information("Windows build completed successfully (simulated)");
                    SendSlackMessage(settings, ":white_check_mark: Windows build completed successfully (simulated)!");
                    SendSlackMessage(settings, ":information_source: To enable actual builds, please configure a valid build script path in appsettings.json");
                    return;
                }

                // Attempt to pull latest changes if repo path is configured
                if (!string.IsNullOrEmpty(settings.WindowsRepoPath))
                {
                    SendSlackMessage(settings, ":arrow_down: Pulling latest changes from Git for PC...");
                    Log.Information("Pulling latest changes from Git repository at {RepoPath}", settings.WindowsRepoPath);
                    
                    var sshService = new SshService(settings, Log.Logger);
                    var gitService = new GitService(settings, Log.Logger, sshService);
                    var pullResult = await gitService.PullLatestChangesWindowsAsync();
                      if (pullResult.Success)
                    {
                        Log.Information("Git pull successful: {Message}", pullResult.Message);
                        SendSlackMessage(settings, $":white_check_mark: Git pull successful: {pullResult.Message}");
                    }
                    else
                    {
                        Log.Error("Git pull failed: {Message}", pullResult.Message);
                        SendSlackMessage(settings, $":x: Build aborted: Git pull failed: {pullResult.Message}");
                        return; // Exit without running build script
                    }
                }

                // Update version in csproj to today's date before building
                var csprojUpdate = UpdateCsprojVersionToToday(settings);
                if (csprojUpdate.Success)
                {
                    SendSlackMessage(settings, $":calendar: {csprojUpdate.Message}");
                }
                else
                {
                    SendSlackMessage(settings, $":warning: Version update warning: {csprojUpdate.Message}");
                }

                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = settings.WindowsBuildScriptPath,
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WorkingDirectory = Path.GetDirectoryName(settings.WindowsBuildScriptPath)
                };
                Log.Information("Executing Windows build script: {ScriptPath}", settings.WindowsBuildScriptPath);
                SendSlackMessage(settings, $":arrow_forward: Windows build v{csprojUpdate.Version} starting...");
                process.Start();
                await Task.Run(() => process.WaitForExit());

                if (process.ExitCode == 0)
                {
                    Log.Information("Windows build completed successfully");
                    SendSlackMessage(settings, ":white_check_mark: Windows build completed successfully!");

                    // Sync version from csproj to Advanced Installer before building
                    var versionSync = SyncVersionToAdvancedInstaller(settings);
                    if (versionSync.Success)
                    {
                        SendSlackMessage(settings, $":label: {versionSync.Message}");
                    }
                    else
                    {
                        SendSlackMessage(settings, $":warning: Version sync warning: {versionSync.Message}");
                    }

                    // Run Advanced Installer to build the PC installer
                    var advancedInstallerPath = settings.AdvancedInstallerProjectPath;
                    if (File.Exists(advancedInstallerPath))
                    {
                        try
                        {
                            Log.Information("Running Advanced Installer to build PC installer: {Path}", advancedInstallerPath);
                            SendSlackMessage(settings, $":gear: Creating installer v{versionSync.Version} with Advanced Installer...");
                            var outputBuilder = new StringBuilder();

                            var appDir = Directory.GetCurrentDirectory();
                            var batchFile = Path.Combine(appDir, "RunAdvancedInstaller.cmd");
                            string batchContent = $"@echo off\necho Running Advanced Installer with elevated privileges...\nset AI_LOG=\"%~dp0AdvancedInstaller.log\"\necho Starting build at %DATE% %TIME% > %AI_LOG%\n\"{settings.AdvancedInstallerExePath}\" /build \"{advancedInstallerPath}\" >> %AI_LOG% 2>&1\nset EXIT_CODE=%errorlevel%\necho Build finished with exit code %EXIT_CODE% at %DATE% %TIME% >> %AI_LOG%\nexit /b %EXIT_CODE%";
                            await File.WriteAllTextAsync(batchFile, batchContent);
                            using var aiProcess = new Process();
                            aiProcess.StartInfo = new ProcessStartInfo
                            {
                                FileName = batchFile,
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = false,
                                WorkingDirectory = Path.GetDirectoryName(advancedInstallerPath)
                            };

                            if (!File.Exists(settings.AdvancedInstallerExePath))
                            {
                                Log.Error("Advanced Installer executable not found at: {Path}", settings.AdvancedInstallerExePath);
                                SendSlackMessage(settings, $":x: Advanced Installer executable not found at: {settings.AdvancedInstallerExePath}");
                                return;
                            }
                            try
                            {
                                aiProcess.Start();
                                Log.Information("Advanced Installer process started");
                                aiProcess.OutputDataReceived += (s, e) =>
                                {
                                    if (!string.IsNullOrEmpty(e.Data))
                                    {
                                        outputBuilder.AppendLine(e.Data);
                                        Log.Information("AI: {Output}", e.Data);
                                    }
                                };
                                aiProcess.ErrorDataReceived += (s, e) =>
                                {
                                    if (!string.IsNullOrEmpty(e.Data))
                                    {
                                        outputBuilder.AppendLine(e.Data);
                                        Log.Error("AI Error: {Output}", e.Data);
                                    }
                                };
                                aiProcess.BeginOutputReadLine();
                                aiProcess.BeginErrorReadLine();
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Failed to start Advanced Installer process");
                                SendSlackMessage(settings, $":x: Failed to start Advanced Installer: {ex.Message}");
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
                                    Log.Error(ex, "Error waiting for Advanced Installer process");
                                    return false;
                                }
                            });
                            if (!completed)
                            {
                                Log.Error("Advanced Installer build timed out after 10 minutes");
                                SendSlackMessage(settings, ":x: Advanced Installer build timed out after 10 minutes");
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
                                    Log.Error(ex, "Error killing Advanced Installer process");
                                }
                                return;
                            }
                            var exitCode = aiProcess.ExitCode;
                            Log.Information("Advanced Installer process completed with exit code: {ExitCode}", exitCode);
                            var aipLogDir = Path.GetDirectoryName(advancedInstallerPath);
                            if (!string.IsNullOrEmpty(aipLogDir))
                            {
                                var aipLogPath = Path.Combine(aipLogDir, "AdvancedInstaller.log");
                                if (File.Exists(aipLogPath))
                                {
                                    try
                                    {
                                        var logLines = File.ReadAllLines(aipLogPath).TakeLast(20);
                                        var logContent = string.Join("\n", logLines);
                                        Log.Information("Advanced Installer log content:\n{Log}", logContent);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error(ex, "Error reading Advanced Installer log file");
                                    }
                                }
                            }
                            if (exitCode == 0)
                            {
                                Log.Information("PC installer built successfully");
                                SendSlackMessage(settings, ":white_check_mark: PC installer built successfully!");
                                string installerDir = Path.GetDirectoryName(advancedInstallerPath) ?? Directory.GetCurrentDirectory();
                                string[] potentialOutputDirs = new[]
                                {
                                    Path.Combine(installerDir, "Output"),
                                    Path.Combine(installerDir, "Builds"),
                                    Path.Combine(installerDir, "Setup"),
                                    installerDir
                                };
                                string outputDir = potentialOutputDirs.FirstOrDefault(dir => Directory.Exists(dir)) ?? installerDir;
                                Log.Information("Checking for installer files in: {Dir}", outputDir);
                                if (Directory.Exists(outputDir))
                                {
                                    string[] installerFiles = Directory.GetFiles(outputDir, "*.exe")
                                        .Concat(Directory.GetFiles(outputDir, "*.msi"))
                                        .ToArray();
                                    if (installerFiles.Length > 0)
                                    {                                        settings.WindowsInstallerPath = installerFiles.OrderByDescending(f => new FileInfo(f).CreationTime).First();
                                        var fileName = Path.GetFileName(settings.WindowsInstallerPath);
                                        var fileSize = new FileInfo(settings.WindowsInstallerPath).Length / (1024.0 * 1024.0);
                                        SaveSettings(settings);
                                        SendSlackMessage(settings, $":package: Windows installer built: {fileName} ({fileSize:F2} MB)");

                                        // Copy to network path with versioned filename
                                        try
                                        {
                                            string networkPath = @"\\BeeStation\home\Files\build-server";
                                            string versionedFileName = GetVersionedFileName(fileName, versionSync.Version);
                                            string networkFilePath = Path.Combine(networkPath, versionedFileName);
                                            Log.Information("Copying Windows installer to network path: {NetworkPath}", networkFilePath);

                                            if (!Directory.Exists(networkPath))
                                            {
                                                Directory.CreateDirectory(networkPath);
                                            }

                                            File.Copy(settings.WindowsInstallerPath, networkFilePath, true);
                                            SendSlackMessage(settings, $":file_folder: Windows installer copied to network: {versionedFileName}");

                                            // Add web download link
                                            string webDownloadUrl = "http://j8cd6qcvrjt956vxvyixiuoss2n6mes.quickconnect.to/sharing/fcRcMqktX";
                                            SendSlackMessage(settings, $":globe_with_meridians: Web download link: {webDownloadUrl}");
                                        }
                                        catch (Exception ex)
                                        {
                                            Log.Error(ex, "Failed to copy Windows installer to network path: {Error}", ex.Message);
                                            SendSlackMessage(settings, $":warning: Failed to copy Windows installer to network path: {ex.Message}");
                                        }


                                    }
                                    else
                                    {
                                        Log.Warning("No installer files found in output directory: {Dir}", installerDir);
                                    }
                                }
                                else
                                {
                                    Log.Warning("Output directory not found: {Dir}", installerDir);
                                }
                            }
                            else
                            {
                                Log.Error("Advanced Installer failed with exit code {ExitCode}", aiProcess.ExitCode);
                                SendSlackMessage(settings, $":x: PC installer build failed with exit code {aiProcess.ExitCode}");
                                var output = outputBuilder.ToString().Trim();
                                if (output.Length > 500)
                                {
                                    output = "..." + output.Substring(Math.Max(0, output.Length - 500));
                                }
                                if (!string.IsNullOrWhiteSpace(output))
                                {
                                    SendSlackMessage(settings, $"```\n{output}\n```");
                                }
                                if (output.Contains("Build finished because an error was encountered"))
                                {
                                    if (output.Contains("Permission denied") || output.Contains("Access is denied"))
                                    {
                                        SendSlackMessage(settings, ":warning: Advanced Installer may need administrative privileges. Please check file permissions.");
                                    }
                                    else if (output.Contains("is in use by another process"))
                                    {
                                        SendSlackMessage(settings, ":warning: Some files are locked by another process. Please close any applications that might be using the installer files.");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error running Advanced Installer: {ErrorMessage}", ex.Message);
                            SendSlackMessage(settings, $":x: Error Creating PC installer: {ex.Message}");
                        }
                    }
                    else
                    {
                        Log.Warning("Advanced Installer project not found at: {Path}", advancedInstallerPath);
                        SendSlackMessage(settings, $":warning: Advanced Installer project not found at: {advancedInstallerPath}");
                    }
                }
                else
                {
                    Log.Error("Windows build failed with exit code {ExitCode}", process.ExitCode);
                    SendSlackMessage(settings, $":x: Windows build failed with exit code {process.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during Windows build: {ErrorMessage}", ex.Message);
                SendSlackMessage(settings, $":x: Error during Windows build: {ex.Message}");
            }
        }
        
         static async Task TriggerMacBuild(SlackCISettings settings)
        {
            try
            {
                var sshService = new SlackCIApp.SshService(settings, Log.Logger);
                
                // Attempt to pull latest changes if repo path is configured
                if (!string.IsNullOrEmpty(settings.MacRepoPath))
                {
                    SendSlackMessage(settings, ":arrow_down: Pulling latest changes from Git on Mac...");
                    Log.Information("Pulling latest changes from Git repository at {RepoPath}", settings.MacRepoPath);
                    
                    var gitService = new GitService(settings, Log.Logger, sshService);
                    var pullResult = await gitService.PullLatestChangesMacAsync();
                    
                    if (pullResult.Success)
                    {
                        Log.Information("Git pull on Mac successful: {Message}", pullResult.Message);
                        SendSlackMessage(settings, $":white_check_mark: Git pull on Mac successful: {pullResult.Message}");
                    }                    else
                    {
                        Log.Error("Git pull on Mac failed: {Message}", pullResult.Message);
                        SendSlackMessage(settings, $":x: Git pull on Mac failed: {pullResult.Message}");
                        
                        // Check if this is a publickey error and try to help
                        if (pullResult.Message.Contains("Permission denied (publickey)"))
                        {
                            SendSlackMessage(settings, ":key: Attempting to generate and configure SSH key for GitHub...");
                            var (keyGenSuccess, sshPublicKey, keyGenMessage) = await sshService.GenerateMacSshKeyAsync();
                            
                            if (keyGenSuccess)
                            {
                                SendSlackMessage(settings, ":white_check_mark: SSH key generated successfully!");
                                SendSlackMessage(settings, $":information_source: Please add this public key to your GitHub repository:\n```\n{sshPublicKey}\n```");
                                SendSlackMessage(settings, "Once you've added the key, try the build again.");
                            }
                            else
                            {
                                SendSlackMessage(settings, $":x: Failed to generate SSH key: {keyGenMessage}");
                            }
                        }
                        return; // Exit if Git pull fails
                    }
                }
                  Log.Information("Starting Mac build via SSH");
                SendSlackMessage(settings, ":arrow_forward: Mac build starting...");
                
                var (success, output) = await sshService.ExecuteMacBuildAsync();

                if (success)
                {
                    Log.Information("Mac build completed successfully");
                    SendSlackMessage(settings, ":white_check_mark: Mac build completed successfully!");

                    // Get version from csproj for Mac installer filename
                    var macVersionSync = SyncVersionToAdvancedInstaller(settings);
                    string macVersion = macVersionSync.Version;
                    string macInstallerFileName = string.IsNullOrEmpty(macVersion)
                        ? "Install basehead v2025.pkg"
                        : $"Install basehead v{macVersion}.pkg";

                    // Set Mac installer path based on convention with version
                    settings.MacInstallerPath = $"/Users/steve/Desktop/GitHub/basehead/build/{macInstallerFileName}";
                    if (!string.IsNullOrEmpty(settings.MacInstallerPath))
                    {
                        // Download Mac installer via SCP and copy to BeeStation
                        string localDownloadPath = Path.Combine(Directory.GetCurrentDirectory(), "downloads");
                        if (!Directory.Exists(localDownloadPath))
                        {
                            Directory.CreateDirectory(localDownloadPath);
                        }
                        string localFilePath = Path.Combine(localDownloadPath, macInstallerFileName);

                        Log.Information("Downloading Mac installer via SCP: {RemotePath} -> {LocalPath}", settings.MacInstallerPath, localFilePath);
                        SendSlackMessage(settings, $":arrow_down: Downloading Mac installer...");

                        if (await sshService.DownloadInstallerAsync(settings.MacInstallerPath, localFilePath))
                        {
                            var fileSize = new FileInfo(localFilePath).Length / (1024.0 * 1024.0);
                            SendSlackMessage(settings, $":package: Mac installer downloaded: {macInstallerFileName} ({fileSize:F2} MB)");

                            // Copy to BeeStation network path
                            try
                            {
                                string networkPath = @"\\BeeStation\home\Files\build-server";
                                string networkFilePath = Path.Combine(networkPath, macInstallerFileName);
                                Log.Information("Copying Mac installer to network path: {NetworkPath}", networkFilePath);

                                if (!Directory.Exists(networkPath))
                                {
                                    Directory.CreateDirectory(networkPath);
                                }

                                File.Copy(localFilePath, networkFilePath, true);
                                SendSlackMessage(settings, $":file_folder: Mac installer copied to network: {macInstallerFileName}");

                                // Add web download link
                                string webDownloadUrl = "http://j8cd6qcvrjt956vxvyixiuoss2n6mes.quickconnect.to/sharing/fcRcMqktX";
                                SendSlackMessage(settings, $":globe_with_meridians: Web download link: {webDownloadUrl}");
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Failed to copy Mac installer to network path: {Error}", ex.Message);
                                SendSlackMessage(settings, $":warning: Failed to copy Mac installer to network path: {ex.Message}");
                            }
                        }
                        else
                        {
                            Log.Warning("Failed to download Mac installer via SCP");
                            SendSlackMessage(settings, $":warning: Failed to download Mac installer. File may be on Mac at: {settings.MacInstallerPath}");

                            // Still show web download link
                            string webDownloadUrl = "http://j8cd6qcvrjt956vxvyixiuoss2n6mes.quickconnect.to/sharing/fcRcMqktX";
                            SendSlackMessage(settings, $":globe_with_meridians: Web download link: {webDownloadUrl}");
                        }
                    }
                }
                else
                {
                    Log.Error("Mac build failed: {Error}", output);
                    SendSlackMessage(settings, $":x: Mac build failed: {output}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during Mac build: {ErrorMessage}", ex.Message);
                SendSlackMessage(settings, $":x: Error during Mac build: {ex.Message}");
            }
        }

        static async Task TriggerLLSBuild(SlackCISettings settings)
        {
            try
            {
                if (string.IsNullOrEmpty(settings.LLSBuildScriptPath) || !File.Exists(settings.LLSBuildScriptPath))
                {
                    Log.Warning("LLS build script not found. Running in simulation mode.");
                    await Task.Delay(3000);
                    
                    Log.Information("LLS build completed successfully (simulated)");
                    SendSlackMessage(settings, ":white_check_mark: LLS build completed successfully (simulated)!");
                    SendSlackMessage(settings, ":information_source: To enable actual builds, please configure a valid LLS build script path in appsettings.json");
                    return;
                }

                // Attempt to pull latest changes if repo path is configured
                if (!string.IsNullOrEmpty(settings.WindowsRepoPath))
                {
                    SendSlackMessage(settings, ":arrow_down: Pulling latest changes from Git for LLS...");
                    Log.Information("Pulling latest changes from Git repository at {RepoPath}", settings.WindowsRepoPath);
                    
                    var sshService = new SshService(settings, Log.Logger);
                    var gitService = new GitService(settings, Log.Logger, sshService);
                    var pullResult = await gitService.PullLatestChangesWindowsAsync();
                    
                    if (pullResult.Success)
                    {
                        Log.Information("Git pull successful: {Message}", pullResult.Message);
                        SendSlackMessage(settings, $":white_check_mark: Git pull successful: {pullResult.Message}");
                    }
                    else
                    {
                        Log.Error("Git pull failed: {Message}", pullResult.Message);
                        SendSlackMessage(settings, $":x: Build aborted: Git pull failed: {pullResult.Message}");
                        return; // Exit without running build script
                    }
                }

                // Update LLS version in csproj to today's date before building
                var llsCsprojUpdate = UpdateLLSCsprojVersionToToday(settings);
                if (llsCsprojUpdate.Success)
                {
                    SendSlackMessage(settings, $":calendar: {llsCsprojUpdate.Message}");
                }
                else
                {
                    SendSlackMessage(settings, $":warning: LLS version update warning: {llsCsprojUpdate.Message}");
                }

                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = settings.LLSBuildScriptPath,
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WorkingDirectory = Path.GetDirectoryName(settings.LLSBuildScriptPath)
                };

                Log.Information("Executing LLS build script: {ScriptPath}", settings.LLSBuildScriptPath);
                SendSlackMessage(settings, $":arrow_forward: LLS build v{llsCsprojUpdate.Version} starting...");
                process.Start();
                await Task.Run(() => process.WaitForExit());

                if (process.ExitCode == 0)
                {
                    Log.Information("LLS build completed successfully");
                    SendSlackMessage(settings, ":white_check_mark: LLS build completed successfully!");

                    // Sync LLS version to Advanced Installer before building installer
                    var llsVersionSync = SyncLLSVersionToAdvancedInstaller(settings);
                    if (llsVersionSync.Success)
                    {
                        SendSlackMessage(settings, $":label: {llsVersionSync.Message}");
                    }
                    else
                    {
                        SendSlackMessage(settings, $":warning: LLS version sync warning: {llsVersionSync.Message}");
                    }

                    // Run Advanced Installer to build the LLS installer
                    var advancedInstallerPath = settings.LLSAdvancedInstallerProjectPath;
                    if (File.Exists(advancedInstallerPath))
                    {
                        try
                        {
                            Log.Information("Running Advanced Installer to build LLS installer: {Path}", advancedInstallerPath);
                            SendSlackMessage(settings, $":gear: Creating LLS installer v{llsVersionSync.Version} with Advanced Installer...");
                            var outputBuilder = new StringBuilder();

                            var appDir = Directory.GetCurrentDirectory();
                            var batchFile = Path.Combine(appDir, "RunAdvancedInstaller_LLS.cmd");
                            string batchContent = $"@echo off\necho Running Advanced Installer for LLS with elevated privileges...\nset AI_LOG=\"%~dp0AdvancedInstaller_LLS.log\"\necho Starting LLS build at %DATE% %TIME% > %AI_LOG%\n\"{settings.AdvancedInstallerExePath}\" /build \"{advancedInstallerPath}\" >> %AI_LOG% 2>&1\nset EXIT_CODE=%errorlevel%\necho LLS build finished with exit code %EXIT_CODE% at %DATE% %TIME% >> %AI_LOG%\nexit /b %EXIT_CODE%";
                            await File.WriteAllTextAsync(batchFile, batchContent);
                            
                            using var aiProcess = new Process();
                            aiProcess.StartInfo = new ProcessStartInfo
                            {
                                FileName = batchFile,
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = false,
                                WorkingDirectory = Path.GetDirectoryName(advancedInstallerPath)
                            };

                            if (!File.Exists(settings.AdvancedInstallerExePath))
                            {
                                Log.Error("Advanced Installer executable not found at: {Path}", settings.AdvancedInstallerExePath);
                                SendSlackMessage(settings, $":x: Advanced Installer executable not found at: {settings.AdvancedInstallerExePath}");
                                return;
                            }
                            
                            try
                            {
                                aiProcess.Start();
                                Log.Information("Advanced Installer process started for LLS");
                                aiProcess.OutputDataReceived += (s, e) =>
                                {
                                    if (!string.IsNullOrEmpty(e.Data))
                                    {
                                        outputBuilder.AppendLine(e.Data);
                                        Log.Information("AI LLS: {Output}", e.Data);
                                    }
                                };
                                aiProcess.ErrorDataReceived += (s, e) =>
                                {
                                    if (!string.IsNullOrEmpty(e.Data))
                                    {
                                        outputBuilder.AppendLine(e.Data);
                                        Log.Error("AI LLS Error: {Output}", e.Data);
                                    }
                                };
                                aiProcess.BeginOutputReadLine();
                                aiProcess.BeginErrorReadLine();
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Failed to start Advanced Installer process for LLS");
                                SendSlackMessage(settings, $":x: Failed to start Advanced Installer for LLS: {ex.Message}");
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
                                    Log.Error(ex, "Error waiting for Advanced Installer LLS process");
                                    return false;
                                }
                            });
                            
                            if (!completed)
                            {
                                Log.Error("Advanced Installer LLS build timed out after 10 minutes");
                                SendSlackMessage(settings, ":x: Advanced Installer LLS build timed out after 10 minutes");
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
                                    Log.Error(ex, "Error killing Advanced Installer LLS process");
                                }
                                return;
                            }
                            
                            var exitCode = aiProcess.ExitCode;
                            Log.Information("Advanced Installer LLS process completed with exit code: {ExitCode}", exitCode);
                            
                            var aipLogDir = Path.GetDirectoryName(advancedInstallerPath);
                            if (!string.IsNullOrEmpty(aipLogDir))
                            {
                                var aipLogPath = Path.Combine(aipLogDir, "AdvancedInstaller_LLS.log");
                                if (File.Exists(aipLogPath))
                                {
                                    try
                                    {
                                        var logLines = File.ReadAllLines(aipLogPath).TakeLast(20);
                                        var logContent = string.Join("\n", logLines);
                                        Log.Information("Advanced Installer LLS log content:\n{Log}", logContent);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error(ex, "Error reading Advanced Installer LLS log file");
                                    }
                                }
                            }
                            
                            if (exitCode == 0)
                            {
                                Log.Information("LLS installer built successfully");
                                SendSlackMessage(settings, ":white_check_mark: LLS installer built successfully!");
                                
                                string installerDir = Path.GetDirectoryName(advancedInstallerPath) ?? Directory.GetCurrentDirectory();
                                string[] potentialOutputDirs = new[]
                                {
                                    Path.Combine(installerDir, "Output"),
                                    Path.Combine(installerDir, "Builds"),
                                    Path.Combine(installerDir, "Setup"),
                                    installerDir
                                };
                                
                                string outputDir = potentialOutputDirs.FirstOrDefault(dir => Directory.Exists(dir)) ?? installerDir;
                                Log.Information("Checking for LLS installer files in: {Dir}", outputDir);

                                if (Directory.Exists(outputDir))
                                {
                                    string[] installerFiles = Directory.GetFiles(outputDir, "*.exe")
                                        .Concat(Directory.GetFiles(outputDir, "*.msi"))
                                        .ToArray();
                                    if (installerFiles.Length > 0)
                                    {
                                        settings.LLSInstallerPath = installerFiles.OrderByDescending(f => new FileInfo(f).CreationTime).First();
                                        var fileName = Path.GetFileName(settings.LLSInstallerPath);
                                        var fileSize = new FileInfo(settings.LLSInstallerPath).Length / (1024.0 * 1024.0);
                                        SaveSettings(settings);
                                        SendSlackMessage(settings, $":package: LLS installer built: {fileName} ({fileSize:F2} MB)");

                                        // Copy to network path with versioned filename
                                        try
                                        {
                                            string networkPath = @"\\BeeStation\home\Files\build-server";
                                            string versionedFileName = GetVersionedFileName(fileName, llsVersionSync.Version);
                                            string networkFilePath = Path.Combine(networkPath, versionedFileName);
                                            Log.Information("Copying LLS installer to network path: {NetworkPath}", networkFilePath);

                                            if (!Directory.Exists(networkPath))
                                            {
                                                Directory.CreateDirectory(networkPath);
                                            }

                                            File.Copy(settings.LLSInstallerPath, networkFilePath, true);
                                            SendSlackMessage(settings, $":file_folder: LLS installer copied to network: {versionedFileName}");

                                            // Add web download link
                                            string webDownloadUrl = "http://j8cd6qcvrjt956vxvyixiuoss2n6mes.quickconnect.to/sharing/fcRcMqktX";
                                            SendSlackMessage(settings, $":globe_with_meridians: Web download link: {webDownloadUrl}");
                                        }
                                        catch (Exception ex)
                                        {
                                            Log.Error(ex, "Failed to copy LLS installer to network path: {Error}", ex.Message);
                                            SendSlackMessage(settings, $":warning: Failed to copy LLS installer to network path: {ex.Message}");
                                        }
                                    }
                                    else
                                    {
                                        Log.Warning("No LLS installer files found in output directory: {Dir}", installerDir);
                                    }
                                }
                                else
                                {
                                    Log.Warning("LLS output directory not found: {Dir}", installerDir);
                                }
                            }
                            else
                            {
                                Log.Error("Advanced Installer LLS failed with exit code {ExitCode}", aiProcess.ExitCode);
                                SendSlackMessage(settings, $":x: LLS installer build failed with exit code {aiProcess.ExitCode}");
                                var output = outputBuilder.ToString().Trim();
                                if (output.Length > 500)
                                {
                                    output = "..." + output.Substring(Math.Max(0, output.Length - 500));
                                }
                                if (!string.IsNullOrWhiteSpace(output))
                                {
                                    SendSlackMessage(settings, $"```\n{output}\n```");
                                }
                                if (output.Contains("Build finished because an error was encountered"))
                                {
                                    if (output.Contains("Permission denied") || output.Contains("Access is denied"))
                                    {
                                        SendSlackMessage(settings, ":warning: Advanced Installer may need administrative privileges. Please check file permissions.");
                                    }
                                    else if (output.Contains("is in use by another process"))
                                    {
                                        SendSlackMessage(settings, ":warning: Some files are locked by another process. Please close any applications that might be using the installer files.");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error running Advanced Installer for LLS: {ErrorMessage}", ex.Message);
                            SendSlackMessage(settings, $":x: Error Creating LLS installer: {ex.Message}");
                        }
                    }
                    else
                    {
                        Log.Warning("Advanced Installer LLS project not found at: {Path}", advancedInstallerPath);
                        SendSlackMessage(settings, $":warning: Advanced Installer LLS project not found at: {advancedInstallerPath}");
                    }
                }
                else
                {
                    Log.Error("LLS build failed with exit code {ExitCode}", process.ExitCode);
                    SendSlackMessage(settings, $":x: LLS build failed with exit code {process.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during LLS build: {ErrorMessage}", ex.Message);
                SendSlackMessage(settings, $":x: Error during LLS build: {ex.Message}");
            }
        }

        static void SendSlackMessage(SlackCISettings settings, string message)
        {
            try
            {
                if (string.IsNullOrEmpty(settings.SlackWebhookUrl))
                {
                    Log.Warning("Slack webhook URL not configured. Message not sent: {Message}", message);
                    return;
                }
                
                var webhookClient = new SlackClient(settings.SlackWebhookUrl);
                var slackMessage = new SlackMessage
                {
                    Text = message,
                    Channel = settings.ChannelName,
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

        static async Task PollSlackMessages(SlackCISettings settings)
        {
            Log.Information("Now Listening for Slack messages");
            lastCheckTime = DateTime.UtcNow;

            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(10000, cts.Token);

                    var timestamp = DateTimeToUnixTimestamp(lastCheckTime);
                    var url = $"https://slack.com/api/conversations.history?channel={settings.ChannelId}&oldest={timestamp}";

                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("Authorization", $"Bearer {settings.SlackBotToken}");

                    using var response = await httpClient.SendAsync(request, cts.Token);
                    if (!response.IsSuccessStatusCode)
                    {
                        Log.Warning("Failed to get messages from Slack: {StatusCode}", response.StatusCode);
                        continue;
                    }

                    var content = await response.Content.ReadAsStringAsync(cts.Token);
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
                            await ProcessCommand(settings, text);
                        }
                    }

                    lastCheckTime = DateTime.UtcNow;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error polling Slack messages: {Error}", ex.Message);
                    await Task.Delay(30000, cts.Token);
                }
            }
        }

        static double DateTimeToUnixTimestamp(DateTime dateTime)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            if (dateTime == DateTime.MinValue)
            {
                return Math.Round((DateTime.UtcNow.AddMinutes(-5) - epoch).TotalSeconds, 3);
            }
            return Math.Round((dateTime - epoch).TotalSeconds, 3);
        }

        /// <summary>
        /// Saves the current settings back to the appsettings.json file
        /// </summary>
        private static void SaveSettings(SlackCISettings settings)
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
                            JsonSerializer.Serialize(writer, settings);
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
        
        static async Task TestSlackUpload(SlackCISettings settings)
        {
            try
            {
                var installerPath = settings.WindowsInstallerPath;
                if (string.IsNullOrEmpty(installerPath) || !File.Exists(installerPath))
                {
                    Log.Error("No installer file found at: {Path}", installerPath);
                    return;
                }

                var fileName = Path.GetFileName(installerPath);
                var fileSize = new FileInfo(installerPath).Length / (1024.0 * 1024.0);
                
                Log.Information("Testing upload of installer: {File} ({Size:F2} MB)", fileName, fileSize);
                SendSlackMessage(settings, ":gear: Testing installer file upload...");

                var notifier = new SlackNotifier(settings, Log.Logger);
                var success = await notifier.UploadFileAsync(
                    installerPath,
                    title: $"basehead PC Installer {DateTime.Now:yyyy-MM-dd} (Test)",
                    initialComment: "Test upload of BaseHead PC installer"
                );

                if (success)
                {
                    SendSlackMessage(settings, ":white_check_mark: Test upload successful!");
                }
                else
                {
                    SendSlackMessage(settings, ":x: Test upload failed. Check the logs for more details.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error testing file upload");
                SendSlackMessage(settings, $":x: Error testing file upload: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the version in basehead.csproj to today's date (YYYY.MM.DD format)
        /// </summary>
        private static (bool Success, string Version, string Message) UpdateCsprojVersionToToday(SlackCISettings settings)
        {
            try
            {
                string repoPath = settings.WindowsRepoPath;
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
        /// Syncs the version from the csproj file to the Advanced Installer .aip file
        /// </summary>
        private static (bool Success, string Version, string Message) SyncVersionToAdvancedInstaller(SlackCISettings settings)
        {
            try
            {
                // Find the csproj file in the repo
                string repoPath = settings.WindowsRepoPath;
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
                string aipPath = settings.AdvancedInstallerProjectPath;
                if (!File.Exists(aipPath))
                {
                    Log.Warning("Could not find Advanced Installer project at: {Path}", aipPath);
                    return (false, version, $"Could not find Advanced Installer project at: {aipPath}");
                }

                string aipContent = File.ReadAllText(aipPath);

                // Use regex to find and replace ProductVersion value
                var regex = new Regex(@"(<ROW Property=""ProductVersion"" Value="")([^""]+)("")");
                var match = regex.Match(aipContent);

                if (!match.Success)
                {
                    Log.Warning("Could not find ProductVersion property in .aip file");
                    return (false, version, "Could not find ProductVersion property in .aip file");
                }

                string oldVersion = match.Groups[2].Value;
                if (oldVersion == version)
                {
                    Log.Information("Version already matches: {Version}", version);
                    return (true, version, $"Version already up to date: {version}");
                }

                // Replace the version
                string newContent = regex.Replace(aipContent, $"${{1}}{version}${{3}}");
                File.WriteAllText(aipPath, newContent);

                Log.Information("Updated Advanced Installer version from {OldVersion} to {NewVersion}", oldVersion, version);
                return (true, version, $"Updated version from {oldVersion} to {version}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error syncing version to Advanced Installer");
                return (false, "", $"Error syncing version: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the version in LLS csproj to today's date (YYYY.MM.DD format)
        /// </summary>
        private static (bool Success, string Version, string Message) UpdateLLSCsprojVersionToToday(SlackCISettings settings)
        {
            try
            {
                string csprojPath = Path.Combine(settings.LLSRepoPath, "basehead.LicenseServer.csproj");

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
        /// </summary>
        private static (bool Success, string Version, string Message) SyncLLSVersionToAdvancedInstaller(SlackCISettings settings)
        {
            try
            {
                // Read version from LLS csproj
                string csprojPath = Path.Combine(settings.LLSRepoPath, "basehead.LicenseServer.csproj");

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
                string aipPath = settings.LLSAdvancedInstallerProjectPath;
                if (!File.Exists(aipPath))
                {
                    Log.Warning("Could not find LLS Advanced Installer project at: {Path}", aipPath);
                    return (false, version, $"Could not find LLS Advanced Installer project at: {aipPath}");
                }

                string aipContent = File.ReadAllText(aipPath);

                // Use regex to find and replace ProductVersion value
                var regex = new Regex(@"(<ROW Property=""ProductVersion"" Value="")([^""]+)("")");
                var match = regex.Match(aipContent);

                if (!match.Success)
                {
                    Log.Warning("Could not find ProductVersion property in LLS .aip file");
                    return (false, version, "Could not find ProductVersion property in LLS .aip file");
                }

                string oldVersion = match.Groups[2].Value;
                if (oldVersion == version)
                {
                    Log.Information("LLS Advanced Installer version already matches: {Version}", version);
                    return (true, version, $"LLS AI version already up to date: {version}");
                }

                // Replace the version
                string newContent = regex.Replace(aipContent, $"${{1}}{version}${{3}}");
                File.WriteAllText(aipPath, newContent);

                Log.Information("Updated LLS Advanced Installer version from {OldVersion} to {NewVersion}", oldVersion, version);
                return (true, version, $"Updated LLS AI version from {oldVersion} to {version}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error syncing LLS version to Advanced Installer");
                return (false, "", $"Error syncing LLS version: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a versioned filename for the installer
        /// Example: "Install basehead v2025.exe" -> "Install basehead v2025.12.04.exe"
        /// </summary>
        private static string GetVersionedFileName(string originalFileName, string version)
        {
            if (string.IsNullOrEmpty(version))
                return originalFileName;

            string extension = Path.GetExtension(originalFileName);
            string nameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);

            // Pattern: "Install basehead v2025" or similar - replace the version part
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
    }
}
