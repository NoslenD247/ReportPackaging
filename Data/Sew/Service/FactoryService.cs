using ReportPackaging.Data.Sew.DTO;
using ReportPackaging.Data.Sew.IService;
using System.Diagnostics;
using System.Net.Http.Json;

namespace ReportPackaging.Data.Sew.Service
{
    public class FactoryService : IFactoryService
    {
        private readonly HttpClient _httpClient;

        public FactoryService(IHttpClientFactory factory)
        {
            _httpClient = factory.CreateClient("ApiClient");
        }

        public async Task<IEnumerable<BuyerDTO>> GetBuyersByCenter(int centerId)
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<IEnumerable<BuyerDTO>>(
                    $"Factory/GetBuyers/{centerId}");
                return result ?? Enumerable.Empty<BuyerDTO>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FactoryService] GetBuyersByCenter error: {ex.Message}");
                return Enumerable.Empty<BuyerDTO>();
            }
        }

        public async Task<IEnumerable<SupplierDTO>> GetSuppliersByCenter(int centerId)
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<IEnumerable<SupplierDTO>>(
                    $"Factory/GetSuppliers/{centerId}");
                return result ?? Enumerable.Empty<SupplierDTO>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FactoryService] GetSuppliersByCenter error: {ex.Message}");
                return Enumerable.Empty<SupplierDTO>();
            }
        }
    }
}