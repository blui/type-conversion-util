# File Conversion Utility

A comprehensive Node.js file conversion utility with a REST API and modern web interface. Built with pure Node.js libraries only — no external software dependencies required. Features enterprise-grade security, comprehensive error handling, and production-ready architecture.

## Key Features

- **Zero External Dependencies** - Pure Node.js libraries only
- **Enterprise Security** - Rate limiting, CORS, CSP headers, input validation
- **Comprehensive API** - OpenAPI 3.0 specification with Swagger UI
- **Production Ready** - Structured logging, concurrency control, error handling
- **Vercel Compatible** - Serverless deployment support
- **Cross-Platform** - Consistent behavior across all environments

## Supported Conversions

### Documents and Spreadsheets

- **DOCX to PDF** - High-quality conversion with formatting preservation
- **PDF to DOCX** - Text extraction and document recreation
- **PDF to TXT** - Clean text extraction
- **XLSX to CSV** - Spreadsheet data extraction with proper quoting
- **CSV to XLSX** - Streaming conversion for large files
- **XLSX to PDF** - Spreadsheet to PDF with table formatting
- **PPTX to PDF** - Simplified conversion with basic formatting
- **TXT to PDF/HTML/DOCX** - Complete text formatting support
- **HTML to PDF/DOCX** - Web content conversion with styling
- **XML to PDF/HTML** - Structured data formatting

### Images

- **Format Conversions** - JPG, JPEG, PNG, GIF, BMP, TIFF, SVG, PSD
- **High-Quality Processing** - Sharp library with configurable quality settings
- **Specialized Support** - PSD and SVG conversion with optimized rendering
- **Metadata Preservation** - Image information and properties maintained

### Audio and Video

- **WAV to MP3** - Full conversion using lamejs encoder
- **MP3 to WAV** - Informational file (decoding requires additional libraries)
- **Video Formats** - MP4, MOV, AVI informational support with cloud recommendations

### Archives

- **ZIP Extraction** - Secure extraction with zip bomb protection
- **Content Listing** - Detailed file and directory information
- **Summary Reports** - Extraction logs and file inventories

## Installation

### Prerequisites

- Node.js version 16 or higher
- npm package manager

### Quick Start

1. **Install dependencies:**

   ```bash
   npm install
   ```

2. **Run setup:**

   ```bash
   npm run setup
   ```

3. **Verify security:**

   ```bash
   npm run security:check
   ```

4. **Start development server:**
   ```bash
   npm run dev
   ```

### Security Verification

Verify zero production vulnerabilities:

```bash
npm audit --omit=dev
```

Expected result: "found 0 vulnerabilities"

## Usage

### Development Mode

```bash
npm run dev
```

Server runs on `http://localhost:3000` with auto-restart on file changes.

### Production Mode

```bash
npm start
```

Optimized production server with enhanced security and performance.

### Testing

```bash
# Run test suite
npm test

# Run with coverage
npm run test:coverage
```

## API Documentation

### Interactive Documentation

- **Swagger UI**: `http://localhost:3000/api-docs`
- **OpenAPI JSON**: `http://localhost:3000/api-docs.json`
- **OpenAPI YAML**: `http://localhost:3000/api-docs.yaml`

### Core Endpoints

- `GET /api` - API information and feature overview
- `GET /api/health` - Health check with uptime and version
- `GET /api/supported-formats` - Complete format mapping
- `POST /api/convert` - File conversion with multipart upload

### Web Interface

- `GET /` - Modern web interface for file uploads and conversions

## Architecture

### Code Organization

```
src/
├── config/          # Application configuration
├── middleware/      # Request handling and error management
├── routes/          # API endpoint definitions
├── services/        # File conversion logic
├── utils/           # Utility functions and helpers
└── server.js        # Main application server
```

### Key Components

- **Request Context Middleware** - Request tracking and performance monitoring
- **Error Handler** - Comprehensive error management and file validation
- **Semaphore Utility** - Concurrency control and rate limiting
- **Conversion Services** - Specialized handlers for each file type
- **Security Middleware** - CORS, CSP, rate limiting, and input validation

## Configuration

### Environment Variables

Create a `.env` file in the project root:

```env
PORT=3000
UPLOAD_LIMIT=50mb
TEMP_DIR=./temp
MAX_FILE_SIZE=52428800
NODE_ENV=development
LOG_LEVEL=info
MAX_CONCURRENCY=2
MAX_QUEUE=10
```

### Configuration Options

