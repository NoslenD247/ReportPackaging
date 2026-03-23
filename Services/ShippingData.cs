namespace ReportPackaging.Services
{
    public class ShippingData
    {
        // ── Encabezado extraído del OCR ──────────────────
        public string TextileCompany { get; set; } = string.Empty;
        public string Dia { get; set; } = string.Empty;
        public string Mes { get; set; } = string.Empty;
        public string Ano { get; set; } = string.Empty;
        public string No { get; set; } = string.Empty;
        public string Cliente { get; set; } = string.Empty;
        public string Buyer { get; set; } = string.Empty;
        public string FabricaDeLlegada { get; set; } = string.Empty;
        public string Serie { get; set; } = string.Empty;
        public string TipoDeTela { get; set; } = string.Empty;
        public string Ancho { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Lot { get; set; } = string.Empty;

        // ── Totales ──────────────────────────────────────
        public int TotalRolls { get; set; }
        public decimal TotalPesoCrudo { get; set; }
        public decimal TotalPesoAcabado { get; set; }
        public decimal TotalYards { get; set; }

        // ── Campos adicionales (se guardan en DB) ────────
        public decimal BodyReceived { get; set; }
        public decimal RibReceived { get; set; }
        public string Rack { get; set; } = string.Empty;

        // ── Pie de documento ─────────────────────────────
        public string OrderNumber { get; set; } = string.Empty;
    }
}