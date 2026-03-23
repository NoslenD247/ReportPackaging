using System.Diagnostics;
using System.Net.Http.Json;
using ReportPackaging.Data.Sew.DTO;
using ReportPackaging.Data.Sew.IService;

namespace ReportPackaging.Data.Sew.Service
{
    public class Sew_FabricInboundScanService : ISew_FabricInboundScanService
    {
        private readonly HttpClient _httpclient;

        public Sew_FabricInboundScanService(IHttpClientFactory factory)
        {
            _httpclient = factory.CreateClient("ApiClient");
        }

        public async Task<bool> CreateFabricInboundScan(Sew_FabricInboundScanDTO item)
        {
            try
            {
                var response = await _httpclient.PostAsJsonAsync("Sew_FabricInboundScan/Create", item);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Sew_FabricInboundScanService] Error: {ex.Message}");
                return false;
            }
        }
    }
}