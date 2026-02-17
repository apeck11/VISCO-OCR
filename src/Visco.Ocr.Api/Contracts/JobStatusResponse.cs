namespace Visco.Ocr.Api.Contracts;

public sealed class JobStatusResponse
{
    public required Guid JobId { get; init; }
    public required string Status { get; init; }
    public string? DocumentType { get; init; }
    public float ConfidenceSummary { get; init; }
    public string? XmlResult { get; init; }
    public List<string> Warnings { get; init; } = [];
    public string? Error { get; init; }
    public DateTimeOffset CreatedUtc { get; init; }
    public DateTimeOffset? StartedUtc { get; init; }
    public DateTimeOffset? CompletedUtc { get; init; }
}
