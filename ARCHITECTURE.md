# Architecture

File Conversion API - a .NET 8 service that converts Office documents.

## Overview

**What it does:** Converts Office document formats using bundled LibreOffice.

**Where it runs:** Windows Server 2016+ with IIS hosting.

**Deployment model:** Self-contained with no external dependencies.

## How It Works

```
Client sends file
    ↓
Security checks (API keys, rate limits, file validation)
    ↓
API layer (REST endpoints)
    ↓
Business logic (routes to right converter)
    ↓
Conversion (LibreOffice, iText7, OpenXml, or NPOI)
    ↓
Converted file sent back
```

### The Layers

**Security Layer:**

- API key authentication (optional but recommended)
- Rate limiting per IP (10 conversions/min, 30 requests/min)
- File type and size validation
- CORS control
- Security headers

**API Layer:**

- ConversionController - File uploads and conversions
- HealthController - Service health for load balancers
- Swagger/OpenAPI documentation

**Service Layer:**

- DocumentService - Routes to appropriate converter
- LibreOfficeService - LibreOffice lifecycle management
- LibreOfficeProcessManager - Process execution
- PdfService - PDF operations (iText7)
- SpreadsheetService - Excel/CSV (NPOI)
- InputValidator - File and conversion validation
- SemaphoreService - Concurrency control

**Processing Layer:**

- LibreOffice - Most conversions (DOC, DOCX, XLSX, PPTX, etc.); also hop 1 of DOC/DOCX -> HTML
- Bundled Node PDF -> HTML engine - hop 2 of DOC/DOCX -> HTML (renders the intermediate PDF into self-contained HTML with inline base64 PNG page images)
- iText7 - PDF operations
- DocumentFormat.OpenXml - DOCX handling
- NPOI - Excel processing

Each conversion runs in an isolated process with timeout.

## Key Components

### Controllers

**ConversionController** handles file conversions:

1. Validates file (type, size, extension)
2. Creates unique folder for isolation
3. Saves file with original name (preserves field codes)
4. Waits for conversion slot
5. Routes to appropriate service
6. Returns converted file
7. Cleans up temporary files

**HealthController** provides health checks:

- Reports LibreOffice availability
- Returns system info, uptime, resource usage
- Used by load balancers

### Core Services

**DocumentService** - Routes conversions to appropriate service (LibreOffice, iText7, OpenXml, NPOI)

**LibreOfficeService & LibreOfficeProcessManager:**

- 5-minute timeout
- Headless mode (no GUI)
- Automatic cleanup

**PdfService** - PDF operations

**SpreadsheetService** - Excel/CSV operations

**DocxToHtmlPipeline & NodeEngineProcessManager:**

- Composes LibreOffice (DOC/DOCX -> PDF) with the bundled Node engine (PDF -> HTML)
- Intermediate PDF and per-conversion LibreOffice profile dir cleaned up in `finally`
- Output HTML is self-contained: every page image inlined as `data:image/png;base64,...`, no `file://` references, no external network references
- Bundled `node.exe` resolved from `<app>/engine/node/node.exe` with a two-gate path check (name match + containment under `<app>/engine`) to defeat sibling-prefix attacks

**NodeEnginePathResolver** - Validates the bundled `node.exe` path (or honors a configured override)

**SemaphoreService** - Limits concurrent conversions (default: 2)

**InputValidator:**

- File extension whitelist
- MIME type verification
- File size limits (default 50MB)
- Conversion path validation
- Path traversal prevention

### Middleware

**ApiKeyMiddleware** - Validates X-API-Key header (configurable, off by default)

**SecurityMiddleware** - Adds security headers (CSP, X-Frame-Options, etc.)

**ExceptionHandlingMiddleware** - Error handling and logging with operation ID

**IpRateLimitMiddleware** - Prevents abuse (configurable)

## Data Flow

1. Client uploads file via POST to `/api/convert`
2. API key check (if enabled)
3. Rate limit check
4. File validation (extension, size, MIME type)
5. Create unique folder for isolation
6. Save file with original filename
7. Wait for conversion slot
8. Convert using appropriate service
9. Read result into memory
10. Clean up temporary files
11. Release slot
12. Return file to client

If anything fails, cleanup happens automatically.

## Security

**Network Layer:**

