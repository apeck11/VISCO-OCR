using Visco.Ocr.Api.Domain;

namespace Visco.Ocr.Api.Services;

public sealed class OcrJobProcessor(
    ILogger<OcrJobProcessor> logger,
    IOcrJobQueue queue,
    IOcrJobStore store,
    InvoiceExtractionService extractionService,
    XmlBatchRenderer xmlBatchRenderer) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OCR job processor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobId = await queue.DequeueAsync(stoppingToken);
                await ProcessJobAsync(jobId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception in OCR job worker loop.");
            }
        }

        logger.LogInformation("OCR job processor stopped.");
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = store.Get(jobId);
        if (job is null)
        {
            logger.LogWarning("Job {JobId} not found in store.", jobId);
            return;
        }

        try
        {
            job.MarkProcessing();
            store.Update(job);

            var extraction = await extractionService.ExtractAsync(job, cancellationToken);
            var xml = xmlBatchRenderer.RenderSingleVoucherBatch(extraction);

            job.MarkCompleted(xml, extraction.Confidence, extraction.Warnings);
            store.Update(job);

            logger.LogInformation("Processed OCR job {JobId} with status {Status}.", jobId, job.Status);
        }
        catch (Exception ex)
        {
            job.MarkFailed(ex.Message);
            store.Update(job);
            logger.LogError(ex, "Failed processing OCR job {JobId}", jobId);
        }
    }
}
