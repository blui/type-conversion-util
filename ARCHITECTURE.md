# File Conversion API - Architecture

## Executive Summary

Self-contained Office document conversion service built on .NET 8 for Windows Server environments. Provides 32 conversion paths across 16 input formats. Zero external dependencies. Designed for Windows Server IIS deployments requiring predictable, secure file format transformations.

**Core Principle:** Operational simplicity through elimination of external dependencies and network isolation.

**Platform:** Windows Server 2016+ / Windows 11 with IIS

## System Context

### Boundaries

**Input:** Multipart HTTP file uploads  
**Processing:** Local-only conversion using bundled LibreOffice and .NET libraries  
**Output:** Converted files via HTTP response  
**Network:** No external API calls. Complete air-gap operation.  
**Platform:** Windows Server with IIS hosting

### Supported Operations

- 32 conversion paths (16 input formats → 7 output formats)
- Document, spreadsheet, and presentation conversions
- Batch processing with configurable concurrency
- Health monitoring and telemetry

## Architecture Layers

```
┌─────────────────────────────────────────────────────┐
│  Client Applications (HTTP/HTTPS)                   │
└─────────────────┬───────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────┐
│  Security Layer                                     │
│  • IP Whitelist (CIDR)                              │
│  • Rate Limiting (per IP)                           │
│  • Input Validation                                 │
└─────────────────┬───────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────┐
│  Application Layer (ASP.NET Core)                   │
│  • Kestrel HTTP Server                              │
│  • Middleware Pipeline                              │
│  • API Controllers                                  │
└─────────────────┬───────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────┐
│  Service Layer                                      │
│  • DocumentService (orchestrator)                   │
│  • ConversionEngine (LibreOffice integration)       │
│  • Specialized Services (PDF, Spreadsheet)          │
└─────────────────┬───────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────┐
│  Processing Layer                                   │
│  • LibreOffice (headless soffice.exe)               │
│  • iText7 (PDF manipulation)                        │
│  • DocumentFormat.OpenXml (Office documents)        │
│  • NPOI (Excel)                                     │
└─────────────────────────────────────────────────────┘
```

## Component Details

### 1. Security Layer

**Purpose:** Defense-in-depth protection

**Components:**

- **SecurityMiddleware:** IP whitelist enforcement with CIDR validation, rate limiting integration
- **InputValidator:** File type verification, size limits, extension validation
- **ConversionValidator:** Post-conversion output validation
- **ExceptionHandlingMiddleware:** Sanitized error responses, no information leakage

**Security Controls:**

- IP filtering with proper CIDR subnet masking
- 30 requests/minute default rate limit per IP
- File size limit: 50MB (configurable)
- Supported format whitelist enforcement
- Path traversal prevention
- No exception details exposed to clients

### 2. Application Layer

**Purpose:** HTTP request handling and routing

**Key Classes:**

- `Program.cs`: Service registration, middleware pipeline, configuration binding
- `ConversionController`: Main API endpoint, multipart form parsing, response generation
- `HealthController`: Health check endpoints with LibreOffice availability verification

**Middleware Pipeline:**

```
Request → SecurityMiddleware
        → ExceptionHandlingMiddleware
        → Rate Limiting
        → Routing
        → Controller
        → Response
```

### 3. Service Layer

**Purpose:** Business logic orchestration

**Key Services:**

| Service              | Responsibility                            | Lifetime  |
| -------------------- | ----------------------------------------- | --------- |
| `DocumentService`    | Route conversions to appropriate handlers | Singleton |
| `ConversionEngine`   | Coordinate LibreOffice conversions        | Singleton |
| `LibreOfficeService` | LibreOffice process management            | Singleton |
| `PdfService`         | PDF creation and text extraction          | Singleton |
| `SpreadsheetService` | XLSX/CSV conversions                      | Singleton |
| `SemaphoreService`   | Concurrency control                       | Singleton |

**Service Pattern:**

