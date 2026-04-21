using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;

namespace ReportPackaging.Services
{
    /// <summary>
    /// OCR Service for "REPORTE DE EMPAQUE" document.
    /// Each document can have multiple PO sections, each with dynamic size columns.
    /// </summary>
    public class AzurePackingReportService
    {
        private readonly string _endpoint;
        private readonly string _apiKey;

        public AzurePackingReportService(string endpoint, string apiKey)
        {
            _endpoint = endpoint;
            _apiKey = apiKey;
        }

        public async Task<PackingReportData> AnalyzeAsync(Stream imageStream)
        {
            try
            {
                var credential = new AzureKeyCredential(_apiKey);
                var client = new DocumentAnalysisClient(new Uri(_endpoint), credential);

                System.Diagnostics.Debug.WriteLine("[PackingOCR] Analyzing...");

                var operation = await client.AnalyzeDocumentAsync(
                    WaitUntil.Completed, "prebuilt-layout", imageStream);

                var result = operation.Value;
                var reportData = new PackingReportData();

                if (result.Pages?.Count > 0)
                {
                    var lines = result.Pages
                        .SelectMany(p => p.Lines)
                        .Select(l => l.Content.Trim())
                        .ToList();

                    foreach (var l in lines)
                        System.Diagnostics.Debug.WriteLine($"  LINE: '{l}'");

                    ExtractFromLines(lines, reportData);
                }

                System.Diagnostics.Debug.WriteLine($"[PackingOCR] Sections extracted: {reportData.Sections.Count}");
                return reportData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PackingOCR] Error: {ex.Message}");
                throw;
            }
        }

        private void ExtractFromLines(List<string> lines, PackingReportData data)
        {
            var n = lines.Select(l => l.Trim()).ToList();

            // ── Global header ─────────────────────────────────────────────────
            for (int i = 0; i < n.Count; i++)
            {
                var low = n[i].ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(data.FabricaName) && i == 0)
                {
                    data.FabricaName = n[i];
                    continue;
                }

                if (low.StartsWith("fecha") && string.IsNullOrWhiteSpace(data.Fecha))
                {
                    var afterColon = AfterColon(n[i]);
                    data.Fecha = !string.IsNullOrWhiteSpace(afterColon)
                        ? afterColon
                        : (i + 1 < n.Count ? n[i + 1] : string.Empty);
                    continue;
                }
            }

            // ── Split into sections by P.O. ───────────────────────────────────
            var sectionStartIndices = new List<int>();
            for (int i = 0; i < n.Count; i++)
            {
                if (n[i].ToLowerInvariant().StartsWith("p.o"))
                    sectionStartIndices.Add(i);
            }

