using Microsoft.AspNetCore.Mvc;
using Visco.Ocr.Api.Contracts;
using Visco.Ocr.Api.Domain;
using Visco.Ocr.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IOcrJobStore, InMemoryOcrJobStore>();
builder.Services.AddSingleton<IOcrJobQueue, InMemoryOcrJobQueue>();
builder.Services.AddSingleton<InvoiceExtractionService>();
builder.Services.AddSingleton<XmlBatchRenderer>();
builder.Services.AddHostedService<OcrJobProcessor>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/ocr/parse", async (
    [FromBody] ParseInvoiceRequest request,
    InvoiceExtractionService extractionService,
    XmlBatchRenderer xmlBatchRenderer,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.FilePath))
    {
        return Results.BadRequest(new { message = "filePath is required." });
    }

    if (!File.Exists(request.FilePath))
    {
        return Results.BadRequest(new { message = $"filePath does not exist: {request.FilePath}" });
    }

    var extension = Path.GetExtension(request.FilePath);
    if (!IsSupportedExtension(extension))
    {
        return Results.BadRequest(new { message = $"Unsupported file extension '{extension}'. Allowed: .pdf, .xls, .xlsx, .txt" });
    }

    var transientJob = new OcrJob
    {
        JobId = Guid.NewGuid(),
        SourceName = Path.GetFileName(request.FilePath),
        SourcePath = request.FilePath,
        DocumentHint = request.DocumentHint
    };

    var extraction = await extractionService.ExtractAsync(transientJob, cancellationToken);
    var xml = xmlBatchRenderer.RenderSingleVoucherBatch(extraction);

    return Results.Content(xml, "application/xml");
})
.WithName("ParseInvoicePath")
.WithSummary("Synchronously parse a single invoice path and return XML (stored procedure friendly)");

app.MapPost("/ocr/jobs", async (
    [FromBody] CreateJobRequest request,
    HttpContext http,
    IOcrJobStore store,
    IOcrJobQueue queue) =>
{
    if (string.IsNullOrWhiteSpace(request.FilePath) && string.IsNullOrWhiteSpace(request.FileUrl))
    {
        return Results.BadRequest(new { message = "Either filePath or fileUrl is required." });
    }

    if (!string.IsNullOrWhiteSpace(request.FilePath) && !File.Exists(request.FilePath))
    {
        return Results.BadRequest(new { message = $"filePath does not exist: {request.FilePath}" });
    }

    var sourceName = !string.IsNullOrWhiteSpace(request.FilePath)
        ? Path.GetFileName(request.FilePath)
        : TryGetRemoteName(request.FileUrl) ?? "remote-document";

    var job = store.Create(new OcrJob
    {
        JobId = Guid.NewGuid(),
        SourceName = sourceName,
        SourcePath = request.FilePath,
        SourceUrl = request.FileUrl,
        DocumentHint = request.DocumentHint
    });

    await queue.QueueAsync(job.JobId);

    var response = new CreateJobResponse
    {
        JobId = job.JobId,
        Status = "queued",
        StatusUrl = $"{http.Request.Scheme}://{http.Request.Host}/ocr/jobs/{job.JobId}"
    };

    return Results.Accepted($"/ocr/jobs/{job.JobId}", response);
})
.WithName("CreateOcrJob")
.WithSummary("Create an OCR job by file path or URL");

app.MapPost("/ocr/jobs/upload", async (
    HttpRequest request,
    HttpContext http,
    IOcrJobStore store,
    IOcrJobQueue queue) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { message = "Content-Type must be multipart/form-data" });
    }

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { message = "Multipart form field 'file' is required." });
    }

    var documentHint = form["documentHint"].FirstOrDefault();

    var root = Path.Combine(AppContext.BaseDirectory, "App_Data", "inbound");
    Directory.CreateDirectory(root);

    var safeExt = Path.GetExtension(file.FileName);
    var stagedName = $"{Guid.NewGuid():N}{safeExt}";
    var stagedPath = Path.Combine(root, stagedName);

    await using (var stream = File.Create(stagedPath))
    {
        await file.CopyToAsync(stream);
    }

    var job = store.Create(new OcrJob
    {
        JobId = Guid.NewGuid(),
        SourceName = file.FileName,
        SourcePath = stagedPath,
        DocumentHint = documentHint
    });

    await queue.QueueAsync(job.JobId);

    var response = new CreateJobResponse
    {
        JobId = job.JobId,
        Status = "queued",
        StatusUrl = $"{http.Request.Scheme}://{http.Request.Host}/ocr/jobs/{job.JobId}"
    };

    return Results.Accepted($"/ocr/jobs/{job.JobId}", response);
})
.WithName("UploadOcrJob")
.WithSummary("Create an OCR job by uploading a file");

