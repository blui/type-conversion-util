# Architecture

How the File Conversion API works - a .NET 8 service that converts Office documents on Windows Server.

## Overview

**What it does:** Converts between 21 different Office document formats using a bundled copy of LibreOffice.

**Where it runs:** Windows Server 2016+ with IIS hosting.

**Design philosophy:** Keep it simple, make it stateless, layer the security.

**Deployment model:** Self-contained - works in air-gapped environments with no external dependencies.

## How It Works

```
Client sends file
    ↓
Security checks (rate limits, file validation)
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

**Security Layer** (First line of defense)

- Rate limiting per IP (10 conversions/min, 30 general requests/min)
- File type and size validation
- CORS control for browser access
- Security headers on every response

**API Layer** (What clients talk to)

- `ConversionController` - Handles file uploads and conversion requests
- `HealthController` - Reports service health for load balancers
- Swagger/OpenAPI docs for interactive testing
- Standard ASP.NET Core middleware

**Service Layer** (The brains)

- `DocumentService` - Figures out which converter to use
- `LibreOfficeService` - Handles LibreOffice lifecycle
- `LibreOfficeProcessManager` - Manages LibreOffice process execution
- `PdfService` - PDF operations using iText7
- `SpreadsheetService` - Excel/CSV with NPOI
- `InputValidator` - Checks files are safe and supported
- `SemaphoreService` - Prevents too many conversions at once

All services are singletons (single instance shared across requests) for better performance.

**Processing Layer** (Does the actual work)

- **LibreOffice** - Handles most formats (DOC, DOCX, XLSX, PPTX, ODT, etc.)
- **iText7** - Creates and extracts text from PDFs
- **DocumentFormat.OpenXml** - Works with DOCX files directly
- **NPOI** - Reads and writes Excel files

Each conversion runs in its own isolated process with a timeout to prevent hangs.

## Key Components

### Controllers

**ConversionController**

The main endpoint for file conversions. Here's what it does:

1. Validates your file (type, size, extension)
2. Creates a unique folder for this conversion (keeps files isolated)
3. Saves the file with its original name (important for field codes like {FILENAME})
4. Waits for a free conversion slot (respects concurrency limits)
5. Routes to the right conversion service
6. Returns the converted file or an error
7. Cleans up all temporary files

**HealthController**

Simple health checks for monitoring:

- Basic check: Is LibreOffice available? Is the service running?
- Detailed check: System info, uptime, resource usage
- Used by load balancers to know if this server is healthy

### Core Services

**DocumentService**

The traffic director for conversions:

- Looks at what you want to convert (PDF to DOCX? DOC to PDF?)
- Picks the right service to handle it
- Routes to specialized services (LibreOffice, iText7, OpenXml, NPOI)
- Handles errors gracefully

**LibreOfficeService & LibreOfficeProcessManager**

Manages the LibreOffice integration:

- Starts a fresh LibreOffice process for each conversion (isolation!)
- Sets a 5-minute timeout (prevents hung conversions)
- Runs headless (no GUI, no user interaction)
- Cleans up after itself
- Supports 11 different conversion types

**PdfService**

PDF operations using iText7:

- Create PDFs from plain text
- Extract text from PDFs
- Basic PDF manipulation
- Handles 4 conversion types

**SpreadsheetService**

Excel and CSV operations using NPOI:

- Convert Excel to CSV (exports data cleanly)
- Convert CSV to Excel (imports data)
- Handles multi-sheet workbooks

**SemaphoreService**

Prevents overload:

- Limits how many conversions run at once (default: 2)
- Queues requests when at capacity
- Simple semaphore-based concurrency control

**InputValidator**

Multi-layer security checks:

- File extension whitelist (only known types)
- MIME type verification
- File size limits (default 50MB)
- Conversion path validation
- Path traversal prevention

### Utilities

**UniqueIdGenerator**

Creates short, unique identifiers for operations:

- Format: `yyMMddHHmmssffff` (16 characters)
- Example: `2510270058301234` = Oct 27, 2025 at 00:58:30.1234
- **Why we use this:** GUIDs are 36 characters, these are 16 - saves 40 characters per conversion
- **Why it matters:** Windows has a 260-character path limit, shorter IDs keep us safe
- Human-readable (good for debugging)
- Chronologically sortable (good for logs)
- Low collision risk with only 2 concurrent conversions

**FileSystemHelper**

File system utilities:

- Creates directories safely
- Generates temporary file paths
- Sanitizes paths for security

### Middleware

**SecurityMiddleware**

Adds security headers to responses:

- Content-Security-Policy
- X-Frame-Options
- X-Content-Type-Options
- X-XSS-Protection
- Referrer-Policy

**ExceptionHandlingMiddleware**

Catches errors before they escape:

- Logs all exceptions with operation ID
- Returns clean error messages (no stack traces to clients)
- Standard HTTP status codes
- Tracking ID for support

**IpRateLimitMiddleware** (from AspNetCoreRateLimit)

Prevents abuse:

- 30 requests/minute general limit
- 10 requests/minute for conversions
- Tracks per IP address
- Fully configurable

## Data Flow

Here's what happens when you convert a file:

1. **Client uploads** file via POST to `/api/convert`
2. **Rate limit check** - Have you made too many requests?
3. **File validation** - Valid extension? Acceptable size? Safe MIME type?
4. **Create unique folder** - Isolated directory for this conversion
5. **Save file** - Keep the exact original filename
6. **Wait for slot** - Queue if too many conversions running
7. **Convert** - Run the appropriate conversion service
8. **Read result** - Load converted file into memory
9. **Clean up** - Delete all temporary files
10. **Release slot** - Let the next request proceed
11. **Return file** - Stream to client or return error

If anything goes wrong at any step, we clean up and return a helpful error message.

## Conversion Fidelity

### The 1:1 Principle

We do **pure 1:1 conversions** - the output is exactly what's in the input:

- No content scrubbing
- No metadata removal
- No formatting changes (except DOCX preprocessing, which is optional)
- Field codes work correctly (FILENAME, DATE, PATH all evaluate properly)

### How We Preserve Filenames

To make field codes work right, we preserve the original filename:

```
App_Data/temp/
├── uploads/2510270058301234/
│   └── My Report v2.0 (Final).doc  ← Original name kept
└── converted/2510270058301234/
    └── My Report v2.0 (Final).pdf  ← Same name, new extension