- Optional API key authentication
- Rate limiting (prevents DoS)
- CORS (controls browser access)

**Application Layer:**

- File type whitelist
- File size limits (default 50MB)
- MIME type validation
- Path traversal prevention
- Timeout enforcement

**Processing Layer:**

- Isolated processes
- Timeout controls
- Unique directories per request
- Automatic cleanup
- Concurrency limits

### API Key Authentication

Built-in support via configuration:

```json
{
  "Security": {
    "RequireApiKey": true,
    "ApiKeys": ["apikey_live_your_key_here"]
  }
}
```

Clients include key in `X-API-Key` header. Disabled by default.

### Dependencies

All NuGet packages restored from nuget.org. The full pinned set lives in `FileConversionApi.csproj`; the headline runtime dependencies are:

- `DocumentFormat.OpenXml` (Microsoft): DOCX parsing and emit.
- `Swashbuckle.AspNetCore`: OpenAPI/Swagger surface for `/api-docs`.
- `Serilog.AspNetCore` plus console, file, and Windows Event Log sinks.
- `iText7`: PDF parse/emit.
- `NPOI`: XLSX read/write.
- `AspNetCoreRateLimit`: per-IP request quotas.
- `CsvHelper`: CSV read/write.

**LibreOffice:**

- Open source, Mozilla Public License v2.0.
- Bundled as a self-contained subset of the upstream install so the service ships without
  requiring LibreOffice on the host. The bundling script (`bundle-libreoffice.ps1`) drops
  components the service never invokes (Base, Math, examples, locale packs beyond what is
  needed); the remaining footprint is roughly half the size of the full install.
- Headless mode only; no macros; no network access from the soffice.exe child process.

## Configuration

Configuration loads from:

1. `appsettings.json` - Base settings
2. Environment variables - Overrides
3. Command line arguments - Automation

Per-section binding via `IOptions<T>` (FileHandlingConfig, SecurityConfig, ConcurrencyConfig, LibreOfficeConfig, NodeEngineConfig). No additional validation runs at startup today.

## Performance

### Resource Usage

**Memory:** 150-500MB per conversion, singleton services

**Disk:** Temp files in isolated directories, auto cleanup

**CPU:** 10-30% per conversion, configurable concurrency

## Monitoring

### Health Checks

`/health` returns:

- Service status (200 OK or 503 unavailable)
- System info (OS, .NET version, CPU count)
- LibreOffice availability
- Uptime and resource usage

### Logging

Logging with Serilog:

- Operation IDs track requests end-to-end
- Context (file sizes, formats, timings)
- Multiple outputs (console, file, Windows Event Log)
- Configurable verbosity
- Daily rotation, 30-day retention

**Log levels:**

- Information - Conversion requests, results, timings
- Warning - Fallbacks, cleanup failures
- Error - Conversion failures, exceptions

**Example:**

```
2025-10-25 14:30:15 [INF] Conversion completed - Input: docx, Target: pdf, Size: 524KB, Time: 3421ms, Success: true
```

## Design Decisions

**Windows-only.** The deploy target is IIS on Windows Server. ASP.NET Core on IIS via the
ASP.NET Core Module is well-understood enterprise infrastructure, and the bundled LibreOffice
binary set is Windows-only.

**Bundled LibreOffice.** Eliminates the host-side LibreOffice install as a deployment
prerequisite. The trade-off is bundle size (around 500 MB) for predictability of which exact
LibreOffice version converts which exact file.

**Singleton services.** The conversion services are stateless after construction (the
per-conversion state lives in `App_Data/temp/{operationId}/`), so the cheaper singleton
lifetime fits. The semaphore and config bindings are the only fields that hold state.

**Per-operation directories.** Each request gets isolated `uploads/{operationId}/` and
`converted/{operationId}/` subdirectories. Preserving the original filename matters because
LibreOffice's `{FILENAME}` field code expands at conversion time; renaming the upload to a
guid breaks documents that reference their own filename.

**20-character operation IDs.** UTC timestamp (`yyMMddHHmmssffff`) plus 4 hex characters of
`Random.Shared` entropy. UTC because IDs surface in `X-Operation-Id` response headers; the
hex suffix closes the same-tick collision window left open by the 0.1ms timestamp resolution
when two requests reach the controller within the same tick.
