namespace Visco.Ocr.Api.Contracts;

public sealed class CreateJobResponse
{
    public required Guid JobId { get; init; }
    public required string Status { get; init; }
    public required string StatusUrl { get; init; }
}