app.MapGet("/ocr/jobs/{jobId:guid}", ([FromRoute] Guid jobId, IOcrJobStore store) =>
{
    var job = store.Get(jobId);
    if (job is null)
    {
        return Results.NotFound(new { message = "Job not found" });
    }

    var response = new JobStatusResponse
    {
        JobId = job.JobId,
        Status = ToApiStatus(job.Status),
        DocumentType = "Invoice",
        ConfidenceSummary = job.Confidence,
        XmlResult = job.XmlResult,
        Warnings = job.Warnings,
        Error = job.Error,
        CreatedUtc = job.CreatedUtc,
        StartedUtc = job.StartedUtc,
        CompletedUtc = job.CompletedUtc
    };

    return Results.Ok(response);
})
.WithName("GetOcrJob")
.WithSummary("Get OCR job status and XML output");

app.MapGet("/", () => Results.Content(GetHomePageHtml(), "text/html"));
app.MapGet("/test", () => Results.Content(GetHomePageHtml(), "text/html"));

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

static string GetHomePageHtml() => """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <title>VISCO OCR API</title>
  <style>
    body { font-family: Arial, sans-serif; margin: 2rem; max-width: 900px; }
    .card { border: 1px solid #ccc; border-radius: 8px; padding: 1rem; margin-bottom: 1rem; }
    label { display: block; font-weight: 600; margin-top: 0.75rem; }
    input { width: 100%; padding: 0.5rem; }
    button { margin-top: 1rem; padding: 0.6rem 1rem; }
    pre { white-space: pre-wrap; background: #f7f7f7; padding: 1rem; border-radius: 6px; }
  </style>
</head>
<body>
  <h1>VISCO OCR API</h1>
  <p>This service is running. For endpoint docs, open <a href="/swagger">Swagger UI</a>.</p>

  <div class="card">
    <h2>Quick Test: POST /ocr/parse</h2>
    <p>Use a server-accessible file path (for example a UNC path readable by the IIS app pool identity).</p>

    <label for="filePath">filePath</label>
    <input id="filePath" value="C:\\VISCO\\Uploads\\invoice-123.pdf" />

    <label for="documentHint">documentHint (optional)</label>
    <input id="documentHint" value="commercial invoice" />

    <button onclick="runParse()">Run Parse</button>
    <pre id="output">Output will appear here.</pre>
  </div>

  <script>
    async function runParse() {
      const output = document.getElementById('output');
      output.textContent = 'Calling /ocr/parse...';

      const body = {
        filePath: document.getElementById('filePath').value,
        documentHint: document.getElementById('documentHint').value
      };

      try {
        const response = await fetch('/ocr/parse', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(body)
        });

        const text = await response.text();
        output.textContent = response.ok
          ? text
          : `ERROR ${response.status}: ${text}`;
      } catch (err) {
        output.textContent = `Request failed: ${err}`;
      }
    }
  </script>
</body>
</html>
""";

static bool IsSupportedExtension(string extension)
{
    var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".xls", ".xlsx", ".txt"
    };

    return supported.Contains(extension);
}

static string? TryGetRemoteName(string? fileUrl)
{
    if (string.IsNullOrWhiteSpace(fileUrl))
    {
        return null;
    }

    return Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri)
        ? uri.Segments.LastOrDefault()?.Trim('/')
        : null;
}

static string ToApiStatus(OcrJobStatus status) => status switch
{
    OcrJobStatus.Queued => "queued",
    OcrJobStatus.Processing => "processing",
    OcrJobStatus.Completed => "completed",
    OcrJobStatus.Failed => "failed",
    OcrJobStatus.NeedsReview => "needs_review",
    _ => "queued"
};