- `PORT` - Server port (default: 3000)
- `UPLOAD_LIMIT` - Maximum file upload size (default: 50mb)
- `TEMP_DIR` - Temporary files directory (default: ./temp)
- `MAX_FILE_SIZE` - Maximum file size in bytes (default: 52428800)
- `NODE_ENV` - Environment mode (development/production)
- `LOG_LEVEL` - Logging level (info/debug/warn/error)
- `MAX_CONCURRENCY` - Maximum concurrent conversions (default: 2)
- `MAX_QUEUE` - Maximum queued requests (default: 10)

## Security Features

### Enterprise-Grade Security

- **Rate Limiting** - 100 requests per 15-minute window per IP
- **File Type Validation** - Content-based and extension validation
- **Content Security Policy** - Comprehensive CSP headers
- **CORS Configuration** - Configurable cross-origin settings
- **Input Validation** - Comprehensive request validation
- **Secure File Handling** - Automatic cleanup and temporary file management
- **Concurrency Control** - Semaphore-based request limiting
- **Zip Bomb Protection** - Archive size and entry limits

### Security Headers

- Helmet.js integration
- XSS protection
- Content type sniffing prevention
- Frame options and referrer policy

## Deployment

### Vercel Deployment

The application is optimized for Vercel serverless deployment:

```bash
# Deploy to Vercel
vercel --prod
```

### Docker Deployment

```dockerfile
FROM node:18-alpine
WORKDIR /app
COPY package*.json ./
RUN npm ci --only=production
COPY . .
RUN mkdir -p temp
EXPOSE 3000
CMD ["npm", "start"]
```

### Environment-Specific Configuration

- **Development**: Local file serving and debugging
- **Production**: Optimized for serverless with `/tmp` directory
- **Testing**: Isolated test environment with cleanup

## Performance Features

### Optimization Strategies

- **Streaming Processing** - Large file handling without memory issues
- **Concurrency Control** - Prevents resource exhaustion
- **Automatic Cleanup** - Temporary file management
- **Memory Management** - Efficient resource utilization
- **Request Tracking** - Performance monitoring and debugging

### Monitoring

- **Structured Logging** - JSON-formatted request logs
- **Request IDs** - Traceable request tracking
- **Performance Metrics** - Response time and throughput monitoring
- **Error Tracking** - Comprehensive error logging and reporting

## Library Details

### Core Dependencies

- **Puppeteer** - PDF generation from HTML content
- **Sharp** - High-performance image processing
- **pdf-lib & pdf-parse** - PDF manipulation and text extraction
- **docx** - DOCX document creation and parsing
- **mammoth** - DOCX to HTML conversion
- **exceljs** - Excel file processing with streaming support
- **html-to-docx** - HTML to DOCX conversion
- **extract-zip & jszip** - ZIP extraction with security validation
- **lamejs** - MP3 encoding for audio conversion

### Why These Libraries?

- **Pure JavaScript** - No native binary dependencies
- **Well-Maintained** - Active development and security updates
- **Performance Optimized** - Production-ready performance
- **Memory Efficient** - Proper cleanup and resource management
- **Security Focused** - Regular security updates and vulnerability fixes

## Production Recommendations

### For Video Processing

Since video conversion requires significant resources:

- **Cloud Services**: AWS MediaConvert, Azure Media Services
- **Dedicated APIs**: Cloudinary, Mux, Encoding.com
- **Microservices**: Separate video processing service

### For Advanced Audio

For production MP3 decoding:

- **Cloud APIs**: Specialized audio processing services
- **Additional Libraries**: Consider `node-ffmpeg` in controlled environments

## Troubleshooting

### Common Issues

1. **Puppeteer Installation**

   ```bash
   PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=true npm install
   ```

2. **Sharp Installation (Alpine Linux)**

   ```bash
   RUN apk add --no-cache vips-dev
   ```

3. **Memory Usage**
   - Monitor heap usage with large files
   - Implement appropriate file size limits
   - Use streaming processing where available

### Performance Tips

- **High Volume**: Implement request queuing and worker threads
- **Large Files**: Increase Node.js memory limit and use streaming
- **Monitoring**: Track memory usage and implement cleanup procedures

## License

MIT License - see LICENSE file for details

## Support

This utility provides:

- Easy deployment and scaling
- No external dependencies
- Consistent cross-platform behavior
- Full document and image processing capabilities
- Limited video/audio processing (by design)

For advanced video/audio needs, integrate with cloud services or dedicated microservices.
