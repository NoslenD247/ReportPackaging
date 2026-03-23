
using Microsoft.Extensions.Logging;
using ReportPackaging.Data.Sew.IService;
using ReportPackaging.Data.Sew.Service;
using ReportPackaging.Services;

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
                client.BaseAddress = new Uri(AppSettings.Current.ApiSettings.TestUrl);
            });

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();

            builder.Services.AddScoped<ITempPackingService, TempPackingScanService>();
            builder.Services.AddScoped<ISew_FabricInboundScanService, Sew_FabricInboundScanService>();
            builder.Services.AddScoped<IFactoryService, FactoryService>();

            //builder.Configuration.AddJsonFile("appsettings.json");
#endif

            return builder.Build();
        }
    }
}
