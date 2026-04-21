using ReportPackaging.Data.Sew.DTO;

namespace ReportPackaging.Data.Sew.IService
{
    public interface ISew_CuttingStatusService
    {
        Task<IEnumerable<Sew_CuttingStatusDTO>> GetTodayAsync(int centerId);
        Task<bool> ScanAsync(Sew_CuttingStatusScanDTO item);
        Task<bool> RevertAsync(int idCutting);
    }
}
