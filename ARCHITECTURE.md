# Architecture

How the File Conversion API works - a .NET 8 service that converts Office documents on Windows Server.

## Overview

**What it does:** Converts between 21 different Office document formats using bundled LibreOffice.

**Where it runs:** Windows Server 2016+ with IIS hosting.

**Design philosophy:** Keep it simple, make it stateless, layer the security.

**Deployment model:** Self-contained - works in air-gapped environments with no external dependencies.

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

All services are singletons for better performance.

**Processing Layer:**
- LibreOffice - Most conversions (DOC, DOCX, XLSX, PPTX, etc.)
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
- Fresh process per conversion (isolation)
- 5-minute timeout
- Headless mode (no GUI)
- Automatic cleanup
- Handles 11 conversion types

**PdfService** - PDF operations (4 conversion types)

**SpreadsheetService** - Excel/CSV operations (2 conversion types)

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

**ExceptionHandlingMiddleware** - Catches errors, logs with operation ID, returns clean messages

**IpRateLimitMiddleware** - Prevents abuse (configurable limits)

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

## Conversion Fidelity

**1:1 Principle:** Pure conversions with no content modification.

**Filename preservation:** Original filenames preserved in isolated folders so field codes (FILENAME, DATE, PATH) work correctly.

**Filename sanitization:** Only dangerous characters removed (path traversal, invalid chars, control chars). Spaces, parentheses, version numbers, and international characters preserved.

## Security

### Defense in Depth

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
- Error message sanitization

**Processing Layer:**
- Isolated processes (conversions can't affect each other)
- Timeout controls (5 minutes max)
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

Clients include key in `X-API-Key` header. Disabled by default - enable for production.

### What's Included

- API key authentication (configurable)
- Input validation and sanitization
- Rate limiting
- CORS configuration
- Security headers
- Structured logging with operation IDs
- File system security with auto cleanup
- Process isolation
- No database (no SQL injection risk)
- No HTML rendering (no XSS risk)

### Dependencies

All packages from official NuGet.org:

**Microsoft:**
- Microsoft.AspNetCore.* (8.0.21)
- DocumentFormat.OpenXml (3.0.2)

**Industry Standard:**
- Swashbuckle.AspNetCore (6.6.2) - 1.5B+ downloads
- Serilog.* (8.0.1+) - 500M+ downloads
- iText7 (8.0.2) - Fortune 500 trusted
- NPOI (2.7.0) - 50M+ downloads
- AspNetCoreRateLimit (5.0.0) - 25M+ downloads
- CsvHelper (30.0.1) - 200M+ downloads

**LibreOffice:**
- Open source, Mozilla Public License v2.0
- Bundled and optimized (60-70% smaller than full install)
- Headless mode only, no macros, no network access
- Used by governments worldwide

## Configuration

Configuration loads from:
1. `appsettings.json` - Base settings
2. Environment variables - Overrides
3. Command line arguments - Automation

**Main sections:**
- `Serilog` - Logging configuration
- `FileHandling` - File size, directories, allowed extensions
- `Security` - API keys, CORS origins
- `LibreOffice` - Timeout settings
- `Concurrency` - Max conversions, queue size
- `IpRateLimiting` - Rate limit rules
- `SecurityHeaders` - CSP, XSS protection, etc.

Configuration validated at startup.

## Performance & Scalability

### Resource Usage

**Memory:** 150-500MB per conversion, singleton services reduce overhead

**Disk:** Temp files in isolated directories, auto cleanup after conversion

**CPU:** 10-30% per conversion, configurable concurrency

### Scaling

**Vertical:** More CPUs → increase MaxConcurrentConversions

**Horizontal:** Deploy to multiple servers, load balance with `/health` endpoint, no session affinity needed (stateless)

**Server sizing:**
| Workload | CPU Cores | RAM | Max Concurrent |
|----------|-----------|-----|----------------|
| Light | 2-4 | 8GB | 2 |
| Medium | 4-8 | 16GB | 4 |
| Heavy | 8+ | 32GB | 6-8 |

## Monitoring

### Health Checks

`/health` endpoint returns comprehensive diagnostics:
- Service status (200 OK or 503 unavailable)
- System info (OS, .NET version, CPU count)
- LibreOffice availability
- Uptime and resource usage

Load balancers should check `/health` every 30 seconds, mark unhealthy after 2 consecutive failures.

### Logging

Structured logging with Serilog:
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

**Windows-only:** Simpler deployment (native IIS), corporate standard, better LibreOffice compatibility

**Bundled LibreOffice:** Works in air-gapped environments, no external dependencies, consistent versioning

**Singleton services:** Better performance (resource reuse), easier management, lower overhead

**Stateless:** Horizontal scaling without sticky sessions, simpler deployment, better fault tolerance

**Operation-specific folders:** Complete isolation, preserves filenames, safe concurrent conversions, easy cleanup

**DateTime IDs (not GUIDs):** Shorter (16 vs 36 chars), saves 40 chars per conversion, critical for Windows 260-char path limit, human-readable, sortable

## Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| Process hangs | 5-minute timeout kills process |
| Disk fills up | Size limits, auto cleanup |
| Memory leaks | Process isolation, auto restarts |
| LibreOffice crashes | Process isolation, error handling |
| Malicious files | Validation, process isolation |
| DoS attacks | Rate limiting, concurrency controls |
| Info leaks | Sanitized errors, structured logs |
| Path traversal | Filename checks, isolated directories |

---

This architecture keeps things simple while being secure and scalable. Everything is self-contained, making it perfect for corporate environments and air-gapped deployments.
