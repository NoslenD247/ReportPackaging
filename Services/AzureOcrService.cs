using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;

namespace ReportPackaging.Services
{
    public class AzureOcrService
    {
        private readonly string _endpoint;
        private readonly string _apiKey;

        // Nombres posibles de cada columna (Azure puede variar mayúsculas/tildes)
        private static readonly string[] ColFecha = { "fecha" };
        private static readonly string[] ColCodigo = { "codigo", "código", "cod" };
        private static readonly string[] ColBuyer = { "buyer" };
        private static readonly string[] ColStyle = { "style", "stile" };
        private static readonly string[] ColPO = { "po#", "po" };
        private static readonly string[] ColColor = { "color" };
        private static readonly string[] ColOrden = { "orden" };
        //private static readonly string[] ColXS = { "xs" };
        //private static readonly string[] ColS = { "s" };
        //private static readonly string[] ColM = { "m" };
        private static readonly string[] ColXS = { "xs", "6/8 xs" };
        private static readonly string[] ColS = { "s", "10/12 s" };
        private static readonly string[] ColM = { "m", "14/16 m" };
        private static readonly string[] ColL = { "l" };
        private static readonly string[] ColXL = { "xl" };
        private static readonly string[] ColXL2 = { "2xl", "xl2" };
        private static readonly string[] ColXL3 = { "3xl", "xl3" };
        private static readonly string[] ColTotalHoy = { "total hoy", "totalhoy", "total" };
        private static readonly string[] ColAcumulado = { "acumulado" };
        private static readonly string[] ColBalance = { "balance" };

        public AzureOcrService(string endpoint, string apiKey)
        {
            _endpoint = endpoint;
            _apiKey = apiKey;
        }

        public async Task<ReportData> AnalyzeReportAsync(Stream imageStream)
        {
            try
            {
                var credential = new AzureKeyCredential(_apiKey);
                var client = new DocumentAnalysisClient(new Uri(_endpoint), credential);

                System.Diagnostics.Debug.WriteLine("[AzureOCR] Analizando con prebuilt-layout...");

                var operation = await client.AnalyzeDocumentAsync(
                    WaitUntil.Completed, "prebuilt-layout", imageStream);

                var result = operation.Value;
                var reportData = new ReportData();

                System.Diagnostics.Debug.WriteLine($"[AzureOCR] Tablas detectadas: {result.Tables.Count}");

                if (result.Pages?.Count > 0)
                {
                    var lines = result.Pages
                        .SelectMany(p => p.Lines)
                        .Select(l => l.Content.Trim())
                        .ToList();

                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (lines[i].ToLowerInvariant().StartsWith("fecha") && i + 1 < lines.Count)
                        {
                            reportData.Fecha = lines[i + 1];
                            break;
                        }
                    }
                }

                foreach (var table in result.Tables)
                {
                    System.Diagnostics.Debug.WriteLine($"[AzureOCR] Tabla: {table.RowCount} filas x {table.ColumnCount} columnas");

                    // 1. Leer encabezados (fila 0)
                    var headers = new Dictionary<int, string>();
                    var fila0 = new Dictionary<int, string>();
                    var fila1 = new Dictionary<int, string>();
                    foreach (var cell in table.Cells)
                    {
                        if (cell.RowIndex == 0)
                            fila0[cell.ColumnIndex] = cell.Content.Trim();
                        else if (cell.RowIndex == 1)
                            fila1[cell.ColumnIndex] = cell.Content.Trim();
                    }

                    foreach (var kv in fila0)
                    {
                        var parte1 = kv.Value;
                        var parte2 = fila1.TryGetValue(kv.Key, out var v) ? v : string.Empty;

                        headers[kv.Key] = string.IsNullOrWhiteSpace(parte2)
                            ? parte1.ToLowerInvariant()
                            : $"{parte1} {parte2}".ToLowerInvariant().Trim();

                        System.Diagnostics.Debug.WriteLine($"[AzureOCR] Header col {kv.Key}: '{headers[kv.Key]}'");
                    }

                    // Verificar que esta tabla tiene al menos una columna que nos sirva
                    bool esTablaRelevante = headers.Values.Any(h =>
                        ColFecha.Contains(h) || ColBuyer.Contains(h) ||
                        ColStyle.Contains(h) || ColPO.Contains(h));

                    if (!esTablaRelevante)
                    {
                        System.Diagnostics.Debug.WriteLine("[AzureOCR] Tabla ignorada, no parece ser el reporte.");
                        continue;
                    }

                    // 2. Agrupar celdas por fila
                    var rowCells = new Dictionary<int, Dictionary<int, string>>();
                    foreach (var cell in table.Cells)
                    {
                        if (cell.RowIndex == 0 || cell.RowIndex == 1) continue;// skip header
                        if (!rowCells.ContainsKey(cell.RowIndex))
                            rowCells[cell.RowIndex] = new Dictionary<int, string>();
                        rowCells[cell.RowIndex][cell.ColumnIndex] = cell.Content.Trim();
                    }

                    // 3. Mapear cada fila a ReportRow
                    foreach (var rowKv in rowCells.OrderBy(r => r.Key))
                    {
                        var cells = rowKv.Value;
                        var row = new ReportRow();

                        foreach (var headerKv in headers)
                        {
                            var colIdx = headerKv.Key;
                            var headerName = headerKv.Value;
                            var value = cells.TryGetValue(colIdx, out var v) ? v : string.Empty;

                            if (Match(headerName, ColCodigo)) row.Codigo = value;
                            else if (Match(headerName, ColBuyer)) row.Buyer = value;
                            else if (Match(headerName, ColStyle)) row.Style = value;
                            else if (Match(headerName, ColPO)) row.PO = value;
                            else if (Match(headerName, ColColor)) row.Color = value;
                            else if (Match(headerName, ColOrden)) row.Orden = value;
                            else if (Match(headerName, ColXS)) row.XS = ParseDecimal(value);
                            else if (Match(headerName, ColS)) row.S = ParseDecimal(value);
                            else if (Match(headerName, ColM)) row.M = ParseDecimal(value);
                            else if (Match(headerName, ColL)) row.L = ParseDecimal(value);
                            else if (Match(headerName, ColXL)) row.XL = ParseDecimal(value);
                            else if (Match(headerName, ColXL2)) row.XL2 = ParseDecimal(value);
                            else if (Match(headerName, ColXL3)) row.XL3 = ParseDecimal(value);
                            else if (Match(headerName, ColTotalHoy)) row.TotalHoy = ParseDecimal(value);
                            else if (Match(headerName, ColAcumulado)) row.Acumulado = ParseDecimal(value);
                            else if (Match(headerName, ColBalance)) row.Balance = ParseDecimal(value);
                        }

                        // Solo agregar si la fila tiene algo útil
                        if (!string.IsNullOrEmpty(row.Buyer) || row.TotalHoy > 0 || !string.IsNullOrEmpty(row.Style))
                        {
                            reportData.Rows.Add(row);
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[AzureOCR] Total filas extraídas: {reportData.Rows.Count}");
                return reportData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AzureOCR] Error: {ex.Message}");
                throw;
            }
        }

        private bool Match(string header, string[] options)
            => options.Any(o => header.Equals(o, StringComparison.OrdinalIgnoreCase));

        private decimal ParseDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            value = System.Text.RegularExpressions.Regex
                .Replace(value, @"[^\d\.,]", "").Replace(",", ".");
            return decimal.TryParse(value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var result) ? result : 0;
        }
    }
}