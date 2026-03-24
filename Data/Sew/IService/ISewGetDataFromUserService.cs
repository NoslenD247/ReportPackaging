using ReportPackaging.Data.Sew.DTO; 

namespace ReportPackaging.Data.Sew.IService
{
    public interface ISewGetDataFromUserService
    {
        Task<IEnumerable<Sew_GetDataFromUserDTO>> GetAllAsync(String User);
    }
}
