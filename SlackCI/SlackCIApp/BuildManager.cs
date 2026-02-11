using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Serilog;
using SlackCIApp.Config;
using Renci.SshNet;

namespace SlackCIApp
{
    public class BuildManager
    {        private readonly SlackCISettings _settings;
        private readonly SlackNotifier _slackNotifier;
        private readonly SshService _sshService;
        private readonly GitService _gitService;
        private readonly ILogger _logger;

        public BuildManager(
            SlackCISettings settings, 
            SlackNotifier slackNotifier, 
            SshService sshService,
            GitService gitService,
            ILogger logger)
        {
            _settings = settings;
            _slackNotifier = slackNotifier;
            _sshService = sshService;
            _gitService = gitService;
            _logger = logger;
        }

        public async Task<bool> BuildWindowsAsync()
        {
            _logger.Information("Starting Windows build process");
            
            try
            {                // Pull latest changes from Git
                var pullResult = await _gitService.PullLatestChangesWindowsAsync();
                if (!pullResult.Success)
                {
                    _logger.Error("Failed to pull latest changes on Windows: {Message}", pullResult.Message);
                    await _slackNotifier.SendNotificationAsync($"❌ Build aborted: Git pull failed:\n{pullResult.Message}");
                    return false; // Stop the build if Git fails
                }

                _logger.Information("Successfully pulled latest changes on Windows: {Message}", pullResult.Message);
                await _slackNotifier.SendNotificationAsync($"📥 Windows repository updated:\n{pullResult.Message}");

                // Send build starting notification
                await _slackNotifier.SendNotificationAsync("🖥️ Windows build started");
                  // First read version from csproj
                var csprojFile = Path.Combine(_settings.WindowsRepoPath, "basehead", "basehead.csproj");
                if (!File.Exists(csprojFile))
                {
                    _logger.Warning("Could not find csproj file at: {Path}", csprojFile);
                    await _slackNotifier.SendNotificationAsync($"❌ Windows build failed: Could not find csproj file at {csprojFile}");
                    return false;
                }

                var xmlContent = await File.ReadAllTextAsync(csprojFile);
                var versionMatch = System.Text.RegularExpressions.Regex.Match(xmlContent, @"<Version>(.*?)</Version>");
                if (!versionMatch.Success)
                {
                    _logger.Warning("Could not find Version tag in csproj file");
                    await _slackNotifier.SendNotificationAsync("❌ Windows build failed: Could not find Version tag in csproj file");
                    return false;
                }

                _settings.BuildVersion = versionMatch.Groups[1].Value;
                _logger.Information("Found version {Version} in csproj file", _settings.BuildVersion);
                SaveSettings();

                // Check if build script exists
                if (!File.Exists(_settings.WindowsBuildScriptPath))
                {
                    _logger.Warning("Windows build script not found at {ScriptPath}", _settings.WindowsBuildScriptPath);
                    await _slackNotifier.SendNotificationAsync($"❌ Windows build failed: Build script not found at {_settings.WindowsBuildScriptPath}");
                    return false;
                }

                // Run the build script
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _settings.WindowsBuildScriptPath,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = false
                    }
                };

                _logger.Information("Starting Windows build process with script: {ScriptPath}", _settings.WindowsBuildScriptPath);
                
