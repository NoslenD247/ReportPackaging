using System.Diagnostics;
using System.Net.Http.Json;
using ReportPackaging.Data.Sew.DTO;
using ReportPackaging.Data.Sew.IService;

namespace ReportPackaging.Data.Sew.Service
{
    public class SewGetDataFromUserService : ISewGetDataFromUserService
    {
        private readonly HttpClient _httpclient;

        public SewGetDataFromUserService(IHttpClientFactory factory)
        {
            _httpclient = factory.CreateClient("ApiClient");
        }

        public async Task<IEnumerable<Sew_GetDataFromUserDTO>> GetAllAsync(String User)
        {
            try
            {
                var result = await _httpclient.GetFromJsonAsync<IEnumerable<Sew_GetDataFromUserDTO>>(
                    $"Sew_GetDataFromUser/GetAll/{User}");
                return result ?? Enumerable.Empty<Sew_GetDataFromUserDTO>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetDataFromUser error: {ex.Message}");
                return Enumerable.Empty<Sew_GetDataFromUserDTO>();
            }
        }

    }
}
