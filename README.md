# File Conversion API

A self-contained .NET 8 service for converting Office documents using bundled LibreOffice.

## What It Does

Converts between these formats:

- **Office**: DOC, DOCX, XLSX, PPTX, PDF
- **Other**: CSV, TXT, HTML, HTM

## Quick Start

**Prerequisites:**

- .NET 8 SDK
- LibreOffice installed (for the local bundling step below; the bundle then ships with the
  service and the host no longer needs LibreOffice installed)

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

Source of truth: `FileConversionApi/Constants.cs` (`SupportedFormats.ConversionMatrix`).

### Documents

| Input Format | Output Formats               |
| ------------ | ---------------------------- |
| DOC          | PDF, TXT, DOCX, HTML, HTM    |
| DOCX         | PDF, TXT, DOC, HTML, HTM     |
| PDF          | DOCX, DOC, TXT               |
| TXT          | PDF, DOCX, DOC               |
| HTML         | PDF, DOCX                    |
| HTM          | PDF, DOCX                    |

### Spreadsheets

| Input Format | Output Formats |
| ------------ | -------------- |
| XLSX         | CSV, PDF       |
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
    "MaxConcurrentConversions": 2
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

Two production deploy paths are supported, both Windows-hosted:

- **On-premises IIS**: Windows Server 2016+ with IIS 8.5+ and the ASP.NET Core 8 Hosting Bundle. See [DEPLOYMENT.md](DEPLOYMENT.md).
- **Azure App Service (Windows)**: managed App Service Plan (P1V3 recommended, B2 acceptable) plus a Bicep template at `azure/appservice.bicep`. See [DEPLOYMENT-AZURE.md](DEPLOYMENT-AZURE.md).

Both paths consume the same site bundle produced by `FileConversionApi\deploy.ps1`. Build it once on a developer machine that has LibreOffice installed:

```powershell
.\bundle-libreoffice.ps1
.\create-libreoffice-profile-template.ps1
cd FileConversionApi
.\deploy.ps1
# Output: FileConversionApi\deploy\release\  (~700 MB across the .NET app, the LibreOffice bundle,
# the profile template, the bundled Node engine, and the App_Data\ subdirectories)
```

For the IIS path, copy `deploy\release\` onto the target server and follow `DEPLOYMENT.md` from "Deploying to the Server" onwards. For the Azure path, run `.\deploy-azure.ps1` from the repo root to wrap the bundle into a zip and follow `DEPLOYMENT-AZURE.md` from "One-Time Provisioning" onwards.

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
- LibreOffice (document conversions; hop 1 of the DOC/DOCX -> HTML pipeline; fallback HTML -> PDF for arbitrary HTML via the `writer_web_pdf_Export` filter after a small print-CSS preprocess in `DocumentService`)
- Bundled Node engine — PDF -> HTML via `engine/pdf-to-html.mjs` (pdfjs-dist + @napi-rs/canvas) as hop 2 of the DOC/DOCX -> HTML pipeline
- iText7 + itext7.bouncy-castle-adapter (txt -> pdf, and HTML -> PDF reconstruction of pipeline-output HTML via `PipelineOutputHtmlToPdfRenderer`)
- DocumentFormat.OpenXml (DOCX read/write, and HTML -> DOCX reconstruction of pipeline-output HTML via `PipelineOutputHtmlToDocxRenderer`)
- HtmlToOpenXml.dll (fallback in-process HTML -> DOCX for arbitrary HTML)
- NPOI (Excel processing)
- Serilog (logging)
- AspNetCoreRateLimit (rate limiting)

## Documentation

- [DEPLOYMENT.md](DEPLOYMENT.md) - Complete IIS deployment guide
- [ARCHITECTURE.md](ARCHITECTURE.md) - Technical architecture details
