# Changelog

All notable changes to the File Conversion API will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.5.0] - 2025-01-XX

### Breaking Changes

**Deprecated Format Removal:**
- Legacy file formats have been completely removed from the API
- Formats removed: sxw, sxc, sxi, sxd, odg, odf
- Attempting to upload these formats will now return a 400 Bad Request error
- No configuration option to re-enable these formats

### Removed

**Deprecated File Format Support:**
- StarOffice 1.x formats: sxw (Writer), sxc (Calc), sxi (Impress), sxd (Draw)
- Specialized ODF formats: odg (Graphics), odf (Formula)
- Removed 6 conversion paths (all to PDF only)
- Removed deprecation configuration properties from FileHandlingConfig
- Removed deprecation status from health endpoints
- Removed deprecation warning headers
- Removed deprecation logging infrastructure

**API Changes:**
- Removed `FileHandling:EnableDeprecatedFormats` configuration property
- Removed `FileHandling:WarnOnDeprecatedFormats` configuration property
- Removed `FileHandling:DeprecatedFormats` configuration property
- Removed `DeprecatedFormat` model class
- Removed `DeprecationStatus` model class
- Removed `Deprecation` property from `DetailedHealthResponse`
- Removed `DeprecatedFormats` property from `SupportedFormatsResponse`

### Changed

- API version updated from 0.4.0 to 0.5.0
- Reduced format conversion count from 32 to 26 conversions
- Updated documentation to reflect format removal
- Simplified configuration by removing deprecation-related settings

### Migration

**For users with files in removed formats:**
- Convert files to supported formats before upgrading to version 0.5.0
- See MIGRATION.md for detailed conversion instructions
- Recommended target formats:
  - sxw → ODT or DOCX
  - sxc → ODS or XLSX
  - sxi → ODP or PPTX
  - sxd → PDF
  - odg → PDF or SVG
  - odf → PDF or MathML

**For administrators:**
- Remove deprecation configuration from appsettings.json before deploying
- Update allowed file extensions if explicitly configured
- Test file uploads to ensure only supported formats are accepted

## [0.4.0] - 2025-01-XX

### Breaking Changes

**Deprecated Format Support:**
- Legacy file formats (sxw, sxc, sxi, sxd, odg, odf) are now **disabled by default**
- New deployments will reject these formats with 400 Bad Request unless explicitly enabled
- Existing deployments must opt-in to continue using deprecated formats

**To re-enable deprecated formats temporarily:**
```json
{
  "FileHandling": {
    "EnableDeprecatedFormats": true
  }
}
```

**Important:** Deprecated formats will be completely removed in version 0.5.0.

### Added

**Deprecation Infrastructure:**
- New configuration properties:
  - `FileHandling:EnableDeprecatedFormats` (default: false) - Control deprecated format support
  - `FileHandling:WarnOnDeprecatedFormats` (default: true) - Control deprecation warnings
  - `FileHandling:DeprecatedFormats` - List of deprecated format extensions
- Deprecation warnings via `X-Deprecation-Warning` HTTP response header
- Comprehensive deprecation logging with operation ID, IP address, and format details
- Deprecation status in `/health/detailed` endpoint showing:
  - Current configuration status
  - List of deprecated formats
  - Removal version (0.5.0)
  - Migration guidance
- Startup validation with warning/info logs about deprecation status

**API Enhancements:**
- `/api/supported-formats` now includes `DeprecatedFormats` array with:
  - Format extension
  - Deprecation reason
  - Removal version
  - Migration path guidance
- Enhanced error messages for rejected deprecated formats with migration instructions

**Documentation:**
- New `MIGRATION.md` guide with:
  - Overview of deprecated formats and rationale
  - Step-by-step migration instructions
  - Batch conversion PowerShell script
  - LibreOffice CLI conversion examples
  - FAQ and troubleshooting
- Updated `README.md` with deprecation notices
- Updated API documentation (Swagger/OpenAPI) with deprecation markers

### Changed

- API version updated from 0.3.0 to 0.4.0
- Swagger UI now displays deprecation notice in API description
- Default behavior: deprecated formats disabled (can be enabled in configuration)

### Deprecated

The following file formats are deprecated and will be removed in version 0.5.0:

