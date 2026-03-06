using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;

namespace ReportPackaging.Services
{
    public class AzureShippingService
    {
        private readonly string _endpoint;
        private readonly string _apiKey;

        private static readonly string[] ColNumero = { "#", "no", "n°" };
        private static readonly string[] ColNumeroDeRoll = { "numero de roll", "número de roll", "num de roll", "no de roll" };
        private static readonly string[] ColPesoCrudo = { "peso de crudo", "peso crudo", "crudo" };
        private static readonly string[] ColPesoAcabado = { "peso de acabado", "peso acabado", "acabado" };
        private static readonly string[] ColYards = { "yards", "yardas" };

        public AzureShippingService(string endpoint, string apiKey)
        {
            _endpoint = endpoint;
            _apiKey = apiKey;
        }

        public async Task<ShippingData> AnalyzeShippingAsync(Stream imageStream)
        {
            try
            {
                var credential = new AzureKeyCredential(_apiKey);
                var client = new DocumentAnalysisClient(new Uri(_endpoint), credential);

                System.Diagnostics.Debug.WriteLine("[AzureShipping] Analyzing with prebuilt-layout...");

                var operation = await client.AnalyzeDocumentAsync(
                    WaitUntil.Completed, "prebuilt-layout", imageStream);

                var result = operation.Value;
                var shippingData = new ShippingData();

                System.Diagnostics.Debug.WriteLine($"[AzureShipping] Tables detected: {result.Tables.Count}");

                // ── 1. Extract header fields (Key-Value Pairs) ──
                foreach (var kvPair in result.KeyValuePairs)
                {
                    var key = kvPair.Key?.Content?.Trim().ToLowerInvariant() ?? "";
                    var value = kvPair.Value?.Content?.Trim() ?? "";

                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] KV: '{key}' = '{value}'");

                    if (key.Contains("cliente")) shippingData.Cliente = value;
                    else if (key.Contains("buyer")) shippingData.Buyer = value;
                    else if (key.Contains("fabrica") || key.Contains("fábrica")) shippingData.FabricaDeLlegada = value;
                    else if (key.Contains("s/#") || key.Contains("s/")) shippingData.Serie = value;
                    else if (key.Contains("tipo de tela")) shippingData.TipoDeTela = value;
                    else if (key.Contains("ancho")) shippingData.Ancho = value;
                    else if (key.Contains("color")) shippingData.Color = value;
                    else if (key.Contains("lot")) shippingData.Lot = value;
                    else if (key.Contains("dia") || key.Contains("día")) shippingData.Dia = value;
                    else if (key.Contains("mes")) shippingData.Mes = value;
                    else if (key.Contains("ano") || key.Contains("año")) shippingData.Ano = value;
                    else if (key.Contains("no.") || key == "no") shippingData.No = value;
                    else if (key.Contains("grand total")) shippingData.GrandTotal = value;
                    else if (key.Contains("numero de orden") || key.Contains("número de orden")) shippingData.OrderNumber = value;
                }

                // ── 2. Extract rows from the 3 roll tables ──
                foreach (var table in result.Tables)
                {
                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] Table: {table.RowCount} rows x {table.ColumnCount} cols");

                    // Read headers (row 0)
                    var headers = new Dictionary<int, string>();
                    foreach (var cell in table.Cells)
                    {
                        if (cell.RowIndex == 0)
                        {
                            var h = cell.Content.Trim().ToLowerInvariant();
                            headers[cell.ColumnIndex] = h;
                            System.Diagnostics.Debug.WriteLine($"[AzureShipping] Header col {cell.ColumnIndex}: '{h}'");
                        }
                    }

                    // Check if this is a rolls table
                    bool isRollsTable = headers.Values.Any(h =>
                        ColNumeroDeRoll.Any(c => h.Contains(c)) ||
                        ColPesoCrudo.Any(c => h.Contains(c)));

                    if (!isRollsTable)
                    {
                        System.Diagnostics.Debug.WriteLine("[AzureShipping] Table ignored, not a rolls table.");
                        continue;
                    }

                    // Group cells by row
                    var rowCells = new Dictionary<int, Dictionary<int, string>>();
                    foreach (var cell in table.Cells)
                    {
                        if (cell.RowIndex == 0) continue;
                        if (!rowCells.ContainsKey(cell.RowIndex))
                            rowCells[cell.RowIndex] = new Dictionary<int, string>();
                        rowCells[cell.RowIndex][cell.ColumnIndex] = cell.Content.Trim();
                    }

                    // Map each row to ShippingRollRow
                    foreach (var rowKv in rowCells.OrderBy(r => r.Key))
                    {
                        var cells = rowKv.Value;
                        var row = new ShippingRollRow();

                        foreach (var headerKv in headers)
                        {
                            var colIdx = headerKv.Key;
                            var headerName = headerKv.Value;
                            var value = cells.TryGetValue(colIdx, out var v) ? v : string.Empty;

                            if (Match(headerName, ColNumero)) row.Numero = value;
                            else if (Match(headerName, ColNumeroDeRoll)) row.NumeroDeRoll = value;
                            else if (Match(headerName, ColPesoCrudo)) row.PesoDeCrudo = ParseDecimal(value);
                            else if (Match(headerName, ColPesoAcabado)) row.PesoDeAcabado = ParseDecimal(value);
                            else if (Match(headerName, ColYards)) row.Yards = ParseDecimal(value);
                        }

                        // Only add if row has useful data
                        if (!string.IsNullOrEmpty(row.Numero) || !string.IsNullOrEmpty(row.NumeroDeRoll) || row.Yards > 0)
                        {
                            shippingData.Rolls.Add(row);
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[AzureShipping] Total rolls extracted: {shippingData.Rolls.Count}");
                return shippingData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AzureShipping] Error: {ex.Message}");
                throw;
            }
        }

        private bool Match(string header, string[] options)
            => options.Any(o => header.Contains(o, StringComparison.OrdinalIgnoreCase));

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
