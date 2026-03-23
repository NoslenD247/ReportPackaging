using ReportPackaging.Data.Sew.DTO;

namespace ReportPackaging.Data.Sew.IService
{
    public interface IFactoryService
    {
        Task<IEnumerable<BuyerDTO>> GetBuyersByCenter(int centerId);
        Task<IEnumerable<SupplierDTO>> GetSuppliersByCenter(int centerId);
    }
}