All services implement interfaces for testability. Null checks in constructors enforce defensive programming. All services are singletons for performance.

### 4. Processing Layer

**Purpose:** Actual format conversion

**Engines:**

| Engine                 | Formats                         | Technology                     |
| ---------------------- | ------------------------------- | ------------------------------ |
| LibreOffice            | DOC, DOCX, ODT, XLSX, PPTX, RTF | Bundled soffice.exe (headless) |
| iText7                 | PDF generation, text extraction | iText7 library                 |
| DocumentFormat.OpenXml | DOCX creation                   | OpenXML SDK                    |
| NPOI                   | XLSX reading/writing            | NPOI library                   |

**LibreOffice Integration:**

- Headless execution: `soffice.exe --headless --convert-to pdf --outdir <dir> <file>`
- Process isolation: Each conversion spawns separate process
- Timeout enforcement: 60 seconds default (configurable)
- Automatic cleanup: Process termination on timeout or error

## Data Flow

### Conversion Request

```
1. Client uploads file + target format
   ↓
2. SecurityMiddleware validates IP, rate limit
   ↓
3. ConversionController parses multipart form
   ↓
4. InputValidator checks format, size, extension
   ↓
5. File saved to temporary directory (App_Data/temp/uploads)
   ↓
6. SemaphoreService acquires slot (concurrency control)
   ↓
7. DocumentService routes to appropriate handler
   ↓
8. Handler executes conversion (LibreOffice, iText7, etc.)
   ↓
9. ConversionValidator verifies output file
   ↓
10. Response generated with converted file
   ↓
11. Temporary files cleaned up
   ↓
12. SemaphoreService releases slot
```

### Error Handling

```
Any Step Fails
   ↓
ExceptionHandlingMiddleware catches
   ↓
Error categorized (validation, processing, system)
   ↓
Temporary files cleaned
   ↓
Generic error response (no sensitive details)
   ↓
Full details logged internally with operation ID
```

## Security Architecture

### Threat Model

**Primary Threats:**

1. Unauthorized access via IP spoofing
2. Malicious file uploads (exploits, viruses)
3. DoS through resource exhaustion
4. Information leakage through error messages

**Mitigations:**

| Threat              | Mitigation                              | Implementation                     |
| ------------------- | --------------------------------------- | ---------------------------------- |
| IP spoofing         | CIDR validation with bitwise comparison | SecurityMiddleware.IsInCIDRRange() |
| Malicious files     | Multi-layer validation                  | InputValidator, file size limits   |
| Resource exhaustion | Concurrency limits, timeouts            | SemaphoreService, process timeouts |
| Information leakage | Generic error messages                  | ExceptionHandlingMiddleware        |

### Security Validations

**Request Level:**

- IP whitelist check (if enabled)
- Rate limit verification
- Request size validation

**File Level:**

- Extension whitelist
- MIME type validation
- File size limit
- Content inspection

**Processing Level:**

- Process timeout enforcement
- Resource usage monitoring
- Output file validation

## Deployment Architecture

### Single-Server Windows Deployment

```
┌──────────────────────────────────────────┐
│  Windows Server 2016+ / Windows 11       │
├──────────────────────────────────────────┤
│  IIS 8.5+                                │
│  ├─ File Conversion API (.NET 8)         │
│  ├─ LibreOffice Bundle (program/)        │
│  └─ App_Data/ (temp files, logs)         │
└──────────────────────────────────────────┘
```

### High-Availability Windows Deployment

```
             Windows Load Balancer
                  │
      ┌───────────┴───────────┐
      │                       │
   Server 1               Server 2
   (Windows Server)       (Windows Server)
   ├─ IIS + API          ├─ IIS + API
   ├─ LibreOffice        ├─ LibreOffice
   └─ Local Storage      └─ Local Storage
```

**Characteristics:**

- Stateless design (no session affinity required)
- Independent processing on each node
- Health check endpoint for load balancer
- No shared storage required
- Windows Network Load Balancing (NLB) or hardware load balancer

