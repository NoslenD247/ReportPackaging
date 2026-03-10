
using Microsoft.Extensions.Logging;
using ReportPackaging.Data.Sew.IService;
using ReportPackaging.Data.Sew.Service;
using ReportPackaging.Services;
using Syncfusion.Blazor;

namespace ReportPackaging
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });
            builder.Services.AddMauiBlazorWebView();

            AppSettingsLoader.Load();

            builder.Services.AddHttpClient("ApiClient", client =>
            {
                client.BaseAddress = new Uri(AppSettings.Current.ApiSettings.TestUrl); // ← aquí
            });

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
            builder.Services.AddSyncfusionBlazor();

            builder.Services.AddScoped<ITempPackingService, TempPackingService>();

            //builder.Configuration.AddJsonFile("appsettings.json");
#endif

            return builder.Build();
        }
    }
}
