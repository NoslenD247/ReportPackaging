using ReportPackaging.Data.Sew.IService;
using System.Net.Http.Json;

namespace ReportPackaging.Data.Sew.Service
{
    public class BlobStorageService : IBlobStorageService
    {
        private readonly HttpClient _httpClient;

        public BlobStorageService(IHttpClientFactory factory)
        {
            _httpClient = factory.CreateClient("ApiClient");
        }

        public async Task<string> UploadFileToBlobAsync(string fileName, string contentType, Stream fileStream)
        {
            var safeContentType = Uri.EscapeDataString(contentType);
            var safeFileName = Uri.EscapeDataString(fileName);
            try
            {
                using var multipartContent = new MultipartFormDataContent();
                using var orderContent = new StreamContent(fileStream);
                orderContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                multipartContent.Add(orderContent, "file", fileName);

                var response = await _httpClient.PostAsync(
                    $"BlobStorage/UploadFileToBlobAsync/{safeFileName}/{safeContentType}",
                    multipartContent);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync() ?? string.Empty;
                }

                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error subiendo archivo: {response.StatusCode} - {error}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en UploadFileToBlobAsync: {ex.Message}");
            }
            return string.Empty;
        }

        public async Task<bool> DeleteFileToBlobAsync(string strFileName)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"BlobStorage/DeleteFileToBlobAsync/{strFileName}");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<bool>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en DeleteFileToBlobAsync: {ex.Message}");
            }
            return false;
        }
    }
}