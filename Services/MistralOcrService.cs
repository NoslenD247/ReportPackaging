using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ReportPackaging.Services
{
    public class MistralOcrService
    {
        private readonly string _apiKey;
        private static readonly HttpClient _httpClient = new HttpClient();

        private const string STRUCTURING_PROMPT = @"Se te da una imagen de un documento junto con el texto extraído por OCR de esa misma imagen.

Tu tarea es hacer una TRANSCRIPCIÓN LITERAL del documento a JSON, como un copy-paste exacto.

PASO 1 - ANTES de escribir el JSON, analiza la tabla en la imagen:
- Cuenta TODAS las columnas del encabezado de la tabla, de izquierda a derecha, incluyendo la primera y la última.
- Escríbelas mentalmente en orden. Típicamente la última columna es 'Total' - NO LA OMITAS.

PASO 2 - Genera el JSON con estas reglas ESTRICTAS:
- USA el texto OCR como fuente de los valores. La imagen te sirve para entender la ESTRUCTURA y POSICIÓN.
- Si hay tablas, cada fila es un objeto PLANO. Cada columna es una key directa. NO anides en sub-objetos.
- El ORDEN de las keys debe ser EXACTAMENTE de IZQUIERDA a DERECHA como en la tabla de la imagen.
- Cada fila DEBE tener EXACTAMENTE el mismo número de keys que columnas tiene la tabla. Si una celda está vacía, usa null.
- TODAS las filas deben aparecer (incluyendo TOTAL, ACUMULADO, BALANCE como filas del mismo array).
- Preserva los valores y nombres de columnas EXACTAMENTE como aparecen. No renombres, no traduzcas, no reordenes.
- NO INVENTES datos. Campo vacío = null.

PASO 3 - VERIFICA antes de responder:
- ¿Cada fila tiene el mismo número de keys? Si no, te falta una columna.
- ¿La primera y última columna de la tabla están presentes? Revisa los bordes.

Responde SOLO con JSON válido.";

        public MistralOcrService(string apiKey)
        {
            _apiKey = apiKey;
        }

        public async Task<ReportData> AnalyzeReportAsync(Stream imageStream)
        {
            try
            {
                // 1. Convertir imagen a base64
                using var memoryStream = new MemoryStream();
                await imageStream.CopyToAsync(memoryStream);
                var base64Image = Convert.ToBase64String(memoryStream.ToArray());
                var dataUri = $"data:image/jpeg;base64,{base64Image}";

                // 2. PASO 1: OCR para extraer texto fiel
                System.Diagnostics.Debug.WriteLine("[OCR] Extrayendo texto con mistral-ocr-latest...");
                var ocrText = await ExtractOcrText(dataUri);
                System.Diagnostics.Debug.WriteLine($"[OCR] Extraídos {ocrText.Length} caracteres");

                // 3. PASO 2: Vision ve imagen + texto OCR para estructurar
                System.Diagnostics.Debug.WriteLine("[VISION] Estructurando con pixtral-large-latest...");
                var rawJson = await ExtractWithVision(dataUri, ocrText);
                System.Diagnostics.Debug.WriteLine($"[VISION] Respuesta: {rawJson?[..Math.Min(500, rawJson.Length)]}");

                // 4. Parsear resultado
                var reportData = new ReportData
                {
                    RawMarkdown = ocrText,
                    Rows = ParseRows(rawJson ?? "")
                };

                System.Diagnostics.Debug.WriteLine($"[MistralOCR] Filas extraídas: {reportData.Rows.Count}");
                return reportData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MistralOCR] Excepción: {ex.Message}");
                throw;
            }
        }

        // PASO 1: OCR puro — extrae el texto de la imagen
        private async Task<string> ExtractOcrText(string dataUri)
        {
            try
            {
                var requestBody = new
                {
                    model = "mistral-ocr-latest",
                    document = new
                    {
                        type = "image_url",
                        image_url = dataUri
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _apiKey);

                var response = await _httpClient.PostAsync(
                    "https://api.mistral.ai/v1/ocr", content);

                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[OCR] Error: {responseJson}");
                    return string.Empty;
                }

                // Concatenar markdown de todas las páginas
                using var doc = JsonDocument.Parse(responseJson);
                var sb = new StringBuilder();
                foreach (var page in doc.RootElement.GetProperty("pages").EnumerateArray())
                {
                    if (page.TryGetProperty("markdown", out var md))
                        sb.AppendLine(md.GetString());
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OCR] Excepción: {ex.Message}");
                return string.Empty;
            }
        }

        // PASO 2: Vision — ve imagen + texto OCR y devuelve JSON estructurado
        private async Task<string?> ExtractWithVision(string dataUri, string ocrText)
        {
            var fullPrompt = $"{STRUCTURING_PROMPT}\n\n--- TEXTO OCR EXTRAÍDO ---\n{ocrText}";

            var requestBody = new
            {
                model = "pixtral-large-latest",
                max_tokens = 4000,
                response_format = new { type = "json_object" },
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = fullPrompt },
                            new { type = "image_url", image_url = new { url = dataUri } }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.PostAsync(
                "https://api.mistral.ai/v1/chat/completions", content);

            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[VISION] Error: {responseJson}");
                throw new Exception($"Mistral Vision falló: {response.StatusCode} - {responseJson}");
            }

            using var doc = JsonDocument.Parse(responseJson);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
        }

        // Parsear el JSON que devuelve Vision y mapear a ReportRow
        private List<ReportRow> ParseRows(string rawJson)
        {
            var rows = new List<ReportRow>();
            if (string.IsNullOrWhiteSpace(rawJson)) return rows;

            try
            {
                rawJson = rawJson.Trim();
                rawJson = Regex.Replace(rawJson, @"```json|```", "").Trim();

                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                // Buscar el array de filas — puede estar en cualquier key
                JsonElement? arrayEl = null;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    arrayEl = root;
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    // Buscar la primera propiedad que sea array
                    foreach (var prop in root.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            arrayEl = prop.Value;
                            System.Diagnostics.Debug.WriteLine($"[PARSE] Usando array de key: '{prop.Name}'");
                            break;
                        }
                    }
                }

                if (arrayEl == null)
                {
                    System.Diagnostics.Debug.WriteLine("[PARSE] No se encontró array en el JSON");
                    return rows;
                }

                foreach (var item in arrayEl.Value.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;

                    var row = new ReportRow();

                    // Mapear por nombre de columna — flexible con mayúsculas/minúsculas
                    foreach (var prop in item.EnumerateObject())
                    {
                        var key = prop.Name.ToLowerInvariant().Trim();
                        switch (key)
                        {
                            case "fecha": row.Fecha = GetString(prop.Value); break;
                            case "codigo": case "código": case "cod": row.Codigo = GetString(prop.Value); break;
                            case "buyer": row.Buyer = GetString(prop.Value); break;
                            case "style": case "stile": row.Style = GetString(prop.Value); break;
                            case "po#": case "po": row.PO = GetString(prop.Value); break;
                            case "color": row.Color = GetString(prop.Value); break;
                            case "orden": row.Orden = GetString(prop.Value); break;
                            case "xs": row.XS = GetDecimal(prop.Value); break;
                            case "s": row.S = GetDecimal(prop.Value); break;
                            case "m": row.M = GetDecimal(prop.Value); break;
                            case "l": row.L = GetDecimal(prop.Value); break;
                            case "xl": row.XL = GetDecimal(prop.Value); break;
                            case "2xl": case "xl2": row.XL2 = GetDecimal(prop.Value); break;
                            case "3xl": case "xl3": row.XL3 = GetDecimal(prop.Value); break;
                            case "total hoy": case "totalhoy": row.TotalHoy = GetDecimal(prop.Value); break;
                            case "acumulado": row.Acumulado = GetDecimal(prop.Value); break;
                            case "balance": row.Balance = GetDecimal(prop.Value); break;
                        }
                    }
                    rows.Add(row);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PARSE] Error: {ex.Message}");
            }

            return rows;
        }

        private string GetString(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Null) return string.Empty;
            if (el.ValueKind == JsonValueKind.String) return el.GetString() ?? string.Empty;
            return el.ToString();
        }

        private decimal GetDecimal(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Number) return el.GetDecimal();
            if (el.ValueKind == JsonValueKind.String)
            {
                var s = Regex.Replace(el.GetString() ?? "", @"[^\d\.,]", "").Replace(",", ".");
                return decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
            }
            return 0;
        }
    }
}
