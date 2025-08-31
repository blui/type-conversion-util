# Installation Guide

This guide will help you set up the File Conversion Utility using only Node.js libraries - no external software dependencies required.

## Approach

This project avoids external software like LibreOffice, FFmpeg, or ImageMagick. It relies on Node.js libraries only.

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
- XLSX to CSV and CSV to XLSX (full spreadsheet support)
- TXT to PDF, HTML, and DOCX (complete formatting)
- HTML to PDF and DOCX (with styling preservation)
- XML to PDF and HTML (formatted output)

**Image Processing:**

- All major formats using Sharp library
- JPG, PNG, GIF, BMP, TIFF, SVG, PSD format support
- High-quality processing and optimization
- Professional-grade image conversion capabilities

**Archive Processing:**

- ZIP file extraction and creation
- Full directory structure support
- Comprehensive archive handling

**Audio Processing:**

- WAV to MP3 conversion (using lamejs encoder)

### Limited Support (Creates Informational Files)

**Video File Processing:**

- MP4, MOV, AVI formats create detailed information files with recommendations
- Suggests cloud-based solutions for production video processing
- Pure Node.js video processing has significant limitations

**Audio Processing:**

- MP3 to WAV creates informational file (MP3 decoding complexity)

**Presentation Processing:**

- PPTX to PDF creates simplified conversion with basic formatting

## Security Updates Applied

### Updated Dependencies

- **Multer**: Upgraded to v2.x (fixes multiple vulnerabilities)
- **Puppeteer**: Updated to v24.9.0+ (latest stable, no deprecation warnings)
- **Supertest**: Updated to v7.x (security patches applied)
- **Sharp**: Updated to v0.33.x (performance and security improvements)
- **Express**: Updated to latest v4.x stable release
- **UUID**: Updated to v10.x for improved security
- **ExcelJS**: Replaced vulnerable xlsx library (secure Excel processing)
- **html-to-docx**: Replaced vulnerable html-docx-js library
- **Swagger UI**: Added OpenAPI 3.0 documentation and interactive API explorer

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
- html-docx-js (moderate severity) replaced with html-to-docx (secure alternative)
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

## Key Benefits

### Security & Compliance

- **No external software dependencies**
- **No system-level installations required**
- **Sandboxed execution environment**
- **Audit-friendly pure JavaScript**

### Deployment Advantages

- **Docker-friendly** (smaller images)
- **Cloud-native** (works in any Node.js environment)
- **Scalable** (no external process dependencies)
- **Cross-platform** (consistent behavior everywhere)

### Operational Notes

- Easy CI/CD integration
- Containerization friendly
- Microservices compatible

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
- **pdf-lib & pdf-parse**: PDF manipulation and text extraction
- **docx**: DOCX document creation and parsing
- **mammoth**: DOCX to HTML conversion
- **exceljs**: Excel file processing
- **html-to-docx**: HTML to DOCX conversion
- **extract-zip & jszip**: ZIP extraction and metadata
- **lamejs**: MP3 encoding

### Why These Libraries?

- **Pure JavaScript** - No native binaries
- **Well-maintained** - Active development and security updates
- **Performance optimized** - Suitable for production use
- **Memory efficient** - Proper cleanup and resource management

## Production Recommendations

### For Video Processing

Since video conversion requires significant resources and complex libraries:

1. **Cloud Services:**

   - AWS MediaConvert
   - Azure Media Services
   - Google Cloud Video Intelligence
   - Cloudinary Video API

2. **Dedicated Microservices:**

   - Separate video processing service
   - Queue-based processing
   - Containerized with specialized tools

3. **Third-party APIs:**
   - Mux Video API
   - Encoding.com
   - Zencoder

### For Advanced Audio

For production MP3 decoding and advanced audio processing:

1. **Specialized Services:**

   - Cloud audio processing APIs
   - Dedicated audio microservices

2. **Additional Libraries:**
   - Consider `node-ffmpeg` in controlled environments
   - Web Audio API for browser-based processing

## Docker Deployment

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

## Environment Variables

```bash
PORT=3000
UPLOAD_LIMIT=50mb
TEMP_DIR=./temp
MAX_FILE_SIZE=52428800
NODE_ENV=production
```

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

## Support

This approach provides:

- Easy deployment and scaling
- No external dependencies
- Consistent cross-platform behavior
- Full document and image processing
- Limited video/audio processing (by design)

For advanced video/audio needs, integrate with cloud services or dedicated microservices.
