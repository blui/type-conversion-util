# Architecture

High-level design of the File Conversion API - a .NET 8 web service for converting Office documents on Windows Server.

## Overview

**Purpose:** Convert between 32 different Office document formats using bundled LibreOffice

**Platform:** Windows Server 2016+ with IIS hosting

**Design Philosophy:** Self-contained, stateless, defense-in-depth security

**Deployment:** Air-gapped and isolated network environments (no external dependencies)

## System Architecture

```
Client Request
    ↓
[Security Layer] - IP filtering, rate limiting, input validation
    ↓
[API Layer] - ASP.NET Core controllers
    ↓
[Service Layer] - Business logic and orchestration
    ↓
[Processing Layer] - LibreOffice, iText7, OpenXml, NPOI
    ↓
Converted File Response
```

### Layers

**1. Security Layer**

- IP whitelisting with CIDR support
- Rate limiting per IP address and endpoint
- Request size validation
- CORS configuration for access control
- Security headers on all responses

**2. API Layer**

- `ConversionController` - File upload and conversion orchestration
- `HealthController` - Service health monitoring
- Swagger/OpenAPI documentation
- Standard ASP.NET Core middleware pipeline

**3. Service Layer**

- `DocumentService` - Routes conversion requests to appropriate handlers
- `ConversionEngine` - Manages LibreOffice process execution
- `LibreOfficeService` - LibreOffice integration and lifecycle management
- `PdfService` - PDF operations using iText7
- `SpreadsheetService` - Excel/CSV handling with NPOI
- `InputValidator` - File type and format validation
- `SemaphoreService` - Concurrency control and resource management

All services are registered as singletons for performance and resource efficiency.

**4. Processing Layer**

- **LibreOffice** - Handles most Office formats (DOC, DOCX, XLSX, PPTX, ODT, ODS, ODP)
- **iText7** - PDF creation and text extraction
- **DocumentFormat.OpenXml** - DOCX manipulation and creation
- **NPOI** - Excel file reading and writing

Each conversion runs in an isolated process with timeout protection.

## Components

### Controllers

**ConversionController**

Handles file conversion requests:

- Validates uploaded files (type, size, extension)
- Creates operation-specific subdirectories for isolation
- Preserves original filenames for 1:1 conversion fidelity
- Acquires concurrency slot via semaphore
- Routes to appropriate conversion service
- Returns converted file or detailed error response
- Cleans up temporary files after completion

**HealthController**

Provides health monitoring:

- Basic health check (LibreOffice availability, service status)
- Detailed health check (system info, diagnostics, uptime)
- Used by load balancers for availability checks

### Core Services

**DocumentService**

Primary conversion orchestration service that:

- Determines conversion path based on source and target formats
- Routes to specialized services (LibreOffice, PDF, Spreadsheet)
- Handles preprocessing for DOCX files (font normalization, color conversion)
- Manages conversion workflow and error handling

**LibreOfficeService & LibreOfficeProcessManager**

Manages LibreOffice integration:

- Spawns isolated LibreOffice processes for each conversion
- Applies timeout controls (300 seconds default)
- Handles process cleanup and resource management
- Runs in headless mode (no GUI, no user interaction)
- Supports 23 different conversion paths

**PdfService**

PDF operations using iText7:

- Create PDFs from plain text
- Extract text from PDFs
- Basic PDF manipulation
- Handles 4 conversion paths

**SpreadsheetService**

Excel and CSV operations using NPOI:

- Read Excel files and export to CSV
- Parse CSV and create Excel files
- Handles spreadsheet-specific conversions

**PreprocessingService & DocxPreProcessor**

Improves DOCX to PDF conversion fidelity:

- Normalizes fonts for LibreOffice compatibility (Aptos → Calibri)
- Converts Office theme colors to explicit RGB values
- Simplifies complex formatting for better rendering
- Fixes bold text rendering issues
- Optional - can be disabled via configuration

**SemaphoreService**

Concurrency control:

- Limits concurrent conversions based on configuration
- Manages conversion queue
- Prevents resource exhaustion
- Tracks active operations and queue size
- Provides statistics for monitoring

**InputValidator**

Multi-layer validation:

- File extension whitelist
- MIME type verification
- File size limits
- Conversion path validation
- Format compatibility checking

### Middleware

**SecurityMiddleware**

Applies security controls:

