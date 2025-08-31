# File Conversion Utility

A Node.js file conversion utility with a REST API and a simple web interface. Built with pure Node.js libraries only — no external software dependencies required.

## Supported Conversions

### Documents and Spreadsheets

- DOCX to PDF and PDF to DOCX
- XLSX to CSV and CSV to XLSX
- PPTX to PDF (simplified conversion)
- TXT to PDF, HTML, and DOCX
- HTML to PDF and DOCX
- XML to PDF and HTML

### Images

- JPG, JPEG, PNG, GIF, BMP, TIFF format conversions
- PSD to PNG and JPG conversion
- SVG to raster format conversion
- High-quality image processing using Sharp library

### Audio and Video

- WAV to MP3 conversion (full support)
- MP3 to WAV conversion (creates informational file)
- Video format information (MP4, MOV, AVI - informational support only)

### Archives

- ZIP file extraction with detailed summary
- Archive information and content listing

## Installation

No external software required. This project uses only Node.js libraries.

### Prerequisites

- Node.js version 16 or higher
- npm package manager

### Installation Steps

1. Install dependencies:

```bash
npm install
```

2. Run initial setup:

```bash
npm run setup
```

This creates necessary directories, environment files, and runs security checks.

### Security Verification

Verify zero production vulnerabilities:

```bash
npm audit --omit=dev
```

Expected result: "found 0 vulnerabilities"

## Highlights

- No external software dependencies
- Pure Node.js libraries only
- Sandboxed execution environment
- Docker-friendly and cloud-compatible
- Cross-platform consistency
- Comprehensive error handling and logging

## Usage

### Development Mode

Start the development server with automatic restart on file changes:

```bash
npm run dev
```

### Production Mode

Start the production server:

```bash
npm start
```

### Testing

Run the test suite:

```bash
npm test
```

Run tests with coverage report:

```bash
npm run test:coverage
```

## API Documentation

### OpenAPI 3.0 Specification

The application provides comprehensive API documentation using OpenAPI 3.0 standards:

- `GET /api-docs` - Interactive Swagger UI documentation
- `GET /api-docs.json` - OpenAPI specification in JSON format
- `GET /api-docs.yaml` - OpenAPI specification in YAML format

Notes:

- No API key required; the Swagger "Authorize" button is removed.
- Uses up-to-date dependencies to avoid deprecated packages.

### Available API Endpoints

- `GET /api` - API information and available endpoints
- `GET /api/health` - Health check endpoint for monitoring
- `GET /api/supported-formats` - List of supported conversion formats
- `POST /api/convert` - File conversion endpoint (multipart form data)

### Web Interface

- `GET /` - Main web interface for file uploads and conversions

## Configuration

### Environment Variables

Create a `.env` file in the project root with the following variables:

```
PORT=3000
UPLOAD_LIMIT=50mb
TEMP_DIR=./temp
MAX_FILE_SIZE=52428800
NODE_ENV=development
LOG_LEVEL=info
```

### Configuration Options

- `PORT` - Server port (default: 3000)
- `UPLOAD_LIMIT` - Maximum file upload size (default: 50mb)
- `TEMP_DIR` - Temporary files directory (default: ./temp)
- `MAX_FILE_SIZE` - Maximum file size in bytes (default: 52428800)
- `NODE_ENV` - Environment mode (development/production)
- `LOG_LEVEL` - Logging level (info/debug/warn/error)

## Security Features

- Rate limiting
- File type validation
- Content Security Policy headers
- CORS configuration
- Input validation
- Secure file handling with automatic cleanup

## License

MIT License - see LICENSE file for details
