# File Conversion API

A simple .NET 8 service for converting Office documents on Windows Server. Supports 32 different format conversions using bundled LibreOffice. No external dependencies required.

## Quick Start

```powershell
# Get the code
git clone <repository-url>
cd type-conversion-util

# Bundle LibreOffice (creates ~500MB optimized bundle)
.\bundle-libreoffice.ps1

# Build and run
dotnet run --project FileConversionApi/FileConversionApi.csproj -- --urls "http://localhost:3000"

# Test it works
curl http://localhost:3000/health
```

API docs available at `http://localhost:3000/swagger`

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

**Quick deploy:**

```powershell
cd FileConversionApi
.\deploy.ps1
# Copy the 'deployment' folder to your IIS directory
```

See [DEPLOYMENT_NOTES.md](DEPLOYMENT_NOTES.md) for details

## Security

Built with security in mind:

- IP whitelisting and rate limiting
- File type and size validation
- Isolated file processing
- No external network calls
- Secure error handling

Enable IP filtering in production by setting `"EnableIPFiltering": true` in appsettings.json

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
dotnet run --project FileConversionApi/FileConversionApi.csproj
```

**LibreOffice:** Required for most Office document conversions. Run `.\bundle-libreoffice.ps1` to create the bundle.

## Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) - System design
- [DEPLOYMENT_NOTES.md](DEPLOYMENT_NOTES.md) - IIS deployment
- [SUPPORTED_CONVERSIONS.md](SUPPORTED_CONVERSIONS.md) - Format matrix

Built with .NET 8. MIT License.
