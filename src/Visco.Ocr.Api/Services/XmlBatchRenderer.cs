using System.Xml.Linq;

namespace Visco.Ocr.Api.Services;

public sealed class XmlBatchRenderer
{
    public string RenderSingleVoucherBatch(VoucherExtraction voucher)
    {
        var batch = new XElement("batch",
            new XAttribute("id", voucher.BatchId),
            new XAttribute("createdUtc", voucher.CreatedUtc.ToString("O")),
            new XAttribute("sourceSystem", "VISCO-OCR"),
            new XElement("voucher",
                new XAttribute("voucherId", voucher.VoucherId),
                new XAttribute("sourceFile", voucher.SourceFile),
                new XAttribute("documentType", voucher.DocumentType),
                new XElement("supplierName", voucher.SupplierName),
                new XElement("invoiceNumber", voucher.InvoiceNumber),
                new XElement("invoiceDate", voucher.InvoiceDate),
                new XElement("currency", voucher.Currency),
                new XElement("totalAmount", voucher.TotalAmount.ToString("0.00")),
                new XElement("costEntries",
                    voucher.CostEntries.Select(entry =>
                        new XElement("costEntry",
                            new XElement("costType", entry.CostType),
                            new XElement("amount", entry.Amount.ToString("0.00"))))))));

        return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), batch).ToString();
    }
}

public sealed record VoucherExtraction(
    string BatchId,
    string VoucherId,
    DateTimeOffset CreatedUtc,
    string SourceFile,
    string DocumentType,
    string SupplierName,
    string InvoiceNumber,
    string InvoiceDate,
    string Currency,
    decimal TotalAmount,
    IReadOnlyCollection<CostEntryExtraction> CostEntries,
    float Confidence,
    IReadOnlyCollection<string> Warnings);

public sealed record CostEntryExtraction(string CostType, decimal Amount);
