namespace Visco.Ocr.Api.Domain;

public enum OcrJobStatus
{
    Queued,
    Processing,
    Completed,
    Failed,
    NeedsReview
}
