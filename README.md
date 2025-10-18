# File Conversion API

Production-ready Office document conversion service built with .NET 8 for Windows Server environments. Converts between 32 format combinations using bundled LibreOffice. Self-contained, secure, no external dependencies.

## Table of Contents

- [Quick Start](#quick-start)
- [Supported Conversions](#supported-conversions)
- [API Documentation](#api-documentation)
- [Configuration](#configuration)
- [Deployment](#deployment)
- [Security](#security)

## Quick Start

```powershell
# Clone and setup
git clone <repository-url>
cd type-conversion-util

# Bundle LibreOffice (required - creates optimized 517 MB bundle)
.\bundle-libreoffice.ps1

# Build
dotnet build FileConversionApi/FileConversionApi.csproj

# Run
dotnet run --project FileConversionApi/FileConversionApi.csproj --no-build

# Test
curl http://localhost:3000/health
```

Access API documentation at `http://localhost:3000/api-docs`

## Supported Conversions

**32 conversion paths across 16 input formats (Office documents only)**

### Documents (29 paths)

| Format | Converts To                    |
| ------ | ------------------------------ |
| DOC    | PDF, DOCX, TXT, RTF, ODT, HTML |
| DOCX   | PDF, TXT, DOC                  |
| PDF    | DOCX, DOC, TXT                 |
| TXT    | PDF, DOCX, DOC                 |
| RTF    | PDF                            |
| XML    | PDF                            |
| HTML   | PDF                            |

### Spreadsheets (5 paths)

| Format | Converts To |
| ------ | ----------- |
| XLSX   | PDF, CSV    |
| CSV    | XLSX        |
| ODS    | PDF, XLSX   |

### Presentations (3 paths)

| Format | Converts To |
| ------ | ----------- |
| PPTX   | PDF         |
| ODP    | PDF, PPTX   |

### Legacy Formats (4 paths)

OpenOffice 1.x formats (SXW, SXC, SXI, SXD) → PDF

**See [SUPPORTED_CONVERSIONS.md](SUPPORTED_CONVERSIONS.md) for complete details**

## API Documentation

### Base Endpoint

```
POST /api/convert
```

**Parameters:**

- `file` (form-data): File to convert
- `targetFormat` (string): Target extension (pdf, docx, txt, etc.)

**Example:**

```powershell
curl -X POST http://localhost:3000/api/convert `
  -F "file=@document.doc" `
  -F "targetFormat=pdf" `
  -o output.pdf
```

**Response (200):**

```json
{
  "success": true,
  "inputFormat": "doc",
  "outputFormat": "pdf",
  "fileSize": 245760,
  "processingTime": 3200
}
```

### Additional Endpoints

| Endpoint                 | Method | Purpose                        |
| ------------------------ | ------ | ------------------------------ |
| `/api/supported-formats` | GET    | List all supported conversions |
| `/health`                | GET    | Basic health check             |
| `/health/detailed`       | GET    | System diagnostics             |
| `/api-docs`              | GET    | Interactive Swagger UI         |

### Error Codes

| Code                | HTTP | Description            |
| ------------------- | ---- | ---------------------- |
| INVALID_FORMAT      | 400  | Unsupported conversion |
| FILE_TOO_LARGE      | 413  | Exceeds size limit     |
| CONVERSION_FAILED   | 500  | Processing error       |
| RATE_LIMIT_EXCEEDED | 429  | Too many requests      |
| IP_NOT_ALLOWED      | 403  | Not whitelisted        |

## Configuration

Configure via `appsettings.json` or environment variables.

### Security Settings

```json
{
  "Security": {
    "EnableIPFiltering": true,
    "IPWhitelist": ["192.168.1.0/24", "10.0.0.0/8"]
  },
  "IpRateLimiting": {
    "GeneralRules": [
      {
        "Endpoint": "*",
        "Period": "1m",
        "Limit": 30
      }
    ]
  }
}
```

### File Handling

```json
{
  "FileHandling": {
    "MaxFileSize": 52428800,
    "TempDirectory": "App_Data/temp/uploads",
    "TempFileRetentionHours": 24
  }
}
```

### LibreOffice

```json
{
  "LibreOffice": {
    "SdkPath": "LibreOffice",
    "ForceBundled": true,
    "TimeoutSeconds": 300,
    "MaxConcurrentConversions": 2
  }
}
```

### Performance

```json
{
  "Concurrency": {
    "MaxConcurrentConversions": 2,
    "MaxQueueSize": 10
  }
}
```

**Configuration Guide:** See `env.example` for all available settings

## Deployment

### Prerequisites

**System Requirements:**

- Windows Server 2016+ or Windows 11
- .NET 8.0 Runtime
- IIS 8.5 or later
- 4GB RAM (8GB recommended)
- 2GB disk space

**Network Requirements:**

- No internet access required for operation
- Optional: HTTPS certificate for secure communication

### Windows IIS Deployment (Recommended)

```powershell
# Navigate to API directory
cd FileConversionApi

# Build deployment package
.\deploy.ps1

# The script creates a 'deployment' folder ready to copy to IIS
# Follow the on-screen manual deployment instructions
```

The deployment script creates:

- Release build of application
- Production appsettings.json
- LibreOffice bundle (517 MB)
- Required directory structure

### Manual Deployment

```powershell
# Build deployment package (from FileConversionApi directory)
cd FileConversionApi
.\deploy.ps1

# This creates a 'deployment' folder with:
# - Compiled application
# - LibreOffice bundle (517 MB)
# - Production appsettings.json
# - Required directory structure

# Manually copy to IIS directory
Copy-Item deployment\* -Destination C:\inetpub\wwwroot\FileConversionApi -Recurse

# Configure IIS
# See DEPLOYMENT_NOTES.md for detailed instructions
```

**Deployment Guide:** See [DEPLOYMENT_NOTES.md](DEPLOYMENT_NOTES.md) for comprehensive instructions

## Security

### Defense-in-Depth Architecture

**Network Layer:**

- IP whitelisting with CIDR notation
- Rate limiting per IP address
- No external API dependencies

**Application Layer:**

- Multi-layer input validation
- File size and type restrictions
- Malicious content detection
- Secure error handling

**Process Layer:**

- Isolated temporary directories
- Automatic file cleanup
- Resource usage limits
- Process timeout enforcement

### Security Best Practices

```powershell
# Enable IP filtering (production)
{
  "Security": {
    "EnableIPFiltering": true,
    "IPWhitelist": ["10.0.0.0/8"]
  }
}

# Configure rate limits
{
  "IpRateLimiting": {
    "GeneralRules": [
      { "Endpoint": "*", "Period": "1m", "Limit": 30 }
    ]
  }
}

# Restrict file permissions
icacls "C:\inetpub\FileConversionApi" /grant "IIS_IUSRS:(OI)(CI)RX" /T
icacls "C:\inetpub\FileConversionApi\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T
```

## Performance

### Benchmark Results

| Document Type  | Size        | Conversion Time | Memory Usage |
| -------------- | ----------- | --------------- | ------------ |
| DOCX (simple)  | 1-5 pages   | 2-4s            | 150-250MB    |
| DOCX (complex) | 10-20 pages | 3-6s            | 200-350MB    |
| DOCX (large)   | 50+ pages   | 6-12s           | 300-500MB    |
| XLSX           | <100KB      | 1-3s            | 100-200MB    |
| Image          | <10MB       | 1-2s            | 150-300MB    |

### Performance Tuning

Adjust concurrent conversions based on CPU cores:

```json
{
  "Concurrency": {
    "MaxConcurrentConversions": 4,
    "MaxQueueSize": 20
  }
}
```

**Recommendation:** 1-2 concurrent conversions per CPU core

## Troubleshooting

### Common Issues

**LibreOffice not found:**

```powershell
# Verify bundle exists
Test-Path FileConversionApi\LibreOffice\program\soffice.exe

# Rebundle if missing
.\bundle-libreoffice.ps1
```

**Port already in use:**

```powershell
# Find process
netstat -ano | findstr :3000

# Kill or change port in launchSettings.json
```

**Conversion timeout:**

```json
{
  "LibreOffice": {
    "TimeoutSeconds": 600
  }
}
```

**Permission errors:**

```powershell
# Grant IIS permissions
icacls "C:\inetpub\FileConversionApi\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T
```

### Diagnostic Commands

```powershell
# Check health
curl http://localhost:3000/health
curl http://localhost:3000/health/detailed

# View logs
Get-Content C:\inetpub\logs\file-conversion-api-*.log -Tail 50

# Check Event Log
Get-EventLog -LogName Application -Source FileConversionApi -Newest 20
```

## Development

### Build and Test

```powershell
# Restore packages
   dotnet restore FileConversionApi/FileConversionApi.csproj
   dotnet restore FileConversionApi.Tests/FileConversionApi.Tests.csproj

# Build
   dotnet build FileConversionApi/FileConversionApi.csproj

# Run tests
dotnet test FileConversionApi.Tests/FileConversionApi.Tests.csproj

# Run with hot reload
dotnet watch run --project FileConversionApi/FileConversionApi.csproj
```

### Project Structure

```
FileConversionApi/
├── Controllers/           # API endpoints
├── Services/             # Business logic
│   ├── Interfaces/       # Service contracts
│   ├── ConversionEngine.cs
│   ├── LibreOfficeService.cs
│   └── DocumentService.cs
├── Middleware/           # Security, logging
├── Models/               # Configuration, DTOs
└── Program.cs            # Entry point

FileConversionApi.Tests/  # Unit tests
```

## LibreOffice Requirements

**Office conversions require LibreOffice bundle.**

### What Works Without LibreOffice:

- PDF text extraction
- XLSX/CSV conversions
- Image conversions
- Text to PDF

### What Requires LibreOffice:

- DOC/DOCX conversions
- Office to PDF conversions
- PDF to Office conversions
- OpenDocument formats

**Bundle command:** `.\bundle-libreoffice.ps1` (creates optimized 517 MB bundle)

## Documentation

- **Architecture:** [ARCHITECTURE.md](ARCHITECTURE.md)
- **Deployment:** [DEPLOYMENT_NOTES.md](DEPLOYMENT_NOTES.md)
- **Conversion Matrix:** [SUPPORTED_CONVERSIONS.md](SUPPORTED_CONVERSIONS.md)
- **API Specification:** `http://localhost:3000/api-docs` (when running)

## License

MIT License - see LICENSE file

### Third-Party Components

- **LibreOffice:** LGPL v3
- **.NET 8:** MIT License
- **NuGet Packages:** Various permissive licenses

## Support

- **Issues:** GitHub Issues
- **Testing:** Use `test-conversion.ps1` for operational verification

---

**Built with .NET 8**
