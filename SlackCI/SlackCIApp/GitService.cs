using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Security.AccessControl;
using Renci.SshNet;
using Serilog;
using SlackCIApp.Config;

namespace SlackCIApp
{
    public class GitService
    {        private readonly SlackCISettings _settings;
        private readonly ILogger _logger;
        private readonly SshService _sshService;
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 1000;
        private const int CommandTimeoutMs = 30000;
        private const string DefaultRemote = "origin";

        public GitService(SlackCISettings settings, ILogger logger, SshService sshService)
        {
            _settings = settings;
            _logger = logger;
            _sshService = sshService;
        }

        /// <summary>
        /// Pulls the latest changes from the Git repository on Windows
        /// </summary>
        public async Task<(bool Success, string Message)> PullLatestChangesWindowsAsync()
        {            if (string.IsNullOrEmpty(_settings.WindowsRepoPath))
            {
                _logger.Warning("Windows Git repository path is not configured");
                return (false, "Windows Git repository path is not configured");
            }            _logger.Information("Starting Git pull process on Windows with repository path: {RepoPath}", _settings.WindowsRepoPath);
              // Validate SSH configuration and configure Git credentials
            var sshValidation = await ValidateGitSshConfiguration(includeMac: false);
            if (!sshValidation.Valid)
            {
                _logger.Error("SSH validation failed: {Message}", sshValidation.Message);
                return (false, sshValidation.Message);
            }
            _logger.Debug("SSH validation passed: {Message}", sshValidation.Message);

            var credConfig = await ConfigureGitCredentials();
            if (!credConfig.Success)
            {
                _logger.Error("Git credential configuration failed: {Message}", credConfig.Message);
                return (false, credConfig.Message);
            }
            _logger.Debug("Git credentials configured successfully");
            try
            {
                // Verify repository exists and is a git repo
                if (!Directory.Exists(_settings.WindowsRepoPath))
                {
                    _logger.Error("Repository directory does not exist at {RepoPath}", _settings.WindowsRepoPath);
                    return (false, $"Repository directory does not exist at {_settings.WindowsRepoPath}");
                }                // Check if git is available
                _logger.Information("Checking Git installation...");
                // First try to find git directly
                var gitPath = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator)
                    .Select(p => Path.Combine(p, "git.exe"))
                    .FirstOrDefault(File.Exists);

                if (gitPath != null)
                {
                    _logger.Information("Found Git at: {GitPath}", gitPath);
                }
                
                var versionResult = await ExecuteGitCommandWindowsAsync("--version");
                if (!versionResult.Success)
                {
                    _logger.Error("Git version check failed: {Error}. PATH: {Path}", versionResult.Output, GetSystemPath());
                    return (false, "Git executable not found or not working properly. Please ensure Git is installed and in the system PATH.");
                }
                _logger.Information("Git version: {Version}", versionResult.Output.Trim());

                _logger.Information("Verifying Git repository status...");
                var verifyResult = await ExecuteGitCommandWindowsAsync("rev-parse", "--git-dir");
                if (!verifyResult.Success)
                {
                    _logger.Error("Not a valid Git repository at {RepoPath}: {Error}", _settings.WindowsRepoPath, verifyResult.Output);
                    return (false, $"Not a valid Git repository at {_settings.WindowsRepoPath}");
                }
                
                _logger.Information("Checking current Git branch...");
                var branch = await GetCurrentBranchWindowsAsync();
                if (!branch.Success)
                {
                    _logger.Error("Failed to determine current Git branch: {Error}", branch.Branch);
                    return (false, $"Failed to determine current Git branch: {branch.Branch}");
                }

                if (branch.Branch != _settings.GitBranch)
                {
                    _logger.Warning("Current branch ({CurrentBranch}) is not the expected branch ({ExpectedBranch})", 
                        branch.Branch, _settings.GitBranch);
                    
                    // Checkout the right branch
                    var checkoutResult = await ExecuteGitCommandWindowsAsync("checkout", _settings.GitBranch);
                    if (!checkoutResult.Success)
                    {
                        return (false, $"Failed to checkout branch {_settings.GitBranch}: {checkoutResult.Output}");
                    }
                }                _logger.Information("Testing connection to remote...");
                
                // First, check current remote URL
                _logger.Information("Checking remote URL configuration...");
                var remoteResult = await ExecuteGitCommandWindowsAsync("remote", "get-url", "origin");
                if (!remoteResult.Success) {
                    _logger.Error("Failed to get remote URL: {Error}", remoteResult.Output);
                    return (false, "Failed to get remote URL. Please check repository configuration.");
                }
                
                _logger.Information("Current remote URL: {Url}", remoteResult.Output.Trim());
                
                // Always ensure we're using SSH URL
                if (remoteResult.Output.Contains("https://", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Information("Converting HTTPS remote to SSH...");
                    var setUrlResult = await ExecuteGitCommandWindowsAsync("remote", "set-url", "origin", "git@bitbucket.org:basehead/basehead.git");
                    if (!setUrlResult.Success)
                    {
                        _logger.Error("Failed to set SSH remote URL: {Error}", setUrlResult.Output);
                        return (false, "Failed to set SSH remote URL. Please check the repository configuration.");
                    }
                    _logger.Information("Successfully converted to SSH URL");
                }                // First try a basic SSH connection test with verbose output
                _logger.Information("Testing basic SSH connectivity to Bitbucket using key: {KeyPath}", _settings.WindowsKeyPath);
                var startInfo = new ProcessStartInfo
                {
                    FileName = "ssh",
                    Arguments = $"-vvv -T -i \"{_settings.WindowsKeyPath}\" -o StrictHostKeyChecking=accept-new -o BatchMode=yes -o ConnectTimeout=10 -o ServerAliveInterval=5 -o ServerAliveCountMax=3 -o IdentitiesOnly=yes git@bitbucket.org",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                try
                {
                    using var process = new Process { StartInfo = startInfo };
                    var output = new System.Text.StringBuilder();
                    var error = new System.Text.StringBuilder();

                    process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                    process.ErrorDataReceived += (s, e) => { if (e.Data != null) error.AppendLine(e.Data); };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    await process.WaitForExitAsync();

                    var sshOutput = output.ToString().Trim();
                    var sshError = error.ToString().Trim();
                    _logger.Debug("SSH test output: {Output}", sshOutput);
                    _logger.Debug("SSH test error: {Error}", sshError);

                    if (process.ExitCode != 0 || sshError.Contains("Permission denied", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.Error("SSH connection test failed. Exit code: {Code}, Error: {Error}", process.ExitCode, sshError);
                        return (false, $"SSH connection test failed. Please verify your SSH key and Bitbucket access. Details: {sshError}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error during SSH connectivity test");
                    return (false, $"SSH connection test failed: {ex.Message}");                }

                _logger.Information("Basic SSH connectivity test passed, testing Git remote access...");
                var testResult = await ExecuteGitCommandWindowsAsync("ls-remote", "--exit-code", "origin", "HEAD");
                if (!testResult.Success)
                {
                    string errorMessage = testResult.Output.Contains("Permission denied") ? "Permission denied accessing the repository." :
                                       testResult.Output.Contains("Could not resolve host") ? "Could not resolve the Git host. Check your network connection." :
                                       testResult.Output.Contains("timeout") ? "Connection to the repository timed out." :
                                       testResult.Output;

                    _logger.Error("Could not connect to remote repository: {Error}", errorMessage);
                    return (false, $"Could not connect to remote Git repository: {errorMessage}");
                }

                _logger.Information("Fetching latest changes from remote...");
                var fetchResult = await ExecuteGitCommandWindowsAsync("fetch", "origin", _settings.GitBranch);
                if (!fetchResult.Success)
                {
                    _logger.Error("Git fetch failed: {Error}", fetchResult.Output);
                    return (false, $"Git fetch failed: {fetchResult.Output}");
                }

                _logger.Information("Pulling latest changes from {Branch}...", _settings.GitBranch);
                var pullResult = await ExecuteGitCommandWindowsAsync("pull", "--verbose", "origin", _settings.GitBranch);
                if (!pullResult.Success)
                {
                    // Try to get more specific error information
                    string errorDetail = pullResult.Output.Contains("Permission denied") ? "Permission denied accessing the repository." :
                                      pullResult.Output.Contains("Could not resolve host") ? "Could not resolve the Git host. Check your network connection." :
                                      pullResult.Output.Contains("timeout") ? "The connection timed out while accessing the repository." :
                                      pullResult.Output;

                    _logger.Error("Git pull failed: {Error}", errorDetail);
                    return (false, $"Git pull failed: {errorDetail}");
                }

                // Get the latest commit info for the notification
                var commitInfo = await GetLastCommitWindowsAsync();
                
                return (true, $"Successfully pulled latest changes from {_settings.GitBranch}. Latest commit: {commitInfo}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error pulling latest changes from Git repository on Windows");
                return (false, $"Error pulling latest changes: {ex.Message}");
            }
        }

        /// <summary>
        /// Pulls the latest changes from the Git repository on Mac
        /// </summary>
        public async Task<(bool Success, string Message)> PullLatestChangesMacAsync()
        {
            if (string.IsNullOrEmpty(_settings.MacRepoPath))
            {
                _logger.Warning("Mac Git repository path is not configured");
                return (false, "Mac Git repository path is not configured");
            }            _logger.Information("Pulling latest changes from Git repository on Mac");
            
            try
            {
                // Validate Mac SSH configuration first
                var macSshConfig = await CheckMacSshConfiguration();
                if (!macSshConfig.Valid)
                {
                    _logger.Error("Mac SSH validation failed: {Message}", macSshConfig.Message);
                    return (false, macSshConfig.Message);
                }
                
                using var client = new SshClient(_settings.MacHostname, _settings.MacUsername, new PrivateKeyFile(_settings.MacKeyPath));
                
                _logger.Information("Connecting to Mac via SSH");
                await Task.Run(() => client.Connect());

                if (!client.IsConnected)
                {
                    _logger.Error("Failed to connect to Mac via SSH");
                    return (false, "Failed to connect to Mac via SSH");
                }

                // Check if we're on the right branch
                var checkBranchCommand = $"cd {_settings.MacRepoPath} && git branch --show-current";
                var branchResult = await Task.Run(() => client.RunCommand(checkBranchCommand));
                
                if (branchResult.ExitStatus != 0)
                {
                    _logger.Error("Failed to determine current Git branch on Mac: {Error}", branchResult.Error);
                    return (false, $"Failed to determine current Git branch on Mac: {branchResult.Error}");
                }

                var currentBranch = branchResult.Result.Trim();
                if (currentBranch != _settings.GitBranch)
                {
                    _logger.Warning("Current branch on Mac ({CurrentBranch}) is not the expected branch ({ExpectedBranch})", 
                        currentBranch, _settings.GitBranch);
                    
                    // Checkout the right branch
                    var checkoutCommand = $"cd {_settings.MacRepoPath} && git checkout {_settings.GitBranch}";
                    var checkoutResult = await Task.Run(() => client.RunCommand(checkoutCommand));
                    if (checkoutResult.ExitStatus != 0)
                    {
                        _logger.Error("Failed to checkout branch {Branch} on Mac: {Error}", 
                            _settings.GitBranch, checkoutResult.Error);
                        return (false, $"Failed to checkout branch {_settings.GitBranch} on Mac: {checkoutResult.Error}");
                    }
                }                // Configure Git pull strategy first
                var configCommand = $"cd {_settings.MacRepoPath} && git config pull.rebase false";
                var configResult = await Task.Run(() => client.RunCommand(configCommand));
                if (configResult.ExitStatus != 0)
                {
                    _logger.Error("Failed to configure Git pull strategy: {Error}", configResult.Error);
                    return (false, "Failed to configure Git pull strategy.");
                }
                _logger.Information("Configured Git pull strategy to use merge");

                // Check remote URL
                var remoteCommand = $"cd {_settings.MacRepoPath} && git remote -v";
                var remoteResult = await Task.Run(() => client.RunCommand(remoteCommand));
                _logger.Information("Git remote configuration: {Output}", remoteResult.Result);

                // Ensure we're using SSH URL                
                if (remoteResult.Result.Contains("https://", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Information("Converting HTTPS remote to SSH...");
                    var setUrlCommand = $"cd {_settings.MacRepoPath} && git remote set-url origin git@bitbucket.org:basehead/basehead.git";
                    var setUrlResult = await Task.Run(() => client.RunCommand(setUrlCommand));
                    if (setUrlResult.ExitStatus != 0)
                    {
                        _logger.Error("Failed to set SSH remote URL: {Error}", setUrlResult.Error);
                        return (false, "Failed to set SSH remote URL. Please check the repository configuration.");
                    }
                }

                // Now fetch after ensuring SSH URL
                var fetchCommand = $"cd {_settings.MacRepoPath} && git fetch origin {_settings.GitBranch}";
                var fetchResult = await Task.Run(() => client.RunCommand(fetchCommand));
                
                if (fetchResult.ExitStatus != 0)
                {
                    _logger.Error("Git fetch failed on Mac: {Error}", fetchResult.Error);
                    return (false, $"Git fetch failed on Mac: {fetchResult.Error}");
                }

                // Pull latest changes
                var pullCommand = $"cd {_settings.MacRepoPath} && git pull origin {_settings.GitBranch}";
                var pullResult = await Task.Run(() => client.RunCommand(pullCommand));
                
                if (pullResult.ExitStatus != 0)
                {
                    _logger.Error("Git pull failed on Mac: {Error}", pullResult.Error);
                    return (false, $"Git pull failed on Mac: {pullResult.Error}");
                }

                // Get Git status after pull
                var statusCommand = $"cd {_settings.MacRepoPath} && git status --no-ahead-behind --porcelain=v1";
                var statusResult = await Task.Run(() => client.RunCommand(statusCommand));
                _logger.Debug("Mac Git status: {Status}", statusResult.Result);

                // Get the last commit info
                var commitCommand = $"cd {_settings.MacRepoPath} && git log -1 --pretty=format:'%h - %s (%an)'";
                var commitResult = await Task.Run(() => client.RunCommand(commitCommand));
                
                return (true, $"Successfully pulled latest changes on Mac from {_settings.GitBranch}. Latest commit: {commitResult.Result}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error pulling latest changes from Git repository on Mac: {Error}", ex.Message);
                return (false, $"Error pulling latest changes on Mac: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the current Git branch on Windows
        /// </summary>
        private async Task<(bool Success, string Branch)> GetCurrentBranchWindowsAsync()
        {
            var result = await ExecuteGitCommandWindowsAsync("branch", "--show-current");
            return (result.Success, result.Output.Trim());
        }

        /// <summary>
        /// Gets information about the last commit on Windows
        /// </summary>
        private async Task<string> GetLastCommitWindowsAsync()
        {
            var result = await ExecuteGitCommandWindowsAsync("log", "-1", "--pretty=format:%h - %s (%an)");
            return result.Success ? result.Output.Trim() : "Unknown";
        }

        /// <summary>
        /// Executes a Git command on Windows with retry logic for transient failures
        /// </summary>
        private string GetSystemPath()
        {
            return Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        }        private async Task<(bool Success, string Output)> ExecuteGitCommandWindowsAsync(params string[] args)
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    _logger.Debug("Executing Git command in {WorkingDir} (attempt {Attempt}/{MaxAttempts}): git {Args}", 
                        _settings.WindowsRepoPath, attempt, MaxRetries, string.Join(" ", args));
                    
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "git",
                        WorkingDirectory = _settings.WindowsRepoPath,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,                        Environment = 
                        {
                            // Ensure Git can be found
                            ["PATH"] = GetSystemPath(),                            // Configure minimal SSH settings that we know work
                            ["GIT_SSH_COMMAND"] = "ssh -v",
                            ["SSH_AUTH_SOCK"] = "",  // Disable ssh-agent
                            ["GIT_TERMINAL_PROMPT"] = "0",
                            ["GIT_ASKPASS"] = "echo",// Configure Git behavior for network operations
                            ["GIT_HTTP_LOW_SPEED_LIMIT"] = "500",   // 500 bytes/s minimum transfer speed
                            ["GIT_HTTP_LOW_SPEED_TIME"] = "30",     // If speed < limit for 30 sec, abort
                            ["GIT_TRACE"] = "1",                    // Enable Git tracing for debugging
                            ["GIT_CURL_VERBOSE"] = "1",            // Enable verbose output for HTTPS operations
                            // Configure Git paths
                            ["GIT_CONFIG_NOSYSTEM"] = "1",          // Don't read system config
                            ["GIT_CONFIG_GLOBAL"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gitconfig"),
                            ["GIT_EXEC_PATH"] = @"C:\Program Files\Git\mingw64\libexec\git-core",
                            ["HOME"] = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            // Configure credential behavior
                            ["GCM_INTERACTIVE"] = "never",
                            ["GIT_TERMINAL_PROMPT"] = "0"
                        }
                    };

                    foreach (var arg in args)
                    {
                        startInfo.ArgumentList.Add(arg);
                    }

                    using var process = new Process { StartInfo = startInfo };
                    var outputBuilder = new System.Text.StringBuilder();
                    var errorBuilder = new System.Text.StringBuilder();
                    bool processStarted = false;

                    try
                    {
                        process.OutputDataReceived += (sender, e) => 
                        {
                            if (e.Data != null)
                            {
                                lock (outputBuilder)
                                {
                                    outputBuilder.AppendLine(e.Data);
                                    _logger.Debug("Git output: {Output}", e.Data);
                                }
                            }
                        };

                        process.ErrorDataReceived += (sender, e) => 
                        {
                            if (e.Data != null)
                            {
                                lock (errorBuilder)
                                {
                                    errorBuilder.AppendLine(e.Data);
                                    _logger.Debug("Git error: {Error}", e.Data);
                                }
                            }
                        };

                        process.Start();
                        processStarted = true;
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        
                        if (!process.WaitForExit(CommandTimeoutMs))
                        {
                            process.Kill(entireProcessTree: true);
                            throw new TimeoutException($"Git command timed out after {CommandTimeoutMs/1000} seconds");
                        }

                        var output = outputBuilder.ToString().TrimEnd();
                        var error = errorBuilder.ToString().TrimEnd();
                        var exitCode = process.ExitCode;

                        if (exitCode != 0)
                        {
                            var errorMessage = $"Git command exited with code {exitCode}. Error: {error}".TrimEnd();
                            _logger.Warning("Git command failed (attempt {Attempt}/{MaxAttempts}): {Error}", 
                                attempt, MaxRetries, errorMessage);
                            return (false, errorMessage);
                        }

                        _logger.Debug("Git command completed successfully");
                        return (true, output);
                    }
                    finally
                    {
                        if (processStarted && !process.HasExited)
                        {
                            try 
                            { 
                                process.Kill(entireProcessTree: true);
                            }
                            catch (Exception ex)
                            {
                                _logger.Warning(ex, "Failed to kill Git process");
                            }
                        }
                    }
                }
                catch (TimeoutException ex)
                {
                    if (attempt < MaxRetries)
                    {
                        _logger.Warning("Git command timed out (attempt {Attempt}/{MaxAttempts}): {Error}", 
                            attempt, MaxRetries, ex.Message);
                        await Task.Delay(RetryDelayMs * attempt);
                        continue;
                    }
                    
                    _logger.Error(ex, "Git command timed out after {Attempt} attempts", attempt);
                    return (false, $"Git command timed out after {CommandTimeoutMs/1000} seconds");
                }                catch (Exception ex)
                {
                    // Detect common Git errors that should be retried
                    bool shouldRetry = ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) || 
                                     ex.Message.Contains("network", StringComparison.OrdinalIgnoreCase) ||
                                     ex.Message.Contains("Could not resolve host", StringComparison.OrdinalIgnoreCase) ||
                                     ex.Message.Contains("Connection refused", StringComparison.OrdinalIgnoreCase) ||
                                     ex.Message.Contains("Connection reset", StringComparison.OrdinalIgnoreCase) ||
                                     ex.Message.Contains("TLS handshake failed", StringComparison.OrdinalIgnoreCase) ||
                                     ex.Message.Contains("temporarily unavailable", StringComparison.OrdinalIgnoreCase);

                    if (shouldRetry && attempt < MaxRetries)
                    {
                        var delayMs = RetryDelayMs * (int)Math.Pow(2, attempt - 1); // Exponential backoff
                        _logger.Warning("Network error executing Git command (attempt {Attempt}/{MaxAttempts}): {Error}. Retrying in {Delay}ms", 
                            attempt, MaxRetries, ex.Message, delayMs);
                        await Task.Delay(delayMs);
                        continue;
                    }
                    
                    _logger.Error(ex, "Error executing Git command (attempt {Attempt}/{MaxAttempts})", 
                        attempt, MaxRetries);
                    return (false, $"Git command failed: {ex.Message}");
                }
            }

            _logger.Error("Git command failed after all retry attempts");
            return (false, "Git command failed after all retry attempts. Please check the logs for details.");
        }

        private async Task<(bool Valid, string Message)> CheckMacSshConfiguration()
        {            try
            {
                // First check if hostname resolves or is an IP
                string hostname = _settings.MacHostname;

                // If it's just a machine name, try appending .local for Bonjour/mDNS resolution
                if (!hostname.Contains(".") && !IPAddress.TryParse(hostname, out _))
                {
                    hostname = $"{hostname}.local";
                    _logger.Information("Converting hostname to mDNS format: {Hostname}", hostname);
                    _settings.MacHostname = hostname; // Update the setting for future use
                }

                _logger.Information("Resolving Mac hostname: {Hostname}", hostname);
                try
                {
                    var hostEntry = await Dns.GetHostEntryAsync(hostname);
                    var ipAddress = hostEntry.AddressList.FirstOrDefault()?.ToString() ?? "none";
                    _logger.Information("Successfully resolved {Hostname} to {IpAddress}", hostname, ipAddress);
                }
                catch (SocketException ex)
                {
                    _logger.Error(ex, "Failed to resolve Mac hostname: {Hostname}. Error: {Message}", hostname, ex.Message);
                    return (false, $"Cannot resolve Mac hostname '{hostname}'. Please check:\n1. The Mac is running and on the network\n2. Try using the Mac's IP address instead\n3. Verify the hostname in System Settings > Sharing");
                }                // Test SSH connection and Bitbucket key
                _logger.Information("Testing SSH connection to Mac and Bitbucket...");
                using var client = new SshClient(_settings.MacHostname, _settings.MacUsername, new PrivateKeyFile(_settings.MacKeyPath));
                
                try 
                {
                    await Task.Run(() => client.Connect());
                }
                catch (SocketException ex)
                {
                    _logger.Error(ex, "Failed to connect to Mac via SSH: {Error}", ex.Message);
                    return (false, $"Cannot connect to Mac via SSH. Please check that:\n1. The Mac is running and accessible\n2. The hostname/IP is correct\n3. SSH is enabled on the Mac\nError: {ex.Message}");
                }

                var testResult = await Task.Run(() => client.RunCommand("ssh -T -o StrictHostKeyChecking=accept-new git@bitbucket.org"));
                
                // If we get "permission denied", we need to generate a new key
                if (testResult.ExitStatus != 0 || 
                    testResult.Error.Contains("Permission denied", StringComparison.OrdinalIgnoreCase) || 
                    testResult.Error.Contains("No such file", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Information("SSH key for Bitbucket not configured on Mac, generating new key...");
                    var keyResult = await _sshService.GenerateMacSshKeyAsync();
                    if (!keyResult.Success)
                    {
                        return (false, keyResult.Message);
                    }

                    // Return the public key so it can be added to Bitbucket
                    return (false, $"Generated new SSH key for Mac. Please add this public key to your Bitbucket account:\n\n{keyResult.PublicKey}");
                }

                return (true, "Mac SSH configuration verified.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking Mac SSH configuration");
                return (false, $"Error checking Mac SSH configuration: {ex.Message}");
            }
        }        /// <summary>
        /// Validates that the SSH key for Git is properly configured
        /// </summary>
        private async Task<(bool Valid, string Message)> ValidateGitSshConfiguration(bool includeMac = false)
        {            
            // First validate Windows SSH config
            var windowsResult = await ValidateWindowsSshConfiguration();
            if (!windowsResult.Valid)
            {
                return windowsResult;
            }

            // Then validate Mac SSH config only if explicitly requested and Mac settings are configured
            if (includeMac && 
                !string.IsNullOrEmpty(_settings.MacHostname) && 
                !string.IsNullOrEmpty(_settings.MacUsername) && 
                !string.IsNullOrEmpty(_settings.MacKeyPath))
            {
                var macResult = await CheckMacSshConfiguration();
                if (!macResult.Valid)
                {
                    return macResult;
                }
                _logger.Information("SSH configuration verified for both Windows and Mac");
                return (true, "SSH configuration verified for both Windows and Mac");
            }

            _logger.Information("SSH configuration verified for Windows");
            return (true, "SSH configuration verified for Windows");
        }

        private async Task<(bool Valid, string Message)> ValidateWindowsSshConfiguration()
        {
            // Determine SSH key path - use service-specific path if configured and running as service
            string sshKeyPath;
            string sshDir;
            
            // Check if we're running as a service and have a service-specific SSH key path
            if (!string.IsNullOrEmpty(_settings.ServiceUserSshKeyPath) && 
                Environment.UserInteractive == false) // Running as service
            {
                sshKeyPath = _settings.ServiceUserSshKeyPath;
                sshDir = Path.GetDirectoryName(sshKeyPath) ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SlackCI", ".ssh");
            }
            else if (!string.IsNullOrEmpty(_settings.WindowsKeyPath))
            {
                sshKeyPath = _settings.WindowsKeyPath;
                sshDir = Path.GetDirectoryName(sshKeyPath) ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
            }
            else
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                sshDir = Path.Combine(userProfile, ".ssh");
                sshKeyPath = Path.Combine(sshDir, "id_rsa");
            }

            var knownHostsPath = Path.Combine(sshDir, "known_hosts");

            _logger.Information("Using SSH key path: {KeyPath}", sshKeyPath);
            _logger.Information("Using SSH directory: {SshDir}", sshDir);

            // Check if .ssh directory exists
            if (!Directory.Exists(sshDir))
            {
                _logger.Warning("SSH directory not found at {SshDir}, attempting to create", sshDir);
                try
                {
                    Directory.CreateDirectory(sshDir);
                    _logger.Information("Created SSH directory at {SshDir}", sshDir);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to create SSH directory");
                    return (false, "SSH directory not found and could not be created. Please ensure Git is configured for SSH authentication.");
                }
            }

            // Check if SSH key exists
            if (!File.Exists(sshKeyPath))
            {
                _logger.Warning("SSH key not found at {KeyPath}", sshKeyPath);
                
                // If we're using the user's SSH key but it doesn't exist, try to copy it to service location
                if (!string.IsNullOrEmpty(_settings.ServiceUserSshKeyPath) && 
                    Environment.UserInteractive == false &&
                    !string.IsNullOrEmpty(_settings.WindowsKeyPath) &&
                    File.Exists(_settings.WindowsKeyPath))
                {
                    try
                    {
                        _logger.Information("Copying user SSH key to service location");
                        File.Copy(_settings.WindowsKeyPath, sshKeyPath, true);
                        
                        // Also copy the public key if it exists
                        var userPublicKey = _settings.WindowsKeyPath + ".pub";
                        var servicePublicKey = sshKeyPath + ".pub";
                        if (File.Exists(userPublicKey))
                        {
                            File.Copy(userPublicKey, servicePublicKey, true);
                        }
                        
                        _logger.Information("SSH key copied successfully to service location");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to copy SSH key to service location");
                        return (false, "SSH key not found and could not be copied. Please ensure you have generated an SSH key and added it to your Bitbucket account.");
                    }
                }
                else
                {
                    return (false, "SSH key not found. Please ensure you have generated an SSH key and added it to your Bitbucket account.");
                }
            }

            // Check if the key is readable
            try
            {
                using (var fs = File.OpenRead(sshKeyPath))
                {
                    // Just checking if we can read the file
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Could not read SSH key file");
                return (false, "Could not read SSH key file. Please check file permissions.");
            }

            // Ensure known_hosts exists and contains Bitbucket's host key
            if (!File.Exists(knownHostsPath))
            {
                _logger.Warning("known_hosts file not found at {Path}", knownHostsPath);
                _logger.Information("Creating known_hosts file and adding bitbucket.org host key");
                try
                {
                    Directory.CreateDirectory(sshDir);
                    File.WriteAllText(knownHostsPath, "bitbucket.org ssh-rsa AAAAB3NzaC1yc2EAAAABIwAAAQEAubiN81eDcafrgMeLzaFPsw2kNvEcqTKl/VqLat/MaB33pZy0y3rJZtnqwR2qOOvbwKZYKiEO1O6VqNEBxKvJJelCq0dTXWT5pbO2gDXC6h6QDXCaHo6pOHGPUy+YBaGQRGuSusMEASYiWunYN0vCAI8QaXnWMXNMdFP3jHAJH0eDsoiGnLPBlBp4TN");
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Could not create known_hosts file");
                    return (false, "Could not create known_hosts file.");
                }
            }

            // Test basic SSH connectivity
            _logger.Information("Testing basic SSH connectivity to Bitbucket...");
            var sshCommand = new ProcessStartInfo
            {
                FileName = "ssh",
                Arguments = $"-T -i \"{sshKeyPath}\" -o StrictHostKeyChecking=accept-new -o BatchMode=yes -o IdentitiesOnly=yes git@bitbucket.org",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var process = new Process { StartInfo = sshCommand };
                var output = new System.Text.StringBuilder();
                var error = new System.Text.StringBuilder();

                process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) error.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                var sshOutput = output.ToString().Trim();
                var sshError = error.ToString().Trim();
                _logger.Debug("SSH test output: {Output}", sshOutput);
                _logger.Debug("SSH test error: {Error}", sshError);

                if (process.ExitCode != 0 || sshError.Contains("Permission denied", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Error("SSH connection test failed. Exit code: {Code}, Error: {Error}", process.ExitCode, sshError);
                    return (false, $"SSH connection test failed. Please verify your SSH key and Bitbucket access. Details: {sshError}");
                }

                return (true, "Windows SSH configuration verified successfully.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during SSH connectivity test");
                return (false, $"Error testing SSH connectivity: {ex.Message}");
            }
        }

        private async Task<(bool Success, string Message)> ConfigureGitCredentials()
        {
            try
            {
                // First ensure we're actually in a Git repository
                var gitDirCheck = await ExecuteGitCommandWindowsAsync("rev-parse", "--git-dir");
                if (!gitDirCheck.Success)
                {
                    _logger.Error("Not a valid Git repository at {Path}", _settings.WindowsRepoPath);
                    return (false, $"Not a valid Git repository at {_settings.WindowsRepoPath}");
                }

                // Check and configure remote
                var remoteCheck = await ExecuteGitCommandWindowsAsync("remote", "get-url", "origin");
                if (!remoteCheck.Success)
                {
                    _logger.Error("Failed to get remote URL: {Error}", remoteCheck.Output);
                    return (false, "Remote 'origin' is not configured properly");
                }
                
                _logger.Information("Current remote URL: {Url}", remoteCheck.Output.Trim());
                
                // Always ensure we're using SSH URL
                if (remoteCheck.Output.Contains("https://", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Information("Converting HTTPS remote to SSH...");
                    var setUrlResult = await ExecuteGitCommandWindowsAsync("remote", "set-url", "origin", "git@bitbucket.org:basehead/basehead.git");
                    if (!setUrlResult.Success)
                    {
                        _logger.Error("Failed to set SSH remote URL: {Error}", setUrlResult.Output);
                        return (false, "Failed to set SSH remote URL. Please check the repository configuration.");
                    }
                    _logger.Information("Successfully converted to SSH URL");
                }

                return (true, "Git configuration successful");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error configuring Git credentials");
                return (false, $"Error configuring Git credentials: {ex.Message}");
            }
        }
    }
}
