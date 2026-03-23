using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;

namespace ReportPackaging.Services
{
    public class AzureShippingService
    {
        private readonly string _endpoint;
        private readonly string _apiKey;

        private static readonly string[] KnownLabels = {
            "buyer", "buye", "fabrica", "no.", "envio", "mercaderia",
            "llegada", "tipo", "ancho", "color", "lot", "grand", "numero",
            "peso", "crudo", "acabado", "yards", "frima", "solicitante",
            "recibi", "fecha", "textiles", "tel:", "fax:", "km "
        };

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
                var shippingData = new ShippingData();

                System.Diagnostics.Debug.WriteLine("[AzureShipping] Analyzing with prebuilt-layout...");

                var operation = await client.AnalyzeDocumentAsync(
                    WaitUntil.Completed, "prebuilt-layout", imageStream);

                var result = operation.Value;

                if (result.Pages?.Count > 0)
                {
                    var lines = result.Pages
                        .SelectMany(p => p.Lines)
                        .Select(l => l.Content.Trim())
                        .ToList();

                    System.Diagnostics.Debug.WriteLine("[AzureShipping] Lines:");
                    //foreach (var l in lines)
                    //    System.Diagnostics.Debug.WriteLine($"  LINE: '{l}'");

                    ExtractHeaderFromLines(lines, shippingData);
                }

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

            foreach (var l in lines)
                System.Diagnostics.Debug.WriteLine($"  LINE: '{l}'");


            var n = lines.Select(l => l.Trim()).ToList();

            // ── TEXTILE COMPANY pre-scan ──────────────────────────────────
            // Primera línea que contenga "TEXTILES" o "TEXTI"
            var textileLine = n.FirstOrDefault(l =>
                l.ToLowerInvariant().Contains("textiles") ||
                l.ToLowerInvariant().StartsWith("texti"));
            if (!string.IsNullOrWhiteSpace(textileLine))
            {
                data.TextileCompany = textileLine;
                System.Diagnostics.Debug.WriteLine($"[AzureShipping] TextileCompany: {data.TextileCompany}");
            }


            // ── RACK pre-scan ─────────────────────────────────────────────
            // Rack aparece entre FECHA y DIA, sin etiqueta propia
            int fechaIdx = n.FindIndex(l => l.ToLowerInvariant() == "fecha");
            int diaIdx2 = FindIndex(n, new[] { "dia", "día" }, fechaIdx + 1, fechaIdx + 5);

            if (fechaIdx >= 0 && diaIdx2 > fechaIdx + 1)
            {
                // Buscar entre FECHA y DIA
                for (int j = fechaIdx + 1; j < diaIdx2; j++)
                {
                    var candidate = n[j];
                    if (!IsKnownLabel(candidate) && !IsNoise(candidate) && !candidate.All(char.IsDigit))
                    {
                        data.Rack = candidate;
                        System.Diagnostics.Debug.WriteLine($"[AzureShipping] Rack: {data.Rack}");
                        break;
                    }
                }
            }

            // ── Pre-scan: extract S/# before main loop ────────────────────────
            // S/# format: "ST052-01", "26BT053-01", "S6/T053-01"
            // Must be done before loop so Buyer doesn't steal it
            if (string.IsNullOrWhiteSpace(data.Serie))
            {
                var serie = n.FirstOrDefault(l =>
                    System.Text.RegularExpressions.Regex.IsMatch(l,
                        @"^[A-Z0-9]{1,4}[A-Z]\d{2,4}-\d{2,3}$"));
                if (!string.IsNullOrWhiteSpace(serie))
                {
                    data.Serie = serie;
                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] Serie (pre-scan): {data.Serie}");
                }
            }

