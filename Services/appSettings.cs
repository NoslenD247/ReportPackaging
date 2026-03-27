using System.Text.Json;
using static ReportPackaging.Services.AppSettingsLoader;

namespace ReportPackaging.Services
{
    public class AppSettings
    {
        public AzureComputerVisionSettings AzureComputerVision { get; set; } = new AzureComputerVisionSettings();
        //Para poder leer la linea de conexion con azure 
        public Dictionary<string, string> ConnectionStrings { get; set;  }= new Dictionary<string, string>();


        //Haora para poder conectar el mistral 
        public string MistralApiKey { get; set; }
        public ApiSettingsConfig ApiSettings { get; set; } = new ApiSettingsConfig();
        public string SyncfusionLicense { get; set; } = string.Empty;
        public static AppSettings Current { get; internal set; } = new AppSettings();
    }

    public class AzureComputerVisionSettings
    {
        public string Endpoint { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
    }

    public static class AppSettingsLoader
    {
        public static string LastLoadSource { get; private set; } = string.Empty;
        public static string LastEndpoint { get; private set; } = string.Empty;
        public static int LastKeyLength { get; private set; }
        public static string LocalPath { get; private set; } = string.Empty;
        public static bool LocalExists { get; private set; }

        public static void Load()
        {
            try
            {
                // 1) Prefer packaged appsettings.json (developer/local secret file)
                // 2) Fallback to local AppDataDirectory/appsettings.json
                // 3) If missing, seed AppDataDirectory/appsettings.json from packaged appsettings.example.json
                LastLoadSource = string.Empty;
                LastEndpoint = string.Empty;
                LastKeyLength = 0;
                LocalPath = Path.Combine(FileSystem.AppDataDirectory, "appsettings.json");
                LocalExists = File.Exists(LocalPath);

                // Prefer local appsettings.json first (easy to override per device/emulator)
                var json = ReadLocalAppSettingsJson();
                if (!string.IsNullOrWhiteSpace(json))
                {
                    LastLoadSource = "appdata:appsettings.json";
                }
                else
                {
                    json = ReadFromAppPackage("appsettings.json");
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        LastLoadSource = "package:appsettings.json";
                    }
                }

#if DEBUG
                // In DEBUG, if a packaged appsettings.json exists and contains real values,
                // keep the emulator/device local file in sync so rebuilds take effect.
                var packagedJson = ReadFromAppPackage("appsettings.json");
                if (!string.IsNullOrWhiteSpace(packagedJson))
                {
                    var packagedSettings = SafeDeserialize(packagedJson);
                    if (packagedSettings != null && LooksConfigured(packagedSettings))
                    {
                        TryWriteLocalAppSettingsJson(packagedJson, overwrite: true);
                        json = packagedJson;
                        LastLoadSource = "package:appsettings.json (synced to appdata)";
                        LocalExists = File.Exists(LocalPath);
                    }
                }
#endif

                if (string.IsNullOrWhiteSpace(json))
                {
                    var exampleJson = ReadFromAppPackage("appsettings.example.json");
                    if (!string.IsNullOrWhiteSpace(exampleJson))
                    {
                        TryWriteLocalAppSettingsJson(exampleJson, overwrite: false);
                        json = exampleJson;
                        LastLoadSource = "package:appsettings.example.json";
                        LocalExists = File.Exists(LocalPath);
                    }
                }

                if (string.IsNullOrWhiteSpace(json))
                {
                    AppSettings.Current = new AppSettings();
                    LastLoadSource = string.IsNullOrWhiteSpace(LastLoadSource) ? "none" : LastLoadSource;
                    System.Diagnostics.Debug.WriteLine("[AppSettingsLoader] No settings JSON found.");
                    return;
                }

                var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                AppSettings.Current = settings ?? new AppSettings();

                LastEndpoint = AppSettings.Current.AzureComputerVision.Endpoint ?? string.Empty;
                LastKeyLength = AppSettings.Current.AzureComputerVision.Key?.Length ?? 0;

                System.Diagnostics.Debug.WriteLine($"[AppSettingsLoader] Loaded from: {LastLoadSource}");
                System.Diagnostics.Debug.WriteLine($"[AppSettingsLoader] AzureComputerVision.Endpoint: '{LastEndpoint}'");
                System.Diagnostics.Debug.WriteLine($"[AppSettingsLoader] AzureComputerVision.Key length: {LastKeyLength}");
                System.Diagnostics.Debug.WriteLine($"[AppSettingsLoader] Local path: {LocalPath}");
                System.Diagnostics.Debug.WriteLine($"[AppSettingsLoader] Local exists: {LocalExists}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading appsettings.json: {ex.Message}");
                AppSettings.Current = new AppSettings();
                LastLoadSource = "error";
                LastEndpoint = string.Empty;
                LastKeyLength = 0;
                LocalPath = Path.Combine(FileSystem.AppDataDirectory, "appsettings.json");
                LocalExists = File.Exists(LocalPath);
            }
        }

        private static string ReadFromAppPackage(string filename)
        {
            try
            {
                using var stream = FileSystem.OpenAppPackageFileAsync(filename).Result;
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ReadLocalAppSettingsJson()
        {
            try
            {
                return File.Exists(LocalPath) ? File.ReadAllText(LocalPath) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void TryWriteLocalAppSettingsJson(string json, bool overwrite)
        {
            try
            {
                if (overwrite || !File.Exists(LocalPath))
                {
                    File.WriteAllText(LocalPath, json);
                }
            }
            catch
            {
                // ignore
            }
        }

        private static bool LooksConfigured(AppSettings settings)
        {
            var endpoint = settings.AzureComputerVision?.Endpoint ?? string.Empty;
            var key = settings.AzureComputerVision?.Key ?? string.Empty;

            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
                return false;

            if (endpoint.Contains("your-resource-name", StringComparison.OrdinalIgnoreCase))
                return false;

            if (key.Contains("your-api-key", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private static AppSettings? SafeDeserialize(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return null;
            }
        }

        public class ApiSettingsConfig
        {
            public string BaseUrl { get; set; } = string.Empty;
            public string TestUrl { get; set; } = string.Empty;
            public string ProdUrl { get; set; } = string.Empty; 
        }
    }
}
