using System;

namespace SlackCIApp.Config
{
    public class SlackCISettings
    {
        // Logging
        public string LogFilePath { get; set; } = "logs/slackci.log";
        
        // Slack API Settings
        public string SlackBotToken { get; set; } = string.Empty;
        public string SlackSigningSecret { get; set; } = string.Empty;
        public string SlackAppToken { get; set; } = string.Empty;
        public string SlackWebhookUrl { get; set; } = string.Empty;
        public string ChannelId { get; set; } = string.Empty;       
        public string ChannelName { get; set; } = "#builds";
        
        // Git repository settings
        public string WindowsRepoPath { get; set; } = @"C:\Users\carbo\Desktop\GitHub\basehead";
        public string MacRepoPath { get; set; } = "/Users/steve/Desktop/GitHub/basehead";
        public string LLSRepoPath { get; set; } = @"C:\Users\carbo\Desktop\GitHub\basehead\basehead.LicenseServer";
        public string GitBranch { get; set; } = "master";  // Default to master/main branch
        public bool EnableGitPullForService { get; set; } = true;  // Enable Git pull for basehead builds
        public string ServiceUserSshKeyPath { get; set; } = @"C:\ProgramData\SlackCI\.ssh\id_rsa";  // Service-specific SSH key path
        
        // Build Script Paths
        public string WindowsBuildScriptPath { get; set; } = @"C:\Users\carbo\Desktop\GitHub\basehead\build.bat";
        public string MacBuildScriptPath { get; set; } = @"/Users/steve/Desktop/GitHub/basehead/build.sh";
        public string LLSBuildScriptPath { get; set; } = @"C:\Users\carbo\Desktop\GitHub\basehead\basehead.LicenseServer\build.bat";
        
        // Build Commands       
        public string TriggerBothCommand { get; set; } = "build bh";
        public string TriggerWindowsCommand { get; set; } = "build bh-pc";
        public string TriggerMacCommand { get; set; } = "build bh-mac";
        public string TriggerLLSCommand { get; set; } = "build lls";
        
        // Remote Mac Settings (if needed for SSH)
        public string MacHostname { get; set; } = string.Empty;
        public string MacUsername { get; set; } = string.Empty;        
        public string MacKeyPath { get; set; } = string.Empty;
        public string WindowsKeyPath { get; set; } = string.Empty;  // Path to Windows SSH key for Git
        
        // Installer Paths (for uploading back to Slack)
        public string WindowsInstallerPath { get; set; } = string.Empty;
        public string MacInstallerPath { get; set; } = string.Empty;
        public string MacInstallerLocalPath { get; set; } = string.Empty;
        public string LLSInstallerPath { get; set; } = string.Empty;
          // Advanced Installer
        public string AdvancedInstallerProjectPath { get; set; } = @"C:\Users\carbo\Desktop\GitHub\basehead\Installer\_buildInstaller2025.aip";
        public string AdvancedInstallerExePath { get; set; } = @"C:\Program Files (x86)\Caphyon\Advanced Installer 21.8.2\bin\x86\AdvancedInstaller.com";
        public string LLSAdvancedInstallerProjectPath { get; set; } = @"C:\Users\carbo\Desktop\GitHub\basehead\basehead.LicenseServer\Installer\PC\LLS_Installer.aip";
        public string BuildVersion { get; set; } = "1.0.0";
    }
}
