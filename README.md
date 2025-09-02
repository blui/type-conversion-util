# File Conversion Utility

A lightweight Node.js application providing comprehensive file conversion capabilities without external software dependencies. Features production-ready security, comprehensive error handling, and optimized architecture for internal use.

## Key Features

- **Pure Node.js Libraries** - No external software dependencies required
- **Production Security** - Rate limiting, CORS, CSP headers, input validation
- **Comprehensive Format Support** - Document and image format conversions
- **High Performance** - Concurrency control, request queuing, efficient processing
- **Production Ready** - Structured logging, health monitoring, serverless support
- **Enhanced Accuracy** - Advanced conversion accuracy with formatting preservation

## Supported Conversions

### Documents and Spreadsheets

- **DOCX to PDF** - High-quality conversion with formatting preservation
- **PDF to DOCX** - Text extraction and document recreation with enhanced accuracy
- **PDF to TXT** - Clean text extraction
- **XLSX to CSV** - Spreadsheet data extraction with proper quoting
- **CSV to XLSX** - Streaming conversion for large files
- **XLSX to PDF** - Spreadsheet to PDF with table formatting
- **PPTX to PDF** - Simplified conversion with basic formatting
- **TXT to PDF/DOCX** - Complete text formatting support
- **XML to PDF** - Structured data formatting

### Images

- **Format Conversions** - JPG, JPEG, PNG, GIF, BMP, TIFF, SVG, PSD
- **High-Quality Processing** - Sharp library with configurable quality settings
- **Specialized Support** - PSD and SVG conversion with optimized rendering
- **Metadata Preservation** - Image information and properties maintained

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
|-- config/          # Application configuration
|-- middleware/      # Request handling and error management
|-- routes/          # API endpoint definitions
|-- services/        # File conversion logic
+-- server.js        # Main application server
```

### Key Components

- **Request Context Middleware** - Request tracking and performance monitoring
- **Error Handler** - Comprehensive error management and file validation
- **Semaphore Utility** - Concurrency control and rate limiting
- **Conversion Services** - Specialized handlers for each file type
- **Accuracy Service** - Enhanced conversion accuracy and validation
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

### Production Security

- **Rate Limiting** - 100 requests per 15-minute window per IP
- **File Type Validation** - Content-based and extension validation
- **Content Security Policy** - Comprehensive CSP headers
- **CORS Configuration** - Configurable cross-origin settings
- **Input Validation** - Comprehensive request validation
- **Secure File Handling** - Automatic cleanup and temporary file management
- **Concurrency Control** - Semaphore-based request limiting

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
- **pdf-parse** - PDF text extraction and parsing
- **docx** - DOCX document creation and parsing
- **mammoth** - DOCX to HTML conversion (for PDF generation)
- **exceljs** - Excel file processing with streaming support
- **csv-parser & csv-stringify** - CSV processing with proper quoting
- **PDFDocument** - PDF creation and manipulation

### Why These Libraries?

- **Pure JavaScript** - No native binary dependencies
- **Well-Maintained** - Active development and security updates
- **Performance Optimized** - Production-ready performance
- **Memory Efficient** - Proper cleanup and resource management
- **Security Focused** - Regular security updates and vulnerability fixes

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
- Enhanced conversion accuracy and validation
- Production-grade security and monitoring
