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
- .NET 8.0 Runtime
- IIS 8.5+
- 4GB RAM minimum

**Automated IIS Deployment (Recommended):**

```powershell
cd FileConversionApi

# Production deployment with all optimizations
.\deploy-iis.ps1 -EnableBackup -ConfigureFirewall

# Basic deployment
.\deploy-iis.ps1

# Custom configuration
.\deploy-iis.ps1 -IISSiteName "MyConversionAPI" -Port 8080 -EnableBackup
```

**What the automated script does:**
- Validates all prerequisites (.NET 8, IIS, disk space)
- Builds and deploys the application
- Configures IIS App Pool with production optimizations
- Sets up proper file permissions
- Configures Windows Firewall (optional)
- Enables HTTPS (optional)
- Verifies deployment with health checks
- **Backs up existing deployment** before updating (optional)

**Manual Deployment:**

```powershell
cd FileConversionApi
.\deploy.ps1
# Then manually configure IIS Manager
```

See [DEPLOYMENT_NOTES.md](DEPLOYMENT_NOTES.md) for complete deployment guide and IIS configuration details

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

**Build and test:**

```powershell
dotnet build FileConversionApi/FileConversionApi.csproj
dotnet test FileConversionApi.Tests/FileConversionApi.Tests.csproj
dotnet run --project FileConversionApi/FileConversionApi.csproj -- --urls "http://localhost:3000"
```

**LibreOffice:** Required for most Office document conversions. Run `.\bundle-libreoffice.ps1` to create the bundle.

## Project Structure

```
FileConversionApi/          - Main .NET 8 application
├── Controllers/            - API endpoints
├── Services/               - Business logic
├── Middleware/             - Security and request handling
├── LibreOffice/            - Bundled LibreOffice (after running bundle script)
└── deploy.ps1              - Deployment script

FileConversionApi.Tests/    - Unit and integration tests
bundle-libreoffice.ps1      - Create LibreOffice bundle
test-conversion.ps1         - API testing script
```

## Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) - System design and components
- [DEPLOYMENT_NOTES.md](DEPLOYMENT_NOTES.md) - Complete IIS deployment guide
- [SUPPORTED_CONVERSIONS.md](SUPPORTED_CONVERSIONS.md) - Full conversion matrix
- [LIBREOFFICE_BUNDLE_OPTIMIZATION.md](LIBREOFFICE_BUNDLE_OPTIMIZATION.md) - Bundle optimization details

Built with .NET 8. MIT License.