## Performance Characteristics

### Throughput Metrics

| Operation   | Small | Medium | Large |
| ----------- | ----- | ------ | ----- |
| DOC → PDF   | 2-4s  | 3-6s   | 6-12s |
| XLSX → CSV  | <1s   | 1-2s   | 2-4s  |
| Image → PDF | 1-2s  | 2-3s   | 3-5s  |

**Small:** 1-5 pages, <1MB  
**Medium:** 10-20 pages, 1-5MB  
**Large:** 50+ pages, 5-10MB

### Resource Utilization

**Per Conversion:**

- CPU: 10-30%
- Memory: 150-350MB
- Disk I/O: Moderate (temp file operations)

**Base Process:**

- CPU: 1-5%
- Memory: 100-200MB

### Scalability

**Vertical Scaling:**

- Increase `MaxConcurrentConversions` with more CPU cores
- More RAM enables higher concurrency
- SSD storage reduces I/O bottlenecks

**Horizontal Scaling:**

- Add Windows Server instances behind load balancer
- Linear throughput increase
- No coordination required between nodes

## Monitoring and Observability

### Health Endpoints

| Endpoint           | Purpose                  | Response Time |
| ------------------ | ------------------------ | ------------- |
| `/health`          | Basic availability check | <50ms         |
| `/health/detailed` | System diagnostics       | <100ms        |
| `/api-docs`        | API documentation        | <50ms         |

### Logging

**Structured Logging (Serilog):**

```json
{
  "Timestamp": "2025-10-17T10:30:00Z",
  "Level": "Information",
  "MessageTemplate": "Conversion completed: {InputFormat} to {OutputFormat}",
  "Properties": {
    "InputFormat": "doc",
    "OutputFormat": "pdf",
    "ProcessingTimeMs": 3200,
    "OperationId": "abc-123-def"
  }
}
```

**Log Levels:**

- ERROR: Conversion failures, system errors
- WARNING: Performance degradation, retry attempts
- INFO: Successful conversions, health checks
- DEBUG: Detailed processing steps

**Windows Event Log Integration:**

- Application source: `FileConversionApi`
- Critical errors logged to Windows Event Log
- Viewable in Event Viewer

### Metrics Collected

- Conversion success/failure rates
- Processing time per format
- Concurrent operation count
- Queue depth
- Memory and CPU usage
- File sizes processed

## Technology Stack

### Core Framework

**.NET 8:** High performance, strong typing, excellent async support, native Windows integration

**ASP.NET Core:** Modern web framework, middleware architecture, built-in health checks, IIS hosting support

**IIS Hosting:** In-process hosting model for optimal performance, Windows authentication support, proven production reliability

### Conversion Engines

| Library                | Purpose                     | License           |
| ---------------------- | --------------------------- | ----------------- |
| LibreOffice            | Office document conversions | LGPL v3           |
| iText7                 | PDF generation/manipulation | AGPL / Commercial |
| DocumentFormat.OpenXml | DOCX creation               | MIT               |
| NPOI                   | Excel processing            | Apache 2.0        |

### Supporting Libraries

- **Serilog:** Structured logging
- **AspNetCoreRateLimit:** Rate limiting
- **CsvHelper:** CSV parsing

## Configuration Management

### Hierarchical Configuration

```
appsettings.json (base)
  ↓
appsettings.{Environment}.json (overrides)
  ↓
Environment Variables (overrides)
  ↓
Command Line Arguments (overrides)
```

### Key Configuration Sections

| Section      | Purpose           | Critical Settings              |
| ------------ | ----------------- | ------------------------------ |
| Security     | Access control    | EnableIPFiltering, IPWhitelist |
| LibreOffice  | Conversion engine | SdkPath, TimeoutSeconds        |
| Concurrency  | Resource limits   | MaxConcurrentConversions       |
| FileHandling | Temp files        | MaxFileSize, TempDirectory     |

### Validation

`ConfigValidator` service validates configuration at startup. Application fails fast if misconfigured.

