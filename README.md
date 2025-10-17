# File Conversion API

A production-ready, security-hardened document conversion service built with .NET 8 and C#. This API provides reliable, high-quality format conversions while maintaining strict network isolation and comprehensive security controls.

## Table of Contents

- [Overview](#overview)
- [Key Features](#key-features)
- [Quick Start](#quick-start)
- [API Documentation](#api-documentation)
- [Configuration](#configuration)
- [Deployment](#deployment)
- [Security](#security)
- [Performance](#performance)
- [Troubleshooting](#troubleshooting)
- [Development](#development)

## Overview

The File Conversion API bridges the gap between different document formats, enabling seamless conversion between Office documents, PDFs, images, and other formats. Built with operational security and reliability in mind using .NET 8 and C#, it serves enterprise environments where data isolation and processing predictability are critical.

### Why This API?

- **Zero External Dependencies**: Completely self-contained with LibreOffice bundled runtime
- **Enterprise Security**: Defense-in-depth approach with IP whitelisting, rate limiting, and input validation
- **Production Ready**: Comprehensive error handling, monitoring, and health checks
- **Operational Simplicity**: Configuration-driven operation with automated deployment
- **Cross-Platform**: Runs on Windows, Linux, and macOS

## Key Features

### Core Conversion Capabilities

| From/To         | PDF | DOCX | XLSX | CSV | Images | TXT |
| --------------- | --- | ---- | ---- | --- | ------ | --- |
| **DOCX**        | Yes | -    | -    | -   | -      | Yes |
| **PDF**         | -   | Yes  | -    | -   | Yes    | Yes |
| **XLSX**        | Yes | -    | -    | Yes | -      | Yes |
| **CSV**         | -   | -    | Yes  | -   | -      | Yes |
| **Images**      | Yes | -    | -    | -   | Yes    | -   |
| **TXT**         | Yes | Yes  | -    | -   | -      | -   |
| **XML**         | Yes | -    | -    | -   | -      | -   |
| **HTML**        | Yes | -    | -    | -   | -      | -   |
| **PSD**         | Yes | -    | -    | -   | Yes    | -   |
| **SVG**         | Yes | -    | -    | -   | Yes    | -   |
| **TIFF**        | Yes | -    | -    | -   | -      | -   |
| **ODT/ODS/ODP** | Yes | Yes  | Yes  | -   | -      | -   |

### Advanced Features

#### Document Processing

- **High-Fidelity Conversions**: Optimized LibreOffice integration for maximum document fidelity
- **Preprocessing Pipeline**: Advanced DOCX normalization, font mapping, and compatibility fixes
- **Batch Processing**: Concurrent processing with semaphore-based resource management
- **Format Detection**: Automatic file type detection and MIME validation
- **Advanced Image Processing**: PSD layers, SVG rendering, multi-page TIFF extraction

#### Security & Compliance

- **IP Whitelist**: CIDR-based access control with runtime configuration
- **Rate Limiting**: ASP.NET Core rate limiting with distributed cache support
- **Input Validation**: Multi-layer file type verification and content analysis
- **Audit Logging**: Structured Serilog logging with telemetry
- **No External Calls**: Complete network isolation - zero external API dependencies

#### Operational Excellence

- **Health Monitoring**: Multiple health check endpoints with detailed system status
- **Performance Monitoring**: Built-in resource usage tracking and alerting
- **Graceful Shutdown**: Clean process termination with connection draining
- **Error Recovery**: Automatic cleanup and resource recovery on failures
- **Configuration Validation**: Startup-time configuration verification

#### Developer Experience

- **RESTful API**: Intuitive HTTP endpoints with standard response formats
- **OpenAPI Documentation**: Interactive API documentation at `/api-docs`
- **Comprehensive Logging**: Structured JSON logging with configurable levels
- **Development Mode**: Hot-reload and debug-friendly configuration

## Quick Start

```powershell
# Clone the repository
git clone <repository-url>
cd type-conversion-util

# Restore NuGet packages
dotnet restore FileConversionApi/FileConversionApi.csproj

# Build the application
dotnet build FileConversionApi/FileConversionApi.csproj --configuration Release

# Bundle LibreOffice runtime (required for Office document conversions)
.\bundle-libreoffice.ps1

# Run the API server
dotnet run --project FileConversionApi/FileConversionApi.csproj --urls=http://localhost:3000
```

API runs at `http://localhost:3000`  
API documentation at `http://localhost:3000/api-docs`

## LibreOffice Runtime Requirement

**Office document conversion (DOCX, XLSX, PPTX) requires bundled LibreOffice runtime.**

### What Works Without LibreOffice:

- PDF text extraction
- XLSX to CSV conversion
- CSV to XLSX conversion
- Text to PDF creation
- XML/HTML to PDF creation
- Image format conversions

### What Requires LibreOffice Runtime:

- DOCX to PDF (needs `soffice.exe`)
- XLSX to PDF (needs `soffice.exe`)
- PPTX to PDF (needs `soffice.exe`)
- PDF to DOCX (needs `soffice.exe`)

### Bundling LibreOffice Runtime

To enable Office document conversion:

```powershell
# 1. Install LibreOffice temporarily
# Download from: https://www.libreoffice.org/download/download/

# 2. Bundle the runtime
.\bundle-libreoffice.ps1

# 3. Test Office conversions
curl -X POST http://localhost:3000/api/convert `
  -F "file=@document.docx" `
  -F "targetFormat=pdf" `
  -o converted.pdf

# 4. Uninstall system LibreOffice (optional)
```

## API Documentation

### Base URL

```
http://localhost:3000
```

All endpoints return JSON responses unless otherwise specified.

### Authentication & Authorization

- **IP-based**: Configure `Security:IPWhitelist` in appsettings.json
- **Rate Limiting**: Configurable per IP address (default: 30 requests/minute)
- **No Authentication Tokens**: Relies on network-level access control

### Core Endpoints

#### Convert File

**POST** `/api/convert`

Converts a file from one format to another.

**Parameters:**

- `file` (multipart/form-data): The file to convert (required)
- `targetFormat` (string): Target format (required)
  - Supported: `pdf`, `docx`, `xlsx`, `csv`, `txt`, `xml`
- `metadata` (boolean): Include conversion metadata in response (optional)

**Example Request:**

```powershell
curl -X POST http://localhost:3000/api/convert `
  -F "file=@document.docx" `
  -F "targetFormat=pdf" `
  -o output.pdf
```

**Success Response (200):**

```json
{
  "success": true,
  "inputFormat": "docx",
  "outputFormat": "pdf",
  "fileSize": 245760,
  "processingTime": 3200,
  "conversionId": "abc-123-def"
}
```

**Error Response (400/500):**

```json
{
  "error": "Unsupported conversion: docx to invalid",
  "errorCode": "INVALID_FORMAT"
}
```

#### Get Supported Formats

**GET** `/api/supported-formats`

Returns list of supported conversion formats.

**Response (200):**

```json
{
  "documents": {
    "input": ["docx", "pdf", "xlsx", "csv", "txt", "xml"],
    "conversions": {
      "docx": ["pdf", "txt"],
      "pdf": ["docx", "txt"],
      "xlsx": ["pdf", "csv"],
      "csv": ["xlsx", "pdf"]
    }
  },
  "images": {
    "input": ["jpg", "jpeg", "png", "gif", "bmp", "tiff", "svg", "psd"],
    "output": ["pdf", "png", "jpg", "bmp"]
  }
}
```

#### Health Check

**GET** `/health`

Basic service availability check for load balancers.

**Response (200):**

```json
{
  "status": "Healthy",
  "timestamp": "2025-10-17T10:30:00Z",
  "services": {
    "LibreOffice": {
      "status": "Healthy",
      "message": "Available"
    }
  }
}
```

#### Detailed Health Check

**GET** `/health/detailed`

Comprehensive system health and diagnostic information.

**Response (200):**

```json
{
  "status": "Healthy",
  "timestamp": "2025-10-17T10:30:00Z",
  "systemInfo": {
    "osVersion": "Microsoft Windows NT 10.0.26100.0",
    "frameworkVersion": "8.0.0",
    "processorCount": 8,
    "workingSet": 157286400,
    "uptime": "02:15:30"
  },
  "services": {
    "LibreOffice": {
      "status": "Healthy",
      "message": "Available"
    }
  }
}
```

#### API Documentation

**GET** `/api-docs`

Interactive OpenAPI/Swagger documentation interface.

### Error Codes

| Code                  | Description                      | HTTP Status |
| --------------------- | -------------------------------- | ----------- |
| `INVALID_FORMAT`      | Unsupported conversion format    | 400         |
| `FILE_TOO_LARGE`      | File exceeds size limit          | 413         |
| `MALICIOUS_CONTENT`   | File contains suspicious content | 400         |
| `CONVERSION_FAILED`   | Conversion process failed        | 500         |
| `SERVICE_UNAVAILABLE` | Service temporarily unavailable  | 503         |
| `RATE_LIMIT_EXCEEDED` | Too many requests from IP        | 429         |
| `IP_NOT_ALLOWED`      | IP address not whitelisted       | 403         |

### Rate Limiting

- **Default Limit**: 30 requests per minute per IP
- **Headers**: Standard RateLimit headers included in responses
- **Reset**: Automatic reset every minute
- **Configuration**: Set in `IpRateLimiting` section of appsettings.json

## Configuration

The service is configured through `appsettings.json` files and environment variables. The application supports multiple environments with hierarchical configuration.

### Configuration Files

- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development overrides
- `appsettings.Production.json` - Production overrides
- Environment variables (prefixed with section name)

### Key Configuration Sections

#### Security Configuration

```json
{
  "Security": {
    "EnableIPFiltering": true,
    "IPWhitelist": ["192.168.1.0/24", "10.0.0.0/8"],
    "MaxRequestSize": 52428800,
    "RequestTimeoutSeconds": 300
  },
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": true,
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

#### File Handling Configuration

```json
{
  "FileHandling": {
    "MaxFileSize": 52428800,
    "TempDirectory": "App_Data\\temp\\uploads",
    "OutputDirectory": "App_Data\\temp\\converted",
    "CleanupTempFiles": true,
    "TempFileRetentionHours": 24
  }
}
```

#### LibreOffice Configuration

```json
{
  "LibreOffice": {
    "SdkPath": "LibreOffice",
    "ForceBundled": true,
    "UseSdkIntegration": false,
    "TimeoutSeconds": 300,
    "MaxConcurrentConversions": 2
  }
}
```

#### Performance Configuration

```json
{
  "Concurrency": {
    "MaxConcurrentConversions": 2,
    "MaxQueueSize": 10
  },
  "Timeouts": {
    "DocumentConversion": 60000,
    "ImageConversion": 30000
  }
}
```

## Deployment

### Prerequisites

**System Requirements:**

- .NET 8.0 Runtime or SDK
- 4GB RAM minimum (8GB recommended)
- 2GB free disk space
- Windows Server 2016+ / Ubuntu 20.04+ / RHEL 8+

**Network Requirements:**

- No internet access required for operation
- Optional: HTTPS certificate for secure communication

### Windows IIS Deployment

The recommended deployment method for Windows Server environments.

```powershell
# 1. Navigate to project directory
cd FileConversionApi

# 2. Run automated deployment script as Administrator
.\deploy.ps1

# 3. Test deployment
curl http://localhost/health
```

The deployment script automatically:

- Creates IIS application pool
- Publishes the .NET application
- Configures permissions
- Sets up directories
- Copies LibreOffice bundle
- Creates production appsettings.json

### Manual Deployment

For custom deployment scenarios:

```powershell
# 1. Publish the application
dotnet publish FileConversionApi/FileConversionApi.csproj `
  -c Release `
  -o C:\inetpub\FileConversionApi

# 2. Bundle LibreOffice (if not already done)
.\bundle-libreoffice.ps1

# 3. Copy LibreOffice bundle to deployment
Copy-Item FileConversionApi\LibreOffice `
  -Destination C:\inetpub\FileConversionApi\LibreOffice `
  -Recurse

# 4. Configure IIS (see IIS_DEPLOYMENT_README.md)
```

### Linux Deployment

```bash
# 1. Install .NET 8 Runtime
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0

# 2. Publish application
dotnet publish FileConversionApi/FileConversionApi.csproj \
  -c Release \
  -o /opt/file-conversion-api

# 3. Create systemd service
sudo tee /etc/systemd/system/file-conversion-api.service > /dev/null <<EOF
[Unit]
Description=File Conversion API
After=network.target

[Service]
Type=simple
User=fileconversion
WorkingDirectory=/opt/file-conversion-api
ExecStart=/usr/bin/dotnet FileConversionApi.dll
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
EOF

# 4. Enable and start service
sudo systemctl daemon-reload
sudo systemctl enable file-conversion-api
sudo systemctl start file-conversion-api
```

## Security

### Defense-in-Depth Approach

The service implements multiple security layers to protect against various threat vectors.

#### Network Security

- **IP Whitelisting**: CIDR-based access control
- **Rate Limiting**: Request throttling per IP address
- **No External Dependencies**: Zero external API calls

#### Application Security

- **Input Validation**: Multi-layer file type verification
- **Content Analysis**: Malicious pattern detection
- **Secure File Handling**: Isolated temporary directories
- **Error Sanitization**: No sensitive information leakage

#### Process Security

- **Process Isolation**: Separate execution contexts
- **Resource Limits**: CPU and memory constraints
- **Automatic Cleanup**: Temporary file removal

### Security Best Practices

#### Production Deployment

```powershell
# Restrict file permissions (Windows)
icacls "C:\inetpub\FileConversionApi" /grant "IIS_IUSRS:(OI)(CI)F" /T

# Configure Windows Firewall
New-NetFirewallRule -DisplayName "File Conversion API" `
  -Direction Inbound `
  -LocalPort 3000 `
  -Protocol TCP `
  -Action Allow
```

## Performance

### Benchmark Results

| Document Type  | Size Range  | Conversion Time | CPU Usage | Memory Usage |
| -------------- | ----------- | --------------- | --------- | ------------ |
| DOCX (simple)  | 1-5 pages   | 2-4 seconds     | 10-20%    | 150-250MB    |
| DOCX (complex) | 10-20 pages | 3-6 seconds     | 15-30%    | 200-350MB    |
| DOCX (large)   | 50+ pages   | 6-12 seconds    | 20-40%    | 300-500MB    |
| XLSX           | < 100KB     | 1-3 seconds     | 5-15%     | 100-200MB    |
| PDF to DOCX    | < 10MB      | 4-8 seconds     | 15-25%    | 250-400MB    |

### Performance Tuning

Configure in `appsettings.json`:

```json
{
  "Concurrency": {
    "MaxConcurrentConversions": 4,
    "MaxQueueSize": 20
  },
  "Timeouts": {
    "DocumentConversion": 120000,
    "ImageConversion": 60000
  }
}
```

## Troubleshooting

### Common Issues

#### Service Won't Start

**Error:** `LibreOffice not found`

```powershell
# Solution: Bundle LibreOffice runtime
.\bundle-libreoffice.ps1

# Verify LibreOffice
Test-Path FileConversionApi\LibreOffice\program\soffice.exe
```

**Error:** `Port already in use`

```powershell
# Find process using port
netstat -ano | findstr :3000

# Kill process (replace PID)
taskkill /PID <PID> /F

# Or change port in launchSettings.json
```

#### Conversion Failures

**Error:** `Conversion timeout`

```json
{
  "LibreOffice": {
    "TimeoutSeconds": 600
  }
}
```

#### Permission Issues

```powershell
# Grant permissions to temp directory
icacls "C:\inetpub\FileConversionApi\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T
```

### Diagnostic Commands

#### Check Service Health

```powershell
curl http://localhost:3000/health
curl http://localhost:3000/health/detailed
```

#### View Logs

```powershell
# Application logs
Get-Content C:\inetpub\logs\file-conversion-api-*.log -Tail 50

# Windows Event Log
Get-EventLog -LogName Application -Source FileConversionApi -Newest 10
```

## Development

### Development Setup

```powershell
# Clone repository
git clone <repository-url>
cd type-conversion-util

# Restore packages
dotnet restore FileConversionApi/FileConversionApi.csproj
dotnet restore FileConversionApi.Tests/FileConversionApi.Tests.csproj

# Build
dotnet build FileConversionApi/FileConversionApi.csproj

# Run with hot reload
dotnet watch run --project FileConversionApi/FileConversionApi.csproj
```

### Testing

```powershell
# Run all tests
dotnet test FileConversionApi.Tests/FileConversionApi.Tests.csproj

# Run with coverage
dotnet test FileConversionApi.Tests/FileConversionApi.Tests.csproj `
  --collect:"XPlat Code Coverage"

# Run specific test
dotnet test --filter "TestName"
```

### Code Quality

```powershell
# Code analysis
dotnet build FileConversionApi/FileConversionApi.csproj /p:RunCodeAnalysis=true

# Format code
dotnet format FileConversionApi/FileConversionApi.csproj
```

### Code Structure

```
FileConversionApi/
├── Controllers/               # API controllers
├── Services/                  # Business logic services
│   ├── Interfaces/            # Service interfaces
│   ├── ConversionEngine.cs    # Main conversion orchestrator
│   ├── LibreOfficeService.cs  # LibreOffice integration
│   └── ...
├── Middleware/                # ASP.NET middleware
├── Models/                    # Data models and configuration
└── Program.cs                 # Application entry point

FileConversionApi.Tests/       # Test project
├── ConversionValidatorTests.cs
├── ConfigValidatorTests.cs
└── ...
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

### Third-Party Licenses

This project includes software with the following licenses:

- **LibreOffice**: LGPL v3
- **.NET Runtime**: MIT License
- **NuGet Packages**: Various permissive licenses (MIT, BSD, Apache-2.0)
- **ImageMagick**: Apache-2.0 License

### Support

For support and questions:

- **Documentation**: [ARCHITECTURE.md](ARCHITECTURE.md)
- **IIS Deployment**: [FileConversionApi/IIS_DEPLOYMENT_README.md](FileConversionApi/IIS_DEPLOYMENT_README.md)
- **Issues**: GitHub Issues

### Acknowledgments

- LibreOffice project for document processing capabilities
- .NET community for the robust framework and ecosystem
- Open source security tools and libraries
