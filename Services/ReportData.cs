namespace ReportPackaging.Services
{
    // Una fila de la tabla del reporte
    public class ReportRow
    {
        public string Fecha { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public string Buyer { get; set; } = string.Empty;
        public string Style { get; set; } = string.Empty;
        public string PO { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Orden { get; set; } = string.Empty;

        // Tallas
        public decimal XS { get; set; }
        public decimal S { get; set; }
        public decimal M { get; set; }
        public decimal L { get; set; }
        public decimal XL { get; set; }
        public decimal XL2 { get; set; }
        public decimal XL3 { get; set; }

        // Totales
        public decimal TotalHoy { get; set; }
        public decimal Acumulado { get; set; }
        public decimal Balance { get; set; }
    }

    // Contenedor principal — puede tener múltiples tablas (tu reporte tiene 5)
    public class ReportData
    {
        public List<ReportRow> Rows { get; set; } = new List<ReportRow>();
        public string RawMarkdown { get; set; } = string.Empty; // texto crudo de Mistral
    }
}
