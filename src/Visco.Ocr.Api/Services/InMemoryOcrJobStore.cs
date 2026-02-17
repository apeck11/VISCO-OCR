using System.Collections.Concurrent;
using Visco.Ocr.Api.Domain;

namespace Visco.Ocr.Api.Services;

public sealed class InMemoryOcrJobStore : IOcrJobStore
{
    private readonly ConcurrentDictionary<Guid, OcrJob> _jobs = new();

    public OcrJob Create(OcrJob job)
    {
        _jobs[job.JobId] = job;
        return job;
    }

    public OcrJob? Get(Guid jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return job;
    }

    public void Update(OcrJob job)
    {
        _jobs[job.JobId] = job;
    }
}
