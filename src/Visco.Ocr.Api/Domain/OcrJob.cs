namespace Visco.Ocr.Api.Domain;

public sealed class OcrJob
{
    public required Guid JobId { get; init; }
    public required string SourceName { get; init; }
    public string? SourcePath { get; init; }
    public string? SourceUrl { get; init; }
    public string? DocumentHint { get; init; }
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedUtc { get; private set; }
    public DateTimeOffset? CompletedUtc { get; private set; }
    public OcrJobStatus Status { get; private set; } = OcrJobStatus.Queued;
    public string? Error { get; private set; }
    public string? XmlResult { get; private set; }
    public float Confidence { get; private set; }
    public List<string> Warnings { get; } = [];

    public void MarkProcessing()
    {
        Status = OcrJobStatus.Processing;
        StartedUtc = DateTimeOffset.UtcNow;
    }

    public void MarkCompleted(string xmlResult, float confidence, IEnumerable<string>? warnings = null)
    {
        Status = confidence < 0.80f ? OcrJobStatus.NeedsReview : OcrJobStatus.Completed;
        CompletedUtc = DateTimeOffset.UtcNow;
        XmlResult = xmlResult;
        Confidence = confidence;

        if (warnings is not null)
        {
            Warnings.AddRange(warnings);
        }
    }

    public void MarkFailed(string error)
    {
        Status = OcrJobStatus.Failed;
        CompletedUtc = DateTimeOffset.UtcNow;
        Error = error;
    }
}
