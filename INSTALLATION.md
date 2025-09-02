# Installation Guide

This guide provides comprehensive setup instructions for the File Conversion Utility. The application uses only Node.js libraries with no external software dependencies required.

## Project Overview

This utility provides production-grade file conversion capabilities using pure Node.js libraries. It features comprehensive security, structured logging, concurrency control, and production-ready architecture optimized for both traditional and serverless deployments. The application focuses on document and image conversions with enhanced accuracy features.

## Approach

This project eliminates external software dependencies like LibreOffice, FFmpeg, or ImageMagick. All conversions are handled by specialized Node.js libraries, providing consistent cross-platform behavior and simplified deployment.

## Prerequisites

### Node.js Requirements

- Node.js version 16.0.0 or higher
- npm package manager (included with Node.js)

No external software dependencies are required.

## Installation Steps

1. **Clone or download the project**

   ```bash
   git clone <repository-url>
   cd type-conversion-util
   ```

2. **Install Node.js dependencies**

   ```bash
   npm install
   ```

3. **Run setup script (creates .env file and directories)**

   ```bash
   npm run setup
   ```

4. **Security check**

   ```bash
   npm run security:check
   ```

5. **Start the server**

   ```bash
   # Development mode (with auto-restart)
   npm run dev

   # Production mode
   npm start
   ```

## What's Included

### Fully Supported Conversions

**Document Processing:**

- PDF to DOCX and DOCX to PDF (using pdf-parse and docx libraries)
- PDF to TXT (text extraction)
- XLSX to CSV and CSV to XLSX (full spreadsheet support with streaming)
- XLSX to PDF (spreadsheet to PDF with table formatting)
- TXT to PDF and DOCX (complete formatting)
- XML to PDF (formatted output)
- PPTX to PDF (simplified conversion with basic formatting)

**Image Processing:**

- All major formats using Sharp library
- JPG, PNG, GIF, BMP, TIFF, SVG, PSD format support
- High-quality processing and optimization
- Professional-grade image conversion capabilities
- Configurable quality settings for different output formats

**Enhanced Accuracy Features:**

- Advanced PDF to DOCX conversion with structure preservation
- Document formatting validation and accuracy metrics
- Conversion quality assessment and reporting
- Table detection and structure analysis capabilities

## Security Updates Applied

### Updated Dependencies

- **Multer**: Upgraded to v2.x (fixes multiple vulnerabilities)
- **Puppeteer**: Updated to v24.9.0+ (latest stable, no deprecation warnings)
- **Supertest**: Updated to v7.x (security patches applied)
- **Sharp**: Updated to v0.33.x (performance and security improvements)
- **Express**: Updated to latest v4.x stable release
- **UUID**: Updated to v10.x for improved security
- **ExcelJS**: Replaced vulnerable xlsx library (secure Excel processing)

- **Swagger UI**: Added OpenAPI 3.0 documentation and interactive API explorer
- **csv-stringify**: Added for robust CSV generation

### Security Scripts

```bash
# Check for security vulnerabilities
npm run security:check

# Fix automatically resolvable issues
npm audit fix

# Full security audit
npm audit
```

### Security Vulnerabilities Fixed

**Replaced Vulnerable Libraries:**

- xlsx (high severity) replaced with exceljs (secure alternative)

- lodash.merge (high severity) removed (no longer needed)

**Result:** Production vulnerabilities reduced from 4 to 0.

```bash
# Verify zero production vulnerabilities
npm audit --omit=dev
# Expected output: "found 0 vulnerabilities"
```

### Remaining Deprecation Warnings (Non-Security)

The remaining warnings are deprecation notices in transitive dependencies:

- rimraf@2.7.1 - File deletion utility (used by other packages)
- glob@7.2.3 - File pattern matching (used by other packages)
- lodash.isequal@4.5.0 - Deep equality check (used by other packages)
- fstream@1.0.12 - File streaming (used by other packages)

**These are NOT security vulnerabilities** - they're just older versions of utilities used by our dependencies. The package maintainers will update these over time.

## Architecture Overview

### Code Structure

```
src/
|-- config/          # Application configuration and settings
|-- middleware/      # Request handling and error management
|-- routes/          # API endpoint definitions
|-- services/        # File conversion logic by type
+-- server.js        # Main application server
```

### Key Components

**Request Context Middleware:**

- Request ID generation and tracking
- Performance monitoring and timing
- Structured JSON logging

**Error Handler:**

- Comprehensive error management
- File validation and security checks
- Automatic cleanup procedures

**Semaphore Utility:**

- Concurrency control and rate limiting
- Queue management for high-load scenarios
- Resource exhaustion prevention

**Conversion Services:**

- DocumentService: PDF, DOCX, XLSX, CSV, TXT, XML
- ImageService: JPG, PNG, GIF, BMP, TIFF, SVG, PSD
- AccuracyService: Enhanced conversion accuracy and validation

## Key Benefits

### Security & Compliance

- **No external software dependencies**
- **No system-level installations required**
- **Sandboxed execution environment**
- **Audit-friendly pure JavaScript**
- **Production-grade security features**

### Deployment Advantages

- **Docker-friendly** (smaller images)
- **Cloud-native** (works in any Node.js environment)
- **Scalable** (no external process dependencies)
- **Cross-platform** (consistent behavior everywhere)
- **Serverless compatible** (Vercel, AWS Lambda, etc.)

