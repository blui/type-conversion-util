# File Conversion API

Production-ready .NET 8 service for converting Office documents on Windows Server. Supports 32 format conversions using bundled LibreOffice. Fully self-contained for air-gapped and isolated network deployments.

## Features

- 32 different document format conversions
- Microsoft Office formats: DOC, DOCX, XLSX, PPTX, PDF
- Open formats: ODT, ODS, ODP, RTF, CSV, TXT, XML, HTML
- Legacy formats: SXW, SXC, SXI, SXD
- No external dependencies or network calls
- Enterprise security with IP whitelisting and rate limiting
- Automatic file cleanup and resource management
- Health monitoring and structured logging

## Quick Start

```powershell
# Get the code
git clone <repository-url>
cd type-conversion-util

# Bundle LibreOffice (creates ~500MB optimized bundle)
.\bundle-libreoffice.ps1

# Build the application
dotnet build FileConversionApi/FileConversionApi.csproj

# Run the service
dotnet run --project FileConversionApi/FileConversionApi.csproj -- --urls "http://localhost:3000"

# Test it works
curl http://localhost:3000/health
```

API documentation: `http://localhost:3000/api-docs`

## Supported Conversions

**32 total conversion paths across multiple categories:**

| Category | Input Formats | Output Formats |
|----------|---------------|----------------|
| Documents | DOC, DOCX, PDF, TXT, RTF, XML, HTML, ODT | PDF, DOCX, DOC, TXT, RTF, ODT, HTML |
| Spreadsheets | XLSX, CSV, ODS | PDF, XLSX, CSV |
| Presentations | PPTX, ODP | PDF, PPTX |
| Legacy | SXW, SXC, SXI, SXD | PDF |

**Most common conversions:**
- Any Office document to PDF (21 paths)
- PDF to editable DOCX (text extraction)
- Excel to CSV (data export)
- Legacy DOC to modern DOCX

All conversions maintain 1:1 fidelity - the output PDF contains exactly what exists in the original document with zero content modification.

## API Usage

**Convert a file:**

```powershell
curl -X POST http://localhost:3000/api/convert \
  -F "file=@document.doc" \
  -F "targetFormat=pdf" \
  -o output.pdf
```

**With metadata:**

```powershell
curl -X POST "http://localhost:3000/api/convert?metadata=true" \
  -F "file=@document.docx" \
  -F "targetFormat=pdf"
```

**Available endpoints:**

- `POST /api/convert` - Convert uploaded file to target format
- `GET /api` - API information and version
- `GET /api/supported-formats` - List all supported conversions
- `GET /health` - Basic health check
- `GET /health/detailed` - Detailed system diagnostics
- `GET /api-docs` - Interactive API documentation

**Response codes:**

- `200 OK` - Conversion successful
- `400 Bad Request` - Invalid file or unsupported conversion
- `429 Too Many Requests` - Rate limit exceeded
- `500 Internal Server Error` - Conversion failed
- `503 Service Unavailable` - Service at capacity

## Configuration

Key settings in `appsettings.json`:

**Security:**
```json
{
  "Security": {
    "EnableIPFiltering": false,
    "IPWhitelist": ["192.168.0.0/16", "10.0.0.0/8"],
    "EnableRateLimiting": true
  },
  "IpRateLimiting": {
    "GeneralRules": [
      { "Endpoint": "*", "Period": "1m", "Limit": 30 },
      { "Endpoint": "POST:/api/convert", "Period": "1m", "Limit": 10 }
    ]
  }
}
```

**File Handling:**
```json
{
  "FileHandling": {
    "MaxFileSize": 52428800,
    "MaxFilesPerRequest": 5,
    "TempDirectory": "App_Data\\temp\\uploads",
    "OutputDirectory": "App_Data\\temp\\converted",
    "CleanupTempFiles": true,
    "TempFileRetentionHours": 24
  }
}
```

**Performance:**
```json
{
  "Concurrency": {
    "MaxConcurrentConversions": 2,
    "MaxQueueSize": 10
  },
  "LibreOffice": {
    "TimeoutSeconds": 300
  }
}
```

