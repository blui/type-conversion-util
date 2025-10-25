# File Conversion API

Production-ready .NET 8 service for converting Office documents on Windows Server. Supports 32 different format conversions using bundled LibreOffice. No external dependencies required. Fully self-contained for air-gapped and isolated network deployments.

## Quick Start

```powershell
# Get the code
git clone <repository-url>
cd type-conversion-util

# Bundle LibreOffice (creates ~500MB optimized bundle)
.\bundle-libreoffice.ps1

# Build the application
dotnet build FileConversionApi/FileConversionApi.csproj

# Run the service (will show "Now listening on..." when ready)
dotnet run --project FileConversionApi/FileConversionApi.csproj -- --urls "http://localhost:3000"

# Test it works
curl http://localhost:3000/health
```

API docs available at `http://localhost:3000/api-docs`

## Supported Conversions

Converts between 32 different Office document formats:

**Documents:** DOC, DOCX, PDF, TXT, RTF, XML, HTML, ODT
**Spreadsheets:** XLSX, CSV, ODS
**Presentations:** PPTX, ODP
**Legacy:** SXW, SXC, SXI, SXD (OpenOffice 1.x)

All formats convert to PDF. Most Office formats can convert between each other.

Full details in [SUPPORTED_CONVERSIONS.md](SUPPORTED_CONVERSIONS.md)

## API

**Convert a file:**

```powershell
curl -X POST http://localhost:3000/api/convert \
  -F "file=@document.doc" \
  -F "targetFormat=pdf" \
  -o output.pdf
```

**Other endpoints:**

- `GET /api/supported-formats` - List supported conversions
- `GET /health` - Health check
- `GET /api-docs` - Swagger documentation

**Errors:** Standard HTTP codes with JSON error messages

## Configuration

Key settings in `appsettings.json`:

**Security:**

- IP whitelisting and rate limiting
- File size limits (50MB default)

**Performance:**

- Concurrent conversion limits
- Timeout settings

**File handling:**

- Temporary file cleanup
- Allowed file types

See `env.example` for all options

## Deployment

**Requirements:**

- Windows Server 2016+ or Windows 11
- .NET 8.0 Runtime + ASP.NET Core Hosting Bundle
- IIS 8.5+
- 4GB RAM minimum

**Deployment Process:**

```powershell
# Step 1: Build deployment package
cd FileConversionApi
.\deploy.ps1

# Step 2: Copy deploy\release folder to Windows Server
# Transfer to C:\inetpub\FileConversionApi on server

# Step 3: Configure IIS manually
# See DEPLOYMENT_NOTES.md for complete IIS setup instructions
```

**What's included in the package:**
- Compiled .NET application (Release build)
- LibreOffice bundle (500-550 MB optimized)
- Configuration files (appsettings.json, web.config)
- Required directory structure

**Post-deployment configuration:**
- Edit `appsettings.json` on the server to adjust settings
- No recompilation needed for configuration changes
- Restart IIS application pool to apply changes

See [DEPLOYMENT_NOTES.md](DEPLOYMENT_NOTES.md) for:
- Complete step-by-step IIS configuration
- Self-signed certificate setup for intranet
- Post-deployment configuration options
- Troubleshooting guide

## Security

Enterprise-grade security features:

- Proper CIDR-based IP whitelisting with bit-level validation
- Configurable CORS for intranet environments
- Rate limiting per IP and endpoint
- File type, size, and MIME validation
- Isolated file processing with automatic cleanup
- No external network calls (air-gap compliant)
- Comprehensive error handling with sanitized messages
- Security headers (CSP, X-Frame-Options, X-XSS-Protection)

**Production Configuration:**
- Set `"EnableIPFiltering": true` in appsettings.Production.json
- Configure `AllowedOrigins` for CORS restrictions
- Update IP whitelist with your network CIDR ranges

## Performance

Typical conversion times:

- Small documents (1-5 pages): 2-4 seconds
- Medium documents (10-20 pages): 3-6 seconds
- Large documents (50+ pages): 6-12 seconds

Uses about 150-500MB RAM per conversion. Adjust `MaxConcurrentConversions` based on your server capacity.

## Troubleshooting

**LibreOffice not found:**

- Run `.\bundle-libreoffice.ps1` to create the bundle
- Check that `FileConversionApi\LibreOffice\program\soffice.exe` exists

**Timeouts:**

- Increase `TimeoutSeconds` in appsettings.json
- Check server resources

**Permission errors:**

- Grant IIS_IUSRS full access to App_Data folder

**Check health:**

- Visit `/health` endpoint
- Check Windows Event Logs

## Development

**Build and run:**

```powershell
dotnet build FileConversionApi/FileConversionApi.csproj
dotnet run --project FileConversionApi/FileConversionApi.csproj -- --urls "http://localhost:3000"
```

**LibreOffice:** Required for most Office document conversions. Run `.\bundle-libreoffice.ps1` to create the bundle.

## Project Structure

```
FileConversionApi/          - Main .NET 8 application
├── Controllers/            - API endpoints
├── Services/               - Business logic and conversion engines
├── Middleware/             - Security and request handling
├── Models/                 - Configuration and data models
├── LibreOffice/            - Bundled LibreOffice (after running bundle script)
└── deploy.ps1              - Deployment packaging script

bundle-libreoffice.ps1      - Create LibreOffice bundle
test-conversion.ps1         - API testing script
```

## Documentation

**Configuration:**
- [CONFIGURATION_GUIDE.md](CONFIGURATION_GUIDE.md) - Complete configuration and deployment guide
- [DEPLOYMENT_NOTES.md](DEPLOYMENT_NOTES.md) - IIS deployment instructions

**Architecture:**
- [ARCHITECTURE.md](ARCHITECTURE.md) - System design and components
- [REFACTORING_REPORT.md](REFACTORING_REPORT.md) - Recent refactoring summary

**Security:**
- [SECURITY.md](SECURITY.md) - Security features and best practices
- [LIBREOFFICE_SECURITY.md](LIBREOFFICE_SECURITY.md) - LibreOffice security analysis

**Technical:**
- [SUPPORTED_CONVERSIONS.md](SUPPORTED_CONVERSIONS.md) - Full conversion matrix

Built with .NET 8.
