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
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(AppSettings.Current.SyncfusionLicense);

            // Registrar el handler primero
            builder.Services.AddTransient<ClerkAuthHandler>();

            // ApiClient con el handler
            builder.Services.AddHttpClient("ApiClient", client =>
            {
                client.BaseAddress = new Uri(AppSettings.Current.ApiSettings.TestUrl);
            })
            .AddHttpMessageHandler<ClerkAuthHandler>();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            builder.Services.AddSyncfusionBlazor();

            builder.Services.AddScoped<ITempPackingService, TempPackingScanService>();
            builder.Services.AddScoped<ISew_FabricInboundScanService, Sew_FabricInboundScanService>();
            builder.Services.AddScoped<ISew_CuttingStatusService, Sew_CuttingStatusService>();
            builder.Services.AddScoped<IFactoryService, FactoryService>();
            builder.Services.AddScoped<ISewGetDataFromUserService, SewGetDataFromUserService>();
            builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
            builder.Services.AddScoped<AzureAuthService>();

            return builder.Build();
        }
    }
}