- IP address whitelisting (CIDR support)
- Security headers (CSP, X-Frame-Options, X-Content-Type-Options)
- Request validation
- CORS enforcement

**ExceptionHandlingMiddleware**

Centralized error handling:

- Catches all unhandled exceptions
- Logs errors with operation ID for tracing
- Returns sanitized error messages (no information disclosure)
- Standard HTTP status codes

**IpRateLimitMiddleware** (AspNetCoreRateLimit)

Rate limiting:

- 30 requests/minute general limit
- 10 requests/minute for conversion endpoint
- Per-IP tracking
- Configurable rules and whitelist

## Data Flow

Detailed conversion request flow:

1. **Client uploads file** via HTTP POST to `/api/convert`
2. **Security checks** - IP whitelist validation, rate limit check
3. **File validation** - Extension, MIME type, size, format compatibility
4. **Operation isolation** - Create unique subdirectory with GUID
5. **Save uploaded file** - Preserve exact original filename
6. **Acquire concurrency slot** - Wait if at capacity
7. **Preprocessing** (DOCX only) - Font normalization, color conversion
8. **Route to conversion handler** - LibreOffice, PDF, or Spreadsheet service
9. **Execute conversion** - Isolated process with timeout protection
10. **Read converted file** - Load into memory before cleanup
11. **Clean up operation directory** - Delete all temporary files
12. **Release concurrency slot** - Allow next queued request
13. **Return result** - Stream file to client or error response

Errors at any stage trigger cleanup and return appropriate HTTP status codes with sanitized messages.

## Conversion Fidelity

### 1:1 Conversion Principle

The application performs **pure 1:1 conversions** where the output contains exactly what exists in the original document:

- No content modification or scrubbing
- No metadata removal
- No formatting adjustments
- Field codes evaluate correctly (FILENAME, DATE, PATH)

### Implementation

**Operation-Specific Subdirectories:**

```
App_Data/temp/
├── uploads/{operationId}/
│   └── original-filename.doc  ← Exact original name preserved
└── converted/{operationId}/
    └── original-filename.pdf  ← Output maintains naming
```

**Filename Sanitization:**

Only removes filesystem-illegal characters for security:

