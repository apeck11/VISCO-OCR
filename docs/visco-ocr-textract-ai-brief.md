# VISCO OCR + AI Interpretation Brief (Self-Hosted First)

## Executive answer: Textract vs from scratch
If your top priority is **control and supportability on VISCO-owned servers**, then yes: start from scratch with a self-hosted OCR stack.

A practical recommendation is:
- Build a self-hosted OCR pipeline first (Tesseract + layout extraction + VISCO mapping).
- Keep a cloud OCR provider as an optional fallback adapter later.

This gives you architectural control now without locking you out of managed services later.

## Decision framework (quick)

Choose **self-hosted first** when:
- You want everything deployed on your IIS/AWS server environment.
- You need predictable operational ownership by the VISCO team.
- You want to avoid per-page vendor API dependencies and external routing.

Choose **managed OCR first** when:
- You need fastest possible time-to-value for many doc types immediately.
- You want vendor-managed model improvements and minimal ML/OCR tuning effort.
- You can tolerate external service dependency and pay-per-page economics.

## Recommended phased plan (self-hosted)

### Phase 0 — Scope and quality targets (1–2 weeks)
- Finalize document families: invoice, BOL, POD, checks, custom forms.
- Define acceptance metrics:
  - field-level accuracy,
  - line-item accuracy,
  - straight-through processing,
  - latency per page.
- Build a labeled benchmark set (at least 100 documents per major family).

### Phase 1 — OCR service foundation (2–4 weeks)
- Build API endpoints for:
  - `POST /ocr/jobs` (upload or URL ingest),
  - `GET /ocr/jobs/{jobId}` (status/result).
- Store files in object storage (S3 or local blob abstraction), then process asynchronously.
- OCR baseline stack:
  - PDF/image preprocessing (deskew, denoise, contrast normalization),
  - text extraction via Tesseract,
  - basic structure segmentation (page, block, line, token).
- Persist raw OCR output + normalized intermediate JSON.

### Phase 2 — VISCO deterministic parser (2–6 weeks)
- Implement document classifier (invoice/BOL/POD/etc.) using layout + keyword cues.
- Build rules engine for canonical VISCO schema mapping:
  - header fields (`invoiceNumber`, `invoiceDate`, `shipper`, `consignee`),
  - totals and currencies,
  - line items and charges.
- Add confidence scoring per field and per document.

### Phase 3 — AI interpretation layer (3–8 weeks)
- Add an LLM only for ambiguity resolution and normalization.
- Inputs:
  - OCR text + token coordinates,
  - deterministic candidates and scores,
  - VISCO schema constraints.
- Output strict JSON that must pass schema validation.
- Convert validated JSON to XML via deterministic serializer.

### Phase 4 — Human review workflow (2–4 weeks)
- Route low-confidence docs to exception queue.
- Provide correction UI for operators.
- Record corrections as training signals for:
  - rules tuning,
  - prompt exemplars,
  - optional classifier retraining.

### Phase 5 — Hardening and scale (ongoing)
- Add idempotency keys, retries, dead-letter queues.
- Add observability (latency, throughput, confidence drift).
- Add security controls (encryption at rest, PII masking, role-based access).

## Self-hosted reference architecture

1. **Ingress API** (IIS-hosted .NET service)
   - Accept upload or source URL.
   - Validate file type/size; create `jobId`.
2. **Storage layer**
   - Store source document and metadata.
3. **Processing worker**
   - Preprocess pages (OpenCV/Pillow).
   - OCR extraction (Tesseract).
   - Layout + table heuristics (e.g., pdfplumber/camelot/tabula as needed).
4. **VISCO mapping engine**
   - Deterministic extraction rules to canonical schema.
5. **AI interpretation service**
   - Resolve ambiguous mappings only.
6. **XML renderer**
   - Deterministically emit VISCO XML.
7. **Review queue + audit trail**
   - Store confidence, warnings, and human overrides.

## Minimal API contract

### POST /ocr/jobs
Request:
- `file` (multipart) or `fileUrl`
- optional `documentHint`
- optional `callbackUrl`

Response:
- `jobId`
- `status` (`queued`)
- `statusUrl`

### GET /ocr/jobs/{jobId}
Response:
- `status` (`queued|processing|completed|failed|needs_review`)
- `documentType`
- `confidenceSummary`
- `xmlResult` (when complete)
- `warnings[]`

## What “from scratch” really means (honest trade-offs)

