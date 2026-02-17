# VISCO OCR API (PR #1 Foundation)

This repository includes a .NET 8 minimal API scaffold for inbound vendor invoice OCR jobs.

## What this PR implements
- `POST /ocr/parse` for a **single-step path-based parse** that returns XML immediately (ideal for SQL stored procedure integration).
- `POST /ocr/jobs` to create an async job from `filePath` or `fileUrl`.
- `POST /ocr/jobs/upload` to upload a file directly (`multipart/form-data`).
- `GET /ocr/jobs/{jobId}` to poll status and retrieve XML result.
- In-memory job store + in-memory queue + background worker.
- XML output in `batch > voucher > costEntries > costEntry` shape.

> PR #1 is foundation code. Extraction logic is deterministic and lightweight to establish API contract and integration flow first.

## Project layout
- `src/Visco.Ocr.Api` — .NET 8 API service.
- `docs/visco-ocr-textract-ai-brief.md` — roadmap and schema guidance.

## Where this repo is right now (and how to access it)
- **Here in Codex**: the working copy is in this environment at `/workspace/VISCO-OCR`.
- **On GitHub (or another git host)**: only if you/your org has pushed this branch to a remote repository.

Quick check from your machine/terminal:
```bash
git remote -v
```

Interpretation:
- If you see a GitHub URL, the repo is connected to GitHub and you can clone/pull from there.
- If no remote appears, this copy is local-only right now; push it to GitHub/Azure DevOps to access it from elsewhere.

Example push flow (after creating an empty GitHub repo):
```bash
git remote add origin https://github.com/<org-or-user>/VISCO-OCR.git
git push -u origin <branch-name>
```

---

## How to get these files onto your Windows machine
You are not asking a dumb question — this is the exact right deployment question.

### Option A (recommended): clone with Git
From PowerShell on your Windows 11 machine:
```powershell
git clone <your-repo-url>
cd VISCO-OCR
```

### Option B: download ZIP from your repo host
If this repo is on GitHub/Azure DevOps, use **Download ZIP**, then extract it to a folder like:
`C:\VSCO\VISCO-OCR`

### Option C: copy published output only (for IIS test)
If you just want runnable files (DLLs), publish and copy the `publish` folder contents:
```powershell
dotnet publish .\src\Visco.Ocr.Api\Visco.Ocr.Api.csproj -c Release -o .\publish
```
Then copy everything in `publish` to your IIS app folder.

---

## Run locally
```bash
dotnet run --project src/Visco.Ocr.Api/Visco.Ocr.Api.csproj
```

If you're already inside `src/Visco.Ocr.Api`, run:
```bash
dotnet run
```

Windows PowerShell (from repo root):
```powershell
./scripts/run-local.ps1
```

Windows Command Prompt (from repo root):
```cmd
scripts\run-local.cmd
```

**Troubleshooting `MSB1009: Project file does not exist`**
- Make sure your terminal's current folder is the repository root before using `--project src/Visco.Ocr.Api/Visco.Ocr.Api.csproj`.
- If your current folder is already `src/Visco.Ocr.Api`, use `dotnet run` (without `--project`).
- Verify the file exists with:
  - PowerShell: `Test-Path .\src\Visco.Ocr.Api\Visco.Ocr.Api.csproj`
  - Command Prompt: `dir src\Visco.Ocr.Api\Visco.Ocr.Api.csproj`

Swagger UI:
- `http://localhost:5000/swagger` (or assigned ASP.NET port)

## Endpoints

### 1) Stored procedure-friendly endpoint (recommended for VSCO upload flow)
`POST /ocr/parse`

Request body:
```json
{
  "filePath": "C:\\VISCO\\Uploads\\commercial-invoice-001.pdf",
  "documentHint": "freight invoice"
}
```

Response:
- `Content-Type: application/xml`
- XML payload in `batch > voucher > costEntries > costEntry`

### 2) Async endpoints
- `POST /ocr/jobs`
- `POST /ocr/jobs/upload`
- `GET /ocr/jobs/{jobId}`

## curl examples

### Synchronous parse (best for SQL proc workflow)
```bash
curl -X POST http://localhost:5000/ocr/parse \
  -H "Content-Type: application/json" \
  -d '{
    "filePath": "C:\\Inbound\\invoice-123.pdf",
    "documentHint": "freight invoice"
  }'
```

### Async create job
```bash
curl -X POST http://localhost:5000/ocr/jobs \
  -H "Content-Type: application/json" \
  -d '{
    "filePath": "C:\\Inbound\\invoice-123.pdf",
    "documentHint": "freight invoice"
  }'
```

### Upload
```bash
curl -X POST http://localhost:5000/ocr/jobs/upload \
  -F "file=@./invoice-scan.pdf" \
  -F "documentHint=freight invoice"
```

### Poll
```bash
curl http://localhost:5000/ocr/jobs/{jobId}
```


## Do I get DLLs to copy into IIS?
Yes. This project publishes to a folder containing `Visco.Ocr.Api.dll` and supporting runtime files.

Publish output command:
```bash
dotnet publish src/Visco.Ocr.Api/Visco.Ocr.Api.csproj -c Release -o ./publish
```

Then copy the contents of `./publish` to your IIS application folder.

> ASP.NET Core API projects do **not** require a default `.aspx` page.
> The app is started by IIS via `web.config` + Kestrel integration from the Hosting Bundle.

## Built-in homepage/testing page
This API now includes a lightweight homepage for sanity checks:
- `/` (home)
- `/test` (same test page)

The page lets you submit `filePath` + `documentHint` and calls `POST /ocr/parse`, showing returned XML inline.

## SQL Server stored procedure integration sketch
This pattern mirrors your UI flow: upload writes file, proc calls OCR API with path, XML is parsed into cost-entry tables.

```sql
-- PSEUDO-SQL (adapt to your environment/security standards)
DECLARE @Url NVARCHAR(4000) = 'http://your-ocr-host/ocr/parse';
DECLARE @Body NVARCHAR(MAX) = N'{"filePath":"C:\\VISCO\\Uploads\\inv123.pdf","documentHint":"commercial invoice"}';
DECLARE @Obj INT, @Result NVARCHAR(MAX);

EXEC sp_OACreate 'MSXML2.ServerXMLHTTP', @Obj OUT;
EXEC sp_OAMethod @Obj, 'open', NULL, 'POST', @Url, false;
EXEC sp_OAMethod @Obj, 'setRequestHeader', NULL, 'Content-Type', 'application/json';
EXEC sp_OAMethod @Obj, 'send', NULL, @Body;
EXEC sp_OAGetProperty @Obj, 'responseText', @Result OUT;
EXEC sp_OADestroy @Obj;

-- @Result now contains XML ready for OPENXML/XQuery parsing into cost tables.
SELECT @Result AS OcrXml;
```

## IIS hosting notes (Windows Server 2022)
1. Install .NET 8 Hosting Bundle.
2. Create IIS app/site pointing to published output.
3. Run app pool with identity that can read inbound file locations (UNC/local path).
4. Publish:
   ```bash
   dotnet publish src/Visco.Ocr.Api/Visco.Ocr.Api.csproj -c Release -o ./publish
   ```
5. For production, replace in-memory store/queue with SQL + durable queue.

## Next PRs
- PR #2: integrate OCR engine + preprocessing for PDF/images and XLS/XLSX extraction.
- PR #3: deterministic field mapping and richer invoice parsing tuned to your vendor templates.
- PR #4: confidence scoring refinements and review queue workflow.
