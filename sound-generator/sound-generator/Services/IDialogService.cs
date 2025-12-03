using System.Threading.Tasks;

namespace AISoundGenerator.Services;

public interface IDialogService
{
    Task<bool> ShowYesNoDialogAsync(string title, string message);
    Task ShowErrorDialogAsync(string title, string message);
    Task ShowInfoDialogAsync(string title, string message);
    Task<bool> ShowApiPreviewDialogAsync(string endpointInfo, string jsonForDisplay);
}