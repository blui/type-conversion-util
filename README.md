# File Conversion API

RESTful API for document conversion on Windows Server with 98-99% fidelity. Converts DOCX, PDF, images, and spreadsheets using local processing only.

## Features

- **DOCX → PDF** (98-99% fidelity with LibreOffice)
- **PDF → DOCX** (75-85% fidelity)
- **Spreadsheets** (XLSX, CSV)
- **Images** (JPG, PNG, GIF, BMP, TIFF, SVG)
- **Text formats** (TXT, XML)
- **Network isolated** - No external calls or cloud services
- **Security hardened** - IP whitelist, rate limiting, input validation

## Quick Start

```bash
npm install
npm start
```

API runs at `http://localhost:3000`

## API Usage

```bash
# Convert file
curl -X POST http://localhost:3000/api/convert \
  -F "file=@document.docx" \
  -F "targetFormat=pdf" \
  -o output.pdf

# Get conversion metadata
curl -X POST "http://localhost:3000/api/convert?metadata=true" \
  -F "file=@document.docx" \
  -F "targetFormat=pdf"
```

### Endpoints

- `POST /api/convert` - Convert file
- `GET /api/supported-formats` - List supported formats
- `GET /health` - Service health status
- `GET /api-docs` - API documentation

## Configuration

```bash
IP_WHITELIST=192.168.1.0/24      # Optional IP restrictions
ENABLE_PREPROCESSING=true        # Enable DOCX preprocessing (default: false)
MAX_FILE_SIZE=52428800           # 50MB limit
RATE_LIMIT_MAX=30                # Requests per minute
```

## SSL Setup (Optional)

```bash
node scripts/generate-ssl-cert.js
SSL_ENABLED=true
ACCEPT_SELF_SIGNED_CERTS=true
```

## Architecture

```
Client → Security → Conversion Engine → Response
                   ↓
            Pre-processing (DOCX optimization)
                   ↓
            LibreOffice (98-99% fidelity)
```

Single-engine design using LibreOffice for all conversions.

## Security

- IP whitelist with CIDR support
- SSL/TLS with self-signed certificates
- Rate limiting and input validation
- Network isolation (no external calls)

## Performance

| Document Size | Conversion Time |
| ------------- | --------------- |
| 1-5 pages     | 2-4 seconds     |
| 10-20 pages   | 3-6 seconds     |
| 50+ pages     | 6-12 seconds    |

## System Requirements

- Windows Server 2016+ or Windows 10+
- Node.js 16+
- 4GB RAM minimum
- 2GB disk space
