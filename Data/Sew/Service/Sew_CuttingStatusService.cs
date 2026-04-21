using System.Diagnostics;
using System.Net.Http.Json;
using ReportPackaging.Data.Sew.DTO;
using ReportPackaging.Data.Sew.IService;

namespace ReportPackaging.Data.Sew.Service
{
    public class Sew_CuttingStatusService : ISew_CuttingStatusService
    {
        private readonly HttpClient _httpclient;

        public Sew_CuttingStatusService(IHttpClientFactory factory)
        {
            _httpclient = factory.CreateClient("ApiClient");
        }

        public async Task<IEnumerable<Sew_CuttingStatusDTO>> GetTodayAsync(int centerId)
        {
            try
            {
                var result = await _httpclient.GetFromJsonAsync<IEnumerable<Sew_CuttingStatusDTO>>($"Sew_CuttingStatus/Today/{centerId}");
                return result ?? Enumerable.Empty<Sew_CuttingStatusDTO>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Sew_CuttingStatusService] GetTodayAsync Error: {ex.Message}");
                return Enumerable.Empty<Sew_CuttingStatusDTO>();
            }
        }

        public async Task<bool> ScanAsync(Sew_CuttingStatusScanDTO item)
        {
            try
            {
                var response = await _httpclient.PostAsJsonAsync("Sew_CuttingStatus/Scan", item);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Sew_CuttingStatusService] ScanAsync Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RevertAsync(int idCutting)
        {
            try
            {
                var response = await _httpclient.PutAsync($"Sew_CuttingStatus/Revert/{idCutting}", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Sew_CuttingStatusService] RevertAsync Error: {ex.Message}");
                return false;
            }
        }
    }
}