### Benefits
- Full control over deployment, retention, and security posture.
- Easier to align behavior to VISCO-specific document rules.
- No direct dependency on a single OCR vendor API.

### Costs/risks
- More engineering effort up front (preprocessing, tables, edge cases).
- Ongoing model/rules maintenance is your responsibility.
- Accuracy ramp-up may be slower than managed services initially.

## Practical implementation stack (starter)
- API/Orchestration: .NET 8 Web API + background worker.
- OCR: Tesseract (self-hosted).
- Image preprocessing: OpenCV.
- PDF parsing: pdfplumber / PyMuPDF (for hybrid text + OCR strategy).
- Rules/mapping: VISCO-owned deterministic mapping library.
- AI step: LLM with strict JSON schema output and validation.
- XML output: deterministic serializer (no model-generated XML).

## Hybrid fallback pattern (recommended)
Even if you start self-hosted, design a provider interface now:
- `IOcrProvider.Extract(document) -> OcrResult`

Then you can plug in:
- `SelfHostedTesseractProvider` (default)
- `CloudProviderAdapter` (optional fallback for hard documents)

This keeps your current preference (self-hosted) while preserving optionality.

## Customer-facing talk track
- “We’re building OCR in a VISCO-controlled stack for supportability and data control.”
- “Interpretation quality comes from VISCO business rules plus AI for ambiguous fields.”
- “Low-confidence documents are routed to review, and corrections continuously improve accuracy.”
- “The architecture stays API-first and can add provider fallback without major redesign.”

## Final recommendation
Given your preference, start from scratch with a self-hosted OCR pipeline and a strong deterministic VISCO mapping layer. Add AI interpretation as a second stage, and keep a cloud OCR adapter as an optional fallback—not a core dependency.

## What’s next (execution plan starting now)

### Next 48 hours (alignment + kickoff)
- Lock scope for v1 document types (pick 1–2 only, e.g., invoice + BOL).
- Approve canonical XML contract for those document types.
- Name owners:
  - API/orchestration,
  - OCR/preprocessing,
  - VISCO mapping/rules,
  - QA dataset and evaluation.
- Define go/no-go metrics for v1:
  - Header field accuracy target,
  - Line-item accuracy target,
  - Maximum acceptable average processing time.

### Week 1 deliverables
- Repo structure and service skeleton:
  - `POST /ocr/jobs`, `GET /ocr/jobs/{jobId}`,
  - async worker pipeline,
  - storage abstraction.
- Baseline OCR worker running with Tesseract on sample docs.
- First pass normalized JSON schema emitted from OCR output.
- Test corpus assembled (at least 100 docs per selected type) with labeled truth set.

### Week 2 deliverables
- Deterministic mapping for top-priority header fields.
- Deterministic XML renderer producing VISCO-valid output.
- Confidence scoring + `needs_review` routing behavior.
- First benchmark report vs defined KPIs.

### Weeks 3–4 deliverables
- Line-item extraction and table handling improvements.
- Exception queue workflow and correction capture.
- AI ambiguity-resolution prototype behind feature flag.
- Regression suite for extraction accuracy on labeled corpus.

## First backlog (ordered)
1. Define canonical schema + XML examples for invoice and BOL.
2. Implement job orchestration + status model.
3. Build preprocessing pipeline (deskew/denoise/contrast).
4. Integrate Tesseract extraction.
5. Build deterministic rules for core header fields.
6. Add XML serializer + schema validation.
7. Add confidence model and review queue routing.
8. Add operator correction logging.
9. Add AI interpretation step for unresolved/low-confidence fields.
10. Add optional cloud provider adapter only for fallback cases.

## Definition of Done for v1
- API can ingest files and return completed XML through job polling.
- Accuracy goals are met on labeled v1 corpus.
- All low-confidence documents are routed to review (no silent bad mappings).
- Full audit trail exists from source file -> OCR output -> mapped fields -> XML.
- Operational dashboard shows throughput, latency, and error rates.

## Risks to manage now
- **Scope creep**: limit v1 to 1–2 document types.
- **Table complexity**: expect extra iteration on line-item extraction.
- **Data quality**: poor scans can dominate error rates without preprocessing.
- **Rule drift**: add regression tests before expanding field rules.

## Recommendation for your immediate next meeting
Use this concise plan:
1. Confirm v1 document types and success metrics.
2. Commit to 2-week baseline milestone (OCR + deterministic XML).
3. Position AI as phase-2 enhancement for ambiguous cases, not phase-1 dependency.
4. Confirm human-review workflow is part of launch quality strategy.

