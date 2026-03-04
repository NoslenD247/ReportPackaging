using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
//using Metal;
using System.Text.RegularExpressions;

namespace Voucher.Services
{
    //Clase para el detalle de cada linea de la factura 
    public class DetectedItem
    {
        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }

    }

    //Clase principal que tiene la cabezera y la LISTA de los items

    public class InvoiceData
    {
        public DateTime? InvoiceDate { get; set; }

        public string InvoiceNumber { get; set; } = string.Empty;
        public string VendorName { get; set; } = string.Empty;

        //lista para guardar todos los items que se detecten
        public List<DetectedItem> LineItems { get; set; } = new List<DetectedItem>();

        public decimal TotalAmount { get; set; }


        public string Content { get; set; }


    }



    public class AzureOcrService
    {
        private readonly string _endpoint;
        private readonly string _apiKey;

        public AzureOcrService(string endpoint, string apiKey)
        {
            _endpoint = endpoint;
            _apiKey = apiKey;

        }


        public async Task<InvoiceData> AnalyzeInvoiceAsync(Stream imageStream)
        {

            try
            {
                var credential = new AzureKeyCredential(_apiKey);
                var client = new DocumentAnalysisClient(new Uri(_endpoint), credential);

                //modelo prebuilt-invoice peude detectar multilples idiomas
                // var operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-invoice", imageStream);
                var operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-invoice", memoryStream);
                var result = operation.Value;

                var invoiceData = new InvoiceData();
                if (result.Documents.Count > 0)
                {
                    var document = result.Documents[0];


                    invoiceData.Content = result.Content;

                    //La cabecera , los datos comunes para todos los items
                    //Fecha 
                    if (document.Fields.TryGetValue("InvoiceDate", out var dataField) && dataField.FieldType == DocumentFieldType.Date)
                    {
                        var d = dataField.Value.AsDate();
                        invoiceData.InvoiceDate = new DateTime(d.Year, d.Month, d.Day);

                    }

                    //# factura
                    if (document.Fields.TryGetValue("InvoiceId", out var idField))
                    {
                        invoiceData.InvoiceNumber = idField.Value.AsString();

                    }
                    //El vendedor
                    if (document.Fields.TryGetValue("VendorName", out var vendorField))
                    {
                        invoiceData.VendorName = vendorField.Value.AsString();
                    }

                    //Total global de la factura
                    if (document.Fields.TryGetValue("InvoiceTotal", out var totalField) && totalField.FieldType == DocumentFieldType.Currency)
                    {
                        invoiceData.TotalAmount = (decimal)totalField.Value.AsCurrency().Amount;

                    }

                    //Los items , se recorre la lista de manera completa

                    if (document.Fields.TryGetValue("Items", out var itemsField) && itemsField.FieldType == DocumentFieldType.List)
                    {
                        var items = itemsField.Value.AsList();

                        foreach (var item in items)
                        {

                            if (item.FieldType == DocumentFieldType.Dictionary)
                            {
                                var fields = item.Value.AsDictionary();
                                var newItem = new DetectedItem();


                                //Descripcion 
                                if (fields.TryGetValue("Description", out var descField))
                                {
                                    //Limpieza 
                                    newItem.Description = descField.Value.AsString().Replace("\n", " ");
                                }

                                //Cantidad
                                if (fields.TryGetValue("Quantity", out var qtyField) && qtyField.FieldType == DocumentFieldType.Double)
                                {
                                    newItem.Quantity = (decimal)qtyField.Value.AsDouble();
                                }

                                //Monto total 
                                if (fields.TryGetValue("Amount", out var amtField) && amtField.FieldType == DocumentFieldType.Currency)
                                {
                                    newItem.TotalAmount = (decimal)amtField.Value.AsCurrency().Amount;
                                }


                                //Precio unitario 
                                if (fields.TryGetValue("UnitPrice", out var priceField) && priceField.FieldType == DocumentFieldType.Currency)
                                {
                                    newItem.UnitPrice = (decimal)priceField.Value.AsCurrency().Amount;
                                }

                                //Logica de correccion 
                                if (newItem.UnitPrice == 0 && newItem.TotalAmount > 0 && newItem.Quantity > 0)
                                {
                                    newItem.UnitPrice = newItem.TotalAmount / newItem.Quantity;

                                }
                                else if (newItem.TotalAmount == 0 && newItem.UnitPrice > 0)
                                {
                                    newItem.TotalAmount = newItem.UnitPrice * newItem.Quantity;
                                }

                                //POR SI EL MONTO ES MNEOR A  0
                                if (newItem.TotalAmount > 0)
                                {
                                    invoiceData.LineItems.Add(newItem);

                                }

                            }


                        }

                    }


                }
                return invoiceData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OCR error: {ex.Message}");
                throw;


            }
        }

    }
}
