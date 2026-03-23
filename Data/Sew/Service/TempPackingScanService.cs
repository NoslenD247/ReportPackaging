
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
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TempPackingService] Error: {ex.Message}");
                return false;
            }
        }
    }
}