## Operational Considerations

### Startup Sequence

```
1. Load configuration
2. Validate configuration (fail fast if invalid)
3. Initialize services (dependency injection)
4. Verify LibreOffice availability
5. Register with IIS
6. Begin accepting requests
```

### Shutdown Sequence

```
1. IIS signals shutdown
2. Stop accepting new requests
3. Complete in-flight conversions
4. Clean up temporary files
5. Flush logs to Windows Event Log
6. Terminate processes
```

### Maintenance

**Automatic:**

- Temporary file cleanup (24-hour retention)
- Log rotation (daily)
- Failed conversion cleanup

**Manual:**

- LibreOffice bundle updates (via bundle-libreoffice.ps1)
- Application updates (via deployment scripts)
- Configuration changes (via appsettings.json)

**Windows-Specific:**

- IIS application pool recycling (configurable)
- Windows Update integration
- Event Log monitoring

## Risk Analysis

### Operational Risks

| Risk                     | Probability | Impact | Mitigation                                  |
| ------------------------ | ----------- | ------ | ------------------------------------------- |
| LibreOffice process hang | Medium      | Low    | Timeout enforcement, automatic cleanup      |
| Disk space exhaustion    | Low         | Medium | Size limits, retention policies, monitoring |
| Memory leak              | Low         | Low    | Process isolation, restart policies         |

### Security Risks

| Risk                   | Probability | Impact | Mitigation                                |
| ---------------------- | ----------- | ------ | ----------------------------------------- |
| Malicious file upload  | High        | Low    | Multi-layer validation, sandboxing        |
| IP whitelist bypass    | Low         | Medium | Proper CIDR validation, logging           |
| Information disclosure | Low         | High   | Sanitized error messages, minimal logging |

## Design Decisions

### Why Windows-Only?

**Decision:** Target Windows Server exclusively

**Rationale:**

- Simplified deployment (single platform)
- Native IIS integration
- Windows-specific optimizations
- Proven enterprise support infrastructure
- LibreOffice runs reliably on Windows

**Trade-off:** Limited to Windows environments (acceptable per requirements)

### Why Single Binary?

**Decision:** Bundle all dependencies into single deployment

**Rationale:**

- Simplified deployment (no external service coordination)
- Reduced operational complexity
- Eliminated network failure modes
- Predictable behavior

**Trade-off:** Larger deployment size (~500MB with LibreOffice)

### Why LibreOffice?

**Decision:** Use LibreOffice as primary conversion engine

**Rationale:**

- Excellent format support and fidelity
- Free, open-source, no licensing costs
- Reliable headless operation on Windows
- Proven reliability in production

**Trade-off:** 2-4 second startup overhead per conversion

### Why Singleton Services?

**Decision:** All services registered as singletons

**Rationale:**

- Better performance (no repeated initialization)
- Shared state for resource management (semaphores)
- Simplified testing and debugging

**Trade-off:** Must ensure thread safety in all services

## Future Considerations

### Potential Enhancements

**Windows Service Deployment:**

- Native Windows Service installation
- Service Control Manager integration
- Automatic startup with Windows

**Advanced Monitoring:**

- Windows Performance Counters
- Event Tracing for Windows (ETW)
- Application Insights integration

**Caching:**

- Conversion result caching for identical files
- Reduces processing overhead for repeated conversions

### Technology Evolution

**.NET Updates:** Regular security patches and performance improvements

**LibreOffice Updates:** Format support improvements and bug fixes

**Library Updates:** Security patches and feature enhancements

## Conclusion

This architecture prioritizes simplicity, security, and reliability for Windows Server environments. The self-contained design eliminates operational complexity while the layered security approach provides defense-in-depth protection. IIS integration provides enterprise-grade hosting with proven reliability.

**Design Philosophy:** Every component serves a clear purpose. No unnecessary abstraction. No external dependencies. Predictable behavior under all conditions. Windows Server optimized.

---
