# Architecture

A .NET 8 web service that converts Office documents using bundled LibreOffice. Runs on Windows Server with IIS. No external dependencies.

**What it does:**

- Converts between 32 different Office document formats
- Processes files locally without network calls
- Handles concurrent requests with configurable limits
- Provides health monitoring

**Platform:** Windows Server 2016+ with IIS

## How It Works

```
Client → Security → API → Services → LibreOffice/iText7
```

**Layers:**

1. **Security** - IP filtering, rate limiting, input validation
2. **API** - ASP.NET Core controllers handling HTTP requests
3. **Services** - Business logic coordinating conversions
4. **Processing** - LibreOffice and .NET libraries doing the actual work

## Components

**Security:**

- IP whitelisting with CIDR support
- Rate limiting per IP address
- File type and size validation
- Safe error handling

**API Layer:**

- `ConversionController` - handles file uploads and conversion requests
- `HealthController` - provides health checks
- Standard ASP.NET Core middleware pipeline

**Services:**

- `DocumentService` - routes requests to the right conversion logic
- `ConversionEngine` - manages LibreOffice conversions
- `LibreOfficeService` - handles LibreOffice process execution
- `PdfService` - PDF operations using iText7
- `SpreadsheetService` - Excel/CSV handling
- `SemaphoreService` - controls concurrent operations

All services are singletons for performance and implement interfaces for testing.

**Processing Engines:**

- **LibreOffice** - handles most Office formats (DOC, DOCX, XLSX, PPTX, etc.)
- **iText7** - PDF creation and text extraction
- **DocumentFormat.OpenXml** - DOCX manipulation
- **NPOI** - Excel file handling

Each conversion runs in an isolated process with timeout protection.

## Data Flow

1. Client uploads file via HTTP POST
2. Security checks (IP whitelist, rate limiting)
3. File validation (type, size, format)
4. Save to temporary directory
5. Acquire concurrency slot
6. Route to appropriate conversion handler
7. Execute conversion using LibreOffice or .NET libraries
8. Return converted file or error
9. Clean up temporary files
10. Release concurrency slot

Errors are logged internally but only generic messages are returned to clients.

## Security

**Defense in depth:**

- IP whitelisting prevents unauthorized access
- Rate limiting prevents abuse
- File validation blocks malicious uploads
- Process isolation and timeouts prevent resource exhaustion
- Generic error messages prevent information leakage

**Validation layers:**

- Request: IP, rate limits, size
- File: extension, MIME type, content
- Processing: timeouts, resource limits

## Deployment

**Single server:**

```
Windows Server + IIS
├── .NET 8 Application
├── LibreOffice Bundle
└── App_Data (temp files, logs)
```

**High availability:**

```
Load Balancer
├── Server 1 (IIS + API + LibreOffice)
└── Server 2 (IIS + API + LibreOffice)
```

Stateless design - no session affinity needed. Health checks available at `/health`.

## Performance

**Conversion times:**

- Small docs (1-5 pages): 2-4 seconds
- Medium docs (10-20 pages): 3-6 seconds
- Large docs (50+ pages): 6-12 seconds

**Resource usage per conversion:**

- CPU: 10-30%
- Memory: 150-350MB

**Scaling:**

- Vertical: Increase `MaxConcurrentConversions` with more cores/RAM
- Horizontal: Add more servers behind load balancer (linear scaling)

## Monitoring

**Health endpoints:**

- `/health` - Basic availability check
- `/health/detailed` - System diagnostics

**Logging:**

- Structured logs with Serilog
- Windows Event Log integration
- Tracks conversion success/failure, timing, and errors

**Metrics:**

- Conversion rates and times
- Concurrent operations
- Resource usage

## Technology

**.NET 8 + ASP.NET Core** - Web framework with IIS hosting

**Conversion libraries:**

- LibreOffice - Office document conversions
- iText7 - PDF manipulation
- DocumentFormat.OpenXml - DOCX handling
- NPOI - Excel processing

**Supporting tools:**

- Serilog - structured logging
- AspNetCoreRateLimit - rate limiting

## Configuration

Hierarchical config: `appsettings.json` → environment variables → command line

**Key sections:**

- Security: IP filtering, rate limits
- LibreOffice: paths, timeouts
- Concurrency: max concurrent conversions
- FileHandling: sizes, temp directories

Configuration is validated at startup.

## Operations

**Startup:**

1. Load and validate config
2. Initialize services
3. Verify LibreOffice
4. Start accepting requests

**Shutdown:**

1. Stop new requests
2. Finish current conversions
3. Clean up temp files
4. Flush logs

**Maintenance:**

- Automatic temp file cleanup
- Log rotation
- LibreOffice bundle updates via script

## Risks & Decisions

**Operational risks:**

- Process hangs (mitigated by timeouts)
- Disk space (limited by file size controls)
- Memory leaks (isolated processes)

**Security risks:**

- Malicious uploads (multi-layer validation)
- IP bypass (CIDR validation)
- Info disclosure (sanitized errors)

**Key design decisions:**

- **Windows-only:** Simpler deployment, native IIS integration
- **Bundled dependencies:** No external services needed
- **LibreOffice:** Best format support for Office documents
- **Singleton services:** Performance and resource management

## Future

**Potential enhancements:**

- Windows Service deployment
- Advanced monitoring (Performance Counters, ETW)
- Conversion result caching

**Technology updates:**

- Regular .NET security patches
- LibreOffice format improvements
- Library updates

This architecture emphasizes simplicity and reliability for Windows Server deployments. Self-contained with no external dependencies, layered security, and proven IIS integration.

---
