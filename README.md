# File Conversion API

A production-ready, security-hardened document conversion service designed specifically for Windows Server environments. This API provides reliable, high-quality format conversions while maintaining strict network isolation and comprehensive security controls.

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

The File Conversion API bridges the gap between different document formats, enabling seamless conversion between Office documents, PDFs, images, and other formats. Built with operational security and reliability in mind, it serves enterprise environments where data isolation and processing predictability are critical.

### Why This API?

- **Zero External Dependencies**: Completely self-contained with bundled LibreOffice
- **Enterprise Security**: Defense-in-depth approach with IP whitelisting, rate limiting, and input validation
- **Production Ready**: Comprehensive error handling, monitoring, and health checks
- **Operational Simplicity**: Single-command setup and configuration-driven operation
- **Windows Server Optimized**: Designed specifically for Windows Server environments

## Key Features

### Core Conversion Capabilities

| From/To    | PDF | DOCX | XLSX | CSV | Images | TXT | XML |
| ---------- | --- | ---- | ---- | --- | ------ | --- | --- |
| **DOCX**   | Yes | -    | -    | -   | -      | Yes | -   |
| **PDF**    | -   | Yes  | -    | -   | Yes    | Yes | -   |
| **XLSX**   | Yes | -    | -    | Yes | -      | Yes | -   |
| **CSV**    | -   | -    | Yes  | -   | -      | Yes | -   |
| **Images** | Yes | -    | -    | -   | -      | -   | -   |
| **TXT**    | Yes | Yes  | -    | -   | -      | -   | -   |
| **XML**    | Yes | -    | -    | -   | -      | -   | -   |

### Advanced Features

#### Document Processing

- **High-Fidelity Conversions**: Optimized LibreOffice settings for maximum document fidelity
- **Preprocessing Options**: Optional DOCX normalization for improved compatibility
- **Batch Processing**: Sequential processing with configurable concurrency limits
- **Format Detection**: Automatic file type detection and validation

#### Security & Compliance

- **IP Whitelist**: CIDR-based access control with runtime configuration
- **Rate Limiting**: Configurable request throttling per IP address
- **Input Validation**: Multi-layer file type verification and content analysis
- **Audit Logging**: Comprehensive security event logging
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
# Install dependencies
npm install

# Bundle LibreOffice from system installation (automatic during setup)
npm run setup

# Start the API server
npm start
```

API runs at `http://localhost:3000`

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

## Configuration

The service is configured entirely through environment variables. Copy `env.example` to `.env` and modify as needed.

### Server Configuration

| Variable   | Default       | Description         |
| ---------- | ------------- | ------------------- |
| `PORT`     | `3000`        | Server port         |
| `HOST`     | `localhost`   | Server bind address |
| `NODE_ENV` | `development` | Environment mode    |

### Security Configuration

| Variable                   | Default   | Description                                                     |
| -------------------------- | --------- | --------------------------------------------------------------- |
| `IP_WHITELIST`             | _(empty)_ | Comma-separated CIDR ranges (e.g., `192.168.1.0/24,10.0.0.0/8`) |
| `RATE_LIMIT_MAX`           | `30`      | Requests per minute per IP                                      |
| `RATE_LIMIT_WINDOW_MS`     | `900000`  | Rate limit window (15 minutes)                                  |
| `SSL_ENABLED`              | `false`   | Enable HTTPS                                                    |
| `ACCEPT_SELF_SIGNED_CERTS` | `true`    | Allow self-signed certificates                                  |

### File Processing Configuration

| Variable                    | Default            | Description                                      |
| --------------------------- | ------------------ | ------------------------------------------------ |
| `MAX_FILE_SIZE`             | `52428800`         | Maximum file size in bytes (50MB)                |
| `TEMP_DIR`                  | `./temp`           | Temporary file directory for uploads             |
| `OUTPUT_DIR`                | `./temp/converted` | Output directory for converted files             |
| `ENABLE_PREPROCESSING`      | `true`             | Enable DOCX preprocessing                        |
| `FORCE_BUNDLED_LIBREOFFICE` | `true`             | Force bundled LibreOffice (with system fallback) |

### Performance Configuration

| Variable                | Default  | Description                    |
| ----------------------- | -------- | ------------------------------ |
| `MAX_CONCURRENCY`       | `2`      | Maximum concurrent conversions |
| `MAX_QUEUE`             | `10`     | Maximum queued conversions     |
| `CONVERSION_TIMEOUT_MS` | `300000` | Conversion timeout (5 minutes) |

### Monitoring Configuration

| Variable            | Default | Description                              |
| ------------------- | ------- | ---------------------------------------- |
| `LOG_LEVEL`         | `info`  | Logging level (error, warn, info, debug) |
| `TELEMETRY_ENABLED` | `true`  | Enable performance monitoring            |

### Example Configuration Files

#### Development Setup

```bash
# .env
PORT=3000
HOST=localhost
NODE_ENV=development
IP_WHITELIST=
SSL_ENABLED=false
LOG_LEVEL=debug
MAX_CONCURRENCY=1
```

#### Production Setup

```bash
# .env
PORT=3000
HOST=0.0.0.0
NODE_ENV=production
IP_WHITELIST=192.168.1.0/24,10.0.0.0/8
SSL_ENABLED=true
LOG_LEVEL=info
MAX_CONCURRENCY=3
MAX_FILE_SIZE=104857600
TEMP_DIR=C:\temp
OUTPUT_DIR=D:\converted
```

#### Air-Gapped Setup

