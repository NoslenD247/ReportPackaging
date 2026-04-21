using System.Text;
using System.Text.Json;
using ReportPackaging.Data.Models;

namespace ReportPackaging.Services
{
    public class AzureAuthService
    {
        private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(30) };
        private const string BaseUrl = "https://tickets-api-dev-hzd9fegchvedadgx.canadacentral-01.azurewebsites.net/api/";

        public async Task<(AzureUser? User, string? Token, string? Role, string? Error)> LoginAsync(string identifier, string password)
        {
            try
            {
                var loginRequest = new { identifier, password };
                var json = JsonSerializer.Serialize(loginRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _client.PostAsync($"{BaseUrl}Auth/login", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"[AzureAuth] Status: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"[AzureAuth] Body: {responseBody}");

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<AzureLoginResponse>(responseBody,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return (result?.User, result?.Token, result?.Role, null);
                }
                else
                {
                    var errorMsg = responseBody.Trim('"');
                    return (null, null, null, string.IsNullOrEmpty(errorMsg) ? "Credenciales inválidas" : errorMsg);
                }
            }
            catch (Exception ex)
            {
                return (null, null, null, $"Error de conexión: {ex.Message}");
            }
        }

        private class AzureLoginResponse
        {
            public AzureUser? User { get; set; }
            public string? Token { get; set; }
            public string? Role { get; set; }
        }
    }
}
