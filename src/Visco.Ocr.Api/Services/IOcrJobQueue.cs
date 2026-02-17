namespace Visco.Ocr.Api.Services;

public interface IOcrJobQueue
{
    ValueTask QueueAsync(Guid jobId, CancellationToken cancellationToken = default);
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);
}