- Path traversal patterns (../, ..\)
- Invalid characters (<, >, |, :, *, ?, ")
- Control characters and null bytes

Preserves:

- Spaces, parentheses, brackets
- Version numbers (v1.0, v2.0)
- Dates and timestamps
- International characters

This ensures document field codes like `{FILENAME}` evaluate to the correct value in PDF output.

### Preprocessing for Quality

DOCX preprocessing improves LibreOffice conversion quality:

- **Font normalization** - Replaces proprietary fonts (Aptos) with compatible alternatives (Calibri)
- **Color conversion** - Converts theme colors to explicit RGB values
- **Formatting simplification** - Removes complex styles that LibreOffice handles poorly
- **Bold text fixes** - Ensures bold formatting renders correctly

Preprocessing is optional and can be disabled if absolute byte-for-byte fidelity is required.

## Security Architecture

### Defense in Depth

Multiple security layers protect against threats:

**Network Layer:**

- Rate limiting prevents abuse and DoS
- IP whitelisting restricts access to authorized networks
- CORS controls cross-origin requests

**Application Layer:**

- File type whitelist (allowed extensions only)
- File size limits (50MB default, configurable)
- MIME type verification
- Path traversal prevention in file operations
- Request timeout enforcement
- Sanitized error messages

**Processing Layer:**

- Isolated process execution for conversions
- Timeout controls prevent hung processes
- Temporary file isolation with GUID-based directories
- Automatic cleanup with retention policies
- Concurrency controls via semaphore

### Security Features

**Implemented:**

- Input validation and sanitization
- Rate limiting (30 req/min general, 10 req/min conversion)
- CORS configuration for access control
- Security headers on all responses (CSP, X-Frame-Options, HSTS)
- Structured logging with operation IDs
- File system security with automatic cleanup
- Process isolation for conversion tasks
- No database (eliminates SQL injection risk)
- No HTML rendering (eliminates XSS risk)

**Intentionally Not Implemented:**

- Authentication - Deploy behind corporate VPN/firewall or add authentication layer
- IP whitelisting at application level - Use network firewall or reverse proxy
- HTTPS enforcement - Configure via IIS or load balancer
- User sessions - Stateless design

### NuGet Dependencies

All packages from official NuGet.org repository:

**Microsoft Official (Zero Risk):**

- Microsoft.AspNetCore.* (8.0.21)
- DocumentFormat.OpenXml (3.0.2)
- Microsoft.Extensions.* (8.0.0)

**Industry Standard (Low Risk):**

- Swashbuckle.AspNetCore (6.6.2) - 1.5B+ downloads
- Serilog.* (8.0.1+) - 500M+ downloads
- CsvHelper (30.0.1) - 200M+ downloads

**Commercial/Established (Low Risk):**

- iText7 (8.0.2) - AGPL/Commercial license, Fortune 500 adoption
- NPOI (2.7.0) - Apache License, 50M+ downloads
- AspNetCoreRateLimit (5.0.0) - 25M+ downloads

**Community (Medium Risk - Verified Safe):**

- PdfSharpCore (1.3.62) - MIT license, 5M+ downloads
- SharpZipLib (1.4.2) - MIT license, 200M+ downloads, established since 2000

### LibreOffice Security

**Bundle Details:**

- Software: LibreOffice
- License: Mozilla Public License v2.0
- Source: https://www.libreoffice.org/
- Vendor: The Document Foundation (non-profit)

**Security Measures:**

- Runs in headless mode only (no GUI, no user interaction)
- No macro execution enabled
- No network access configured
- Isolated process execution with limited permissions
- Automatic process cleanup with timeout controls
- Optimized bundle (60-70% smaller than full installation)

**Removed from Bundle:**

- Python runtime (macro scripting)
- UI components, wizards, help files
- Gallery, templates, samples
- Auto-correction dictionaries
- Extensions and user packages
- Non-English language packs

**Industry Trust:**

- Used by governments worldwide (German, French, Italian, UK)
- 20+ year open-source history
- Active security patch releases
- No telemetry or network calls in headless mode

## Technology Stack

### Core Framework

- **.NET 8 / ASP.NET Core** - Web framework with IIS hosting
- **C# 12** - Primary programming language
- **Windows-only** - Leverages native Windows features and IIS integration

### Conversion Libraries

- **LibreOffice** - Office document conversions (23 paths)
- **iText7** - PDF manipulation (4 paths)
- **DocumentFormat.OpenXml** - DOCX handling (4 paths)
- **NPOI** - Excel processing (1 path)

### Supporting Libraries

- **Serilog** - Structured logging with file and console sinks
- **AspNetCoreRateLimit** - IP-based rate limiting
- **Swashbuckle.AspNetCore** - OpenAPI/Swagger documentation
- **CsvHelper** - CSV parsing and generation

### Infrastructure

- **IIS** - Production hosting on Windows Server
- **Kestrel** - Development server (embedded)
- **Windows Event Log** - System-level logging integration

## Configuration

Hierarchical configuration system:

1. `appsettings.json` - Base configuration
2. Environment variables - Override specific settings
3. Command line arguments - Deployment automation

**Key Configuration Sections:**

- `Serilog` - Logging levels, sinks, output templates
- `FileHandling` - File size limits, allowed extensions, temp directories
- `Security` - IP filtering, CORS, rate limiting
- `LibreOffice` - Executable path, timeout settings
- `Concurrency` - Max concurrent conversions, queue size, thread pool
- `Preprocessing` - DOCX preprocessing options
- `IpRateLimiting` - Rate limit rules and whitelist
- `SecurityHeaders` - CSP, frame options, XSS protection

Configuration is validated at startup with detailed error messages.

## Operational Considerations

### Startup Sequence

1. Load and validate configuration
2. Initialize dependency injection container
3. Register services (singleton pattern for performance)
4. Configure Serilog logging
5. Verify LibreOffice bundle availability
6. Start Kestrel/IIS hosting
7. Begin accepting requests

### Shutdown Sequence

1. Stop accepting new requests
2. Complete in-flight conversions (graceful shutdown)
3. Clean up temporary files
4. Flush logs to disk
5. Terminate processes

### Resource Management

**Memory:**

- Each conversion uses 150-500MB RAM
- Singleton services reduce memory overhead
- Automatic garbage collection
- Process isolation prevents leaks

**Disk:**

- Temporary files isolated per operation
- Automatic cleanup after conversion
- Configurable retention (24 hours default)
- Log rotation with 30-day retention

**CPU:**

- 10-30% CPU per conversion
- Configurable concurrency limits
- LibreOffice processes isolated
- Thread pool optimization

### Scaling Strategy

**Vertical Scaling:**

- Increase `MaxConcurrentConversions` with more CPU cores
- Add RAM for higher concurrency
- Faster disk I/O improves throughput

**Horizontal Scaling:**

- Deploy to multiple servers
- Load balancer with `/health` endpoint check
- Stateless design - no session affinity required
- Linear scaling with additional servers

**Recommended Server Sizing:**

| Workload | CPU Cores | RAM | Max Concurrent |
|----------|-----------|-----|----------------|
| Light | 2-4 | 8GB | 2 |
| Medium | 4-8 | 16GB | 4 |
| Heavy | 8+ | 32GB | 6-8 |

## Monitoring and Observability

### Health Endpoints

- `/health` - Basic service availability (200 OK or 503 Service Unavailable)
- `/health/detailed` - System diagnostics (OS version, uptime, memory, CPU count)

**Load Balancer Integration:**

- Use `/health` for availability checks
- 30-second interval recommended
- 2 consecutive failures mark unhealthy

### Logging

**Structured Logging with Serilog:**

- Operation ID tracking for request tracing
- Contextual information (file sizes, formats, timing)
- Multiple sinks (Console, File, Windows Event Log)
- Configurable log levels per namespace
- Daily log rotation with retention limits

**Log Levels:**

- **Information** - Conversion requests, results, timing
- **Warning** - LibreOffice fallback, cleanup failures
- **Error** - Conversion failures, exceptions
- **Debug** - Detailed execution flow (development only)

**Sample Log Entry:**

```
2025-10-25 14:30:15.234 [INF] Conversion completed - Input: docx, Target: pdf, Size: 524288 bytes, Time: 3421ms, Success: true
```

### Metrics

Available through logs and health endpoints:

- Conversion success/failure rates
- Processing times per format
- Concurrent operation count
- Queue depth
- Memory and CPU usage
- LibreOffice availability

## Design Decisions

### Why Windows-only?

- Simpler deployment (native IIS integration)
- Corporate standard for many enterprises
- Better LibreOffice compatibility on Windows
- Leverages Windows-specific features (Event Log, NTFS permissions)

### Why bundled LibreOffice?

- Air-gapped deployment support
- No external dependencies
- Consistent version across environments
- No Microsoft Office licenses required
- Best format support for Office documents

### Why singleton services?

- Performance - reuse expensive resources
- Resource management - single semaphore, single LibreOffice manager
- Simplified state management
- Lower memory overhead

### Why stateless design?

- Horizontal scaling without session affinity
- Simpler deployment and maintenance
- Better fault tolerance
- No session store required

### Why operation-specific subdirectories?

- True 1:1 conversion fidelity (original filenames preserved)
- Complete file isolation per request
- Concurrent safety with identical filenames
- Simplified cleanup (delete entire directory)
- Better debugging (all files for an operation grouped)

## Risks and Mitigations

### Operational Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Process hangs | Blocked conversions | Timeout controls (300s default) |
| Disk space exhaustion | Service failure | File size limits, automatic cleanup |
| Memory leaks | Degraded performance | Process isolation, automatic restarts |
| LibreOffice crashes | Failed conversions | Process isolation, error handling |

### Security Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Malicious file uploads | Code execution | Multi-layer validation, process isolation |
| DoS attacks | Service unavailability | Rate limiting, concurrency controls |
| Information disclosure | Data leakage | Sanitized errors, structured logging |
| Path traversal | Unauthorized access | Filename sanitization, isolated directories |

### Business Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| LibreOffice compatibility | Poor conversion quality | Preprocessing, format validation |
| Version conflicts | Breaking changes | Bundled dependencies, controlled updates |
| License compliance | Legal issues | Open-source licenses (MPL, MIT, Apache) |

## Future Enhancements

**Potential Improvements:**

- Windows Service deployment option
- Advanced monitoring (Performance Counters, ETW)
- Conversion result caching for identical files
- Batch conversion support
- Webhook notifications for async processing
- Docker container support (Windows containers)

**Technology Updates:**

- Regular .NET security patches
- LibreOffice version updates for format improvements
- NuGet package updates for bug fixes

This architecture emphasizes simplicity, reliability, and security for Windows Server deployments. The self-contained design with no external dependencies makes it ideal for air-gapped and isolated network environments.
