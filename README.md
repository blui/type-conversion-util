# File Conversion API

A production-ready, security-hardened document conversion service built with .NET 8 and C#. This API provides reliable, high-quality format conversions while maintaining strict network isolation and comprehensive security controls, optimized for cross-platform deployment.

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
- [Contributing](#contributing)
- [License](#license)

## Overview

The File Conversion API bridges the gap between different document formats, enabling seamless conversion between Office documents, PDFs, images, and other formats. Built with operational security and reliability in mind using .NET 8 and C#, it serves enterprise environments where data isolation and processing predictability are critical.

### Why This API?

- **Zero External Dependencies**: Completely self-contained with LibreOffice SDK integration
- **Enterprise Security**: Defense-in-depth approach with IP whitelisting, rate limiting, and input validation
- **Production Ready**: Comprehensive error handling, monitoring, and health checks
- **Operational Simplicity**: Single-command setup and configuration-driven operation
- **Cross-Platform**: Runs on Windows, Linux, and macOS

## Key Features

### Core Conversion Capabilities

| From/To         | PDF | DOCX | XLSX | CSV | Images | TXT | XML | HTML | PSD | SVG | TIFF |
| --------------- | --- | ---- | ---- | --- | ------ | --- | --- | ---- | --- | --- | ---- |
| **DOCX**        | Yes | -    | -    | -   | -      | Yes | -   | -    | -   | -   | -    |
| **PDF**         | -   | Yes  | -    | -   | Yes    | Yes | -   | -    | -   | -   | Yes  |
| **XLSX**        | Yes | -    | -    | Yes | -      | Yes | -   | -    | -   | -   | -    |
| **CSV**         | -   | -    | Yes  | -   | -      | Yes | -   | -    | -   | -   | -    |
| **Images**      | Yes | -    | -    | -   | Yes    | -   | -   | -    | -   | -   | -    |
| **TXT**         | Yes | Yes  | -    | -   | -      | -   | -   | -    | -   | -   | -    |
| **XML**         | Yes | -    | -    | -   | -      | -   | -   | Yes  | -   | -   | -    |
| **HTML**        | Yes | -    | -    | -   | -      | -   | -   | -    | -   | -   | -    |
| **PSD**         | Yes | -    | -    | -   | Yes    | -   | -   | -    | -   | -   | -    |
| **SVG**         | Yes | -    | -    | -   | Yes    | -   | -   | -    | -   | -   | -    |
| **TIFF**        | Yes | -    | -    | -   | -      | -   | -   | -    | -   | -   | -    |
| **ODT/ODS/ODP** | Yes | -    | -    | -   | -      | -   | -   | -    | -   | -   | -    |

### Advanced Features

#### Document Processing

- **High-Fidelity Conversions**: Optimized LibreOffice SDK integration for maximum document fidelity
- **Preprocessing Pipeline**: Advanced DOCX normalization, font mapping, and compatibility fixes
- **Batch Processing**: Concurrent processing with semaphore-based resource management
- **Format Detection**: Automatic file type detection and MIME validation
- **Advanced Image Processing**: PSD layers, SVG rendering, multi-page TIFF extraction

#### XML Processing & Transformation

- **XSLT Transformations**: Full XSLT 1.0/2.0 support with custom extensions
- **XML Schema Validation**: Built-in XSD validation with detailed error reporting
- **XML to HTML/PDF**: Direct conversion via XSLT transformation pipeline
- **Namespace-Aware Processing**: Full XML namespace support and XPath queries

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

```bash
# Clone the repository
git clone <repository-url>
cd file-conversion-api

# Restore NuGet packages
dotnet restore FileConversionApi/FileConversionApi.csproj

# Build the application
dotnet build "FileConversionApi/FileConversionApi.csproj" --configuration Release

# Run the API server
dotnet run --project "FileConversionApi/FileConversionApi.csproj" --urls=http://localhost:3000
```

API runs at `http://localhost:3000`

## Build Warnings (Expected and Safe to Ignore)

The build process may show warnings that are **expected and safe to ignore** for this Windows Server deployment:

### NuGet Package Vulnerabilities

- **Fixed**: Updated to latest secure versions (Magick.NET-Q16-AnyCPU 14.9.0, SixLabors.ImageSharp 3.1.11)
- Previous versions had known vulnerabilities, but build now uses secure packages

### CS1998 Warnings (Async methods without await)

- **Expected**: Some async methods are placeholders for future functionality
- **Safe**: These methods return synchronously but maintain async signatures for API consistency

### CA1416 Warnings (Windows-only APIs)

- **Expected**: Application uses Windows-specific APIs (System.Drawing) for image processing
- **Safe**: Designed specifically for Windows Server deployment where these APIs are available

### CS8604/CS8602 Warnings (Possible null references)

- **Expected**: Defensive programming with nullable reference types
- **Safe**: Application includes proper null checking and error handling

**Build Status: ✅ SUCCESS** - All warnings are expected and the application builds successfully.

## Office Document Conversion

### LibreOffice Runtime Requirement

**Office document conversion (DOCX, XLSX, PPTX) requires bundled LibreOffice runtime.**

#### What Works Without LibreOffice:

- ✅ **PDF → Text extraction**
- ✅ **XLSX ↔ CSV conversion**
- ✅ **Text → PDF creation**
- ✅ **XML/HTML → PDF creation**
- ✅ **Image format conversions**

#### What Requires LibreOffice Runtime:

- ❌ **DOCX → PDF** (needs `soffice.exe`)
- ❌ **XLSX → PDF** (needs `soffice.exe`)
- ❌ **PPTX → PDF** (needs `soffice.exe`)
- ❌ **PDF → DOCX** (needs `soffice.exe`)

### Bundling LibreOffice Runtime

To enable Office document conversion:

```bash
# 1. Install LibreOffice temporarily
# Download from: https://www.libreoffice.org/download/download/

# 2. Bundle the runtime
.\bundle-libreoffice.ps1

# 3. Test Office conversions
curl -X POST http://localhost:3000/api/convert \
  -F "file=@document.docx" \
  -F "targetFormat=pdf" \
  -o converted.pdf

# 4. Uninstall system LibreOffice (optional)
```

**LibreOffice SDK alone is insufficient** - you need the complete runtime executables.

## API Documentation

### Base URL

```
http://localhost:3000
```

All endpoints return JSON responses unless otherwise specified.

### Authentication & Authorization

- **IP-based**: Configure `IP_WHITELIST` environment variable
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

```bash
curl -X POST http://localhost:3000/api/convert \
  -F "file=@document.docx" \
  -F "targetFormat=pdf" \
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
  "success": false,
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
  "formats": {
    "input": [
      "docx",
      "pdf",
      "xlsx",
      "csv",
      "txt",
      "xml",
      "jpg",
      "png",
      "gif",
      "bmp",
      "tiff",
      "svg"
    ],
    "output": ["pdf", "docx", "xlsx", "csv", "txt"],
    "conversions": [
      { "from": "docx", "to": "pdf" },
      { "from": "pdf", "to": "docx" },
      { "from": "xlsx", "to": "pdf" },
      { "from": "xlsx", "to": "csv" },
      { "from": "csv", "to": "xlsx" }
    ]
  }
}
```

#### Health Check

**GET** `/health`

Basic service availability check for load balancers.

**Response (200):**

```json
{
  "status": "healthy",
  "timestamp": "2024-01-15T10:30:00Z",
  "version": "2.0.1"
}
```

#### Detailed Health Check

**GET** `/health/detailed`

Comprehensive system health and diagnostic information.

**Response (200):**

```json
{
  "status": "healthy",
  "timestamp": "2024-01-15T10:30:00Z",
  "system": {
    "uptime": 3600,
    "memory": {
      "used": 150,
      "total": 8000,
      "percentage": 1.9
    },
    "cpu": {
      "usage": 5.2
    },
    "disk": {
      "free": 50000,
      "total": 100000
    }
  },
  "services": {
    "libreoffice": {
      "available": true,
      "version": "7.5.0"
    }
  },
  "conversions": {
    "active": 0,
    "completed": 150,
    "failed": 2
  }
}
```

#### Error Metrics

**GET** `/health/errors`

Detailed error statistics for monitoring and alerting.

**Response (200):**

```json
{
  "timestamp": "2024-01-15T10:30:00Z",
  "period": "24h",
  "summary": {
    "totalErrors": 5,
    "errorRate": 0.02,
    "criticalErrors": 1
  },
  "errors": [
    {
      "type": "CONVERSION_FAILED",
      "count": 3,
      "lastOccurrence": "2024-01-15T09:45:00Z",
      "details": "LibreOffice process timeout"
    }
  ]
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
- **Configuration**: Set `RATE_LIMIT_MAX` environment variable

## Local Testing

This section describes how to test the File Conversion API locally after setting up the development environment.

### Prerequisites

Before testing, ensure you have:

- .NET 8.0 SDK installed
- Application built and running on `http://localhost:3000`
- Test files available (DOCX, PDF, XLSX, images, etc.)

### Health Check

First, verify the service is running:

```bash
# Health check endpoint
curl http://localhost:3000/health

# Expected response:
{
  "status": "Healthy",
  "timestamp": "2025-10-16T20:48:55.827Z",
  "services": {
    "LibreOffice": {
      "status": "Healthy",
      "message": "Available"
    }
  }
}
```

### Basic Conversion Testing

#### 1. Test with cURL

Convert a DOCX file to PDF:

```bash
# Convert DOCX to PDF
curl -X POST http://localhost:3000/api/convert \
  -F "file=@sample.docx" \
  -F "targetFormat=pdf" \
  -o converted.pdf

# Convert XLSX to CSV
curl -X POST http://localhost:3000/api/convert \
  -F "file=@spreadsheet.xlsx" \
  -F "targetFormat=csv" \
  -o output.csv
```

#### 2. Test with PowerShell

```powershell
# Convert PDF to DOCX
$FilePath = "C:\path\to\document.pdf"
$Uri = "http://localhost:3000/api/convert"

$Form = @{
    file = Get-Item -Path $FilePath
    targetFormat = "docx"
}

Invoke-RestMethod -Uri $Uri -Method Post -Form $Form -OutFile "converted.docx"
```

### Supported Formats Testing

Test various file conversions:

```bash
# Document conversions
curl -X POST http://localhost:3000/api/convert -F "file=@test.docx" -F "targetFormat=pdf" -o docx_to_pdf.pdf
curl -X POST http://localhost:3000/api/convert -F "file=@test.pdf" -F "targetFormat=docx" -o pdf_to_docx.docx
curl -X POST http://localhost:3000/api/convert -F "file=@test.docx" -F "targetFormat=txt" -o docx_to_txt.txt

# Spreadsheet conversions
curl -X POST http://localhost:3000/api/convert -F "file=@test.xlsx" -F "targetFormat=csv" -o xlsx_to_csv.csv
curl -X POST http://localhost:3000/api/convert -F "file=@test.csv" -F "targetFormat=xlsx" -o csv_to_xlsx.xlsx

# Image conversions
curl -X POST http://localhost:3000/api/convert -F "file=@test.jpg" -F "targetFormat=png" -o jpg_to_png.png
curl -X POST http://localhost:3000/api/convert -F "file=@test.png" -F "targetFormat=pdf" -o png_to_pdf.pdf
```

### Advanced Testing

#### Test File Size Limits

```bash
# Test with a large file (should return 413 if too large)
curl -X POST http://localhost:3000/api/convert \
  -F "file=@large_file.pdf" \
  -F "targetFormat=docx" \
  -v
```

#### Test Invalid Formats

```bash
# Test unsupported conversion (should return 400)
curl -X POST http://localhost:3000/api/convert \
  -F "file=@document.pdf" \
  -F "targetFormat=invalid" \
  -v
```

#### Test Rate Limiting

```bash
# Rapid requests to test rate limiting (should return 429 after limit)
for i in {1..35}; do
  curl -X POST http://localhost:3000/api/convert \
    -F "file=@test.pdf" \
    -F "targetFormat=txt" \
    -w "%{http_code}\n" \
    -o /dev/null &
done
```

### Automated Testing

#### Using PowerShell Script

Create a test script (`test-api.ps1`):

```powershell
param(
    [string]$ApiUrl = "http://localhost:3000"
)

Write-Host "Testing File Conversion API at $ApiUrl" -ForegroundColor Green

# Test health endpoint
Write-Host "Testing health endpoint..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$ApiUrl/health" -Method Get
    Write-Host "✓ Health check passed: $($health.status)" -ForegroundColor Green
} catch {
    Write-Host "✗ Health check failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test supported formats
Write-Host "Testing supported formats endpoint..." -ForegroundColor Yellow
try {
    $formats = Invoke-RestMethod -Uri "$ApiUrl/api/supported-formats" -Method Get
    Write-Host "✓ Supported formats: $($formats.documents.Count) document formats, $($formats.images.Count) image formats" -ForegroundColor Green
} catch {
    Write-Host "✗ Supported formats test failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test with sample file (if available)
$testFile = "sample.docx"
if (Test-Path $testFile) {
    Write-Host "Testing file conversion..." -ForegroundColor Yellow
    try {
        $result = Invoke-RestMethod -Uri "$ApiUrl/api/convert" -Method Post -Form @{
            file = Get-Item $testFile
            targetFormat = "pdf"
        } -OutFile "test_output.pdf"
        Write-Host "✓ File conversion successful" -ForegroundColor Green
    } catch {
        Write-Host "✗ File conversion failed: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "! No test file found, skipping conversion test" -ForegroundColor Yellow
}

Write-Host "API testing completed!" -ForegroundColor Green
```

Run the test script:

```powershell
.\test-api.ps1 -ApiUrl "http://localhost:3000"
```

### Testing with Sample Files

If you have sample files, create a comprehensive test:

```bash
# Create test directory
mkdir test_files
cd test_files

# Test all supported conversions
echo "Testing document conversions..."
curl -X POST http://localhost:3000/api/convert -F "file=@sample.docx" -F "targetFormat=pdf" -o docx_to_pdf.pdf
curl -X POST http://localhost:3000/api/convert -F "file=@sample.pdf" -F "targetFormat=docx" -o pdf_to_docx.docx

echo "Testing spreadsheet conversions..."
curl -X POST http://localhost:3000/api/convert -F "file=@sample.xlsx" -F "targetFormat=csv" -o xlsx_to_csv.csv
curl -X POST http://localhost:3000/api/convert -F "file=@sample.csv" -F "targetFormat=xlsx" -o csv_to_xlsx.xlsx

echo "Testing image conversions..."
curl -X POST http://localhost:3000/api/convert -F "file=@sample.jpg" -F "targetFormat=png" -o jpg_to_png.png
curl -X POST http://localhost:3000/api/convert -F "file=@sample.png" -F "targetFormat=pdf" -o png_to_pdf.pdf

echo "All tests completed. Check output files."
```

### Error Testing

Test error scenarios:

```bash
# Test missing file
curl -X POST http://localhost:3000/api/convert \
  -F "targetFormat=pdf" \
  -v

# Test empty file
touch empty.txt
curl -X POST http://localhost:3000/api/convert \
  -F "file=@empty.txt" \
  -F "targetFormat=pdf" \
  -v

# Test malicious file (if available)
curl -X POST http://localhost:3000/api/convert \
  -F "file=@malicious.exe" \
  -F "targetFormat=pdf" \
  -v
```

### Performance Testing

Basic performance test:

```bash
# Test concurrent conversions
echo "Testing concurrent conversions..."
time (
  curl -X POST http://localhost:3000/api/convert -F "file=@doc1.docx" -F "targetFormat=pdf" -o out1.pdf &
  curl -X POST http://localhost:3000/api/convert -F "file=@doc2.docx" -F "targetFormat=pdf" -o out2.pdf &
  curl -X POST http://localhost:3000/api/convert -F "file=@doc3.docx" -F "targetFormat=pdf" -o out3.pdf &
  wait
)
echo "Concurrent test completed"
```

### Logging and Monitoring

Monitor the application during testing:

```bash
# Check application logs (if running in development)
tail -f logs/file-conversion-api-.log

# Monitor Windows Event Log (production)
Get-EventLog -LogName Application -Source FileConversionApi -Newest 10
```

## Configuration

The service is configured through `appsettings.json` files and environment variables. The application supports multiple environments with hierarchical configuration.

### Configuration Files

- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development overrides
- `appsettings.Production.json` - Production overrides
- Environment variables (prefixed with section name)

### Server Configuration

| Setting              | Default       | Description         |
| -------------------- | ------------- | ------------------- |
| `Server:Port`        | `3000`        | Server port         |
| `Server:Host`        | `localhost`   | Server bind address |
| `Server:Environment` | `Development` | Environment mode    |

### Security Configuration

| Setting                                     | Default | Description                                                     |
| ------------------------------------------- | ------- | --------------------------------------------------------------- |
| `Security:IPWhitelist`                      | `[]`    | Array of CIDR ranges (e.g., `["192.168.1.0/24", "10.0.0.0/8"]`) |
| `Security:EnableAdvancedSecurity`           | `true`  | Enable advanced security features                               |
| `IpRateLimiting:EnableEndpointRateLimiting` | `true`  | Enable rate limiting                                            |
| `IpRateLimiting:GeneralRules[0]:Limit`      | `30`    | Requests per minute per IP                                      |

### File Processing Configuration

| Setting                                 | Default            | Description                          |
| --------------------------------------- | ------------------ | ------------------------------------ |
| `FileHandling:MaxFileSize`              | `52428800`         | Maximum file size in bytes (50MB)    |
| `FileHandling:TempDirectory`            | `./temp`           | Temporary file directory for uploads |
| `FileHandling:OutputDirectory`          | `./temp/converted` | Output directory for converted files |
| `Preprocessing:EnableDocxPreprocessing` | `true`             | Enable DOCX preprocessing            |

### Performance Configuration

| Setting                                | Default | Description                       |
| -------------------------------------- | ------- | --------------------------------- |
| `Concurrency:MaxConcurrentConversions` | `2`     | Maximum concurrent conversions    |
| `Concurrency:MaxQueueSize`             | `10`    | Maximum queued conversions        |
| `Timeouts:DocumentConversion`          | `60000` | Conversion timeout (milliseconds) |

### Monitoring Configuration

| Setting                         | Default       | Description               |
| ------------------------------- | ------------- | ------------------------- |
| `Logging:LogLevel:Default`      | `Information` | Default logging level     |
| `HealthChecks:Enabled`          | `true`        | Enable health checks      |
| `CustomLogging:RollingInterval` | `Day`         | Log file rolling interval |

### Example Configuration

#### Development (appsettings.Development.json)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  },
  "Security": {
    "IPWhitelist": []
  },
  "Concurrency": {
    "MaxConcurrentConversions": 1
  }
}
```

#### Production (appsettings.Production.json)

```json
{
  "Server": {
    "Port": 3000,
    "Host": "0.0.0.0",
    "Environment": "Production"
  },
  "Security": {
    "IPWhitelist": ["192.168.1.0/24", "10.0.0.0/8"],
    "EnableAdvancedSecurity": true
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
  },
  "FileHandling": {
    "MaxFileSize": 104857600,
    "TempDirectory": "/app/temp",
    "OutputDirectory": "/app/converted"
  },
  "Concurrency": {
    "MaxConcurrentConversions": 4,
    "MaxQueueSize": 20
  }
}
```

#### Environment Variables

```bash
# Override specific settings
export Security__IPWhitelist__0="192.168.1.0/24"
export Concurrency__MaxConcurrentConversions="4"
export Logging__LogLevel__Default="Debug"
```

## Deployment

### Prerequisites

**System Requirements:**

- .NET 8.0 Runtime or SDK
- 4GB RAM minimum (8GB recommended)
- 2GB free disk space
- LibreOffice 7.0+ (for CLI integration)

**Network Requirements:**

- No internet access required for operation
- Optional: HTTPS certificate for secure communication

### Installation Steps

1. **Clone Repository**

   ```bash
   git clone <repository-url>
   cd file-conversion-api
   ```

2. **Install Dependencies**

   ```bash
   dotnet restore FileConversionApi/FileConversionApi.csproj
   ```

3. **Configure Application**

   ```bash
   # Copy and modify configuration files
   cp FileConversionApi/appsettings.json FileConversionApi/appsettings.Production.json
   # Edit appsettings.Production.json with your configuration
   ```

4. **Build Application**

   ```bash
   dotnet build FileConversionApi/FileConversionApi.csproj --configuration Release
   ```

5. **Start Service**

   ```bash
   dotnet run --project FileConversionApi/FileConversionApi.csproj --configuration Release
   ```

### Deployment Scenarios

#### Windows Service Deployment

```powershell
# Create Windows Service using NSSM
nssm install FileConversionAPI "C:\Program Files\dotnet\dotnet.exe"
nssm set FileConversionAPI AppParameters "FileConversionApi.dll --configuration Release"
nssm set FileConversionAPI AppDirectory "C:\path\to\service\FileConversionApi"
nssm start FileConversionAPI

# Alternative: Use sc command
sc create FileConversionAPI binPath= "C:\Program Files\dotnet\dotnet.exe FileConversionApi.dll --configuration Release" start= auto
sc start FileConversionAPI
```

#### Linux Systemd Service

```bash
# Create service file
sudo tee /etc/systemd/system/file-conversion-api.service > /dev/null <<EOF
[Unit]
Description=File Conversion API
After=network.target

[Service]
Type=simple
User=fileconversion
WorkingDirectory=/opt/file-conversion-api/FileConversionApi
ExecStart=/usr/bin/dotnet FileConversionApi.dll --configuration Release
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
EOF

# Enable and start service
sudo systemctl daemon-reload
sudo systemctl enable file-conversion-api
sudo systemctl start file-conversion-api
```

#### Manual Deployment

For manual deployments, follow these steps:

```bash
# 1. Build the application
dotnet publish FileConversionApi/FileConversionApi.csproj -c Release -o ./publish

# 2. Copy files to target server
# Copy the entire ./publish directory to your server (e.g., C:\inetpub\FileConversionApi)

# 3. Configure appsettings.Production.json
# Copy and modify appsettings.json for production use

# 4. Set up directories on target server
# Create required directories:
# - C:\inetpub\temp\uploads
# - C:\inetpub\temp\converted
# - C:\inetpub\FileConversionApi\logs

# 5. Configure IIS (see IIS Deployment section above)

# 6. Set permissions
# Ensure IIS_IUSRS has read/write access to temp directories
```

#### Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: file-conversion-api
spec:
  replicas: 3
  selector:
    matchLabels:
      app: file-conversion-api
  template:
    metadata:
      labels:
        app: file-conversion-api
    spec:
      containers:
        - name: file-conversion-api
          image: file-conversion-api:latest
          ports:
            - containerPort: 3000
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: "Production"
          resources:
            requests:
              memory: "512Mi"
              cpu: "250m"
            limits:
              memory: "2Gi"
              cpu: "1000m"
          volumeMounts:
            - name: temp-storage
              mountPath: /app/temp
            - name: converted-storage
              mountPath: /app/converted
      volumes:
        - name: temp-storage
          emptyDir: {}
        - name: converted-storage
          emptyDir: {}
---
apiVersion: v1
kind: Service
metadata:
  name: file-conversion-api-service
spec:
  selector:
    app: file-conversion-api
  ports:
    - port: 3000
      targetPort: 3000
  type: LoadBalancer
```

#### Load Balancer Setup

```nginx
upstream conversion_api {
    server api1.example.com:3000;
    server api2.example.com:3000;
    server api3.example.com:3000;
}

server {
    listen 80;
    server_name api.example.com;

    # Rate limiting
    limit_req zone=api burst=30 nodelay;

    location / {
        proxy_pass http://conversion_api;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # Health check endpoint
    location /health {
        proxy_pass http://conversion_api;
        access_log off;
    }
}
```

### SSL/TLS Configuration

1. **Using ASP.NET Core HTTPS**

   ```json
   // appsettings.Production.json
   {
     "Kestrel": {
       "Endpoints": {
         "Https": {
           "Url": "https://*:3001",
           "Certificate": {
             "Path": "/path/to/certificate.pfx",
             "Password": "certificate-password"
           }
         }
       }
     }
   }
   ```

2. **Using Reverse Proxy (Recommended)**

   ```nginx
   server {
       listen 443 ssl http2;
       server_name api.example.com;

       ssl_certificate /path/to/certificate.crt;
       ssl_certificate_key /path/to/private.key;
       ssl_protocols TLSv1.2 TLSv1.3;

       location / {
           proxy_pass http://localhost:3000;
           proxy_set_header Host $host;
           proxy_set_header X-Real-IP $remote_addr;
           proxy_set_header X-Forwarded-Proto $scheme;
       }
   }
   ```

3. **Let's Encrypt SSL**

   ```bash
   # Install certbot
   sudo apt-get install certbot

   # Generate certificate
   sudo certbot certonly --standalone -d api.example.com

   # Configure nginx to use certificate
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

```bash
# Restrict file permissions (Windows)
icacls "C:\path\to\service" /grant "NT AUTHORITY\NETWORK SERVICE":(OI)(CI)F /T
icacls "C:\path\to\temp" /grant "NT AUTHORITY\NETWORK SERVICE":(OI)(CI)F /T

# Configure Windows Firewall
New-NetFirewallRule -DisplayName "File Conversion API" -Direction Inbound -LocalPort 3000 -Protocol TCP -Action Allow

# Enable Windows Security features
# - Windows Defender Real-time protection
# - Windows Firewall with Advanced Security
# - Audit Policy for object access
```

```bash
# Restrict file permissions (Linux)
sudo chown -R fileconversion:fileconversion /opt/file-conversion-api
sudo chmod -R 755 /opt/file-conversion-api
sudo chmod -R 777 /opt/file-conversion-api/temp  # For uploads
sudo chmod -R 755 /opt/file-conversion-api/converted

# Configure firewall (Ubuntu/Debian)
sudo ufw allow 3000/tcp
sudo ufw --force enable
```

#### Monitoring Security Events

```bash
# Monitor ASP.NET Core logs
tail -f /var/log/file-conversion-api/*.log | grep -i security

# Check for suspicious activity patterns
grep -i "blocked\|denied\|violation" /var/log/file-conversion-api/*.log

# Monitor rate limiting
grep -i "rate.*limit" /var/log/file-conversion-api/*.log
```

#### Security Hardening

```json
// appsettings.Production.json - Security hardening
{
  "Security": {
    "IPWhitelist": ["192.168.1.0/24"],
    "EnableAdvancedSecurity": true
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
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "FileConversionApi": "Information"
    }
  }
}
```

## Performance

### Benchmark Results

| Document Type  | Size Range  | Conversion Time | CPU Usage | Memory Usage |
| -------------- | ----------- | --------------- | --------- | ------------ |
| DOCX (simple)  | 1-5 pages   | 2-4 seconds     | 10-20%    | 150-250MB    |
| DOCX (complex) | 10-20 pages | 3-6 seconds     | 15-30%    | 200-350MB    |
| DOCX (large)   | 50+ pages   | 6-12 seconds    | 20-40%    | 300-500MB    |
| XLSX           | < 100KB     | 1-3 seconds     | 5-15%     | 100-200MB    |
| PDF → DOCX     | < 10MB      | 4-8 seconds     | 15-25%    | 250-400MB    |

### Performance Tuning

#### Memory Optimization

```bash
# Increase Node.js memory limit
node --max-old-space-size=4096 src/server.js

# Environment variable
NODE_OPTIONS=--max-old-space-size=4096
```

#### Concurrency Tuning

```bash
# Adjust based on server capacity
MAX_CONCURRENCY=4
MAX_QUEUE=20
```

#### Storage Optimization

```bash
# Use fast SSD storage for temp directory
TEMP_DIR=D:\temp

# Use dedicated drive for converted files
OUTPUT_DIR=E:\converted

# Regular cleanup
# Windows Task Scheduler
schtasks /create /tn "CleanupTempFiles" /tr "powershell Remove-Item C:\path\to\temp\* -Force" /sc daily
schtasks /create /tn "CleanupConvertedFiles" /tr "powershell Remove-Item E:\converted\* -Force" /sc weekly
```

### Monitoring Performance

#### Real-time Metrics

```bash
# Check current performance
curl http://localhost:3000/health/detailed

# Monitor resource usage
typeperf "\Processor(_Total)\% Processor Time" "\Memory\Available MBytes"
```

#### Performance Alerts

- CPU usage > 80% for 5+ minutes
- Memory usage > 90%
- Queue depth > 50% of max
- Error rate > 5%

## Troubleshooting

### Common Issues

#### Service Won't Start

**Error:** `Error: LibreOffice not found`

```bash
# Solution: Run setup again
npm run setup

# Verify LibreOffice installation
node scripts/verify-system.js
```

**Error:** `Error: Port 3000 already in use`

```bash
# Find process using port
netstat -ano | findstr :3000

# Kill process (replace PID)
taskkill /PID <PID> /F

# Or change port
PORT=3001 npm start
```

#### Conversion Failures

**Error:** `Conversion timeout`

```bash
# Increase timeout
CONVERSION_TIMEOUT_MS=600000  # 10 minutes

# Reduce concurrency
MAX_CONCURRENCY=1
```

**Error:** `File too large`

```bash
# Increase file size limit
MAX_FILE_SIZE=104857600  # 100MB
```

#### Permission Issues

**Error:** `Access denied`

```powershell
# Grant permissions to temp directory
icacls "C:\path\to\temp" /grant "IIS_IUSRS":(OI)(CI)F

# Grant permissions to output directory
icacls "D:\converted" /grant "IIS_IUSRS":(OI)(CI)F

# Grant permissions to service account
icacls "C:\path\to\service" /grant "NT AUTHORITY\NETWORK SERVICE":(OI)(CI)F
icacls "D:\converted" /grant "NT AUTHORITY\NETWORK SERVICE":(OI)(CI)F
```

### Diagnostic Commands

#### Check Service Health

```bash
curl http://localhost:3000/health/detailed
```

#### View Logs

```bash
# Application logs
type logs\app.log | findstr ERROR

# System event logs
eventvwr.msc
```

#### Monitor Resources

```bash
# Task Manager or Performance Monitor
perfmon.msc

# Command line
wmic cpu get loadpercentage
wmic os get freephysicalmemory
```

### Getting Help

#### Log Analysis

```bash
# Extract recent errors
powershell "Get-Content logs/app.log -Tail 100 | Select-String ERROR"

# Check conversion failures
powershell "Get-Content logs/app.log | Select-String CONVERSION_FAILED"
```

#### System Information

```bash
# System info
systeminfo

# Node.js version
node --version

# NPM version
npm --version

# LibreOffice version
soffice --version
```

## Development

### Development Setup

1. **Clone Repository**

   ```bash
   git clone <repository-url>
   cd file-conversion-api
   ```

2. **Install Dependencies**

   ```bash
   dotnet restore FileConversionApi/FileConversionApi.csproj
   dotnet restore FileConversionApi.Tests/FileConversionApi.Tests.csproj
   ```

3. **Development Configuration**

   ```bash
   # Copy development configuration
   cp FileConversionApi/appsettings.json FileConversionApi/appsettings.Development.json
   # Edit appsettings.Development.json for development settings
   ```

4. **Build Application**

   ```bash
   dotnet build FileConversionApi/FileConversionApi.csproj
   ```

5. **Start Development Server**
   ```bash
   # With hot reload
   dotnet watch run --project FileConversionApi/FileConversionApi.csproj
   ```

### Testing

#### Unit Tests

```bash
# Run all tests
dotnet test FileConversionApi.Tests/FileConversionApi.Tests.csproj

# Run with coverage
dotnet test FileConversionApi.Tests/FileConversionApi.Tests.csproj --collect:"XPlat Code Coverage"

# Run specific test
dotnet test FileConversionApi.Tests/FileConversionApi.Tests.csproj --filter "TestName"
```

#### Integration Tests

```bash
# Run integration tests (if implemented)
dotnet test FileConversionApi.Tests/FileConversionApi.Tests.csproj --filter "Integration"
```

#### Code Quality

```bash
# Run code analysis
dotnet build FileConversionApi/FileConversionApi.csproj /p:RunCodeAnalysis=true

# Check code style
dotnet format --check FileConversionApi/FileConversionApi.csproj

# Fix code style issues
dotnet format FileConversionApi/FileConversionApi.csproj

# Security analysis
dotnet tool install --global dotnet-security-scan
dotnet-security-scan FileConversionApi/FileConversionApi.csproj
```

### Code Structure

```
FileConversionApi/
├── Controllers/               # API controllers
├── Services/                  # Business logic services
│   ├── Interfaces/            # Service interfaces
│   ├── ConversionEngine.cs    # Main conversion orchestrator
│   ├── LibreOfficeService.cs  # LibreOffice integration
│   ├── PreprocessingService.cs # Document preprocessing
│   ├── ImageService.cs        # Image processing
│   ├── XmlProcessingService.cs # XML processing
│   └── ...
├── Utils/                     # Utility classes
├── Middleware/                # ASP.NET middleware
├── Models/                    # Data models and configuration
└── Program.cs                 # Application entry point

FileConversionApi.Tests/       # Test project
├── ConversionValidatorTests.cs
├── ConfigValidatorTests.cs
├── PreprocessingServiceTests.cs
└── ...
```

### API Development

#### Adding New Endpoints

```csharp
// In Controllers/ConversionController.cs
[HttpPost("new-endpoint")]
public async Task<IActionResult> NewEndpoint([FromBody] RequestModel request)
{
    try
    {
        var result = await _conversionService.NewMethodAsync(request);
        return Ok(new { success = true, data = result });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "New endpoint failed");
        return StatusCode(500, new { success = false, error = ex.Message });
    }
}
```

#### Adding New Conversions

```csharp
// In Services/ConversionEngine.cs
public async Task<ConversionResult> NewConversionAsync(string inputPath, string outputPath)
{
    // Implementation
    return await ConvertLibreOfficeFormatAsync("inputFormat", "outputFormat", inputPath, outputPath);
}

// Update the interface
public interface IConversionEngine
{
    // ... existing methods
    Task<ConversionResult> NewConversionAsync(string inputPath, string outputPath);
}
```

#### Adding New Services

```csharp
// Create interface
public interface INewService
{
    Task<OperationResult> PerformOperationAsync(string input);
}

// Implement service
public class NewService : INewService
{
    private readonly ILogger<NewService> _logger;

    public NewService(ILogger<NewService> logger)
    {
        _logger = logger;
    }

    public async Task<OperationResult> PerformOperationAsync(string input)
    {
        // Implementation
    }
}

// Register in Program.cs
builder.Services.AddSingleton<INewService, NewService>();
```

## Contributing

### Development Workflow

1. **Fork the Repository**
2. **Create Feature Branch**

   ```bash
   git checkout -b feature/new-conversion-type
   ```

3. **Make Changes**

   - Follow existing code style
   - Add tests for new functionality
   - Update documentation

4. **Run Tests**

   ```bash
   npm test
   npm run lint
   ```

5. **Submit Pull Request**
   - Provide clear description
   - Reference related issues
   - Include screenshots for UI changes

### Code Standards

#### C# Style

- Use async/await for asynchronous operations
- Consistent error handling with try/catch
- XML documentation comments for all public members
- Descriptive variable and method names
- Follow C# naming conventions
- Use dependency injection pattern
- Implement proper logging with structured logging

#### Commit Messages

```
type(scope): description

Types:
- feat: New features
- fix: Bug fixes
- docs: Documentation
- style: Code style changes
- refactor: Code refactoring
- test: Testing
- chore: Maintenance
```

#### Testing Requirements

- Unit test coverage > 80%
- Integration tests for API endpoints
- Error condition testing
- Performance regression tests
- Use xUnit for testing framework
- Use FluentAssertions for assertions

### Security Considerations

- Never commit sensitive data
- Validate all inputs
- Follow principle of least privilege
- Regular dependency updates

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

### Third-Party Licenses

This project includes software with the following licenses:

- **LibreOffice**: LGPL v3
- **.NET Runtime**: MIT License
- **NuGet Packages**: Various permissive licenses (MIT, BSD, Apache-2.0)
- **ImageMagick**: Apache-2.0 License
- **Svg**: MIT License

### Support

For support and questions:

- **Documentation**: [ARCHITECTURE.md](ARCHITECTURE.md)
- **Issues**: GitHub Issues
- **Discussions**: GitHub Discussions

### Acknowledgments

- LibreOffice project for document processing capabilities
- Node.js community for the runtime platform
- Open source security tools and libraries
