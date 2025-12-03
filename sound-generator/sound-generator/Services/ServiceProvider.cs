using System;
using Microsoft.Extensions.DependencyInjection;
using AISoundGenerator.ViewModels;

namespace AISoundGenerator.Services;

public static class ServiceProvider
{
    // Hardcoded Wasabi settings - these match the ones from the original code
    private const string WASABI_ACCESS_KEY = "NN9UOSFAE1TJVS3UDYNW";
    private const string WASABI_SECRET_KEY = "rfCIc7csj37LdIEM8VjeA6FUt92ItUEbze3ey0tb";
    private const string WASABI_REGION = "us-central-1";
    private const string WASABI_BUCKET = "ai-uploads";

    private static readonly IServiceProvider _instance;

    static ServiceProvider()
    {
        var services = new ServiceCollection();

        // Register services
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IBrowserService, BrowserService>();
        services.AddSingleton<IApiService, ApiService>();
        
        // Register WasabiStorageService with constructor arguments
        services.AddSingleton<WasabiStorageService>(provider => 
            new WasabiStorageService(
                WASABI_ACCESS_KEY,
                WASABI_SECRET_KEY, 
                WASABI_REGION, 
                WASABI_BUCKET
            )
        );

        // Register ViewModels
        services.AddTransient<MainWindowViewModel>();

        _instance = services.BuildServiceProvider();
    }

    public static T GetService<T>() where T : class
    {
        return _instance.GetRequiredService<T>();
    }
}