                var outputBuilder = new System.Text.StringBuilder();
                process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        _logger.Information("Build output: {Output}", args.Data);
                        outputBuilder.AppendLine(args.Data);
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        _logger.Error("Build error: {Error}", args.Data);
                        outputBuilder.AppendLine($"ERROR: {args.Data}");
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                bool success = process.ExitCode == 0;
                  if (success)
                {
                    _logger.Information("Windows build completed successfully");                    await _slackNotifier.SendNotificationAsync("✅ Windows build completed successfully");

                    // Update the version from csproj to Advanced Installer before building installer
                    await UpdateBuildVersionFromProjectAsync();

                    // Run Advanced Installer to build the installer
                    await RunAdvancedInstallerAsync();

                    // Check for installer
                    var installerPath = _settings.WindowsInstallerPath;
                    if (!string.IsNullOrEmpty(installerPath) && File.Exists(installerPath))
                    {
                        var fileInfo = new FileInfo(installerPath);
                        await _slackNotifier.SendNotificationAsync($"📦 Windows installer ready: {installerPath} ({fileInfo.Length / (1024.0 * 1024.0):F2} MB)");
                        
                        // Upload the installer file to Slack
                        if (await _slackNotifier.UploadFileAsync(
                            installerPath, 
                            title: $"basehead PC Installer {DateTime.Now:yyyy-MM-dd}", 
                            initialComment: "Latest basehead PC installer build"))
                        {
                            await _slackNotifier.SendNotificationAsync("📥 Installer has been uploaded to this channel!");
                        }
                        else
                        {
                            _logger.Warning("Failed to upload installer file to Slack");
                            await _slackNotifier.SendNotificationAsync("⚠️ The installer was built but could not be uploaded to Slack");
                        }
                    }
                    
                    return true;
                }
                else
                {
                    _logger.Error("Windows build failed with exit code {ExitCode}", process.ExitCode);
                    await _slackNotifier.SendNotificationAsync($"❌ Windows build failed with exit code {process.ExitCode}");
                    
                    // Send truncated build output if failed
                    var output = outputBuilder.ToString();
                    if (output.Length > 1000)
                    {
                        output = "..." + output.Substring(output.Length - 1000);
                    }
                    await _slackNotifier.SendNotificationAsync($"📝 Last build output:\n```\n{output}\n```");
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during Windows build process");
                await _slackNotifier.SendNotificationAsync($"❌ Error during Windows build: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> BuildMacAsync()
        {
            _logger.Information("Starting Mac build process");

            try
            {
                // Pull latest changes from Git
                var pullResult = await _gitService.PullLatestChangesMacAsync();
                if (!pullResult.Success)
                {
                    _logger.Error("Failed to pull latest changes on Mac: {Message}", pullResult.Message);
                    await _slackNotifier.SendNotificationAsync($"❌ Build aborted: Git pull failed:\n{pullResult.Message}");
                    return false; // Stop the build if Git fails
                }

                _logger.Information("Successfully pulled latest changes on Mac: {Message}", pullResult.Message);
                await _slackNotifier.SendNotificationAsync($"📥 {pullResult.Message}");

                // Send build starting notification
                await _slackNotifier.SendNotificationAsync("🍏 Mac build started");
                
                // Execute the build on Mac
                var (success, output) = await _sshService.ExecuteMacBuildAsync();
                
                if (success)
                {
                    _logger.Information("Mac build completed successfully");
                    await _slackNotifier.SendNotificationAsync("✅ Mac build completed successfully");
                    
                    // Check if we should download the installer
                    if (!string.IsNullOrEmpty(_settings.MacInstallerPath) && !string.IsNullOrEmpty(_settings.MacInstallerLocalPath))
                    {
                        // SSH to Mac and copy installer to BeeStation
                        string macNetworkPath = "/Volumes/home/Files/build-server";
                        var copyCommand = $"mkdir -p {macNetworkPath} && cp {_settings.MacInstallerPath} {macNetworkPath}/";
                        using var client = new SshClient(_settings.MacHostname, _settings.MacUsername, new PrivateKeyFile(_settings.MacKeyPath));
                        client.Connect();
                        
                        if (client.IsConnected)
                        {
                            var cmdResult = client.RunCommand(copyCommand);
                            if (string.IsNullOrEmpty(cmdResult.Error))
                            {
                                await _slackNotifier.SendNotificationAsync($":file_folder: Mac installer copied to: {macNetworkPath}/{Path.GetFileName(_settings.MacInstallerPath)}");
                            }
                            else
                            {
                                _logger.Error("Failed to copy Mac installer to network path: {Error}", cmdResult.Error);
                                await _slackNotifier.SendNotificationAsync($":warning: Failed to copy Mac installer to network path: {cmdResult.Error}");
                            }
                        }

                        // Download the installer to Windows
                        if (await _sshService.DownloadInstallerAsync(_settings.MacInstallerPath, _settings.MacInstallerLocalPath))
                        {
                            var fileInfo = new FileInfo(_settings.MacInstallerLocalPath);
                            await _slackNotifier.SendNotificationAsync($"📦 Mac installer ready: {_settings.MacInstallerLocalPath} ({fileInfo.Length / (1024.0 * 1024.0):F2} MB)");

                            // Upload the installer file to Slack
                            if (await _slackNotifier.UploadFileAsync(
                                _settings.MacInstallerLocalPath,
                                title: $"BaseHead Mac Installer {DateTime.Now:yyyy-MM-dd}",
                                initialComment: "Latest BaseHead Mac installer build"))
                            {
                                await _slackNotifier.SendNotificationAsync("📥 Mac installer has been uploaded to this channel!");

                                // Add web download link
                                string webDownloadUrl = "http://j8cd6qcvrjt956vxvyixiuoss2n6mes.quickconnect.to/sharing/fcRcMqktX";
                                await _slackNotifier.SendNotificationAsync($":globe_with_meridians: Web download link: {webDownloadUrl}");
                            }
                            else
                            {
                                _logger.Warning("Failed to upload Mac installer file to Slack");
                                await _slackNotifier.SendNotificationAsync("⚠️ The Mac installer was built but could not be uploaded to Slack");
                            }
                        }
                        else
                        {
                            await _slackNotifier.SendNotificationAsync("⚠️ Warning: Failed to download Mac installer");
                        }
                    }
                }
                else
                {
                    _logger.Error("Mac build failed: {Output}", output);
                    await _slackNotifier.SendNotificationAsync($"❌ Mac build failed");
                    
                    // Send truncated build output if failed
                    if (output.Length > 1000)
                    {
                        output = "..." + output.Substring(output.Length - 1000);
                    }
                    await _slackNotifier.SendNotificationAsync($"📝 Last build output:\n```\n{output}\n```");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during Mac build process");
                await _slackNotifier.SendNotificationAsync($"❌ Error during Mac build: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Runs Advanced Installer to build the Windows installer after a successful build
        /// </summary>
        /// <returns>True if the installer was successfully built, False otherwise</returns>
        private async Task<bool> RunAdvancedInstallerAsync()
        {
            try
            {
                var advancedInstallerPath = _settings.AdvancedInstallerProjectPath;
                
                if (!File.Exists(advancedInstallerPath))
                {
                    _logger.Warning("Advanced Installer project not found at: {Path}", advancedInstallerPath);
                    await _slackNotifier.SendNotificationAsync($":warning: Advanced Installer project not found at: {advancedInstallerPath}");
                    return false;
                }
                
                if (!File.Exists(_settings.AdvancedInstallerExePath))
                {
                    _logger.Error("Advanced Installer executable not found at: {Path}", _settings.AdvancedInstallerExePath);
                    await _slackNotifier.SendNotificationAsync($":x: Advanced Installer executable not found at: {_settings.AdvancedInstallerExePath}");
                    return false;
                }                // First read version from csproj
                var csprojFile = Path.Combine(_settings.WindowsRepoPath, "basehead", "basehead.csproj");
                if (!File.Exists(csprojFile))
                {
                    _logger.Warning("Could not find csproj file at: {Path}", csprojFile);
                    return false;
                }

                var xmlContent = await File.ReadAllTextAsync(csprojFile);
                var versionMatch = System.Text.RegularExpressions.Regex.Match(xmlContent, @"<Version>(.*?)</Version>");
                if (!versionMatch.Success)
                {
                    _logger.Warning("Could not find Version tag in csproj file");
                    return false;
                }

                var version = versionMatch.Groups[1].Value;
                _settings.BuildVersion = version;
                _logger.Information("Found version {Version} in csproj file", version);
                SaveSettings();                _logger.Information("Running Advanced Installer to build PC installer v{Version}: {Path}", version, advancedInstallerPath);
                await _slackNotifier.SendNotificationAsync($":gear: Creating installer v{version} with Advanced Installer...");

                // Set version in .aip file
                using (var setVersionProcess = new Process())
                {                    setVersionProcess.StartInfo = new ProcessStartInfo
                    {
                        FileName = _settings.AdvancedInstallerExePath,
                        Arguments = $"/edit \"{advancedInstallerPath}\" /SetVersion \"{version}\" /SaveAs \"{advancedInstallerPath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(advancedInstallerPath) ?? Directory.GetCurrentDirectory()
                    };

                    var versionOutputBuilder = new System.Text.StringBuilder();
                    setVersionProcess.OutputDataReceived += (sender, args) =>
                    {
                        if (args.Data != null)
                        {
                            versionOutputBuilder.AppendLine(args.Data);
                            _logger.Information("AI Version: {Output}", args.Data);
                        }
                    };

                    setVersionProcess.ErrorDataReceived += (sender, args) =>
                    {
                        if (args.Data != null)
                        {
                            versionOutputBuilder.AppendLine($"ERROR: {args.Data}");
                            _logger.Error("AI Version Error: {Error}", args.Data);
                        }
                    };

                    setVersionProcess.Start();
                    setVersionProcess.BeginOutputReadLine();
                    setVersionProcess.BeginErrorReadLine();
                    await setVersionProcess.WaitForExitAsync();

                    if (setVersionProcess.ExitCode != 0)
                    {
                        _logger.Error("Failed to update version in Advanced Installer project");
                        await _slackNotifier.SendNotificationAsync(":x: Failed to update version in Advanced Installer project");
                        return false;
                    }
                }

                // Then build the installer
                using (var buildProcess = new Process())
                {                    buildProcess.StartInfo = new ProcessStartInfo
                    {
                        FileName = _settings.AdvancedInstallerExePath,
                        Arguments = $"/build \"{advancedInstallerPath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(advancedInstallerPath) ?? Directory.GetCurrentDirectory()
                    };

                    // Capture output for logging
                    var outputBuilder = new System.Text.StringBuilder();
                    buildProcess.OutputDataReceived += (sender, args) =>
                    {
                        if (args.Data != null)
                        {
                            outputBuilder.AppendLine(args.Data);
                            _logger.Information("AI Build: {Output}", args.Data);
                        }
                    };

                    buildProcess.ErrorDataReceived += (sender, args) =>
                    {
                        if (args.Data != null)
                        {
                            outputBuilder.AppendLine($"ERROR: {args.Data}");
                            _logger.Error("AI Build Error: {Error}", args.Data);
                        }
                    };

                    buildProcess.Start();
                    buildProcess.BeginOutputReadLine();
                    buildProcess.BeginErrorReadLine();

                    // Wait for the process with a timeout
                    bool completed = await Task.Run(() => buildProcess.WaitForExit(300000)); // 5 minutes timeout
                    
                    if (!completed)
                    {
                        _logger.Error("Advanced Installer build timed out after 5 minutes");
                        await _slackNotifier.SendNotificationAsync(":x: Advanced Installer build timed out after 5 minutes");
                        return false;
                    }

                    if (buildProcess.ExitCode != 0)
                    {
                        _logger.Error("Advanced Installer failed with exit code {ExitCode}", buildProcess.ExitCode);
                        
                        // Send truncated output if it's too long
                        var output = outputBuilder.ToString().Trim();
                        if (output.Length > 1000)
                        {
                            output = "..." + output.Substring(output.Length - 1000);
                        }
                        await _slackNotifier.SendNotificationAsync($":x: Advanced Installer failed with exit code {buildProcess.ExitCode}\n```\n{output}\n```");
                        return false;
                    }
                }
                
                _logger.Information("Advanced Installer build completed successfully");
                
                // Find the installer file
                string installerDir = Path.GetDirectoryName(advancedInstallerPath) ?? Directory.GetCurrentDirectory();
                string[] potentialOutputDirs = new[]
                {
                    Path.Combine(installerDir, "Output"),
                    Path.Combine(installerDir, "Builds"),
                    Path.Combine(installerDir, "Setup"),
                    installerDir // Look in the same directory as the .aip file as well
                };
                
                _logger.Information("Looking for installer files in potential output directories");
                
                string? latestInstallerPath = null;
                DateTime latestTimestamp = DateTime.MinValue;
                
                // Find the latest installer file in any of the potential output directories
                foreach (string outputDir in potentialOutputDirs)
                {
                    if (Directory.Exists(outputDir))
                    {
                        _logger.Information("Checking for installer files in: {Dir}", outputDir);
                        string[] installerFiles = Directory.GetFiles(outputDir, "*.exe");
                        
                        foreach (string installerFile in installerFiles)
                        {
                            DateTime fileTimestamp = File.GetLastWriteTime(installerFile);
                            if (fileTimestamp > latestTimestamp)
                            {
                                latestTimestamp = fileTimestamp;
                                latestInstallerPath = installerFile;
                            }
                        }
                    }
                }
                
                if (latestInstallerPath != null)
                {
                    _logger.Information("Found latest installer: {Path}", latestInstallerPath);
                    _settings.WindowsInstallerPath = latestInstallerPath;
                    
                    // Save the updated settings
                    SaveSettings();
                    
                    // If installer was found, send a notification with the file size
                    var fileInfo = new FileInfo(latestInstallerPath);
                    await _slackNotifier.SendNotificationAsync($":package: Windows installer built successfully: {Path.GetFileName(latestInstallerPath)} ({fileInfo.Length / 1024 / 1024} MB)");

                    // Copy to network path
                    try
                    {
                        string networkPath = @"\\BeeStation\home\Files\build-server";
                        string networkFilePath = Path.Combine(networkPath, Path.GetFileName(latestInstallerPath));
                        _logger.Information("Copying Windows installer to network path: {NetworkPath}", networkFilePath);
                        
                        if (!Directory.Exists(networkPath))
                        {
                            Directory.CreateDirectory(networkPath);
                        }
                        
                        File.Copy(latestInstallerPath, networkFilePath, true);
                        await _slackNotifier.SendNotificationAsync($":file_folder: Windows installer copied to network: {networkFilePath}");

                        // Add web download link
                        string webDownloadUrl = "http://j8cd6qcvrjt956vxvyixiuoss2n6mes.quickconnect.to/sharing/fcRcMqktX";
                        await _slackNotifier.SendNotificationAsync($":globe_with_meridians: Web download link: {webDownloadUrl}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to copy Windows installer to network path: {Error}", ex.Message);
                        await _slackNotifier.SendNotificationAsync($":warning: Failed to copy Windows installer to network path: {ex.Message}");
                    }
                    
                    return true;
                }
                else
                {
                    _logger.Warning("No installer files found in any of the output directories");
                    await _slackNotifier.SendNotificationAsync(":warning: Advanced Installer completed successfully, but no installer files were found");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error running Advanced Installer: {ErrorMessage}", ex.Message);
                await _slackNotifier.SendNotificationAsync($":x: Error running Advanced Installer: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Saves the current settings back to the appsettings.json file
        /// </summary>
        private void SaveSettings()
        {
            try
            {
                string appSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                if (File.Exists(appSettingsPath))
                {
                    // Read existing JSON
                    string json = File.ReadAllText(appSettingsPath);
                    using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(json);
                    
                    // Create new JSON with updated settings
                    using var stream = new MemoryStream();
                    using var writer = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions { Indented = true });
                    
                    writer.WriteStartObject();
                    
                    // Copy all root properties
                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        if (property.Name == "SlackCISettings")
                        {
                            // Write updated SlackCISettings
                            writer.WritePropertyName("SlackCISettings");
                            System.Text.Json.JsonSerializer.Serialize(writer, _settings);
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
                    _logger.Information("Settings saved successfully");
                }
                else
                {
                    _logger.Warning("appsettings.json not found, cannot save settings");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error saving settings: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Updates the build version from the .csproj file
        /// </summary>        
        private async Task UpdateBuildVersionFromProjectAsync()
        {
            try
            {
                var csprojFile = Path.Combine(_settings.WindowsRepoPath, "basehead", "basehead.csproj");
                if (File.Exists(csprojFile))
                {
                    var xmlContent = await File.ReadAllTextAsync(csprojFile);
                    var versionMatch = System.Text.RegularExpressions.Regex.Match(xmlContent, @"<Version>(.*?)</Version>");
                    if (versionMatch.Success)
                    {
                        var version = versionMatch.Groups[1].Value;
                        _settings.BuildVersion = version;
                        _logger.Information("Found version {Version} in csproj file", version);
                        SaveSettings();
                    }
                    else
                    {
                        _logger.Warning("Could not find Version tag in csproj file");
                    }
                }
                else
                {
                    _logger.Warning("Could not find csproj file at: {Path}", csprojFile);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error reading version from csproj file");
            }
        }
    }
}