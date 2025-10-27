# File Conversion API

A simple, self-contained .NET 8 service that converts Office documents on Windows Server. Handles 32 different format conversions using a bundled copy of LibreOffice. Works perfectly in air-gapped environments with no internet connection required.

## What It Does

Converts documents between these formats:

- **Office files**: DOC, DOCX, XLSX, PPTX, PDF
- **Open formats**: ODT, ODS, ODP, RTF, CSV, TXT, XML, HTML
- **Legacy stuff**: SXW, SXC, SXI, SXD (old OpenOffice formats)

**The best part?** Everything you need is included - no surprise dependencies, no internet calls, no Microsoft Office license required. Just copy the files to a server and it works.

## Quick Start

**What you'll need first:**
- .NET 8 SDK: [Download here](https://dotnet.microsoft.com/download/dotnet/8.0)
- LibreOffice: [Download here](https://www.libreoffice.org/download/) (just install it normally)
- Visual C++ Redistributable: [Download here](https://aka.ms/vs/17/release/vc_redist.x64.exe)

```powershell
# Get the code
git clone <repository-url>
cd type-conversion-util

# Create the LibreOffice bundle (takes a minute, creates ~500MB package)
.\bundle-libreoffice.ps1

# Create the profile template (prevents startup delays)
.\create-libreoffice-profile-template.ps1

# Build it
dotnet build FileConversionApi/FileConversionApi.csproj

# Run it
dotnet run --project FileConversionApi/FileConversionApi.csproj -- --urls "http://localhost:3000"
```

**Test it out:**

```powershell
# Check if it's running
curl http://localhost:3000/health

# Convert a file
"Hello World" | Out-File test.txt
curl -X POST http://localhost:3000/api/convert -F "file=@test.txt" -F "targetFormat=pdf" -o output.pdf
```

Browse to `http://localhost:3000/api-docs` for interactive API documentation.

## How to Use It

**Convert a file:**

```powershell
curl -X POST http://localhost:3000/api/convert \
  -F "file=@document.doc" \
  -F "targetFormat=pdf" \
  -o output.pdf
```

**Get metadata about the conversion:**

```powershell
curl -X POST "http://localhost:3000/api/convert?metadata=true" \
  -F "file=@document.docx" \
  -F "targetFormat=pdf"
```

**Other useful endpoints:**

- `POST /api/convert` - Convert your file
- `GET /api` - See what the API can do
- `GET /api/supported-formats` - List all conversion options
- `GET /health` - Quick health check
- `GET /api-docs` - Interactive documentation

## What Can It Convert?

**32 different conversions across all these formats:**

| Type | Input | Output |
|------|-------|--------|
| Documents | DOC, DOCX, PDF, TXT, RTF, XML, HTML, ODT | PDF, DOCX, DOC, TXT, RTF, ODT, HTML |
| Spreadsheets | XLSX, CSV, ODS | PDF, XLSX, CSV |
| Presentations | PPTX, ODP | PDF, PPTX |
| Legacy Files | SXW, SXC, SXI, SXD | PDF |

**Popular conversions:**
- Any document to PDF (21 different paths)
- PDF to editable DOCX (extracts text nicely)
- Excel to CSV (great for data exports)
- Old DOC to modern DOCX

**About conversion quality:** The output looks like the original and preserves content. For DOCX files, we do some behind-the-scenes cleanup (fixing fonts, normalizing colors) to make LibreOffice happier, which gives you better-looking PDFs.

## Configuration

Edit `appsettings.json` to customize behavior. Here are the important bits:

**Security settings:**
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

**Performance tuning:**
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
        "Microsoft": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "App_Data\\logs\\file-conversion-api-.log" } }
    ]
  }
}
```

## Deploying to Production

**Server requirements:**
- Windows Server 2016+ (or Windows 11)
- .NET 8 Runtime + ASP.NET Core Hosting Bundle
- IIS 8.5 or newer
- 4GB RAM (8GB is better for busy servers)
- **That's it** - LibreOffice and Visual C++ runtime are bundled with the deployment

**Building the deployment package:**

On your development machine (where you have LibreOffice installed):

```powershell
# Step 1: Create LibreOffice bundle with all dependencies (~500 MB)
.\bundle-libreoffice.ps1

# Step 2: Create profile template (prevents initialization issues)
.\create-libreoffice-profile-template.ps1

# Step 3: Build everything into a deployment package (~550 MB total)
cd FileConversionApi
.\deploy.ps1

# Output folder: FileConversionApi\deploy\release\
```

**Setting up IIS:**

On your server:

```powershell
# 1. Copy files to server (adjust paths as needed)
# Source: FileConversionApi\deploy\release\
# Destination: C:\inetpub\FileConversionApi

# 2. Set permissions (CRITICAL - the app won't work without this!)
$deployPath = "C:\inetpub\FileConversionApi"
icacls "$deployPath\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T
icacls "$deployPath\LibreOffice" /grant "IIS_IUSRS:(OI)(CI)RX" /T
icacls "$deployPath\libreoffice-profile-template" /grant "IIS_IUSRS:(OI)(CI)R" /T

