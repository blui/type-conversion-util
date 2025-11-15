# File Conversion API

A self-contained .NET 8 service for converting Office documents using bundled LibreOffice.

## What It Does

Converts between these formats:

- **Office**: DOC, DOCX, XLSX, PPTX, PDF
- **Other**: CSV, TXT, XML, HTML, HTM

## Quick Start

**Prerequisites:**

- .NET 8 SDK
- LibreOffice installed
- Visual C++ Redistributable

```powershell
# Clone and setup
git clone <repository-url>
cd type-conversion-util

# Create LibreOffice bundle (~500MB)
.\bundle-libreoffice.ps1
.\create-libreoffice-profile-template.ps1

# Build and run
dotnet build FileConversionApi/FileConversionApi.csproj
dotnet run --project FileConversionApi/FileConversionApi.csproj -- --urls "http://localhost:3000"
```

**Test it:**

```powershell
curl http://localhost:3000/health
"Hello World" | Out-File test.txt
curl -X POST http://localhost:3000/api/convert -F "file=@test.txt" -F "targetFormat=pdf" -o output.pdf
```

Browse to `http://localhost:3000/api-docs` for interactive documentation.

## API Usage

**Convert a file:**

```powershell
curl -X POST http://localhost:3000/api/convert -F "file=@document.doc" -F "targetFormat=pdf" -o output.pdf
```

**With metadata:**

```powershell
curl -X POST "http://localhost:3000/api/convert?metadata=true" -F "file=@document.docx" -F "targetFormat=pdf"
```

**Endpoints:**

- `POST /api/convert` - Convert file
- `GET /api` - API information
- `GET /api/supported-formats` - List conversions
- `GET /health` - Health check
- `GET /api-docs` - Documentation

## Supported Conversions

### Documents

| Input Format | Output Formats               |
| ------------ | ---------------------------- |
| DOC          | PDF, DOCX, TXT, HTML, HTM    |
| DOCX         | PDF, DOC, TXT                |
| PDF          | DOCX, DOC, TXT               |
| TXT          | PDF, DOCX, DOC               |
| XML          | PDF                          |
| HTML         | PDF                          |
| HTM          | PDF                          |

### Spreadsheets

| Input Format | Output Formats |
| ------------ | -------------- |
| XLSX         | PDF, CSV       |
| CSV          | XLSX           |

### Presentations

| Input Format | Output Formats |
| ------------ | -------------- |
| PPTX         | PDF            |

## Configuration

Edit `appsettings.json`:

**Rate limiting:**

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

**File handling:**

```json
{
  "FileHandling": {
    "MaxFileSize": 52428800,
    "TempDirectory": "App_Data\\temp\\uploads",
    "OutputDirectory": "App_Data\\temp\\converted"
  }
}
```

**Concurrency:**

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

## Security

**API Key Authentication:**

Edit `appsettings.json`:

```json
{
  "Security": {
    "RequireApiKey": true,
    "ApiKeys": ["apikey_live_your_key_here"]
  }
}
```

Generate a secure key:

```powershell
$bytes = New-Object byte[] 32
[Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
"apikey_live_" + [Convert]::ToBase64String($bytes) -replace '\+','-' -replace '/','_' -replace '=',''
```

Use in requests:

```csharp
client.DefaultRequestHeaders.Add("X-API-Key", "apikey_live_your_key_here");
```

**Security features:**

- API key authentication
- IP-based rate limiting
- CORS support
- File validation (type, size, extension)
- Process isolation per conversion
- Security headers (CSP, X-Frame-Options, etc.)
- Structured logging with operation IDs

## Deployment

**Server requirements:**

- Windows Server 2016+ or Windows 11
- .NET 8 Runtime + ASP.NET Core Hosting Bundle
- IIS 8.5+
- 4GB RAM (8GB recommended)

**Build deployment package:**

```powershell
# On dev machine with LibreOffice
.\bundle-libreoffice.ps1
.\create-libreoffice-profile-template.ps1
cd FileConversionApi
.\deploy.ps1
```

**Deploy to IIS:**

```powershell
# Copy files to ..\inetpub\FileConversionApi

# Set permissions
$deployPath = "..\inetpub\FileConversionApi"
icacls "$deployPath\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T
icacls "$deployPath\LibreOffice" /grant "IIS_IUSRS:(OI)(CI)RX" /T
icacls "$deployPath\libreoffice-profile-template" /grant "IIS_IUSRS:(OI)(CI)R" /T

# Configure IIS (see DEPLOYMENT.md for details)
# Start service
iisreset
Invoke-RestMethod -Uri "http://localhost/health"
```

See [DEPLOYMENT.md](DEPLOYMENT.md) for complete guide.

## Troubleshooting

**Missing bundle:**

```powershell
.\bundle-libreoffice.ps1
.\create-libreoffice-profile-template.ps1
Test-Path FileConversionApi\LibreOffice\program\soffice.exe
```

**Conversions timeout:**

```powershell
taskkill /F /IM soffice.exe
Get-Content "C:\inetpub\FileConversionApi\App_Data\logs\*.log" -Tail 100
```

**Permission errors:**

```powershell
icacls "C:\inetpub\FileConversionApi\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T
iisreset
```

## Tech Stack

- .NET 8 / ASP.NET Core
- LibreOffice (document conversions)
- iText7 (PDF operations)
- DocumentFormat.OpenXml (DOCX handling)
- NPOI (Excel processing)
- Serilog (logging)
- AspNetCoreRateLimit (rate limiting)

## Documentation

- [DEPLOYMENT.md](DEPLOYMENT.md) - Complete IIS deployment guide
- [ARCHITECTURE.md](ARCHITECTURE.md) - Technical architecture details
