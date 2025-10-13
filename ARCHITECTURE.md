# Architecture

Document conversion service for Windows Server internal deployment.

## System Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                         Client                              │
│                    (Browser/API Client)                     │
└────────────────────────┬────────────────────────────────────┘
                         │ HTTPS/HTTP
                         │
┌────────────────────────▼────────────────────────────────────┐
│                   Express Web Server                        │
│  ┌──────────────────────────────────────────────────────┐   │
│  │              Security Middleware                     │   │
│  │  • IP Whitelist  • Rate Limit  • Input Validation    │   │
│  └──────────────────────────────────────────────────────┘   │
└────────────────────────┬────────────────────────────────────┘
                         │
         ┌───────────────┼───────────────┐
         │               │               │
    ┌────▼────┐    ┌────▼─────┐   ┌────▼────┐
    │  API    │    │  Health  │   │  Docs   │
    │ Routes  │    │  Routes  │   │ Swagger │
    └────┬────┘    └──────────┘   └─────────┘
         │
    ┌────▼────────────────────┐
    │  Document Service       │
    │  (Orchestrator)         │
    └────┬────────────────────┘
         │
    ┌────┴─────────┬──────────────┬────────────┐
    │              │              │            │
┌───▼───┐     ┌───▼───┐     ┌───▼────┐  ┌───▼────┐
│ PDF   │     │ DOCX  │     │ XLSX   │  │ Image  │
│ Svc   │     │ Svc   │     │ CSV    │  │ Svc    │
└───┬───┘     └───┬───┘     └───┬────┘  └───┬────┘
    │             │             │           │
    └─────────────┴─────────────┴───────────┘
                  │
         ┌────────┴────────┐
         │                 │
    ┌────▼────┐      ┌────▼────┐
    │LibreOff.│      │  Edge   │
    │(Primary)│      │(Fallback│
    └─────────┘      └─────────┘
```

## Request Flow

### 1. Client Request
```http
POST /api/convert
Content-Type: multipart/form-data
  file: document.docx
  targetFormat: pdf
```

### 2. Security Pipeline
```
Request -> IP Whitelist -> Rate Limiter -> Input Validator -> Pattern Check
```

### 3. File Processing
```
Upload -> Temp Storage -> Format Detection -> Queue -> Process
```

### 4. Conversion
```
Document Service -> Format Handler -> Conversion Engine -> LibreOffice/Edge
```

### 5. Response
```
PDF Generated -> Download -> Cleanup -> Response
```

## Components

### Web Server (`src/server.js`)
- HTTP/HTTPS initialization
- Middleware pipeline
- Route mounting
- Error handling

### Middleware (`src/middleware/`)

**security.js** - Input validation
- File type whitelist
- MIME verification
- Filename sanitization
- Path traversal prevention

**advancedSecurity.js** - Access control
- IP whitelisting (CIDR support)
- Malicious pattern detection
- Content-Type enforcement
- Request integrity validation

**errorHandler.js** - Error processing
- Structured logging
- Cleanup on error
- HTTP error responses

**requestContext.js** - Request tracking
- Request ID generation
- Performance timing
- Audit logging

### Routes (`src/routes/`)

**conversion.js** - File conversion API
- Multer file upload
- Format validation
- Semaphore concurrency control
- Response handling

**health.js** - Health checks
- Service status
- System diagnostics
- Resource monitoring

### Services (`src/services/`)

**documentService.js** - Orchestrator
- Routes requests to specialized services
- Manages conversion workflow
- Returns standardized responses

**document/pdfService.js** - PDF operations
- PDF generation (text, XML, spreadsheets)
- PDF text extraction

**document/docxService.js** - DOCX operations
- Text to DOCX conversion
- Delegates to conversionEngine for high-fidelity operations

**document/spreadsheetService.js** - Spreadsheet operations
- XLSX <-> CSV conversion
- Multi-sheet support

**imageService.js** - Image conversion
- Format conversion using Sharp
- PSD and SVG support

**conversionEngine.js** - Core conversion
- LibreOffice integration (primary)
- Mammoth+Puppeteer (fallback)
- Path auto-detection

### Configuration (`src/config/`)

**config.js** - Environment management
- Variable loading
- Defaults
- Validation

**ssl.js** - SSL/TLS
- Certificate loading
- HTTPS options
- Self-signed support

### Utilities (`src/utils/`)

**semaphore.js** - Concurrency control
- Bounded queue
- Resource management
- 429 responses when saturated

## Data Flow

### Upload
1. Client uploads (multipart/form-data)
2. Multer saves to temp with UUID filename
3. Validation checks type and size
4. Queued for conversion (semaphore)

### Conversion
1. Detect format (docx->pdf, xlsx->csv, etc.)
2. Route to service
3. Service calls engine
4. Engine executes LibreOffice or Edge
5. Output generated in temp
6. Download sent to client
7. Cleanup after 1 second

### Error Handling
1. Conversion fails
2. Log with request ID
3. Cleanup temp files
4. Send error response
5. Release semaphore

## Security

### Network Layer
- SSL/TLS (optional)
- IP whitelist (CIDR)
- Proxy headers

### Request Layer
- Rate limiting (30/min)
- Timeout (120s)
- URL length validation
- Header count limits

### Input Layer
- File type whitelist
- MIME verification
- Filename sanitization
- Path traversal prevention

### Content Layer
- Pattern detection (SQL, XSS, traversal)
- Size limits (50MB)
- Content-Type enforcement

### Monitoring
- Audit logging
- Slow request detection
- Error tracking

## Conversion Methods

### LibreOffice (Primary)

**Auto-detection:**
1. `LIBREOFFICE_PATH` env var
2. `lib/libreoffice/program/soffice.exe`
3. `C:\Program Files\LibreOffice\program\soffice.exe`
4. `C:\Program Files (x86)\LibreOffice\program\soffice.exe`

**Execution:**
```bash
soffice.exe --headless --convert-to pdf --outdir <dir> <input>
```

**Fidelity:** 95-98%

### Edge Browser (Fallback)

**Auto-detection:**
1. `EDGE_PATH` env var
2. `C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe`
3. `C:\Program Files\Microsoft\Edge\Application\msedge.exe`

**Process:**
1. DOCX -> HTML (Mammoth.js)
2. HTML -> PDF (Puppeteer + Edge)
3. 2x scale factor for resolution

**Fidelity:** 60-70%

## Concurrency

### Semaphore Pattern
```javascript
MAX_CONCURRENT = 2
MAX_QUEUE = 10

Request arrives
  |
Semaphore available?
  Yes -> Acquire -> Process -> Release
  No -> Queue full?
    Yes -> 429 Too Many Requests
    No -> Wait in queue
```

### Resource Management
- Max 2 concurrent conversions
- Queue depth 10
- Auto cleanup on completion/error
- Timeout prevents hanging

## Deployment

### Development
```
Developer -> Install LibreOffice -> Bundle -> lib/libreoffice/ -> Git LFS
```

### Production
```
Windows Server -> Git Clone (LFS) -> npm install -> Configure .env -> Start
```

## Network Isolation

### No External Calls
- LibreOffice: Bundled
- Dependencies: npm install (one-time)
- Conversions: Local only
- No telemetry

### Verification
```powershell
netstat -ano | findstr :3000
# Should show only local connections
```

## Performance

### Conversion Times
- Small (1-5 pages): 2-4s
- Medium (10-20 pages): 3-6s
- Large (50+ pages): 6-12s

### Resource Usage
- Memory: 100-300MB per conversion
- CPU: 1 core per conversion
- Disk: 482MB (LibreOffice optimized) + temp

### Bottlenecks
- LibreOffice startup (~1-2s)
- Large file I/O
- Complex rendering

## Scaling

### Vertical
- Increase `MAX_CONCURRENCY` per CPU cores
- Add RAM for concurrent conversions
- Faster storage for temp files

### Horizontal
- Multiple instances + load balancer
- Shared network storage
- Redis for distributed rate limiting
- Stateless (no session affinity)

## Error Recovery

### Automatic
- Failed conversions release semaphore
- Temp files cleaned
- LibreOffice -> Edge fallback

### Manual
- Clear temp directory if full
- Restart for memory leaks
- Check LibreOffice availability

## Monitoring

### Health Endpoints
- `/health` - Status, uptime, memory
- `/health/detailed` - Full diagnostics

### Logs
- Request IDs
- Conversion method
- Duration
- Errors with stack traces

### Alerts
- Error rate >5%
- Requests >10s
- Memory >80%
- Disk <1GB

## Technology Decisions

### LibreOffice
- 95-98% fidelity
- No licensing costs
- Headless support
- Handles complex docs
- Industry standard

### Node.js
- Async I/O
- Mature ecosystem
- Windows deployment
- Process management

### Express
- Lightweight
- Middleware ecosystem
- Production-proven
- Easy maintenance

### Bundled LibreOffice
- Network isolation
- Version consistency
- No runtime dependencies
- Predictable behavior

## Known Limitations

- PPTX support basic (text-only)
- No OCR
- Single file per request
- Synchronous processing
- Windows-specific paths