# 3. Configure IIS (create app pool and site - see DEPLOYMENT.md for details)

# 4. Start it up
iisreset

# 5. Make sure it works
Invoke-RestMethod -Uri "http://localhost/health"
```

**What gets deployed:**
- Your .NET app (~50 MB)
- Complete LibreOffice bundle (~500 MB) - includes all the DLLs it needs
- Profile template (~2 KB) - eliminates startup delays
- Configuration files

**Why this is nice:**
- Completely self-contained - no hunting for missing DLLs
- Works in air-gapped environments (no internet needed)
- No initialization delays when converting files

See [DEPLOYMENT.md](DEPLOYMENT.md) for the full step-by-step guide, troubleshooting tips, and advanced configuration.

## Security

Built with enterprise security in mind:

- **API key authentication** - Control who can use the service
- **Rate limiting** - Prevent abuse and overload
- **CORS support** - Control browser access
- **File validation** - Check file types, sizes, and content
- **Isolated processing** - Each conversion runs separately and cleans up after itself
- **Air-gap ready** - No surprise internet calls
- **Security headers** - Standard web security headers included
- **Full audit logging** - Know what happened and when

**Enabling API keys:**

Edit `appsettings.json`:

```json
{
  "Security": {
    "RequireApiKey": true,
    "ApiKeys": [
      "apikey_live_your_secure_key_here"
    ]
  }
}
```

Generate a secure key:
```powershell
$bytes = New-Object byte[] 32
[Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
"apikey_live_" + [Convert]::ToBase64String($bytes) -replace '\+','-' -replace '/','_' -replace '=',''
```

Then include it in requests:
```csharp
client.DefaultRequestHeaders.Add("X-API-Key", "apikey_live_your_key_here");
```

## Performance

**Typical conversion times:**
- Small files (1-5 pages): 2-4 seconds
- Medium files (10-20 pages): 3-6 seconds
- Large files (50+ pages): 6-12 seconds

**Resource usage per conversion:**
- CPU: 10-30%
- Memory: 150-500MB

**Tuning for your server:**

| Server CPUs | MaxConcurrentConversions |
|-------------|--------------------------|
| 2-4 cores   | 2                        |
| 4-8 cores   | 4                        |
| 8+ cores    | 6-8                      |

## Troubleshooting

**LibreOffice bundle missing:**
```powershell
# Make sure you ran both setup scripts
.\bundle-libreoffice.ps1
.\create-libreoffice-profile-template.ps1

# Verify they worked
Test-Path FileConversionApi\LibreOffice\program\soffice.exe
Test-Path FileConversionApi\libreoffice-profile-template
```

**Conversions hang or time out:**
```powershell
# Check if profile template is deployed
Test-Path "C:\inetpub\FileConversionApi\libreoffice-profile-template"

# Kill any stuck processes
taskkill /F /IM soffice.exe

# Check the logs
Get-Content "C:\inetpub\FileConversionApi\App_Data\logs\*.log" -Tail 100
```

**Missing DLL errors:**
```powershell
# Verify Visual C++ runtime DLLs are in the bundle
Test-Path "C:\inetpub\FileConversionApi\LibreOffice\program\msvcp140.dll"
Test-Path "C:\inetpub\FileConversionApi\LibreOffice\program\vcruntime140.dll"

# If missing, recreate the bundle on a machine with VC++ installed
.\bundle-libreoffice.ps1 -Force
```

**Permission errors:**
```powershell
# Grant IIS users full access to App_Data
icacls "C:\inetpub\FileConversionApi\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T

# Restart IIS
iisreset
```

**Check health:**
```powershell
# Basic health check
curl http://localhost:3000/health

# Detailed diagnostics
curl http://localhost:3000/health/detailed

# View logs
Get-Content FileConversionApi\App_Data\logs\*.log -Tail 50
```

## Project Structure

```
FileConversionApi/
├── Controllers/          - API endpoints (convert files, health checks)
├── Services/             - Conversion logic and business rules
├── Middleware/           - Security and request handling
├── Models/               - Configuration and data structures
├── Utilities/            - Helper functions
├── LibreOffice/          - Bundled LibreOffice (~500MB)
├── libreoffice-profile-template/  - Pre-built profile (~2KB)
└── App_Data/             - Temp files and logs

Scripts:
bundle-libreoffice.ps1                  - Packages LibreOffice with dependencies
create-libreoffice-profile-template.ps1 - Creates the profile template
```

**Tech stack:**
- .NET 8 / ASP.NET Core
- LibreOffice (handles most conversions)
- iText7 (PDF operations)
- DocumentFormat.OpenXml (DOCX manipulation)
- NPOI (Excel files)
- Serilog (logging)
- AspNetCoreRateLimit (rate limiting)

## Documentation

- [DEPLOYMENT.md](DEPLOYMENT.md) - Complete IIS deployment guide with troubleshooting
- [ARCHITECTURE.md](ARCHITECTURE.md) - How it all works under the hood

## License

Built with .NET 8. LibreOffice is included under Mozilla Public License v2.0.