```bash
# .env
PORT=3000
HOST=0.0.0.0
NODE_ENV=production
IP_WHITELIST=192.168.1.0/24
SSL_ENABLED=true
FORCE_BUNDLED_LIBREOFFICE=true
LOG_LEVEL=warn
TEMP_DIR=C:\temp
OUTPUT_DIR=D:\converted
```

## Deployment

### Prerequisites

**System Requirements:**

- Windows Server 2016+ or Windows 10 Pro/Enterprise
- Node.js 16.0.0 or higher
- 4GB RAM minimum (8GB recommended)
- 2GB free disk space
- LibreOffice 7.0+ (automatically bundled)

**Network Requirements:**

- No internet access required for operation
- Optional: HTTPS certificate for secure communication

### Installation Steps

1. **Clone and Install Dependencies**

   ```bash
   git clone <repository-url>
   cd file-conversion-api
   npm install
   ```

2. **Configure Environment**

   ```bash
   cp env.example .env
   # Edit .env with your configuration
   ```

3. **Bundle LibreOffice**

   ```bash
   npm run setup
   ```

4. **Verify Installation**

   ```bash
   npm run verify-system
   ```

5. **Start Service**
   ```bash
   npm start
   ```

### Deployment Scenarios

#### Single Server Deployment

```powershell
# Windows Service (using NSSM)
nssm install FileConversionAPI "C:\Program Files\nodejs\node.exe"
nssm set FileConversionAPI AppParameters "C:\path\to\service\src\server.js"
nssm set FileConversionAPI AppDirectory "C:\path\to\service"
nssm start FileConversionAPI
```

#### Docker Deployment

```dockerfile
FROM node:16-alpine
WORKDIR /app
COPY package*.json ./
RUN npm ci --only=production
COPY . .
EXPOSE 3000
CMD ["npm", "start"]
```

#### Load Balancer Setup

```nginx
upstream conversion_api {
    server api1.example.com:3000;
    server api2.example.com:3000;
}

server {
    listen 80;
    server_name api.example.com;

    location / {
        proxy_pass http://conversion_api;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

### SSL/TLS Configuration

1. **Generate Self-Signed Certificate**

   ```bash
   node scripts/generate-ssl-cert.js
   ```

2. **Configure Environment**

   ```bash
   SSL_ENABLED=true
   ACCEPT_SELF_SIGNED_CERTS=false  # For production
   ```

3. **Using Custom Certificates**
   ```bash
   # Place certificate files in project root
   SSL_CERT_PATH=./ssl/cert.pem
   SSL_KEY_PATH=./ssl/key.pem
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
# Restrict file permissions
icacls "C:\path\to\service" /grant "NT AUTHORITY\NETWORK SERVICE":(OI)(CI)F

# Configure Windows Firewall
New-NetFirewallRule -DisplayName "File Conversion API" -Direction Inbound -LocalPort 3000 -Protocol TCP -Action Allow

# Enable Windows Security features
# - Windows Defender
# - Windows Firewall
# - Audit Policy
```

#### Monitoring Security Events

```bash
# Monitor security logs
Get-EventLog -LogName Security -Newest 10

# Check for suspicious activity
Get-Content logs/app.log | Select-String "SECURITY_VIOLATION"
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
   npm install
   ```

3. **Development Configuration**

   ```bash
   cp env.example .env
   # Edit .env for development
   PORT=3000
   NODE_ENV=development
   LOG_LEVEL=debug
   ```

4. **Bundle LibreOffice**

   ```bash
   npm run setup
   ```

5. **Start Development Server**
   ```bash
   npm run dev  # With auto-restart
   ```

### Testing

#### Unit Tests

```bash
npm test
```

#### Integration Tests

```bash
npm run test:e2e
```

#### Code Quality

```bash
# Run linting
npm run lint

# Check code quality
node scripts/code-quality-check.js

# Security audit
npm audit
```

### Code Structure

```
src/
├── config/          # Configuration management
├── middleware/      # Express middleware
├── routes/          # API route handlers
├── services/        # Business logic services
│   ├── conversionEngine.js    # Main conversion orchestrator
│   ├── libreOfficeService.js  # LibreOffice integration
│   ├── preprocessingService.js # Document preprocessing
│   └── ...
├── utils/           # Utility functions
└── server.js        # Application entry point

scripts/             # Build and utility scripts
tests/               # Test suites
```

### API Development

#### Adding New Endpoints

```javascript
// In src/routes/conversion.js
router.post("/new-endpoint", async (req, res) => {
  try {
    const result = await conversionService.newMethod(req.body);
    res.json({ success: true, data: result });
  } catch (error) {
    next(error);
  }
});
```

#### Adding New Conversions

```javascript
// In src/services/conversionEngine.js
async newConversion(inputPath, outputPath) {
  // Implementation
  return await libreOfficeService.convert(inputPath, outputPath, 'format');
}
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

#### JavaScript Style

- Use async/await for asynchronous operations
- Consistent error handling with try/catch
- JSDoc comments for all public functions
- Descriptive variable and function names

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

### Security Considerations

- Never commit sensitive data
- Validate all inputs
- Follow principle of least privilege
- Regular dependency updates

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

### Third-Party Licenses

This project includes bundled software with the following licenses:

- **LibreOffice**: LGPL v3
- **Node.js Dependencies**: Various permissive licenses (MIT, BSD, Apache-2.0)

### Support

For support and questions:

- **Documentation**: [ARCHITECTURE.md](ARCHITECTURE.md)
- **Issues**: GitHub Issues
- **Discussions**: GitHub Discussions

### Acknowledgments

- LibreOffice project for document processing capabilities
- Node.js community for the runtime platform
- Open source security tools and libraries
