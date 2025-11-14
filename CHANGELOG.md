# Changelog

All notable changes to the File Conversion API are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.5.0] - 2025-01-XX

### Added
- Initial production release
- Support for 21 different document format conversions
- 10 input format types supported
- LibreOffice-based conversion engine
- Self-contained deployment with bundled LibreOffice
- API endpoints:
  - `POST /api/convert` - File conversion
  - `GET /api` - API information
  - `GET /api/supported-formats` - Format capabilities
  - `GET /health` - Health check with full diagnostics
- Security features:
  - API key authentication
  - IP-based rate limiting
  - CORS support
  - File validation (size, type, extension)
  - Security headers (CSP, X-Frame-Options, etc.)
- Concurrency control (configurable max simultaneous conversions)
- Comprehensive structured logging with Serilog
- Automatic temporary file cleanup
- Health check endpoints for load balancer integration
- Swagger/OpenAPI documentation
- IIS hosting support with sub-application deployment

### Format Support

**Supported Formats (10 total):**
- **Documents:** DOC, DOCX, PDF, TXT, HTML, HTM, XML
- **Spreadsheets:** XLSX, CSV
- **Presentations:** PPTX

**Supported Conversions (21 total):**
- DOC → PDF, TXT, DOCX, HTML, HTM
- DOCX → PDF, TXT, DOC
- PDF → DOCX, DOC, TXT
- XLSX → CSV, PDF
- CSV → XLSX
- PPTX → PDF
- TXT → PDF, DOCX, DOC
- XML → PDF
- HTML → PDF
- HTM → PDF

### Architecture

**Services:**
- DocumentService - Conversion orchestration
- LibreOfficeService - LibreOffice integration
- LibreOfficeProcessManager - Process lifecycle management
- PdfService - PDF operations (iText7)
- SpreadsheetService - Excel/CSV operations (NPOI)
- InputValidator - File and conversion validation
- SemaphoreService - Concurrency control

**Key Design Principles:**
- Stateless service design for horizontal scaling
- Process isolation for reliability
- Complete file cleanup after conversions
- Singleton services for performance
- Operation-specific folders for file isolation

### Configuration

**Available Settings:**
- File size limits (default: 50MB)
- Concurrency limits (default: 2 simultaneous conversions)
- LibreOffice timeout (default: 300 seconds)
- Rate limiting rules
- Security policies
- Logging configuration

### Deployment

**Requirements:**
- Windows Server 2016+ support
- .NET 8.0 runtime
- IIS 8.5+ hosting
- Air-gapped environment support
- Self-contained with bundled dependencies

### Code Quality Improvements

**Removed Over-Commenting:**
- Condensed verbose XML documentation to concise inline comments
- Removed obvious comments from simple model classes
- Net reduction: 200+ lines

**Removed Unnecessary Abstractions:**
- Deleted ConversionEngine wrapper class
- DocumentService now calls LibreOfficeService directly
- Extracted ConversionResult to Models namespace
- Net reduction: 160+ lines

**Consolidated Duplicate Code:**
- HealthController: Single comprehensive health endpoint
- DocumentService: Consolidated text conversion methods
- Net reduction: 120+ lines

**Removed Dead Code:**
- SemaphoreService: Removed 5 unused methods
- Simplified interface from 8 methods to 3
- Reduced from 135 lines to 37 lines (72% reduction)

**Reduced Excessive Logging:**
- LibreOfficeProcessManager: Removed 15 trivial/verbose logs
- Retained only critical error logs and key milestones
- Improved production log signal-to-noise ratio

**Total Impact:** 500+ lines removed with zero breaking changes, all functionality verified.

## Links

- [Deployment Guide](DEPLOYMENT.md) - IIS deployment instructions
- [Architecture](ARCHITECTURE.md) - Technical architecture overview
- [README](README.md) - Getting started guide

---

**Legend:**
- **Added** - New features
- **Changed** - Changes to existing functionality
- **Deprecated** - Features marked for removal
- **Removed** - Deleted features
- **Fixed** - Bug fixes
- **Security** - Security improvements
