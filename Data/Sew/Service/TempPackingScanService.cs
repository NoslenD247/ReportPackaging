
using System.Diagnostics;
using System.Net.Http.Json;


using ReportPackaging.Data.Sew.DTO;
using ReportPackaging.Data.Sew.IService;


namespace ReportPackaging.Data.Sew.Service
{
    public class TempPackingScanService : ITempPackingService
    {
        private readonly HttpClient _httpclient;

        public TempPackingScanService(IHttpClientFactory factory)
        {
            _httpclient = factory.CreateClient("ApiClient");
        }

        public async Task<bool> InsertAsync(TempPackingDTO item)
        {
            try
            {
                var response = await _httpclient.PostAsJsonAsync("TempPacking/Insert", item);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[TempPackingService] Error {(int)response.StatusCode}: {body}");
                }
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TempPackingService] InsertAsync Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateAsync(TempPackingDTO item)
        {
            try
            {
                var response = await _httpclient.PutAsJsonAsync("TempPacking/Update", item);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[TempPackingService] UpdateAsync Error {(int)response.StatusCode}: {body}");
                }
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TempPackingService] UpdateAsync Error: {ex.Message}");
                return false;
            }
        }
    }
}
