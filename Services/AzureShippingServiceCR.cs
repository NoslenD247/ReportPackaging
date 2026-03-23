using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;

namespace ReportPackaging.Services
{
    /// <summary>
    /// OCR Service for C.R. Textil "ENVIO DE MERCADERIAS" template.
    /// Fields: CLIENTE, FABRICA DE SALIDA, FABRIC, WIDTH, COLOR, LOT, S/#, GRAND TOTAL
    /// </summary>
    public class AzureShippingServiceCR
    {
        private readonly string _endpoint;
        private readonly string _apiKey;

        public AzureShippingServiceCR(string endpoint, string apiKey)
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
                var shippingData = new ShippingData();

                System.Diagnostics.Debug.WriteLine("[AzureShippingCR] Analyzing...");

                var operation = await client.AnalyzeDocumentAsync(
                    WaitUntil.Completed, "prebuilt-layout", imageStream);

                var result = operation.Value;

                if (result.Pages?.Count > 0)
                {
                    var lines = result.Pages
                        .SelectMany(p => p.Lines)
                        .Select(l => l.Content.Trim())
                        .ToList();

                    foreach (var l in lines)
                        System.Diagnostics.Debug.WriteLine($"  LINE: '{l}'");

                    ExtractFromLines(lines, shippingData);
                }

