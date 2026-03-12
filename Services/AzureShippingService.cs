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

                // 1. Extract header fields from Lines
                if (result.Pages?.Count > 0)
                {
                    var allLines = result.Pages
                        .SelectMany(p => p.Lines)
                        .Select(l => l.Content.Trim())
                        .ToList();

                    ExtractHeaderFromLines(allLines, shippingData);
                }

                // 2. Extract rolls from tables
                foreach (var table in result.Tables)
                {
                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] Table: {table.RowCount} rows x {table.ColumnCount} cols");

                    // Find which row contains the rolls headers (usually row 0, but sometimes row 1)
                    var headers = new Dictionary<int, string>();
                    int headerRowIndex = -1;

                    for (int checkRow = 0; checkRow <= Math.Min(2, table.RowCount - 1); checkRow++)
                    {
                        var candidateHeaders = new Dictionary<int, string>();
                        foreach (var cell in table.Cells)
                        {
                            if (cell.RowIndex == checkRow)
                                candidateHeaders[cell.ColumnIndex] = cell.Content.Trim().ToLowerInvariant();
                        }

                        bool hasRollHeaders = candidateHeaders.Values.Any(h =>
                            ColNumeroDeRoll.Any(c => h.Contains(c)) ||
                            ColPesoCrudo.Any(c => h.Contains(c)));

                        if (hasRollHeaders)
                        {
                            headers = candidateHeaders;
                            headerRowIndex = checkRow;
                            System.Diagnostics.Debug.WriteLine($"[AzureShipping] Roll headers found at row {checkRow}");
                            break;
                        }
                    }

                    bool isRollsTable = headerRowIndex >= 0;

                    if (!isRollsTable)
                    {
                        System.Diagnostics.Debug.WriteLine("[AzureShipping] Table ignored, not a rolls table.");
                        continue;
                    }

                    var groupStarts = new List<int>();
                    foreach (var kv in headers.OrderBy(k => k.Key))
                    {
                        if (Match(kv.Value, ColNumero) || Match(kv.Value, ColNumeroDeRoll))
                        {
                            if (!groupStarts.Any() || kv.Key > groupStarts.Last() + 1)
                                groupStarts.Add(kv.Key);
                        }
                    }

                    if (!groupStarts.Any()) groupStarts.Add(0);

                    var rowCells = new Dictionary<int, Dictionary<int, string>>();
                    foreach (var cell in table.Cells)
                    {
                        if (cell.RowIndex <= headerRowIndex) continue; // skip header and rows above it
                        if (!rowCells.ContainsKey(cell.RowIndex))
                            rowCells[cell.RowIndex] = new Dictionary<int, string>();
                        rowCells[cell.RowIndex][cell.ColumnIndex] = cell.Content.Trim();
                    }

                    for (int g = 0; g < groupStarts.Count; g++)
                    {
                        int colStart = groupStarts[g];
                        int colEnd = (g + 1 < groupStarts.Count)
                            ? groupStarts[g + 1] - 1
                            : headers.Keys.Max();

                        var groupHeaders = headers
                            .Where(kv => kv.Key >= colStart && kv.Key <= colEnd)
                            .ToDictionary(kv => kv.Key, kv => kv.Value);

                        foreach (var rowKv in rowCells.OrderBy(r => r.Key))
                        {
                            var cells = rowKv.Value;
                            var row = new ShippingRollRow();

                            foreach (var headerKv in groupHeaders)
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

                            // Solo agregar si tiene número de roll
                            if (!string.IsNullOrWhiteSpace(row.NumeroDeRoll))
                            {
                                shippingData.Rolls.Add(row);
                                System.Diagnostics.Debug.WriteLine($"[AzureShipping] Row — G{g + 1} Roll:{row.NumeroDeRoll} Yards:{row.Yards}");
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[AzureShipping] Total rolls: {shippingData.Rolls.Count}");
                return shippingData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AzureShipping] Error: {ex.Message}");
                throw;
            }
        }

        private void ExtractHeaderFromLines(List<string> lines, ShippingData data)
        {
            var n = lines.Select(l => l.Trim()).ToList();

            for (int i = 0; i < n.Count; i++)
            {
                var raw = n[i];
                var low = raw.ToLowerInvariant();

                // FECHA DIA/MES/ANO
                // Labels "DIA" "MES" "ANO" appear on consecutive lines,
                // then their values appear right after as 3 more consecutive lines
                if (low == "dia" || low == "día")
                {
                    int diaIdx = i;
                    int mesIdx = FindIndex(n, new[] { "mes" }, i + 1, i + 3);
                    int anoIdx = FindIndex(n, new[] { "ano", "año" }, i + 1, i + 4);

                    if (mesIdx >= 0 && anoIdx >= 0)
                    {
                        int lastLabel = Math.Max(Math.Max(diaIdx, mesIdx), anoIdx);
                        if (lastLabel + 3 < n.Count)
                        {
                            data.Dia = n[lastLabel + 1];
                            data.Mes = n[lastLabel + 2];
                            data.Ano = n[lastLabel + 3];
                            System.Diagnostics.Debug.WriteLine($"[AzureShipping] Fecha: {data.Dia}/{data.Mes}/{data.Ano}");
                        }
                    }
                    continue;
                }

                // CLIENTE
                if (low.StartsWith("cliente"))
                {
                    data.Cliente = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(data.Cliente) && i + 1 < n.Count)
                        data.Cliente = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] Cliente: {data.Cliente}");
                    continue;
                }

                // BUYER
                if (low.StartsWith("buyer"))
                {
                    data.Buyer = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(data.Buyer) && i + 1 < n.Count)
                        data.Buyer = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] Buyer: {data.Buyer}");
                    continue;
                }

                //  TIPO DE TELA
                // "TIPO DE TELA :-" → value is next line
                if (low.StartsWith("tipo de tela"))
                {
                    data.TipoDeTela = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(data.TipoDeTela) || data.TipoDeTela == "-")
                        data.TipoDeTela = i + 1 < n.Count ? n[i + 1] : string.Empty;
                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] TipoDeTela: {data.TipoDeTela}");
                    continue;
                }

                //  No.
                // "No. J02601160118-1" — value on same line after "No."
                if (low.StartsWith("no."))
                {
                    var spaceIdx = raw.IndexOf(' ');
                    data.No = spaceIdx >= 0 ? raw[(spaceIdx + 1)..].Trim() : AfterColon(raw);
                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] No: {data.No}");
                    continue;
                }

                //  FABRICA DE LLEGADA 
                // "FABRICA DE" then next line "LLEGADA:" then value on next line
                if (low.StartsWith("fabrica de") || low.StartsWith("fábrica de"))
                {
                    if (i + 1 < n.Count && n[i + 1].ToLowerInvariant().StartsWith("llegada"))
                    {
                        data.FabricaDeLlegada = AfterColon(n[i + 1]);
                        if (string.IsNullOrWhiteSpace(data.FabricaDeLlegada) && i + 2 < n.Count)
                            data.FabricaDeLlegada = n[i + 2];
                        i++; // skip "LLEGADA:" line
                        System.Diagnostics.Debug.WriteLine($"[AzureShipping] FabricaDeLlegada: {data.FabricaDeLlegada}");
                    }
                    continue;
                }

                // ANCHO
                if (low.StartsWith("ancho"))
                {
                    data.Ancho = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(data.Ancho) && i + 1 < n.Count)
                        data.Ancho = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] Ancho: {data.Ancho}");
                    continue;
                }

                // COLOR
                if (low.StartsWith("color"))
                {
                    data.Color = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(data.Color) && i + 1 < n.Count)
                        data.Color = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] Color: {data.Color}");
                    continue;
                }

                //  LOT 
                if (low.StartsWith("lot"))
                {
                    data.Lot = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(data.Lot) && i + 1 < n.Count)
                        data.Lot = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] Lot: {data.Lot}");
                    continue;
                }

                //  GRAND TOTAL 
                // OCR sometimes reads "RAND TOTAL" (drops the G)
                if (low.Contains("rand total") || low.Contains("grand total"))
                {
                    data.GrandTotal = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(data.GrandTotal) && i + 1 < n.Count)
                        data.GrandTotal = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] GrandTotal: {data.GrandTotal}");
                    continue;
                }

                // NUMERO DE ORDEN 
                if (low.Contains("numero de orden") || low.Contains("número de orden"))
                {
                    data.OrderNumber = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(data.OrderNumber) && i + 1 < n.Count)
                        data.OrderNumber = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] OrderNumber: {data.OrderNumber}");
                    continue;
                }
            }

            // LOT fallback: standalone number right after color value
            if (string.IsNullOrWhiteSpace(data.Lot) && !string.IsNullOrWhiteSpace(data.Color))
            {
                int colorIdx = n.FindIndex(l => l.Equals(data.Color, StringComparison.OrdinalIgnoreCase));
                if (colorIdx >= 0 && colorIdx + 1 < n.Count)
                {
                    var candidate = n[colorIdx + 1];
                    if (candidate.Length <= 10 && System.Text.RegularExpressions.Regex.IsMatch(candidate, @"^\d+$"))
                    {
                        data.Lot = candidate;
                        System.Diagnostics.Debug.WriteLine($"[AzureShipping] Lot (fallback): {data.Lot}");
                    }
                }
            }

            // S/# fallback: look for series number pattern
            if (string.IsNullOrWhiteSpace(data.Serie))
            {
                var candidate = n.FirstOrDefault(l =>
                    System.Text.RegularExpressions.Regex.IsMatch(l, @"^\d{6,}-\d{2,}$"));
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    data.Serie = candidate;
                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] Serie (fallback): {data.Serie}");
                }
            }
        }

        private string AfterColon(string line)
        {
            var idx = line.IndexOf(':');
            if (idx < 0) idx = line.IndexOf(';');
            return idx >= 0 ? line[(idx + 1)..].Trim() : string.Empty;
        }

        private int FindIndex(List<string> lines, string[] searches, int from, int to)
        {
            for (int i = from; i <= Math.Min(to, lines.Count - 1); i++)
                if (searches.Any(s => lines[i].Trim().ToLowerInvariant() == s))
                    return i;
            return -1;
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