            for (int s = 0; s < sectionStartIndices.Count; s++)
            {
                int from = sectionStartIndices[s];
                int to = s + 1 < sectionStartIndices.Count
                    ? sectionStartIndices[s + 1]
                    : n.Count;

                var sectionLines = n.Skip(from).Take(to - from).ToList();
                var section = ParseSection(sectionLines);
                if (section != null)
                    data.Sections.Add(section);
            }
        }

        private PackingSection? ParseSection(List<string> lines)
        {
            var section = new PackingSection();
            var n = lines.Select(l => l.Trim()).ToList();

            for (int i = 0; i < n.Count; i++)
            {
                var raw = n[i];
                var low = raw.ToLowerInvariant();

                // P.O
                if (low.StartsWith("p.o") && string.IsNullOrWhiteSpace(section.PO))
                {
                    section.PO = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(section.PO) && i + 1 < n.Count)
                        section.PO = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[PackingOCR] PO: {section.PO}");
                    continue;
                }

                // STYLE
                if (low.StartsWith("style") && string.IsNullOrWhiteSpace(section.Style))
                {
                    section.Style = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(section.Style) && i + 1 < n.Count)
                        section.Style = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[PackingOCR] Style: {section.Style}");
                    continue;
                }

                // FILE
                if (low.StartsWith("file") && string.IsNullOrWhiteSpace(section.File))
                {
                    section.File = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(section.File) && i + 1 < n.Count)
                        section.File = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[PackingOCR] File: {section.File}");
                    continue;
                }

                // CLIENT
                if (low.StartsWith("client") && string.IsNullOrWhiteSpace(section.Client))
                {
                    section.Client = AfterColon(raw);
                    if (string.IsNullOrWhiteSpace(section.Client) && i + 1 < n.Count)
                        section.Client = n[i + 1];
                    System.Diagnostics.Debug.WriteLine($"[PackingOCR] Client: {section.Client}");
                    continue;
                }

                // Detect size headers row: "Color Orden XS S M L XL 2XL TOTAL"
                // The row that contains "color" and "orden" and "total" marks the header
                if (low.StartsWith("color") || low.StartsWith("orden"))
                {
                    // Find the full header row — collect consecutive non-data lines
                    // Headers: Color, Orden, [sizes...], TOTAL
                    var headerCols = new List<string>();
                    int headerStart = i;

                    // Step back to find "Color" if we're on "Orden"
                    if (low == "orden" && i > 0 && n[i - 1].ToLowerInvariant() == "color")
                        headerStart = i - 1;
                    else if (low == "color")
                        headerStart = i;

                    // Collect headers until we hit "TOTAL"
                    for (int j = headerStart; j < n.Count; j++)
                    {
                        headerCols.Add(n[j]);
                        if (n[j].ToLowerInvariant() == "total") break;
                    }

                    section.SizeHeaders = headerCols
                        .Where(h => !h.ToLowerInvariant().StartsWith("color") &&
                                    !h.ToLowerInvariant().StartsWith("orden"))
                        .ToList();

                    System.Diagnostics.Debug.WriteLine($"[PackingOCR] Size headers: {string.Join(", ", section.SizeHeaders)}");

                    // Data rows start after headers
                    // The next lines are: Color value, then quantities, then total
                    int dataStart = headerStart + headerCols.Count;

                    // Parse data rows — each row: color name + N quantities + total
                    int totalCols = section.SizeHeaders.Count; // includes TOTAL
                    int sizeCols = totalCols - 1;             // excludes TOTAL
                    int colsPerRow = 1 + sizeCols + 1;          // color + sizes + total

                    int j2 = dataStart;
                    while (j2 < n.Count)
                    {
                        // First value is color name
                        var colorName = n[j2];
                        if (string.IsNullOrWhiteSpace(colorName) || IsKnownLabel(colorName))
                        { j2++; continue; }

                        var row = new PackingRow();
                        row.Color = colorName;
                        row.Orden = string.Empty;

                        j2++;

                        // Next values are quantities (sizeCols) + total
                        var sizeNames = section.SizeHeaders.Take(sizeCols).ToList();
                        var quantities = new List<string>();

                        for (int k = 0; k < sizeCols && j2 < n.Count; k++, j2++)
                            quantities.Add(n[j2]);

                        // Total
                        if (j2 < n.Count)
                        {
                            row.Total = ParseDecimal(n[j2]);
                            j2++;
                        }

                        // Map sizes to quantities
                        for (int k = 0; k < sizeNames.Count && k < quantities.Count; k++)
                        {
                            row.Sizes[sizeNames[k]] = ParseDecimal(quantities[k]);
                        }

                        System.Diagnostics.Debug.WriteLine(
                            $"[PackingOCR] Row — Color:{row.Color} Total:{row.Total} Sizes:{string.Join("|", row.Sizes.Select(kv => $"{kv.Key}={kv.Value}"))}");

                        section.Rows.Add(row);
                    }

                    break; // Done with this section
                }
            }

            return string.IsNullOrWhiteSpace(section.PO) ? null : section;
        }

        private bool IsKnownLabel(string value)
        {
            var low = value.ToLowerInvariant();
            return low.StartsWith("p.o") || low.StartsWith("style") ||
                   low.StartsWith("file") || low.StartsWith("client") ||
                   low.StartsWith("color") || low.StartsWith("orden") ||
                   low.StartsWith("total") || low.StartsWith("reporte") ||
                   low.StartsWith("fecha");
        }

        private string AfterColon(string line)
        {
            var idx = line.IndexOf(':');
            return idx >= 0 ? line[(idx + 1)..].Trim() : string.Empty;
        }

        private decimal ParseDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            value = System.Text.RegularExpressions.Regex
                .Replace(value, @"[^\d\.,]", "").Replace(",", "");
            return decimal.TryParse(value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var result) ? result : 0;
        }
    }

    // ── Data Models ───────────────────────────────────────────────────────────

    public class PackingReportData
    {
        public string FabricaName { get; set; } = string.Empty;
        public string Fecha { get; set; } = string.Empty;
        public List<PackingSection> Sections { get; set; } = new();
    }

    public class PackingSection
    {
        public string PO { get; set; } = string.Empty;
        public string Style { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
        public string Client { get; set; } = string.Empty;

        // Dynamic size column names e.g. ["XXS","XS","S","M","L","XL","2XL","TOTAL"]
        public List<string> SizeHeaders { get; set; } = new();

        public List<PackingRow> Rows { get; set; } = new();
    }

    public class PackingRow
    {
        public string Color { get; set; } = string.Empty;
        public string Orden { get; set; } = string.Empty;
        public decimal Total { get; set; } = 0;

        // Dynamic: key = size name, value = quantity
        public Dictionary<string, decimal> Sizes { get; set; } = new();
    }
}