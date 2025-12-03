using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Renci.SshNet;
using Serilog;
using SlackCIApp.Config;

namespace SlackCIApp
{
    public class SshService
    {
        private readonly SlackCISettings _settings;
        private readonly ILogger _logger;

        public SshService(SlackCISettings settings, ILogger logger)
        {
            _settings = settings;
            _logger = logger;
        }
        
        public async Task<(bool Success, string Output)> ExecuteMacBuildAsync()
        {
            _logger.Information("Preparing to execute Mac build via SSH");

            // Check if SSH settings are configured
            if (string.IsNullOrEmpty(_settings.MacHostname) || 
                string.IsNullOrEmpty(_settings.MacUsername) || 
                string.IsNullOrEmpty(_settings.MacKeyPath))
            {
                _logger.Warning("SSH settings not fully configured. Please update settings.");
                return (false, "SSH settings not configured. Please update the configuration.");
            }

            if (!File.Exists(_settings.MacKeyPath))
            {
                _logger.Warning("SSH key file not found: {KeyPath}", _settings.MacKeyPath);
                return (false, $"SSH key file not found: {_settings.MacKeyPath}");
            }            try
            {
                // Resolve the IP address first
                _logger.Information("Resolving hostname: {Host}", _settings.MacHostname);
                var hostEntry = await Dns.GetHostEntryAsync(_settings.MacHostname);
                var ipAddress = hostEntry.AddressList.FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                
                if (ipAddress == null)
                {
                    _logger.Error("No IPv4 address found for {Host}", _settings.MacHostname);
                    return (false, $"No IPv4 address found for {_settings.MacHostname}");
                }

                _logger.Information("Connecting to Mac via SSH using IP: {IpAddress}", ipAddress);
                using var client = new SshClient(ipAddress.ToString(), _settings.MacUsername, new PrivateKeyFile(_settings.MacKeyPath));
                
                // Set connection timeout
                client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(30);
                client.Connect();

                if (!client.IsConnected)
                {
                    _logger.Error("Failed to connect to Mac via SSH");
                    return (false, "Failed to connect to Mac via SSH");
                }
                
                _logger.Information("Successfully connected to Mac. Preparing to open Terminal and execute: {Script}", _settings.MacBuildScriptPath);
                
                // Create a unique identifier for this build run
                string buildId = Guid.NewGuid().ToString("N");
                
                // Create unique files to track progress and capture output
                var markerFile = $"/tmp/build_complete_{buildId}";
                var exitCodeFile = $"/tmp/build_exit_{buildId}";
                var outputFile = $"/tmp/build_output_{buildId}";
                
                // Extract the directory and script name - properly handle both Unix and Windows paths
                string scriptDir = _settings.MacBuildScriptPath.Replace("\\", "/");
                string scriptName = Path.GetFileName(scriptDir);
                scriptDir = Path.GetDirectoryName(scriptDir)?.Replace("\\", "/") ?? "/";
                
                if (string.IsNullOrEmpty(scriptDir))
                {
                    return (false, "Invalid script path. Could not extract directory.");
                }

                // First check if the script exists and make it executable
                var checkScript = client.RunCommand($"test -f {scriptDir}/{scriptName} && echo 'exists' || echo 'not found'");
                if (checkScript.Result.Trim() != "exists")
                {
                    _logger.Error("Build script not found at {ScriptPath}", $"{scriptDir}/{scriptName}");
                    return (false, $"Build script not found at {scriptDir}/{scriptName}");
                }
                
                client.RunCommand($"chmod +x {scriptDir}/{scriptName}");
                
                // Create an AppleScript that will:
                // 1. Open a new Terminal window
                // 2. Run our build script
                // 3. Capture exit code and mark completion
                var appleScript = $@"
                tell application ""Terminal""
                    activate
                    set newTab to do script ""cd '{scriptDir}' && ./{scriptName} > {outputFile} 2>&1; echo $? > {exitCodeFile}; touch {markerFile}""
                    set custom title of tab 1 of window 1 to ""BaseHead Build""
                end tell";

                _logger.Information("Executing AppleScript to open Terminal.app");
                var scriptCmd = client.RunCommand($"osascript -e '{appleScript}'");
                
                if (!string.IsNullOrEmpty(scriptCmd.Error))
                {
                    _logger.Error("AppleScript execution failed: {Error}", scriptCmd.Error);
                    return (false, $"Failed to open Terminal on Mac: {scriptCmd.Error}");
                }
                  // Wait for build to complete by checking for marker file
                _logger.Information("Build running in Terminal.app on Mac. Waiting for completion...");
                var output = new StringBuilder();
                bool buildComplete = false;
                int timeoutCounter = 0;
                int maxTimeout = 1000; // 16.67 minutes timeout (1000 seconds with 1-second checks)
                
                while (!buildComplete && timeoutCounter < maxTimeout)
                {
                    var checkCmd = client.RunCommand($"test -f {markerFile} && echo 'complete' || echo 'running'");
                    if (checkCmd.Result.Trim() == "complete")
                    {
                        buildComplete = true;
                    }
                    else
                    {
                        // Check for new output to display
                        var outputExists = client.RunCommand($"test -f {outputFile} && echo 'exists' || echo 'no'");
                        if (outputExists.Result.Trim() == "exists")
                        {
                            var tailCmd = client.RunCommand($"cat {outputFile} && : > {outputFile}");
                            if (!string.IsNullOrEmpty(tailCmd.Result))
                            {
                                output.Append(tailCmd.Result);
                                
                                // Log new output
                                foreach (var line in tailCmd.Result.Split('\n'))
                                {
                                    if (!string.IsNullOrWhiteSpace(line))
                                    {
                                        _logger.Information("Build output: {Output}", line.TrimEnd());
                                    }
                                }
                            }
                        }
                        
                        await Task.Delay(1000);
                        timeoutCounter++;
                    }
                }
                
                if (!buildComplete)
                {
                    _logger.Error("Build timed out after {Seconds} seconds", maxTimeout);
                    // Try to close Terminal window anyway
                    client.RunCommand(@"osascript -e 'tell application ""Terminal"" to close (every window whose name contains ""BaseHead Build"")'");
                    return (false, "Build timed out");
                }
                
                // Get exit code
                var exitCodeExists = client.RunCommand($"test -f {exitCodeFile} && echo 'exists' || echo 'no'");
                int exitCode = 1; // Default to error
                
                if (exitCodeExists.Result.Trim() == "exists")
                {
                    var exitCodeCmd = client.RunCommand($"cat {exitCodeFile}");
                    if (!int.TryParse(exitCodeCmd.Result.Trim(), out exitCode))
                    {
                        _logger.Warning("Could not parse exit code: {Result}", exitCodeCmd.Result);
                    }
                }
                
                // Get any remaining output
                var finalOutputExists = client.RunCommand($"test -f {outputFile} && echo 'exists' || echo 'no'");
                if (finalOutputExists.Result.Trim() == "exists")
                {
                    var finalOutputCmd = client.RunCommand($"cat {outputFile}");
                    if (!string.IsNullOrEmpty(finalOutputCmd.Result))
                    {
                        output.Append(finalOutputCmd.Result);
                        
                        // Log the output
                        foreach (var line in finalOutputCmd.Result.Split('\n'))
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                _logger.Information("Build output: {Output}", line.TrimEnd());
                            }
                        }
                    }
                }
                
