# File Conversion Service

Document conversion API for Windows Server. Converts DOCX, PDF, images, and spreadsheets with 95-98% fidelity.

## Features

- **DOCX <-> PDF** with high-fidelity conversion:
  - **Enhanced Local** (95-98% fidelity) - Pre-processed + optimized LibreOffice
  - **Fallback** (60-70% fidelity) - Mammoth+Edge for edge cases
- **PDF to DOCX** (75-85% fidelity)
- **Spreadsheets** (XLSX, CSV)
- **Images** (JPG, PNG, GIF, BMP, TIFF, SVG)
- **Text formats** (TXT, XML)
- **Smart pre-processing** - Normalizes DOCX formatting before conversion
- **Network isolated** - Zero external calls, fully local processing
- **Security hardened** - IP whitelist, rate limiting, input validation
- **SSL/TLS support** - Self-signed certificates for internal networks

## Quick Start

```bash
npm install
node scripts/bundle-libreoffice.js
npm start
```

Configure `.env`:
```bash
ACCEPT_SELF_SIGNED_CERTS=true
LIBREOFFICE_PATH=           # Leave blank for auto-detect
EDGE_PATH=                  # Leave blank for auto-detect
```

Server runs at `http://localhost:3000`

## API Usage

### Convert File (Standard)

```bash
# Download converted file (default mode)
curl -X POST http://localhost:3000/api/convert \
  -F "file=@document.docx" \
  -F "targetFormat=pdf" \
  -o output.pdf
```

**Response Headers:**
```
X-Conversion-Method: libreoffice-enhanced
X-Conversion-Fidelity: 95-98%
X-Preprocessing-Enabled: true
X-Preprocessing-Stats: {"fontsNormalized":6,"themeColorsConverted":12,"stylesSimplified":1}
```

### Convert File with Metadata

```bash
# Get conversion details as JSON (no file download)
curl -X POST "http://localhost:3000/api/convert?metadata=true" \
  -F "file=@document.docx" \
  -F "targetFormat=pdf"
```

**Response:**
```json
{
  "success": true,
  "message": "Conversion completed successfully",
  "conversion": {
    "inputFormat": "docx",
    "outputFormat": "pdf",
    "method": "libreoffice-enhanced",
    "fidelity": "95-98%",
    "conversionTime": "3.2s"
  },
  "preprocessing": {
    "enabled": true,
    "fontsNormalized": 6,
    "themeColorsConverted": 12,
    "stylesSimplified": 1,
    "paragraphsAdjusted": 0,
    "boldFixed": 0
  },
  "output": {
    "fileName": "document.pdf",
    "size": 409600,
    "path": "uploads/converted-abc123-document.pdf"
  }
}
```

### Endpoints

- `POST /api/convert` - Convert file (binary response)
- `POST /api/convert?metadata=true` - Convert file (JSON metadata response)
- `GET /api/supported-formats` - List supported formats
- `GET /health` - Service health status
- `GET /api-docs` - Swagger documentation

## Configuration

### Environment Variables

```bash
# Server
PORT=3000
HOST=localhost

# Security
ACCEPT_SELF_SIGNED_CERTS=true    # Required
SSL_ENABLED=false                 # Optional: Enable HTTPS
IP_WHITELIST=                     # Optional: Restrict by IP (CIDR supported)

# Conversion Quality Settings
ENABLE_PREPROCESSING=true         # Pre-process DOCX for better fidelity (recommended)

# Limits
MAX_FILE_SIZE=52428800           # 50MB default
RATE_LIMIT_MAX=30                # Requests per minute
MAX_CONCURRENCY=2                # Concurrent conversions
```

### Conversion Quality

The service uses a two-tier conversion strategy with automatic fallback:

**1. Enhanced Local Conversion (Primary Method)**
- Pre-processes DOCX to normalize fonts, colors, and styles
- Uses optimized LibreOffice with enhanced PDF export settings
- **95-98% fidelity** for most documents
- Fully local processing with no external calls

```bash
ENABLE_PREPROCESSING=true
```

**2. Fallback Conversion (Automatic)**
- Mammoth + Microsoft Edge rendering
- **60-70% fidelity** - basic layout preservation
- Automatically used if LibreOffice conversion fails

**Conversion Flow:**
```
1. Pre-process DOCX (normalize formatting)
2. Use LibreOffice (enhanced settings)
3. Fallback to Mammoth+Edge (if LibreOffice fails)
```

### SSL Setup (Optional)