### Operational Notes

- Easy CI/CD integration
- Containerization friendly
- Microservices compatible
- Comprehensive monitoring and logging

## Verification

1. **Check the web interface**

   ```bash
   # After starting the server
   open http://localhost:3000
   ```

2. **Explore the API documentation**

   ```bash
   # Interactive Swagger UI
   open http://localhost:3000/api-docs
   ```

3. **Test API endpoints**

   ```bash
   # API information
   curl http://localhost:3000/api

   # Health check
   curl http://localhost:3000/api/health

   # Supported formats
   curl http://localhost:3000/api/supported-formats

   # OpenAPI specification
   curl http://localhost:3000/api-docs.json
   ```

4. **Test file conversion**
   ```bash
   # Convert a text file to PDF
   curl -X POST -F "file=@test.txt" -F "targetFormat=pdf" \
        http://localhost:3000/api/convert --output converted.pdf
   ```

## Library Details

### Core Dependencies

- **Puppeteer**: PDF generation from HTML
- **Sharp**: High-performance image processing
- **pdf-parse**: PDF text extraction and parsing
- **docx**: DOCX document creation and parsing
- **mammoth**: DOCX to HTML conversion
- **exceljs**: Excel file processing with streaming support

- **csv-parser & csv-stringify**: CSV processing with proper quoting
- **PDFDocument**: PDF creation and manipulation

### Why These Libraries?

- **Pure JavaScript** - No native binaries
- **Well-maintained** - Active development and security updates
- **Performance optimized** - Suitable for production use
- **Memory efficient** - Proper cleanup and resource management
- **Security focused** - Regular vulnerability updates

## Enhanced Accuracy Features

### Document Conversion Accuracy

- **Formatting Preservation** - Maintains document structure and formatting
- **Table Detection** - Identifies and preserves table structures
- **Structure Analysis** - Analyzes document layout and content organization
- **Validation Metrics** - Provides accuracy scores and conversion quality indicators

### Conversion Validation

- **Accuracy Metrics** - Detailed conversion quality assessment
- **Format Validation** - Ensures output meets target format specifications
- **Content Verification** - Validates that content is properly converted
- **Error Detection** - Identifies and reports conversion issues

## Deployment Options

### Vercel Deployment

The application is optimized for Vercel serverless deployment:

```bash
# Install Vercel CLI
npm i -g vercel

# Deploy to Vercel
vercel --prod
```

### Docker Deployment

```dockerfile
FROM node:18-alpine

WORKDIR /app

# Copy package files
COPY package*.json ./

# Install dependencies
RUN npm ci --only=production

# Copy application code
COPY . .

# Create temp directory
RUN mkdir -p temp

# Expose port
EXPOSE 3000

# Start application
CMD ["npm", "start"]
```

### Traditional Server Deployment

```bash
# Install PM2 for process management
npm install -g pm2

# Start application with PM2
pm2 start npm --name "file-conversion" -- start

# Save PM2 configuration
pm2 save

# Setup PM2 to start on boot
pm2 startup
```

## Environment Variables

```bash
PORT=3000
UPLOAD_LIMIT=50mb
TEMP_DIR=./temp
MAX_FILE_SIZE=52428800
NODE_ENV=production
LOG_LEVEL=info
MAX_CONCURRENCY=2
MAX_QUEUE=10
```

## Performance Configuration

### Concurrency Settings

- **MAX_CONCURRENCY**: Maximum concurrent conversions (default: 2)
- **MAX_QUEUE**: Maximum queued requests (default: 10)

### Memory Management

- **Streaming Processing**: Large files processed without memory issues
- **Automatic Cleanup**: Temporary files removed after processing
- **Resource Limits**: Configurable limits prevent resource exhaustion

## Troubleshooting

### Common Issues

1. **Puppeteer installation issues**

   ```bash
   # Skip Chromium download if needed
   PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=true npm install
   ```

2. **Sharp installation on Alpine Linux**

   ```bash
   # In Dockerfile
   RUN apk add --no-cache vips-dev
   ```

3. **Memory usage with large files**
   - Monitor heap usage
   - Implement file size limits
   - Use streaming where possible

### Performance Tips

1. **For high-volume usage:**

   - Implement request queuing
   - Use worker threads for CPU-intensive tasks
   - Monitor memory usage and implement cleanup

2. **For large files:**

   - Increase Node.js memory limit: `--max-old-space-size=4096`
   - Implement streaming processing where possible
   - Use temporary file cleanup

3. **For production environments:**
   - Use load balancers for horizontal scaling
   - Implement proper monitoring and alerting
   - Configure appropriate rate limiting

## Monitoring and Logging

### Structured Logging

The application provides comprehensive logging:

- **Request Tracking**: Unique request IDs for traceability
- **Performance Metrics**: Response times and throughput
- **Error Logging**: Detailed error information and stack traces
- **JSON Format**: Machine-readable log output

### Health Checks

- **Health Endpoint**: `/api/health` for monitoring systems
- **Dependency Checks**: Automatic verification of required libraries
- **Status Reporting**: Uptime and version information

## Support

This approach provides:

- Easy deployment and scaling
- No external dependencies
- Consistent cross-platform behavior
- Full document and image processing
- Enhanced conversion accuracy and validation
- Production-grade security and monitoring
