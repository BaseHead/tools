using System.Threading.Tasks;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace AISoundGenerator.Services;

public class DialogService : IDialogService
{
    public async Task<bool> ShowYesNoDialogAsync(string title, string message)
    {
        var messageBox = MessageBoxManager.GetMessageBoxStandard(
            title,
            message,
            ButtonEnum.YesNo);
        
        var result = await messageBox.ShowAsync();
        return result == ButtonResult.Yes;
    }

    public async Task ShowErrorDialogAsync(string title, string message)
    {
        var messageBox = MessageBoxManager.GetMessageBoxStandard(
            title,
            message,
            ButtonEnum.Ok);
        
        await messageBox.ShowAsync();
    }

    public async Task ShowInfoDialogAsync(string title, string message)
    {
        var messageBox = MessageBoxManager.GetMessageBoxStandard(
            title,
            message,
            ButtonEnum.Ok);
        
        await messageBox.ShowAsync();
    }
    
    public async Task<bool> ShowApiPreviewDialogAsync(string endpointInfo, string jsonForDisplay)
    {
        var message = $"API Call Preview:\n\n{endpointInfo}\n\nRequest Body:\n{jsonForDisplay}\n\nProceed with generation?";
        
        var previewBox = MessageBoxManager.GetMessageBoxStandard(
            "API Request Preview",
            message,
            ButtonEnum.YesNo
        );
        
        var result = await previewBox.ShowAsync();
        return result == ButtonResult.Yes;
    }
}