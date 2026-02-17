namespace Visco.Ocr.Api.Contracts;

public sealed class ParseInvoiceRequest
{
    public string? FilePath { get; init; }
    public string? DocumentHint { get; init; }
}