```

**Filename cleanup:** We only remove dangerous characters:
- Path traversal (`../`, `..\`)
- Invalid file characters (`<`, `>`, `|`, `:`, `*`, `?`, `"`)
- Control characters and null bytes

**What we keep:**
- Spaces, parentheses, brackets
- Version numbers
- Dates and timestamps
- International characters

This way, when a DOCX has `{FILENAME}` in the header, the PDF shows "My Report v2.0 (Final)" not "document" or some generic name.

## Security

### Defense in Depth

We layer security controls at every level:

**Network Layer:**
- Rate limiting (prevents DoS and abuse)
- CORS (controls browser access)

**Application Layer:**
- File type whitelist (only allowed extensions)
- File size limits (default 50MB, configurable)
- MIME type checks
- Path traversal prevention
- Timeout enforcement
- Error message sanitization (no info leakage)

**Processing Layer:**
- Isolated processes (one conversion can't affect another)
- Timeout controls (max 5 minutes per conversion)
- Unique directories per request (complete file isolation)
- Automatic cleanup
- Concurrency limits (prevents resource exhaustion)

### What's Included

- Input validation and sanitization
- Rate limiting
- CORS configuration
- Security headers
- Structured logging with operation IDs
- File system security with auto cleanup
- Process isolation
- No database (= no SQL injection risk)
- No HTML rendering (= no XSS risk)

### What's Not Included (By Design)

These are better handled at the infrastructure level:

- Authentication - Deploy behind VPN/firewall or add API keys
- HTTPS enforcement - Configure in IIS or load balancer
- User sessions - Stateless design doesn't need them

### Dependencies

All packages from official NuGet.org:

**Microsoft (Zero Risk):**
- Microsoft.AspNetCore.* (8.0.21)
- DocumentFormat.OpenXml (3.0.2)
- Microsoft.Extensions.* (8.0.0)

**Industry Standard (Low Risk):**
- Swashbuckle.AspNetCore (6.6.2) - 1.5B+ downloads
- Serilog.* (8.0.1+) - 500M+ downloads
- CsvHelper (30.0.1) - 200M+ downloads

**Commercial/Established (Low Risk):**
- iText7 (8.0.2) - Fortune 500 companies use this
- NPOI (2.7.0) - 50M+ downloads
- AspNetCoreRateLimit (5.0.0) - 25M+ downloads

**Community (Medium Risk - Vetted):**
- PdfSharpCore (1.3.62) - 5M+ downloads
- SharpZipLib (1.4.2) - 200M+ downloads, around since 2000

### LibreOffice Security

**What we bundle:**
- LibreOffice itself (open source)
- Mozilla Public License v2.0
- From The Document Foundation (non-profit)

**Security measures:**
- Headless mode only (no GUI, no user interaction)
- No macros enabled
- No network access
- Isolated processes
- Automatic cleanup
- Timeout controls
- Optimized bundle (60-70% smaller than full install)

**What we removed from the bundle:**
- Python runtime (used for macros)
- UI components and wizards
- Gallery, templates, samples
- Auto-correction dictionaries
- Extensions
- Non-English language packs

**Trust:** Used by governments worldwide (Germany, France, Italy, UK). 20+ years of open-source development. Active security patches. No telemetry or phone-home behavior in headless mode.

## Tech Stack

### Core

- **.NET 8 / ASP.NET Core** - Web framework with IIS hosting
- **C# 12** - Language features
- **Windows-only** - Designed for Windows Server + IIS

### Conversion Libraries

- **LibreOffice** - Most format conversions (23 paths)
- **iText7** - PDF operations (4 paths)
- **DocumentFormat.OpenXml** - DOCX handling (4 paths)
- **NPOI** - Excel processing (1 path)

### Supporting Libraries

- **Serilog** - Structured logging
- **AspNetCoreRateLimit** - IP-based rate limiting
- **Swashbuckle.AspNetCore** - API documentation
- **CsvHelper** - CSV parsing

### Infrastructure

- **IIS** - Production hosting
- **Kestrel** - Development server
- **Windows Event Log** - System logging

## Configuration

Configuration loads in this order:

1. `appsettings.json` - Base settings
2. Environment variables - Override specific values
3. Command line arguments - Deployment automation

**Main configuration sections:**

- `Serilog` - Logging settings
- `FileHandling` - File size, temp directories, allowed types
- `Security` - CORS, API keys
- `LibreOffice` - Path, timeout
- `Concurrency` - Max conversions, queue size
- `IpRateLimiting` - Rate limit rules
- `SecurityHeaders` - CSP, XSS protection, etc.

Configuration is validated at startup - if something's wrong, you'll know immediately.

## Performance & Scalability

### Startup

1. Load and validate configuration
2. Set up dependency injection
3. Register all services (singletons for performance)
4. Configure logging
5. Check LibreOffice is available
6. Start accepting requests

### Shutdown

1. Stop accepting new requests
2. Finish any in-progress conversions (graceful)
3. Clean up temp files
4. Flush logs
5. Stop

### Resource Usage

**Memory:**
- 150-500MB per conversion
- Singleton services reduce overhead
- Automatic garbage collection
- Isolated processes prevent leaks

**Disk:**
- Temp files in isolated directories
- Auto cleanup after conversion
- 24-hour retention for failed conversions
- Log rotation (30-day retention)

**CPU:**
- 10-30% per conversion
- Configurable concurrency limits
- Isolated processes
- Thread pool tuning available

### Scaling Options

**Vertical (Bigger Server):**
- More CPUs → increase `MaxConcurrentConversions`
- More RAM → handle more concurrent conversions
- Faster disk → better throughput

**Horizontal (More Servers):**
- Deploy to multiple servers
- Use load balancer with `/health` endpoint
- No session affinity needed (stateless)
- Linear scaling

**Server sizing guide:**

| Workload | CPU Cores | RAM | Max Concurrent |
|----------|-----------|-----|----------------|
| Light | 2-4 | 8GB | 2 |
| Medium | 4-8 | 16GB | 4 |
| Heavy | 8+ | 32GB | 6-8 |

## Monitoring

### Health Checks

- `/health` - Comprehensive health check with diagnostics (200 OK or 503 unavailable)
- Returns system info, service status, uptime, and resource usage

**For load balancers:**
- Check `/health` every 30 seconds
- Mark unhealthy after 2 consecutive failures

### Logging

**Structured logging with Serilog:**
- Operation IDs track requests end-to-end
- Context (file sizes, formats, timings)
- Multiple outputs (console, file, Windows Event Log)
- Configurable verbosity per namespace
- Daily rotation, 30-day retention

**Log levels:**
- **Information** - Conversion requests, results, timings
- **Warning** - Fallbacks, cleanup failures
- **Error** - Conversion failures, exceptions
- **Debug** - Detailed execution (development only)

**Example log entry:**
```
2025-10-25 14:30:15 [INF] Conversion completed - Input: docx, Target: pdf, Size: 524KB, Time: 3421ms, Success: true
```

### Metrics

Available in logs and health endpoints:

- Success/failure rates
- Processing times by format
- Concurrent operations count
- Queue depth
- Resource usage
- LibreOffice availability

## Design Decisions

### Why Windows-only?

- Simpler deployment (native IIS)
- Corporate standard for many companies
- Better LibreOffice compatibility on Windows
- Windows-specific features (Event Log, NTFS permissions)

### Why bundle LibreOffice?

- Works in air-gapped environments
- No external dependencies
- Same version everywhere
- No Microsoft Office licenses needed
- Best format support available

### Why singleton services?

- Better performance (reuse resources)
- Easier resource management
- Simplified state management
- Lower memory overhead

### Why stateless?

- Horizontal scaling without sticky sessions
- Simpler to deploy and maintain
- Better fault tolerance
- No session store needed

### Why operation-specific folders?

- Complete file isolation per request
- Preserves original filenames (field codes work)
- Safe with concurrent conversions
- Easy cleanup (delete whole folder)
- Better debugging (files grouped by operation)

### Why DateTime IDs instead of GUIDs?

- Shorter (16 vs 36 characters = 20 char savings)
- Saves 40 characters per conversion (2 IDs)
- Critical for Windows 260-character path limit
- Human-readable (easier debugging)
- Sortable (better for logs)
- Low collision risk at low concurrency

## Risks & Mitigations

### Operational Risks

| Risk | Impact | How We Handle It |
|------|--------|------------------|
| Process hangs | Conversions stuck | 5-minute timeout kills it |
| Disk fills up | Service fails | Size limits, auto cleanup |
| Memory leaks | Slow performance | Process isolation, auto restarts |
| LibreOffice crashes | Failed conversion | Process isolation, error handling |

### Security Risks

| Risk | Impact | How We Handle It |
|------|--------|------------------|
| Malicious files | Code execution | Validation, process isolation |
| DoS attacks | Service down | Rate limiting, concurrency controls |
| Info leaks | Data exposure | Sanitized errors, structured logs |
| Path traversal | Unauthorized access | Filename checks, isolated directories |

### Business Risks

| Risk | Impact | How We Handle It |
|------|--------|------------------|
| Poor conversion quality | Bad PDFs | Preprocessing, format validation |
| Version conflicts | Things break | Bundled dependencies, controlled updates |
| License issues | Legal problems | Open-source licenses (MPL, MIT, Apache) |

## Future Ideas

**Could add:**
- Windows Service deployment option
- Advanced monitoring (Performance Counters, ETW)
- Result caching for identical files
- Batch conversion support
- Webhook notifications for async processing
- Docker containers (Windows containers)

**Regular maintenance:**
- .NET security updates
- LibreOffice updates for format improvements
- NuGet package updates for bug fixes

---

This architecture keeps things simple while being secure and scalable. Everything is self-contained, which makes it perfect for corporate environments and air-gapped deployments.
