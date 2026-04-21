using ReportPackaging.Data.Sew.DTO;

namespace ReportPackaging.Data.Sew.IService
{
    public interface ITempPackingService
    {
        Task<bool> InsertAsync(TempPackingDTO item);
        Task<bool> UpdateAsync(TempPackingDTO item);
    }
}
