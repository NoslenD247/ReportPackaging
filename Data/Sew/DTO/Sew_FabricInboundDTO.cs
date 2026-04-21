namespace ReportPackaging.Data.Sew.DTO
{
    public class Sew_FabricInboundScanDTOasdfs
    {
        public string EnvID { get; set; } = string.Empty; // FabricId en SP
        public DateTime EntryDate { get; set; } = DateTime.Now;
        public string FileNo { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Lot { get; set; } = string.Empty;
        public string Width { get; set; } = string.Empty;
        public int Rolls { get; set; }
        public decimal KgsReceived { get; set; }
        public decimal BodyReceived { get; set; }
        public decimal RibReceived { get; set; }
        public string FabricType { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public int BuyerId { get; set; }
        public string Rack { get; set; } = string.Empty;
        public int CenterId { get; set; }

        #region User
        public string CreatedByUserName { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public string LastUpdatedByUserId { get; set; } = string.Empty;
        public string LastUpdatedByUserNameEn { get; set; } = string.Empty;
        public DateTime LastUpdatedDate { get; set; } = DateTime.Now;
        #endregion
    }
}