# File Conversion API

Production-ready .NET 8 service for converting Office documents on Windows Server. Supports 32 format conversions using bundled LibreOffice. Fully self-contained for air-gapped and isolated network deployments.

## Features

- 32 different document format conversions
- Microsoft Office formats: DOC, DOCX, XLSX, PPTX, PDF
- Open formats: ODT, ODS, ODP, RTF, CSV, TXT, XML, HTML
- Legacy formats: SXW, SXC, SXI, SXD
- **Fully self-contained deployment** - no server dependencies (VC++ runtime bundled)
- **Zero initialization delays** - pre-created LibreOffice profile template
- No external dependencies or network calls (air-gap compliant)
- Enterprise security with rate limiting
- Automatic file cleanup and resource management
- Health monitoring and structured logging

## Quick Start

```powershell
# Get the code
git clone <repository-url>
cd type-conversion-util

# Bundle LibreOffice (creates ~500MB optimized bundle with VC++ runtime DLLs)
.\bundle-libreoffice.ps1

# Create pre-initialized user profile template (eliminates initialization issues)
.\create-libreoffice-profile-template.ps1

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

**Conversion Fidelity:**

All conversions preserve document content and visual appearance. DOCX files undergo optional preprocessing (font normalization, theme color conversion, style simplification) to improve conversion quality and LibreOffice compatibility. The goal is accurate visual representation in the output format, not byte-level fidelity. See [ARCHITECTURE.md](ARCHITECTURE.md#preprocessing-for-quality) for preprocessing details.

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
- **No other dependencies** - VC++ runtime and LibreOffice bundled

**Build on development machine:**

Prerequisites on build machine:
- LibreOffice installed at C:\Program Files\LibreOffice
- Visual C++ Redistributable 2015-2022 (to copy DLLs)
- .NET 8 SDK

```powershell
# Step 1: Create LibreOffice bundle with VC++ runtime DLLs (~500 MB)
.\bundle-libreoffice.ps1

# Step 2: Create pre-initialized profile template (~2 KB)
.\create-libreoffice-profile-template.ps1

# Step 3: Build deployment package (~550 MB total)
cd FileConversionApi
.\deploy.ps1

# Output: FileConversionApi\deploy\release\
```

**Deploy to IIS (on server):**

```powershell
# 1. Copy deployment package to server
# Source: FileConversionApi\deploy\release\
# Destination: D:\inetpub\wwwroot\Service\FileConversionApi

# 2. Set permissions (CRITICAL - required for application to work)
$deployPath = "D:\inetpub\wwwroot\Service\FileConversionApi"
icacls "$deployPath\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T
icacls "$deployPath\LibreOffice" /grant "IIS_IUSRS:(OI)(CI)RX" /T
icacls "$deployPath\libreoffice-profile-template" /grant "IIS_IUSRS:(OI)(CI)R" /T

# 3. Configure IIS application pool and site (see DEPLOYMENT.md)

# 4. Restart IIS
iisreset

# 5. Verify deployment
Invoke-RestMethod -Uri "http://localhost/health"
```

**What gets deployed:**
- .NET 8 application (~50 MB)
- LibreOffice bundle with VC++ runtime DLLs (~500 MB)
- Pre-initialized user profile template (~2 KB)
- Configuration and documentation

**Key benefits:**
- Completely self-contained - no VC++ Redistributable installation required
- Zero initialization delays - pre-created profile template
- Air-gap compliant - no internet connectivity needed

See [DEPLOYMENT.md](DEPLOYMENT.md) for detailed deployment, configuration, and troubleshooting instructions.

## Security

Enterprise-grade security features:

- **Optional API key authentication** - Control access with X-API-Key header
- **Rate limiting** - Per IP and endpoint limits
- **CORS configuration** - Control browser-based access
- **File validation** - Type, size, and MIME checks
- **Isolated processing** - Per-conversion cleanup
- **Air-gap compliant** - No external network calls
- **Security headers** - CSP, X-Frame-Options, X-XSS-Protection
- **Comprehensive logging** - Track all operations

### Enable API Key Authentication

Configure in `appsettings.json`:

```json
{
  "Security": {
    "RequireApiKey": true,
    "ApiKeys": [
      "apikey_live_your_secure_key_here"
    ],
    "AllowedOrigins": [
      "https://intranet.company.local"
    ]
  }
}
```

Generate secure keys:
```powershell
# PowerShell - Generate random API key
$bytes = New-Object byte[] 32
[Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
"apikey_live_" + [Convert]::ToBase64String($bytes) -replace '\+','-' -replace '/','_' -replace '=',''
```

Use in client code:
```csharp
client.DefaultRequestHeaders.Add("X-API-Key", "apikey_live_your_key_here");
```

For production deployments:
- Enable API key authentication for access control
- Configure CORS allowed origins for browser access
- Set appropriate file size limits
- Review rate limiting rules
- Rotate API keys periodically

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
# Run bundle scripts in order
.\bundle-libreoffice.ps1
.\create-libreoffice-profile-template.ps1

# Verify bundle exists
Test-Path FileConversionApi\LibreOffice\program\soffice.exe

# Verify profile template exists (should be 5-10 MB)
Test-Path FileConversionApi\libreoffice-profile-template
```

**Conversions hang or timeout (Exit code 1):**
```powershell
# Check if profile template is deployed
Test-Path "C:\inetpub\FileConversionApi\libreoffice-profile-template"

# If missing, recreate on build machine and redeploy
.\create-libreoffice-profile-template.ps1

# Kill any hung processes
taskkill /F /IM soffice.exe

# Check logs for profile template usage
Get-Content "C:\inetpub\FileConversionApi\App_Data\logs\*.log" -Tail 100 | findstr "profile"
```

**DLL not found (Exit code -1073741515):**
```powershell
# Verify VC++ runtime DLLs are bundled
Test-Path "C:\inetpub\FileConversionApi\LibreOffice\program\msvcp140.dll"
Test-Path "C:\inetpub\FileConversionApi\LibreOffice\program\vcruntime140.dll"
Test-Path "C:\inetpub\FileConversionApi\LibreOffice\program\msvcp140_atomic_wait.dll"

# If missing, recreate bundle on build machine with VC++ installed
.\bundle-libreoffice.ps1 -Force
```

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
FileConversionApi/                      - Main .NET 8 application
├── Controllers/                        - API endpoints (ConversionController, HealthController)
├── Services/                           - Business logic and conversion engines
├── Middleware/                         - Security and request handling
├── Models/                             - Configuration and data models
├── LibreOffice/                        - Bundled LibreOffice runtime (500MB)
├── libreoffice-profile-template/       - Pre-initialized user profile (5-10MB)
└── App_Data/                           - Temporary files and logs

bundle-libreoffice.ps1                  - Create optimized LibreOffice bundle with VC++ DLLs
create-libreoffice-profile-template.ps1 - Create pre-initialized user profile template
test-conversion.ps1                     - API testing script
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
