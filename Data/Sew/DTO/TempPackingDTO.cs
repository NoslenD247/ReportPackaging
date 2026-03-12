namespace ReportPackaging.Data.Sew.DTO
{
    public class TempPackingDTO
    {
        public int Id { get; set; }
        public string ReportDate { get; set; } = string.Empty;
        public string Buyer { get; set; } = string.Empty;
        public string Style { get; set; } = string.Empty;
        public string Po { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Orden { get; set; } = string.Empty;
        public int Xs { get; set; }
        public int S { get; set; }
        public int M { get; set; }
        public int L { get; set; }
        public int Xl { get; set; }
        public int Xl2 { get; set; }
        public int Xl3 { get; set; }
        public int TodayTotal { get; set; }
    }
}