```bash
node scripts/generate-ssl-cert.js
```

Set `SSL_ENABLED=true` in `.env`

## Architecture

See [ARCHITECTURE.md](ARCHITECTURE.md) and [CONVERSION_STRATEGIES.md](CONVERSION_STRATEGIES.md) for detailed system design.

**Enhanced Conversion Flow:**
```
Request -> Security Layer -> Conversion Engine -> Response
              |
              v
       [1] Pre-process DOCX (normalize fonts/colors/styles)
              |
              v
       [2] LibreOffice (95-98% fidelity) - enhanced settings
              |
              v
       [3] Mammoth+Edge (60-70% fidelity) - fallback
```

**Auto-detection** finds LibreOffice and Edge automatically. Override with environment variables if needed.

**Pre-processing Improvements:**
- Theme colors → RGB conversion
- Custom fonts → LibreOffice-compatible fonts (Aptos → Calibri, etc.)
- Unsupported text effects removed (shadows, glows, etc.)
- Bold/italic formatting normalized
- Paragraph spacing standardized

## Security

### Network Isolation
**Zero external calls.** All processing is fully local with LibreOffice.

Verify local-only mode:
```powershell
netstat -ano | findstr :3000
```

### Defense Layers
1. **Network** - SSL/TLS, IP whitelist
2. **Input** - File type validation, MIME verification, path traversal prevention
3. **Content** - Malicious pattern detection, size limits
4. **Resource** - Rate limiting, timeout enforcement, concurrency control
5. **Monitoring** - Audit logging, error tracking

## Performance

| Document Size | Conversion Time |
|--------------|-----------------|
| 1-5 pages    | 2-4 seconds     |
| 10-20 pages  | 3-6 seconds     |
| 50+ pages    | 6-12 seconds    |

**Resource Usage:**
- Memory: 100-300MB per conversion
- CPU: 1 core per conversion
- Disk: 482MB (LibreOffice optimized) + temp files

## System Requirements

- Windows Server 2016+ or Windows 10+
- Node.js 16+
- Microsoft Edge (fallback only)
- 4GB RAM (8GB recommended)
- 2GB disk space

## Project Structure

```
type-conversion-util/
├── lib/libreoffice/                    # Bundled LibreOffice (482MB optimized)
├── scripts/
│   ├── bundle-libreoffice.js           # Bundle LibreOffice from system install
│   ├── bundle-libreoffice-safe.js      # Safe optimization script
│   ├── generate-ssl-cert.js            # Generate self-signed certificates
│   └── verify-system.js                # System verification
├── src/
│   ├── config/                         # Configuration management
│   ├── middleware/                     # Security, validation, error handling
│   ├── routes/                         # API endpoints
│   ├── services/                       # Conversion services
│   │   ├── document/                   # PDF, DOCX, spreadsheet services
│   │   ├── conversionEngine.js         # Main conversion orchestrator
│   │   ├── docxPreProcessorAdvanced.js # Advanced pre-processing for fidelity
│   │   ├── documentService.js
│   │   └── imageService.js
│   └── utils/                          # Semaphore, helpers
├── uploads/                            # Temporary files (auto-cleanup)
├── .env                                # Configuration
├── ARCHITECTURE.md                     # Technical documentation
├── CONVERSION_STRATEGIES.md            # Conversion quality options guide
└── README.md                           # This file
```

## Development

### Verify Installation
```bash
node scripts/verify-system.js
```

Expected: `System Status: READY`

### Run Tests
```bash
npm test
npm run test:coverage
```

### Add New Conversion
1. Add method to appropriate service in `src/services/`
2. Update `src/routes/conversion.js`
3. Add tests
4. Update API documentation

## Troubleshooting

| Issue | Solution |
|-------|----------|
| LibreOffice not found | Run `node scripts/bundle-libreoffice.js` |
| Permission denied | Grant IIS_IUSRS permissions to `uploads/` |
| High memory usage | Reduce `MAX_CONCURRENCY` in `.env` |
| Conversion fails | Check logs, verify file is valid |
| Port in use | Change `PORT` in `.env` |

## Monitoring

### Health Check
```bash
curl http://localhost:3000/health
```

Returns status, uptime, memory usage.

### Logs
- Development: Console
- Production: `C:\inetpub\logs\LogFiles\`

Monitor:
- Conversion method used (LibreOffice vs fallback)
- Error rates
- Response times
- Memory usage

## License

Internal use only. Not for external distribution.
