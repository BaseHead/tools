using System;
using System.Diagnostics;

namespace AISoundGenerator.Services;

public class BrowserService : IBrowserService
{
    public void OpenUrl(string url)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(processInfo);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open URL: {ex.Message}");
            throw;
        }
    }
}