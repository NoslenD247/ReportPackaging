using ReportPackaging.Data.Sew.DTO;
namespace ReportPackaging.Data.Sew.IService
{
    public interface ISew_FabricInboundScanService
    {
        Task<bool> CreateFabricInboundScan(Sew_FabricInboundScanDTO item);
    }
}