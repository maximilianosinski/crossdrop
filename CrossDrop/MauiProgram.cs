using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Storage;
using Microsoft.Extensions.Logging;

namespace CrossDrop;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().UseMauiCommunityToolkit();
        builder.Services.AddSingleton(FileSaver.Default);
#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}