                return shippingData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AzureShippingCR] Error: {ex.Message}");
                throw;
            }
        }

        private void ExtractFromLines(List<string> lines, ShippingData data)
        {
            var n = lines.Select(l => l.Trim()).ToList();

            // ── TEXTILE COMPANY pre-scan ──────────────────────────────────────
            var textileLine = n.FirstOrDefault(l =>
                l.ToLowerInvariant().Contains("textil") ||
                l.ToLowerInvariant().Contains("s.a."));
            if (!string.IsNullOrWhiteSpace(textileLine))
            {
                // Clean address info — keep only company name part
                var commaIdx = textileLine.IndexOf("KM", StringComparison.OrdinalIgnoreCase);
                data.TextileCompany = commaIdx > 0
                    ? textileLine[..commaIdx].Trim().TrimEnd(',', ' ')
                    : textileLine;
                System.Diagnostics.Debug.WriteLine($"[AzureShippingCR] TextileCompany: {data.TextileCompany}");
            }

            // ── No. document pre-scan ─────────────────────────────────────────
            var noLine = n.FirstOrDefault(l => l.ToLowerInvariant().StartsWith("no.0") ||
                                               System.Text.RegularExpressions.Regex.IsMatch(l, @"^No\.\d+$"));
            if (!string.IsNullOrWhiteSpace(noLine))
            {
                data.No = noLine.Substring(noLine.IndexOf('.') + 1).Trim();
                System.Diagnostics.Debug.WriteLine($"[AzureShippingCR] No: {data.No}");
            }

            for (int i = 0; i < n.Count; i++)
            {
                var raw = n[i];
                var low = raw.ToLowerInvariant();

                // ── BUYER ──────────────────────────────────────────────────────
                if (low.StartsWith("buyer"))
                {
                    data.Buyer = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(data.Buyer) && i + 1 < n.Count)
                        data.Buyer = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[AzureShippingCR] Buyer: {data.Buyer}");
                    continue;
                }

                // ── FECHA ──────────────────────────────────────────────────────
                if (low == "fecha")
                {
                    // Buscar hacia adelante por valores numéricos y "MES"/"AÑO"
                    for (int j = i - 1; j >= Math.Max(0, i - 3); j--)
                    {
                        // El DIA a veces está ANTES de "FECHA" (ej: '03' antes de 'FECHA')
                        if (System.Text.RegularExpressions.Regex.IsMatch(n[j].Trim(), @"^\d{1,2}$"))
                        {
                            data.Dia = n[j].Trim();
                            break;
                        }
                    }
                    for (int j = i + 1; j < Math.Min(n.Count, i + 6); j++)
                    {
                        var val = n[j].Trim();
                        // "01 /2026" → mes y año juntos
                        var fechaMatch = System.Text.RegularExpressions.Regex.Match(val, @"(\d{1,2})\s*/\s*(\d{4})");
                        if (fechaMatch.Success)
                        {
                            data.Mes = fechaMatch.Groups[1].Value;
                            data.Ano = fechaMatch.Groups[2].Value;
                            break;
                        }
                    }
                    System.Diagnostics.Debug.WriteLine($"[AzureShippingCR] Fecha: {data.Dia}/{data.Mes}/{data.Ano}");
                    continue;
                }

                // ── CLIENTE ───────────────────────────────────────────────────
                if (low.StartsWith("cliente"))
                {
                    data.Cliente = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(data.Cliente) && i + 1 < n.Count)
                        data.Cliente = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[AzureShippingCR] Cliente: {data.Cliente}");
                    continue;
                }

                // ── FABRICA DE SALIDA → FabricaDeLlegada ─────────────────────
                if (low.StartsWith("fabrica de") || low.StartsWith("fábrica de"))
                {
                    if (i + 1 < n.Count && n[i + 1].ToLowerInvariant().StartsWith("salida"))
                    {
                        data.FabricaDeLlegada = AfterColon(n[i + 1]);
                        if (string.IsNullOrWhiteSpace(data.FabricaDeLlegada) && i + 2 < n.Count)
                            data.FabricaDeLlegada = n[i + 2];
                        i++;
                        System.Diagnostics.Debug.WriteLine($"[AzureShippingCR] FabricaDeSalida: {data.FabricaDeLlegada}");
                    }
                    continue;
                }

                // ── FABRIC → TipoDeTela ───────────────────────────────────────
                if (low.StartsWith("fabric:") || low == "fabric")
                {
                    data.TipoDeTela = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(data.TipoDeTela) && i + 1 < n.Count)
                        data.TipoDeTela = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[AzureShippingCR] TipoDeTela: {data.TipoDeTela}");
                    continue;
                }

                // ── WIDTH → Ancho ─────────────────────────────────────────────
                if (low.StartsWith("width"))
                {
                    data.Ancho = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(data.Ancho) && i + 1 < n.Count &&
                        n[i + 1].Any(char.IsDigit))
                        data.Ancho = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[AzureShippingCR] Ancho: {data.Ancho}");
                    continue;
                }

                // ── COLOR ─────────────────────────────────────────────────────
                if (low.StartsWith("color"))
                {
                    data.Color = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(data.Color) && i + 1 < n.Count)
                        data.Color = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[AzureShippingCR] Color: {data.Color}");
                    continue;
                }

                // ── LOT ───────────────────────────────────────────────────────
                if (low.StartsWith("lot"))
                {
                    data.Lot = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(data.Lot) && i + 1 < n.Count)
                        data.Lot = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[AzureShippingCR] Lot: {data.Lot}");
                    continue;
                }

                // ── S/# ───────────────────────────────────────────────────────
                // "S/2T002-23(130-02" — starts with S/ but no # label
                if (low.StartsWith("s/") && !low.StartsWith("s/a") && string.IsNullOrWhiteSpace(data.Serie))
                {
                    // Remove leading S/ or S/# prefix
                    var afterS = raw.Substring(2).TrimStart('#', ':', ' ');
                    if (!string.IsNullOrWhiteSpace(afterS) && afterS.Length > 3)
                        data.Serie = afterS;
                    System.Diagnostics.Debug.WriteLine($"[AzureShippingCR] Serie: {data.Serie}");
                    continue;
                }

                // ── TOTAL / GRAND TOTAL ───────────────────────────────────────
                if (low.Contains("total") && !low.Contains("observation"))
                {
                    // Extraer rolls del mismo line: "TOTAL 44 Zł" → 44
                    var rollMatch = System.Text.RegularExpressions.Regex.Match(raw, @"(\d+)\s*(=|Zł|z|$)");
                    if (rollMatch.Success && int.TryParse(rollMatch.Groups[1].Value, out var rolls))
                        data.TotalRolls = rolls;

                    // Buscar línea con "Kg" en las siguientes 3 líneas
                    for (int j = i + 1; j < Math.Min(n.Count, i + 4); j++)
                    {
                        if (n[j].ToLowerInvariant().Contains("kg") || n[j].ToLowerInvariant().Contains("yd"))
                        {
                            var numbers = System.Text.RegularExpressions.Regex
                                .Matches(n[j], @"[\d,\.]+")
                                .Select(m => ParseDecimal(m.Value))
                                .Where(v => v > 0)
                                .ToList();

                            if (numbers.Count >= 1) data.TotalPesoCrudo = numbers[0];
                            if (numbers.Count >= 2) data.TotalPesoAcabado = numbers[1];
                            if (numbers.Count >= 3) data.TotalYards = numbers[2];
                            break;
                        }
                    }
                    System.Diagnostics.Debug.WriteLine($"[AzureShippingCR] Total — Rolls:{data.TotalRolls} PesoCrudo:{data.TotalPesoCrudo} PesoAcabado:{data.TotalPesoAcabado} Yds:{data.TotalYards}");
                    continue;
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

        private decimal ParseDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            value = System.Text.RegularExpressions.Regex
                .Replace(value, @"[^\d\.,]", "");

            // Si tiene coma Y punto, la coma es separador de miles → quitarla
            if (value.Contains(',') && value.Contains('.'))
                value = value.Replace(",", "");
            // Si solo tiene coma, puede ser decimal → reemplazar por punto
            else if (value.Contains(','))
                value = value.Replace(",", ".");

            return decimal.TryParse(value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var result) ? result : 0;
        }
    }
}