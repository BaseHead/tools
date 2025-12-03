using System.Threading.Tasks;

namespace AISoundGenerator.Services;

public interface IFileService
{
    string GetSettingsFilePath();
    string GetApiKeyFilePath();
    string GetOutputFolder();
    void SaveTextToFile(string filePath, string content);
    string? ReadTextFromFile(string filePath);
    bool FileExists(string filePath);
    Task WriteAllBytesAsync(string path, byte[] bytes);
    void OpenFolder(string path);
    string GetSafeFilename(string filename, int maxLength = 40);
    void EnsureDirectoryExists(string path);
}