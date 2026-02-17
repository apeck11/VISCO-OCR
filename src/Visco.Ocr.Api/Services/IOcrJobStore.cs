using Visco.Ocr.Api.Domain;

namespace Visco.Ocr.Api.Services;

public interface IOcrJobStore
{
    OcrJob Create(OcrJob job);
    OcrJob? Get(Guid jobId);
    void Update(OcrJob job);
}