**StarOffice 1.x Formats (discontinued 2005):**
- `sxw` - StarOffice Writer (→ migrate to ODT or DOCX)
- `sxc` - StarOffice Calc (→ migrate to ODS or XLSX)
- `sxi` - StarOffice Impress (→ migrate to ODP or PPTX)
- `sxd` - StarOffice Draw (→ migrate to ODG or PDF)

**Specialized ODF Formats (niche use cases):**
- `odg` - OpenDocument Graphics (→ migrate to PDF or SVG)
- `odf` - OpenDocument Formula (→ migrate to PDF or MathML)

**Rationale:**
- StarOffice formats: 20 years obsolete, extremely rare, limited conversion support
- Specialized ODF formats: Not commonly used in document conversion workflows

### Migration

**For users with legacy files:**
1. See `MIGRATION.md` for comprehensive migration guide
2. Use provided PowerShell batch conversion script
3. Convert files to modern formats (ODT, DOCX, XLSX, PPTX, PDF)
4. Test conversions before version 0.5.0 release

**For administrators:**
1. Monitor logs for `DEPRECATION` entries to identify usage
2. Contact users with deprecated format usage
3. Plan migration timeline before version 0.5.0
4. Temporarily enable deprecated formats if needed: `EnableDeprecatedFormats: true`

### Monitoring

**Track deprecated format usage:**
```powershell
# View all deprecation logs
Get-Content "App_Data\logs\*.log" | Select-String "DEPRECATION"

# Count usage by format
Get-Content "App_Data\logs\*.log" |
  Select-String "DEPRECATION: Processing deprecated format" |
  ForEach-Object { ($_ -split "'")[1] } |
  Group-Object |
  Sort-Object Count -Descending
```

**Check deprecation status:**
- Startup logs show deprecation configuration
- `/health/detailed` endpoint shows current status
- Response headers include `X-Deprecation-Warning` when applicable

## [0.3.0] - 2025-01-XX

### Added
- Initial production release
- Support for 32 different document format conversions
- 19 input format types supported
- LibreOffice-based conversion engine
- Self-contained deployment with bundled LibreOffice
- API endpoints:
  - `POST /api/convert` - File conversion
  - `GET /api` - API information
  - `GET /api/supported-formats` - Format capabilities
  - `GET /health` - Basic health check
  - `GET /health/detailed` - Detailed diagnostics
- Security features:
  - API key authentication
  - IP-based rate limiting
  - CORS support
  - File validation (size, type, extension)
  - Security headers (CSP, X-Frame-Options, etc.)
- DOCX preprocessing for improved LibreOffice compatibility
- Concurrency control (configurable max simultaneous conversions)
- Comprehensive structured logging with Serilog
- Automatic temporary file cleanup
- Health check endpoints for load balancer integration
- Swagger/OpenAPI documentation
- IIS hosting support with sub-application deployment

### Format Support
**Documents:** DOC, DOCX, PDF, TXT, RTF, XML, HTML, HTM, ODT
**Spreadsheets:** XLSX, CSV, ODS
**Presentations:** PPTX, ODP
**Legacy:** SXW, SXC, SXI, SXD, ODG, ODF

### Configuration
- File size limits (default: 50MB)
- Concurrency limits (default: 2 simultaneous conversions)
- LibreOffice timeout (default: 300 seconds)
- Rate limiting rules
- Security policies

### Deployment
- Windows Server 2016+ support
- .NET 8.0 runtime
- IIS 8.5+ hosting
- Air-gapped environment support
- Self-contained with bundled dependencies

## Version Numbering

- **0.5.0** - Removal of deprecated formats (current)
- **0.4.0** - Deprecation release
- **1.0.0** - Planned stable release

## Links

- [Migration Guide](MIGRATION.md) - Migrating from deprecated formats
- [Deployment Guide](DEPLOYMENT.md) - IIS deployment instructions
- [Architecture](ARCHITECTURE.md) - Technical architecture overview
- [README](README.md) - Getting started guide

---

**Legend:**
- **Breaking Changes** - May require configuration or code changes
- **Added** - New features
- **Changed** - Changes to existing functionality
- **Deprecated** - Features marked for removal
- **Removed** - Deleted features
- **Fixed** - Bug fixes
- **Security** - Security improvements
