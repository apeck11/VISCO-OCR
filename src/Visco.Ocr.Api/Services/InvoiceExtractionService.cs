using System.Globalization;
using System.Text.RegularExpressions;
using Visco.Ocr.Api.Domain;

namespace Visco.Ocr.Api.Services;

public sealed class InvoiceExtractionService
{
    private static readonly Regex InvoiceNumberRegex = new(@"(?:INVOICE\s*NO\.?\s*[:#]?\s*)([A-Z0-9\-]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DateRegex = new(@"(?:DATE\s*[:#]?\s*)([A-Z]{3}\.??\d{1,2}(?:ST|ND|RD|TH)?[,]?\d{4}|\d{4}-\d{2}-\d{2})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TotalRegex = new(@"(?:TOTAL|TOTAL\s+AMOUNT)\s*(?:USD|US\$|\$)?\s*([0-9]{1,3}(?:,[0-9]{3})*(?:\.[0-9]{2})?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<VoucherExtraction> ExtractAsync(OcrJob job, CancellationToken cancellationToken)
    {
        // PR #1 foundation behavior: deterministic stub extraction with light regex enrichment.
        await Task.Delay(100, cancellationToken);

        var sourceText = await TryReadTextAsync(job, cancellationToken);
        var sourceName = job.SourceName;
        var invoiceNumber = ExtractInvoiceNumber(sourceText) ?? ExtractInvoiceNumberFromFileName(sourceName) ?? $"AUTO-{job.JobId.ToString()[..8].ToUpperInvariant()}";
        var invoiceDate = ExtractInvoiceDate(sourceText) ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var totalAmount = ExtractTotalAmount(sourceText) ?? 0m;

        var warnings = new List<string>();
        if (totalAmount <= 0)
        {
            warnings.Add("Total amount not confidently extracted; defaulted to 0.00.");
        }

        var costType = ResolveCostType(job.DocumentHint, sourceName);
        var costEntries = new List<CostEntryExtraction>
        {
            new(costType, totalAmount)
        };

        var confidence = totalAmount > 0 ? 0.85f : 0.65f;

        return new VoucherExtraction(
            BatchId: $"BATCH-{DateTime.UtcNow:yyyyMMddHHmmss}-{job.JobId.ToString()[..6].ToUpperInvariant()}",
            VoucherId: $"VCH-{job.JobId.ToString()[..8].ToUpperInvariant()}",
            CreatedUtc: DateTimeOffset.UtcNow,
            SourceFile: sourceName,
            DocumentType: "Invoice",
            SupplierName: InferSupplier(sourceText) ?? "Unknown Supplier",
            InvoiceNumber: invoiceNumber,
            InvoiceDate: invoiceDate,
            Currency: "USD",
            TotalAmount: totalAmount,
            CostEntries: costEntries,
            Confidence: confidence,
            Warnings: warnings);
    }

    private static async Task<string?> TryReadTextAsync(OcrJob job, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(job.SourcePath) && File.Exists(job.SourcePath))
        {
            var extension = Path.GetExtension(job.SourcePath);
            if (string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase))
            {
                return await File.ReadAllTextAsync(job.SourcePath, cancellationToken);
            }
        }

        return null;
    }

    private static string? ExtractInvoiceNumber(string? text)
        => string.IsNullOrWhiteSpace(text)
            ? null
            : InvoiceNumberRegex.Match(text) is var match && match.Success
                ? match.Groups[1].Value.Trim()
                : null;

    private static string? ExtractInvoiceNumberFromFileName(string sourceName)
    {
        var raw = Path.GetFileNameWithoutExtension(sourceName);
        var digits = new string(raw.Where(char.IsLetterOrDigit).ToArray());
        return digits.Length >= 6 ? digits[..Math.Min(16, digits.Length)].ToUpperInvariant() : null;
    }

    private static string? ExtractInvoiceDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = DateRegex.Match(text);
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups[1].Value.Replace(".", string.Empty, StringComparison.Ordinal).Trim();
        value = Regex.Replace(value, @"(\d{1,2})(ST|ND|RD|TH)", "$1", RegexOptions.IgnoreCase);

        var formats = new[] { "MMM d,yyyy", "MMM d yyyy", "yyyy-MM-dd", "MMM d, yyyy", "MMM d,yyyy" };
        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return DateOnly.FromDateTime(parsed).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return null;
    }

    private static decimal? ExtractTotalAmount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = TotalRegex.Matches(text).LastOrDefault();
        if (match is null || !match.Success)
        {
            return null;
        }

        if (decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return null;
    }

    private static string ResolveCostType(string? documentHint, string sourceName)
    {
        var lookup = $"{documentHint} {sourceName}".ToLowerInvariant();
        if (lookup.Contains("fuel", StringComparison.Ordinal)) return "Fuel Surcharge";
        if (lookup.Contains("storage", StringComparison.Ordinal) || lookup.Contains("warehouse", StringComparison.Ordinal)) return "Storage";
        if (lookup.Contains("accessorial", StringComparison.Ordinal)) return "Accessorial";
        return "Freight";
    }

    private static string? InferSupplier(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var normalizedLines = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Length > 4)
            .Take(10)
            .ToArray();

        return normalizedLines.FirstOrDefault(line => line.Contains("CO", StringComparison.OrdinalIgnoreCase)
            || line.Contains("INC", StringComparison.OrdinalIgnoreCase)
            || line.Contains("LLC", StringComparison.OrdinalIgnoreCase));
    }
}