            for (int i = 0; i < n.Count; i++)
            {
                var raw = n[i];
                var low = raw.ToLowerInvariant();

                // ── FECHA DIA/MES/ANO ─────────────────────────────────────────
                // Handle both ordered (DIA/MES/ANO) and unordered (ANO/MES/DIA)
                if (low == "dia" || low == "día" || low == "ano" || low == "año" || low == "mes")
                {
                    // Collect all 3 label positions
                    int diaIdx = FindIndex(n, new[] { "dia", "día" }, 0, n.Count - 1);
                    int mesIdx = FindIndex(n, new[] { "mes" }, 0, n.Count - 1);
                    int anoIdx = FindIndex(n, new[] { "ano", "año" }, 0, n.Count - 1);

                    if (diaIdx >= 0 && mesIdx >= 0 && anoIdx >= 0)
                    {
                        // Values follow immediately after each label
                        // Find the last label index, then read values sequentially
                        // Strategy: each label is followed by its value on the next numeric line
                        string diaVal = FindNextNumeric(n, diaIdx + 1, diaIdx + 4);
                        string mesVal = FindNextNumeric(n, mesIdx + 1, mesIdx + 4);
                        string anoVal = FindNextNumeric(n, anoIdx + 1, anoIdx + 4);

                        // Validate: ano should be 4 digits, dia/mes should be 1-2 digits
                        if (!string.IsNullOrWhiteSpace(anoVal) && anoVal.Length == 4)
                        {
                            data.Dia = diaVal;
                            data.Mes = mesVal;
                            data.Ano = anoVal;
                        }
                        else
                        {
                            // Fallback: find the 4-digit year and assign the rest
                            var yearLine = n.FirstOrDefault(l =>
                                System.Text.RegularExpressions.Regex.IsMatch(l, @"^20\d{2}$"));
                            if (yearLine != null)
                            {
                                data.Ano = yearLine;
                                int yearIdx = n.IndexOf(yearLine);
                                // Day and month are nearby 2-digit numbers
                                var nearby = n.Skip(Math.Max(0, yearIdx - 5))
                                              .Take(10)
                                              .Where(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^\d{1,2}$"))
                                              .ToList();
                                if (nearby.Count >= 2)
                                {
                                    data.Dia = nearby[0];
                                    data.Mes = nearby[1];
                                }
                            }
                        }

                        System.Diagnostics.Debug.WriteLine($"[AzureShipping] Fecha: {data.Dia}/{data.Mes}/{data.Ano}");
                    }
                    continue;
                }

                // ── CLIENTE ───────────────────────────────────────────────────
                if (low.StartsWith("cliente"))
                {
                    data.Cliente = AfterColon(raw);

                    // Handle merge "CLIENTEPPAREL LINKS"
                    if (string.IsNullOrWhiteSpace(data.Cliente) && raw.Length > 7)
                    {
                        var merged = raw.Substring(7).TrimStart(':', ' ');
                        if (merged.Length > 3 && !IsKnownLabel(merged))
                            data.Cliente = merged;
                    }

                    // If still empty/noise, search forward avoiding known labels
                    if (string.IsNullOrWhiteSpace(data.Cliente) || IsNoise(data.Cliente))
                    {
                        for (int j = i + 1; j <= Math.Min(i + 5, n.Count - 1); j++)
                        {
                            if (IsValidValue(n[j]) && !IsKnownLabel(n[j]))
                            {
                                data.Cliente = n[j];
                                break;
                            }
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] Cliente: {data.Cliente}");
                    continue;
                }

                // ── BUYER ─────────────────────────────────────────────────────
                // Handles: "BUYER:", "BUYERLUCKY BRAND", "BUYE LUCKY BRAND"
                if (low.StartsWith("buyer") || (low.StartsWith("buye") && low.Length > 4))
                {
                    data.Buyer = AfterColon(raw);

                    // Handle merge "BUYERLUCKY BRAND" → after "BUYER" (5 chars)
                    if (string.IsNullOrWhiteSpace(data.Buyer) && raw.Length > 5)
                    {
                        var merged = raw.Substring(5).TrimStart(':', ' ');
                        if (merged.Length > 3 && !IsKnownLabel(merged))
                            data.Buyer = merged;
                    }

                    // Handle "BUYE LUCKY BRAND" → after first space
                    if (string.IsNullOrWhiteSpace(data.Buyer))
                    {
                        var spaceIdx = raw.IndexOf(' ');
                        if (spaceIdx > 0)
                        {
                            var afterSpace = raw.Substring(spaceIdx + 1).Trim();
                            if (!IsKnownLabel(afterSpace) && afterSpace.Length > 3)
                                data.Buyer = afterSpace;
                        }
                    }

                    // Search forward if still empty
                    if (string.IsNullOrWhiteSpace(data.Buyer) || IsNoise(data.Buyer))
                    {
                        for (int j = i + 1; j <= Math.Min(i + 3, n.Count - 1); j++)
                        {
                            if (IsValidValue(n[j]))
                            {
                                data.Buyer = n[j];
                                break;
                            }
                        }
                    }

                    // Don't let Buyer == Serie
                    if (data.Buyer == data.Serie)
                        data.Buyer = string.Empty;

                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] Buyer: {data.Buyer}");
                    continue;
                }

                // ── No. ───────────────────────────────────────────────────────
                if (low.StartsWith("no."))
                {
                    var spaceIdx = raw.IndexOf(' ');
                    data.No = spaceIdx >= 0 ? raw[(spaceIdx + 1)..].Trim() : AfterColon(raw);
                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] No: {data.No}");
                    continue;
                }

                // ── FABRICA DE LLEGADA ────────────────────────────────────────
                if (low.StartsWith("fabrica de") || low.StartsWith("fábrica de"))
                {
                    if (i + 1 < n.Count && n[i + 1].ToLowerInvariant().StartsWith("llegada"))
                    {
                        data.FabricaDeLlegada = AfterColon(n[i + 1]);
                        if (string.IsNullOrWhiteSpace(data.FabricaDeLlegada) && i + 2 < n.Count)
                            data.FabricaDeLlegada = n[i + 2];
                        i++;
                        System.Diagnostics.Debug.WriteLine($"[AzureShipping] FabricaDeLlegada: {data.FabricaDeLlegada}");
                    }
                    continue;
                }

                // ── TIPO DE TELA ──────────────────────────────────────────────
                // Handles: "TIPO DE TELA305/2...", "TIPO DE TELOPIMA2SS/1...", "TIPO DE THIS"
                if (low.StartsWith("tipo de tel"))
                {
                    data.TipoDeTela = AfterColon(raw);

                    if (string.IsNullOrWhiteSpace(data.TipoDeTela))
                    {
                        // Find where label ends — look for digit or slash after "TIPO DE TEL"
                        var afterPrefix = raw.Length > 11 ? raw.Substring(11) : string.Empty;
                        if (!string.IsNullOrWhiteSpace(afterPrefix))
                        {
                            // Skip remaining label letters until we hit value chars
                            int valueStart = 0;
                            while (valueStart < afterPrefix.Length &&
                                   char.IsLetter(afterPrefix[valueStart]) &&
                                   afterPrefix[valueStart] != '/' &&
                                   valueStart < 5)
                                valueStart++;
                            var extracted = afterPrefix.Substring(valueStart).TrimStart(':', ' ', '-');
                            if (extracted.Length > 3 && !IsKnownLabel(extracted))
                                data.TipoDeTela = extracted;
                        }
                    }

                    // Fallback: peek next lines
                    if (string.IsNullOrWhiteSpace(data.TipoDeTela))
                    {
                        for (int j = i + 1; j <= Math.Min(i + 2, n.Count - 1); j++)
                        {
                            if (IsValidValue(n[j]) && !IsKnownLabel(n[j]))
                            {
                                data.TipoDeTela = n[j];
                                break;
                            }
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] TipoDeTela: {data.TipoDeTela}");
                    continue;
                }

                // ── ANCHO ─────────────────────────────────────────────────────
                // Handles: "ANCHO: 70/727", "ANCHO:" (value on next line), "ANCHO" (no colon)
                if (low == "ancho" || low.StartsWith("ancho:") || low.StartsWith("ancho "))
                {
                    data.Ancho = AfterColon(raw);

                    if (string.IsNullOrWhiteSpace(data.Ancho))
                    {
                        for (int j = i + 1; j <= Math.Min(i + 3, n.Count - 1); j++)
                        {
                            var cLow = n[j].ToLowerInvariant();
                            if (cLow.StartsWith("peso") || cLow.StartsWith("color") || cLow.StartsWith("monk")) continue;

                            // Ancho siempre tiene formato numérico con /
                            if (n[j].Any(char.IsDigit))
                            {
                                var val = n[j].TrimEnd('ª', '"', '\'', ' ', 'a', '*');
                                data.Ancho = val;
                                break;
                            }
                        }
                    }
                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] Ancho: {data.Ancho}");
                    continue;
                }

                // ── COLOR ─────────────────────────────────────────────────────
                // Handles: "COLOR:", "72/74 COLOR:", split values
                if (low.StartsWith("color") || (low.Contains("color:") && !low.StartsWith("ancho")))
                {
                    // Extract after "COLOR:"
                    var colorLabelIdx = raw.ToLowerInvariant().IndexOf("color");
                    if (colorLabelIdx >= 0)
                    {
                        var afterColor = raw.Substring(colorLabelIdx + 5).TrimStart(':', ' ');
                        if (!string.IsNullOrWhiteSpace(afterColor))
                            data.Color = afterColor;
                    }

                    if (string.IsNullOrWhiteSpace(data.Color) && i + 1 < n.Count)
                        data.Color = n[i + 1];

                    // Concatenate next line if color seems incomplete (split across lines)
                    if (!string.IsNullOrWhiteSpace(data.Color) && i + 2 < n.Count)
                    {
                        var nextLow = n[i + 2].ToLowerInvariant();
                        if (!IsKnownLabel(nextLow) && !n[i + 2].All(char.IsDigit) && !IsNoise(n[i + 2]))
                            data.Color = $"{data.Color} {n[i + 2]}".Trim();
                    }

                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] Color: {data.Color}");
                    continue;
                }

