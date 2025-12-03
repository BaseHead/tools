using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;

namespace AISoundGenerator.Services;

public class FileService : IFileService
{
    private const string APP_NAME = "AISoundGenerator";
    private const string API_KEY_FILE = "elevenlabs_api_key.txt";
    private const string SETTINGS_FILE = "app_settings.json";
    
    public string GetSettingsFilePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, APP_NAME);
        EnsureDirectoryExists(appFolder);
        return Path.Combine(appFolder, SETTINGS_FILE);
    }

    public string GetApiKeyFilePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, APP_NAME);
        EnsureDirectoryExists(appFolder);
        return Path.Combine(appFolder, API_KEY_FILE);
    }

    public string GetOutputFolder()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var outputFolder = Path.Combine(documentsPath, APP_NAME, "Generator");
        EnsureDirectoryExists(outputFolder);
        return outputFolder;
    }

    public void SaveTextToFile(string filePath, string content)
    {
        try
        {
            File.WriteAllText(filePath, content);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving text to file: {ex.Message}");
            throw;
        }
    }

    public string? ReadTextFromFile(string filePath)
    {
        try
        {
            if (FileExists(filePath))
            {
                return File.ReadAllText(filePath).Trim();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error reading file: {ex.Message}");
        }
        
        return null;
    }

    public bool FileExists(string filePath)
    {
        return File.Exists(filePath);
    }

    public async Task WriteAllBytesAsync(string path, byte[] bytes)
    {
        await File.WriteAllBytesAsync(path, bytes);
    }

    public void OpenFolder(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true
                };
                process.Start();
            }
            else if (OperatingSystem.IsMacOS())
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true
                };
                process.Start();
            }
            else if (OperatingSystem.IsLinux())
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true
                };
                process.Start();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open folder: {ex.Message}");
            throw;
        }
    }

    public string GetSafeFilename(string filename, int maxLength = 40)
    {
        // Remove invalid filename characters
        string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        string safe = string.Join("_", filename.Split(invalidChars.ToCharArray()));
        
        // Replace spaces and multiple underscores with single underscore
        safe = safe.Replace(' ', '_').Replace("__", "_");
        
        // Limit the length to prevent extremely long filenames
        if (safe.Length > maxLength)
        {
            safe = safe.Substring(0, maxLength);
        }
        
        return safe;
    }

    public void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
}