## Input formats and ingestion rules for your use case
For your expected vendor mix (freight forwarders, shippers, warehouses, material suppliers), support these input types in v1:
- PDF (native or scanned)
- XLS/XLSX (line-item spreadsheets)

Recommended ingest contract:
- One uploaded file == one source bill/invoice document.
- API accepts a batch of files and creates one voucher output per source file.
- Unsupported formats are rejected early with explicit validation errors.

## Proposed XML schema shape (batch -> voucher -> cost entries)

### Conceptual hierarchy
- `batch` (top level)
  - metadata about the upload/job
  - one or more `voucher` elements
- `voucher` (one per uploaded bill/invoice)
  - supplier + document-level fields
  - one or more `costEntry` elements
- `costEntry`
  - summarized cost type and amount (plus optional supporting fields)

### Example XML (starter contract)
```xml
<batch id="BATCH-20260216-001" createdUtc="2026-02-16T18:30:00Z" sourceSystem="VISCO-OCR">
  <voucher voucherId="VCH-0001" sourceFile="acme-freight-invoice-88421.pdf" documentType="Invoice">
    <supplierName>ACME Freight Forwarding LLC</supplierName>
    <invoiceNumber>88421</invoiceNumber>
    <invoiceDate>2026-02-01</invoiceDate>
    <currency>USD</currency>
    <totalAmount>2450.00</totalAmount>

    <costEntries>
      <costEntry>
        <costType>Freight</costType>
        <amount>1800.00</amount>
      </costEntry>
      <costEntry>
        <costType>Fuel Surcharge</costType>
        <amount>350.00</amount>
      </costEntry>
      <costEntry>
        <costType>Warehouse Handling</costType>
        <amount>300.00</amount>
      </costEntry>
    </costEntries>
  </voucher>

  <voucher voucherId="VCH-0002" sourceFile="north-yard-storage-feb.xlsx" documentType="Bill">
    <supplierName>North Yard Storage</supplierName>
    <invoiceNumber>NY-21977</invoiceNumber>
    <invoiceDate>2026-02-05</invoiceDate>
    <currency>USD</currency>
    <totalAmount>975.00</totalAmount>

    <costEntries>
      <costEntry>
        <costType>Storage</costType>
        <amount>825.00</amount>
      </costEntry>
      <costEntry>
        <costType>Accessorial</costType>
        <amount>150.00</amount>
      </costEntry>
    </costEntries>
  </voucher>
</batch>
```

## Field mapping guidance for cost entries
Normalize supplier labels into a VISCO cost taxonomy so reporting is consistent:
- "Line Haul", "Main Freight" -> `Freight`
- "FSC", "Fuel Adj" -> `Fuel Surcharge`
- "WHSE", "Handling" -> `Warehouse Handling`
- "Storage Fee" -> `Storage`
- "Accessorial", "Misc Charge" -> `Accessorial`

Parsing rule recommendation:
- Keep original source label in audit metadata.
- Emit normalized `costType` into XML.
- Route to `needs_review` when cost type normalization confidence is below threshold.

## Next step with your sample invoices
If you provide sample PDFs and XLS/XLSX files, the immediate implementation sequence is:
1. Create a first-pass mapping dictionary for supplier-specific cost labels.
2. Build 10-15 gold-standard XML outputs by hand (ground truth).
3. Run extraction against those files and measure per-field + per-cost-entry accuracy.
4. Lock v1 schema and begin API implementation with confidence thresholds.

## PR #1 adjustment for VSCO upload procedure flow
Given the VSCO UI + stored procedure integration, add a simple synchronous endpoint in addition to async jobs:

### POST /ocr/parse
Request:
- `filePath` (required)
- optional `documentHint`

Response:
- `application/xml`
- single `batch` payload containing one `voucher` derived from the uploaded file path.

Why this helps:
- Stored procedure can call one endpoint and immediately receive XML.
- No polling needed for initial integration.
- Keeps your existing upload workflow straightforward while async endpoints remain available for larger-volume processing.

### Optional lightweight test interface
For operations/support teams, expose a simple built-in test page at `/` (and `/test`) that:
- accepts `filePath` and optional `documentHint`,
- calls `POST /ocr/parse`,
- displays returned XML inline for quick validation.

This keeps the production contract API-first while giving non-developers an easy smoke-test tool in IIS.
