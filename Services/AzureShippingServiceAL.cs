using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;

namespace ReportPackaging.Services
{
    /// <summary>
    /// OCR Service for Apparel Links "REPORTE DE PACKING LIST BODEGA DE TELA" template.
    /// Fields: ESTILO, CLIENTE, COLOR, LOTE, Ancho, Tela, Buyer, S/#, Rack, GRAN TOTAL
    /// TextileCompany and Fecha are written manually in the top corners without labels.
    /// </summary>
    public class AzureShippingServiceAL
    {
        private readonly string _endpoint;
        private readonly string _apiKey;

        public AzureShippingServiceAL(string endpoint, string apiKey)
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

                System.Diagnostics.Debug.WriteLine("[AzureShippingAL] Analyzing...");

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
                System.Diagnostics.Debug.WriteLine($"[AzureShippingAL] Error: {ex.Message}");
                throw;
            }
        }

        private void ExtractFromLines(List<string> lines, ShippingData data)
        {
            var n = lines.Select(l => l.Trim()).ToList();

            // ── Pre-scan: Textile Company y Fecha ────────────────────────────
            // Buscar en las primeras 8 líneas antes del título del reporte
            int tituloIdx = n.FindIndex(l =>
                l.ToLowerInvariant().Contains("reporte") ||
                l.ToLowerInvariant().Contains("packing list"));

            int limite = tituloIdx > 0 ? tituloIdx : Math.Min(8, n.Count);

            for (int i = 0; i < limite; i++)
            {
                var raw = n[i];
                var low = raw.ToLowerInvariant();

                // Textile Company — línea con S.A., TEXTIL o TEXTILES antes del título
                if (string.IsNullOrWhiteSpace(data.TextileCompany) &&
                    !IsNoise(raw) &&
                    !low.Contains("apparel links") && // saltar el encabezado impreso
                    !low.Contains("estilo") &&
                    !low.Contains("color") &&
                    !low.Contains("cliente"))
                {
                    data.TextileCompany = raw;
                    System.Diagnostics.Debug.WriteLine($"[AzureShippingAL] TextileCompany: {data.TextileCompany}");
                    continue;
                }

                // Fecha — patrón DD/MM/AAAA o D/M/AAAA sin etiqueta
                if (string.IsNullOrWhiteSpace(data.Dia))
                {
                    var fechaMatch = System.Text.RegularExpressions.Regex.Match(raw,
                        @"(\d{1,2})[\/\-](\d{1,2})[\/\-](\d{4})");
                    if (fechaMatch.Success)
                    {
                        data.Dia = fechaMatch.Groups[1].Value;
                        data.Mes = fechaMatch.Groups[2].Value;
                        data.Ano = fechaMatch.Groups[3].Value;
                        System.Diagnostics.Debug.WriteLine($"[AzureShippingAL] Fecha: {data.Dia}/{data.Mes}/{data.Ano}");
                        continue;
                    }
                }
            }

            // ── Main loop ─────────────────────────────────────────────────────
            for (int i = 0; i < n.Count; i++)
            {
                var raw = n[i];
                var low = raw.ToLowerInvariant();

                // ── ESTILO → Serie ────────────────────────────────────────────
                if (low.StartsWith("estilo"))
                {
                    data.Serie = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(data.Serie) && i + 1 < n.Count)
                        data.Serie = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[AzureShippingAL] Serie: {data.Serie}");
                    continue;
                }

                // ── CLIENTE ───────────────────────────────────────────────────
                if (low.StartsWith("cliente"))
                {
                    data.Cliente = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(data.Cliente) && i + 1 < n.Count)
                        data.Cliente = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[AzureShippingAL] Cliente: {data.Cliente}");
                    continue;
                }

                // ── COLOR ─────────────────────────────────────────────────────
                if (low.StartsWith("color"))
                {
                    data.Color = AfterColon(raw);
                    if (!string.IsNullOrWhiteSpace(data.Color))
                    {
                        foreach (var stop in new[] { "codigo", "version", "fecha", "pagina" })
                        {
                            var stopIdx = data.Color.ToLowerInvariant().IndexOf(stop);
                            if (stopIdx > 0) { data.Color = data.Color[..stopIdx].Trim(); break; }
                        }
                    }
                    if (string.IsNullOrWhiteSpace(data.Color) && i + 1 < n.Count)
                        data.Color = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[AzureShippingAL] Color: {data.Color}");
                    continue;
                }

                // ── LOTE → Lot ────────────────────────────────────────────────
                if (low.StartsWith("lote"))
                {
                    data.Lot = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(data.Lot) && i + 1 < n.Count)
                        data.Lot = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[AzureShippingAL] Lot: {data.Lot}");
                    continue;
                }

                // ── ANCHO ─────────────────────────────────────────────────────
                if (low.StartsWith("ancho"))
                {
                    data.Ancho = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(data.Ancho) && i + 1 < n.Count)
                        data.Ancho = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[AzureShippingAL] Ancho: {data.Ancho}");
                    continue;
                }

                // ── TELA → TipoDeTela ─────────────────────────────────────────
                if (low.StartsWith("tela"))
                {
                    data.TipoDeTela = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(data.TipoDeTela) && i + 1 < n.Count)
                        data.TipoDeTela = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[AzureShippingAL] TipoDeTela: {data.TipoDeTela}");
                    continue;
                }

                // ── BUYER ─────────────────────────────────────────────────────
                if (low.StartsWith("buyer") || low.StartsWith("boyer"))
                {
                    data.Buyer = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(data.Buyer) && i + 1 < n.Count)
                        data.Buyer = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[AzureShippingAL] Buyer: {data.Buyer}");
                    continue;
                }

                // ── S/# ───────────────────────────────────────────────────────
                if (low.StartsWith("s/#") || low.StartsWith("s/ #"))
                {
                    var afterLabel = raw.Substring(3).TrimStart(':', ' ');
                    if (!string.IsNullOrWhiteSpace(afterLabel))
                        data.Serie = afterLabel;
                    System.Diagnostics.Debug.WriteLine($"[AzureShippingAL] Serie (S/#): {data.Serie}");
                    continue;
                }

                // ── RACK — línea justo antes de GRAN TOTAL ────────────────────
                if (string.IsNullOrWhiteSpace(data.Rack))
                {
                    int granIdx = n.FindIndex(l => l.ToLowerInvariant().Contains("gran total"));
                    if (granIdx > 0 && i == granIdx - 1)
                    {
                        if (!IsNoise(raw) && !raw.All(char.IsDigit))
                            data.Rack = raw;
                    }
                }

                // ── GRAN TOTAL ────────────────────────────────────────────────
                // Estructura: "GRAN TOTAL" → "ROLLOS" "KGS" "YARDAS" → valores
                if (low.Contains("gran total"))
                {
                    int rollosIdx = FindIndex(n, new[] { "rollos" }, i + 1, i + 4);
                    if (rollosIdx >= 0)
                    {
                        var values = new List<string>();
                        for (int j = rollosIdx + 3; j < Math.Min(rollosIdx + 8, n.Count); j++)
                        {
                            if (n[j].Any(char.IsDigit) && !n[j].ToLowerInvariant().StartsWith("pri"))
                                values.Add(n[j]);
                            if (values.Count == 3) break;
                        }
                        if (values.Count >= 1) data.TotalRolls = int.TryParse(values[0].Trim(), out var r) ? r : 0;
                        if (values.Count >= 2) data.TotalPesoCrudo = ParseDecimal(values[1]);
                        if (values.Count >= 3) data.TotalYards = ParseDecimal(values[2]);
                    }
                    System.Diagnostics.Debug.WriteLine($"[AzureShippingAL] GrandTotal — Rolls:{data.TotalRolls} Kgs:{data.TotalPesoCrudo} Yds:{data.TotalYards}");
                    continue;
                }
            }

            // ── RACK fallback: patrón X-XX antes de GRAN TOTAL ───────────────
            if (string.IsNullOrWhiteSpace(data.Rack))
            {
                int granIdx = n.FindIndex(l => l.ToLowerInvariant().Contains("gran total"));
                if (granIdx > 1)
                {
                    for (int j = granIdx - 1; j >= Math.Max(0, granIdx - 4); j--)
                    {
                        if (System.Text.RegularExpressions.Regex.IsMatch(n[j], @"^[A-Z]-\d+$") ||
                            System.Text.RegularExpressions.Regex.IsMatch(n[j], @"^[A-Z]\d+-\d+$"))
                        {
                            data.Rack = n[j];
                            System.Diagnostics.Debug.WriteLine($"[AzureShippingAL] Rack: {data.Rack}");
                            break;
                        }
                    }
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

        private bool IsNoise(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length < 2) return true;
            if (value.Contains('[') || value.Contains(']')) return true;
            return false;
        }

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