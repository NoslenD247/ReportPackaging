namespace ReportPackaging.Services
{
    // Una fila de las tablas de rolls
    public class ShippingRollRow
    {
        public string Numero { get; set; } = string.Empty;
        public string NumeroDeRoll { get; set; } = string.Empty;
        public decimal PesoDeCrudo { get; set; }
        public decimal PesoDeAcabado { get; set; }
        public decimal Yards { get; set; }
    }

    // Contenedor principal del documento
    public class ShippingData
    {
        // ── Encabezado ──────────────────────────────────
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

        // ── Filas de las 3 tablas combinadas ────────────
        public List<ShippingRollRow> Rolls { get; set; } = new List<ShippingRollRow>();

        // ── Pie de documento ────────────────────────────
        public string GrandTotal { get; set; } = string.Empty;
        public string OrderNumber { get; set; } = string.Empty;
    }
}