**Logging:**
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "App_Data\\logs\\file-conversion-api-.log" } }
    ]
  }
}
```

## Deployment

**Requirements:**
- Windows Server 2016+ or Windows 11
- .NET 8.0 Runtime + ASP.NET Core Hosting Bundle
- IIS 8.5+
- 4GB RAM minimum, 8GB recommended

**Deploy to IIS:**

```powershell
# Build deployment package
cd FileConversionApi
.\deploy.ps1

# Package created in deploy\release (~550MB)
# Copy to Windows Server: C:\inetpub\FileConversionApi

# Configure IIS, start application pool
# See DEPLOYMENT.md for complete instructions
```

See [DEPLOYMENT.md](DEPLOYMENT.md) for detailed deployment, configuration, and troubleshooting instructions.

## Security

Enterprise-grade security features:

- CIDR-based IP whitelisting with bit-level validation
- Rate limiting per IP and endpoint
- File type, size, and MIME validation
- Isolated file processing with automatic cleanup
- No external network calls (air-gap compliant)
- Security headers (CSP, X-Frame-Options, X-XSS-Protection)
- Comprehensive error handling with sanitized messages
- Structured logging with operation tracking

For production deployments:
- Enable IP filtering in appsettings.json
- Configure CORS allowed origins
- Set appropriate file size limits
- Review rate limiting rules

## Performance

Typical conversion times:
- Small documents (1-5 pages): 2-4 seconds
- Medium documents (10-20 pages): 3-6 seconds
- Large documents (50+ pages): 6-12 seconds

Resource usage:
- CPU: 10-30% per conversion
- Memory: 150-500MB per conversion

Adjust `MaxConcurrentConversions` based on server capacity:
- 2-4 CPU cores: 2 concurrent conversions
- 4-8 CPU cores: 4 concurrent conversions
- 8+ CPU cores: 6-8 concurrent conversions

## Troubleshooting

**LibreOffice not found:**
```powershell
# Run bundle script
.\bundle-libreoffice.ps1

# Verify bundle exists
Test-Path FileConversionApi\LibreOffice\program\soffice.exe
```

**Timeouts:**
- Increase `LibreOffice.TimeoutSeconds` in appsettings.json
- Check server CPU and memory resources

**Permission errors:**
```powershell
# Grant IIS_IUSRS full access to App_Data
icacls "C:\inetpub\FileConversionApi\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T
```

**Health check:**
```powershell
# Basic health
curl http://localhost:3000/health

# Detailed diagnostics
curl http://localhost:3000/health/detailed

# Check logs
Get-Content FileConversionApi\App_Data\logs\*.log -Tail 50
```

## Development

**Build and run locally:**

```powershell
dotnet build FileConversionApi/FileConversionApi.csproj
dotnet run --project FileConversionApi/FileConversionApi.csproj -- --urls "http://localhost:3000"
```

**Test conversions:**

```powershell
.\test-conversion.ps1
```

**Project structure:**

```
FileConversionApi/          - Main .NET 8 application
├── Controllers/            - API endpoints (ConversionController, HealthController)
├── Services/               - Business logic and conversion engines
├── Middleware/             - Security and request handling
├── Models/                 - Configuration and data models
├── LibreOffice/            - Bundled LibreOffice runtime
└── App_Data/               - Temporary files and logs

bundle-libreoffice.ps1      - Create optimized LibreOffice bundle
test-conversion.ps1         - API testing script
```

**Technology stack:**
- .NET 8 / ASP.NET Core
- LibreOffice (document conversions)
- iText7 (PDF operations)
- DocumentFormat.OpenXml (DOCX manipulation)
- NPOI (Excel processing)
- Serilog (structured logging)
- AspNetCoreRateLimit (rate limiting)

## Documentation

- [DEPLOYMENT.md](DEPLOYMENT.md) - Complete deployment guide with IIS configuration and troubleshooting
- [ARCHITECTURE.md](ARCHITECTURE.md) - System design, components, and security considerations

## License

Built with .NET 8. LibreOffice included under Mozilla Public License v2.0.