                // ── LOT ───────────────────────────────────────────────────────
                // Handles: "LOTAFL", "LOT: AFL", "LIPHF" (OCR noise for LOT)
                // ── LOT ───────────────────────────────────────────────────────
                if ((low.StartsWith("lot") || low.StartsWith("lip") || low.StartsWith("lo:") || low.Contains("lot:"))
                    && !IsPersonName(raw))
                {
                    // Intentar extraer después de LOT:
                    var lotIdx = raw.ToLowerInvariant().IndexOf("lot");
                    if (lotIdx >= 0)
                    {
                        var afterLot = raw.Substring(lotIdx + 3).TrimStart(':', ' ');
                        if (afterLot.Length >= 2 && !IsKnownLabel(afterLot))
                            data.Lot = afterLot;
                    }

                    if (string.IsNullOrWhiteSpace(data.Lot) && raw.Length > 3)
                        data.Lot = raw.Substring(3).TrimStart(':', ' ');

                    if (string.IsNullOrWhiteSpace(data.Lot) && i + 1 < n.Count && !IsNoise(n[i + 1]))
                        data.Lot = n[i + 1];

                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] Lot: {data.Lot}");
                    continue;
                }

                // ── GRAND TOTAL ───────────────────────────────────────────────
                if (low.Contains("rand total") || low.Contains("grand total"))
                {
                    var grandTotalRaw = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(grandTotalRaw) && i + 1 < n.Count)
                        grandTotalRaw = n[i + 1];
                    ParseGrandTotal(grandTotalRaw, data);
                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] GrandTotal raw: {grandTotalRaw}");
                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] TotalRolls:{data.TotalRolls} PesoCrudo:{data.TotalPesoCrudo} PesoAcabado:{data.TotalPesoAcabado} Yards:{data.TotalYards}");
                    continue;
                }

                // ── NUMERO DE ORDEN ───────────────────────────────────────────
                if (low.Contains("numero de orden") || low.Contains("número de orden"))
                {
                    data.OrderNumber = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(data.OrderNumber) && i + 1 < n.Count)
                        data.OrderNumber = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[AzureShipping] OrderNumber: {data.OrderNumber}");
                    continue;
                }
            }

            // ── Post-loop fallbacks ───────────────────────────────────────────

            // Cliente fallback: use FabricaDeLlegada
            if (string.IsNullOrWhiteSpace(data.Cliente) && !string.IsNullOrWhiteSpace(data.FabricaDeLlegada))
            {
                data.Cliente = data.FabricaDeLlegada;
                System.Diagnostics.Debug.WriteLine($"[AzureShipping] Cliente (fallback): {data.Cliente}");
            }

            // Fix: Cliente should not equal Buyer
            if (!string.IsNullOrWhiteSpace(data.Cliente) &&
                data.Cliente.Equals(data.Buyer, StringComparison.OrdinalIgnoreCase))
            {
                data.Cliente = !string.IsNullOrWhiteSpace(data.FabricaDeLlegada)
                    ? data.FabricaDeLlegada
                    : string.Empty;
                System.Diagnostics.Debug.WriteLine($"[AzureShipping] Cliente (dedup fix): {data.Cliente}");
            }
        }

        // ── Parse "56 / 1,355.00 / 1,268.50 / 3.468.0" ──────────────────────
        private void ParseGrandTotal(string raw, ShippingData data)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            var parts = raw.Split('/');
            if (parts.Length >= 1) data.TotalRolls = int.TryParse(parts[0].Trim(), out var r) ? r : 0;
            if (parts.Length >= 2) data.TotalPesoCrudo = ParseDecimalEuropean(parts[1].Trim());
            if (parts.Length >= 3) data.TotalPesoAcabado = ParseDecimalEuropean(parts[2].Trim());
            if (parts.Length >= 4) data.TotalYards = ParseDecimalEuropean(parts[3].Trim());
        }

        private decimal ParseDecimalEuropean(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            value = value.Replace(" ", "");
            int dots = value.Count(c => c == '.');
            int commas = value.Count(c => c == ',');
            if (dots > 1)
            {
                int lastDot = value.LastIndexOf('.');
                value = value[..lastDot].Replace(".", "") + "." + value[(lastDot + 1)..];
            }
            else if (commas == 1 && dots == 1) value = value.Replace(",", "");
            else if (commas == 1 && dots == 0) value = value.Replace(",", "");
            return decimal.TryParse(value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var result) ? result : 0;
        }

        private string FindNextNumeric(List<string> lines, int from, int to)
        {
            for (int i = from; i <= Math.Min(to, lines.Count - 1); i++)
                if (System.Text.RegularExpressions.Regex.IsMatch(lines[i].Trim(), @"^\d{1,4}$"))
                    return lines[i].Trim();
            return string.Empty;
        }

        private bool IsValidValue(string value)
        {
            if (IsNoise(value)) return false;
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (value.All(char.IsDigit)) return false;
            if (IsKnownLabel(value)) return false;
            return true;
        }

        private bool IsKnownLabel(string value)
        {
            var low = value.ToLowerInvariant();
            return KnownLabels.Any(l => low.StartsWith(l));
        }

        private bool IsPersonName(string value)
        {
            if (value.Length < 4 || value.Length > 20) return false;
            if (value.Any(char.IsDigit)) return false;
            var rest = value.Substring(1);
            return rest.Count(char.IsLower) > rest.Length / 2;
        }

        private bool IsNoise(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length < 3) return true;
            if (value.Contains('[') || value.Contains(']')) return true;
            var digits = value.Count(char.IsDigit);
            var letters = value.Count(char.IsLetter);
            if (digits > 3 && letters <= 1) return true;
            return false;
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
    }
}