                // Clean up temporary files
                client.RunCommand($"rm -f {markerFile} {exitCodeFile} {outputFile}");
                
                var success = exitCode == 0;
                _logger.Information("Build finished with exit code: {ExitCode}", exitCode);
                
                // Close the Terminal window
                client.RunCommand(@"osascript -e 'tell application ""Terminal"" to close (every window whose name contains ""BaseHead Build"")'");
                
                return (success, success 
                    ? "Build completed successfully" 
                    : $"Build failed with exit code: {exitCode}\n{output}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error executing Mac build: {ErrorMessage}", ex.Message);
                return (false, $"Error: {ex.Message}");
            }
        }
        
        public async Task<bool> DownloadInstallerAsync(string remotePath, string localPath)
        {
            _logger.Information("Downloading Mac installer via SCP");
            
            return await Task.Run(() => 
            {
                try
                {
                    using var client = new ScpClient(_settings.MacHostname, _settings.MacUsername, new PrivateKeyFile(_settings.MacKeyPath));
                    
                    _logger.Information("Connecting to Mac via SCP");
                    client.Connect();

                    if (!client.IsConnected)
                    {
                        _logger.Error("Failed to connect to Mac via SCP");
                        return false;
                    }

                    _logger.Information("Downloading file from {RemotePath} to {LocalPath}", remotePath, localPath);
                    
                    // Ensure the local directory exists
                    var localDir = Path.GetDirectoryName(localPath);
                    if (!string.IsNullOrEmpty(localDir) && !Directory.Exists(localDir))
                    {
                        Directory.CreateDirectory(localDir);
                    }
                    
                    // Download the file
                    using var fileStream = File.Create(localPath);
                    client.Download(remotePath, fileStream);
                    
                    _logger.Information("Successfully downloaded Mac installer");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error downloading Mac installer: {ErrorMessage}", ex.Message);
                    return false;
                }
            });
        }

        /// <summary>
        /// Generates an SSH key on the Mac and returns the public key for adding to Bitbucket
        /// </summary>
        public async Task<(bool Success, string PublicKey, string Message)> GenerateMacSshKeyAsync()
        {
            _logger.Information("Generating SSH key on Mac for Bitbucket access");

            try
            {
                using var client = new SshClient(_settings.MacHostname, _settings.MacUsername, new PrivateKeyFile(_settings.MacKeyPath));
                
                _logger.Information("Connecting to Mac via SSH");
                await Task.Run(() => client.Connect());

                if (!client.IsConnected)
                {
                    _logger.Error("Failed to connect to Mac via SSH");
                    return (false, string.Empty, "Failed to connect to Mac via SSH");
                }

                // Create .ssh directory if it doesn't exist
                var result = await Task.Run(() => client.RunCommand("mkdir -p ~/.ssh && chmod 700 ~/.ssh"));
                if (result.ExitStatus != 0)
                {
                    _logger.Error("Failed to create .ssh directory: {Error}", result.Error);
                    return (false, string.Empty, $"Failed to create .ssh directory: {result.Error}");
                }

                // Check if key already exists
                result = await Task.Run(() => client.RunCommand("ls ~/.ssh/id_rsa_bitbucket.pub 2>/dev/null"));
                if (result.ExitStatus == 0)
                {
                    _logger.Information("SSH key already exists, reading public key");
                    result = await Task.Run(() => client.RunCommand("cat ~/.ssh/id_rsa_bitbucket.pub"));
                    return (true, result.Result.Trim(), "SSH key already exists");
                }

                // Generate new SSH key
                _logger.Information("Generating new SSH key");
                result = await Task.Run(() => client.RunCommand(
                    "ssh-keygen -t rsa -b 4096 -C \"slackci@basehead.org\" -f ~/.ssh/id_rsa_bitbucket -N \"\""));
                
                if (result.ExitStatus != 0)
                {
                    _logger.Error("Failed to generate SSH key: {Error}", result.Error);
                    return (false, string.Empty, $"Failed to generate SSH key: {result.Error}");
                }

                // Set proper permissions
                result = await Task.Run(() => client.RunCommand("chmod 600 ~/.ssh/id_rsa_bitbucket*"));
                if (result.ExitStatus != 0)
                {
                    _logger.Error("Failed to set SSH key permissions: {Error}", result.Error);
                    return (false, string.Empty, $"Failed to set SSH key permissions: {result.Error}");
                }

                // Configure SSH config to use the key for Bitbucket
                var sshConfig = @"
Host bitbucket.org
    HostName bitbucket.org
    User git
    IdentityFile ~/.ssh/id_rsa_bitbucket
    IdentitiesOnly yes";

                result = await Task.Run(() => client.RunCommand($"echo '{sshConfig}' > ~/.ssh/config && chmod 600 ~/.ssh/config"));
                if (result.ExitStatus != 0)
                {
                    _logger.Error("Failed to create SSH config: {Error}", result.Error);
                    return (false, string.Empty, $"Failed to create SSH config: {result.Error}");
                }

                // Read and return the public key
                result = await Task.Run(() => client.RunCommand("cat ~/.ssh/id_rsa_bitbucket.pub"));
                if (result.ExitStatus != 0 || string.IsNullOrEmpty(result.Result))
                {
                    _logger.Error("Failed to read public key: {Error}", result.Error);
                    return (false, string.Empty, "Failed to read public key");
                }

                var publicKey = result.Result.Trim();
                _logger.Information("Successfully generated SSH key");
                return (true, publicKey, "SSH key generated successfully. Please add this public key to your Bitbucket account.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error generating SSH key");
                return (false, string.Empty, $"Error generating SSH key: {ex.Message}");
            }
        }

        /// <summary>
        /// Tests SSH connectivity to the Mac
        /// </summary>
        public async Task<(bool Success, string Message)> TestConnectionAsync()
        {
            // Check if SSH settings are configured
            if (string.IsNullOrEmpty(_settings.MacHostname) || 
                string.IsNullOrEmpty(_settings.MacUsername) || 
                string.IsNullOrEmpty(_settings.MacKeyPath))
            {
                _logger.Warning("SSH settings not fully configured. Please update settings.");
                return (false, "SSH settings not configured. Please update the configuration.");
            }

            if (!File.Exists(_settings.MacKeyPath))
            {
                _logger.Warning("SSH key file not found: {KeyPath}", _settings.MacKeyPath);
                return (false, $"SSH key file not found: {_settings.MacKeyPath}");
            }

            try
            {
                using var client = new SshClient(_settings.MacHostname, _settings.MacUsername, new PrivateKeyFile(_settings.MacKeyPath));
                
                _logger.Information("Testing SSH connection to Mac");
                await Task.Run(() => client.Connect());

                if (!client.IsConnected)
                {
                    _logger.Error("Failed to connect to Mac via SSH");
                    return (false, "Failed to connect to Mac via SSH");
                }

                // Run a simple command to verify we can execute commands
                var result = await Task.Run(() => client.RunCommand("echo 'SSH test successful'"));
                if (result.ExitStatus != 0)
                {
                    _logger.Error("SSH command test failed: {Error}", result.Error);
                    return (false, $"SSH command test failed: {result.Error}");
                }

                _logger.Debug("SSH test successful");
                return (true, "SSH connection test successful");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error testing SSH connection");
                return (false, $"Error testing SSH connection: {ex.Message}");
            }
        }

        /// <summary>
        /// Executes a command on the remote Mac
        /// </summary>
        public async Task<(bool Success, string Error)> ExecuteCommandAsync(string command)
        {
            try
            {
                using var client = new SshClient(_settings.MacHostname, _settings.MacUsername, new PrivateKeyFile(_settings.MacKeyPath));
                await Task.Run(() => client.Connect());

                if (!client.IsConnected)
                {
                    return (false, "Failed to connect to Mac via SSH");
                }

                var result = await Task.Run(() => client.RunCommand(command));
                return (result.ExitStatus == 0, result.Error);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error executing SSH command: {Error}", ex.Message);
                return (false, ex.Message);
            }
        }
    }
}
