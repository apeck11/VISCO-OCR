namespace Visco.Ocr.Api.Contracts;

public sealed class CreateJobRequest
{
    public string? FilePath { get; init; }
    public string? FileUrl { get; init; }
    public string? DocumentHint { get; init; }
    public string? CallbackUrl { get; init; }
}
