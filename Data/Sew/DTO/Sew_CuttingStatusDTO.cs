namespace ReportPackaging.Data.Sew.DTO
{
    public class Sew_CuttingStatusDTO
    {
        public int IdCutting { get; set; }
        public string FileNo { get; set; } = string.Empty;
        public int? SizeId { get; set; }
        public string? SizeName { get; set; }
        public int? Quantity { get; set; }
        public string? PackageNo { get; set; }
        public bool IsProduction { get; set; }
        public bool IsArchived { get; set; }
        public DateTime Updated_at { get; set; }
    }

    public class Sew_CuttingStatusScanDTO
    {
        public int ID { get; set; }             // IdCutting
        public int OrderGroupedId { get; set; }
        public int BuyerID { get; set; }
        public string BuyerName { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string PO { get; set; } = string.Empty;
        public string NoCorte { get; set; } = string.Empty;
        public string NoEnvio { get; set; } = string.Empty;
        public decimal Yardas { get; set; }
        public string Lote { get; set; } = string.Empty;
        public string Talla { get; set; } = string.Empty;  // SizeName
        public int NoPaq { get; set; }                     // PackageNo
        public int Cant { get; set; }                      // Quantity
    }
}
