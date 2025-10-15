# File Conversion API - Architecture Overview

## Executive Summary

The File Conversion API is a production-ready, security-hardened document conversion service designed for Windows Server environments. It provides reliable format conversion capabilities while maintaining strict network isolation and comprehensive security controls. The system emphasizes operational simplicity, defense-in-depth security, and predictable performance.

## System Context and Boundaries

### External Interfaces

- **API Clients**: RESTful HTTP/HTTPS endpoints for file conversion requests
- **Supported Formats**: DOCX, PDF, XLSX, CSV, images (JPG, PNG, etc.), and text formats
- **Network Boundary**: No external API calls or cloud service dependencies

### System Boundaries

- **Input**: File uploads via HTTP multipart/form-data
- **Processing**: Local-only document conversion using bundled LibreOffice
- **Output**: Converted files returned via HTTP response
- **Isolation**: Air-gapped operation with no internet connectivity requirements

## Architecture Overview

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Client Applications                           │
└─────────────────────┬───────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────┐
│                 Security Perimeter                              │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  IP Whitelist  │  Rate Limiting  │  Input Validation    │    │
│  └─────────────────────────────────────────────────────────┘    │
└─────────────────────┬───────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────┐
│                Application Layer                                │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │ Express Server │ Middleware Stack │ Route Handlers      │    │
│  └─────────────────────────────────────────────────────────┘    │
└─────────────────────┬───────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────┐
│                 Service Layer                                   │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │ Document Service │ Conversion Engine │ Specialized Svcs │    │
│  └─────────────────────────────────────────────────────────┘    │
└─────────────────────┬───────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────┐
│                 Processing Layer                                │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │ Preprocessing │ LibreOffice Engine │ File Management    │    │
│  └─────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────┘
```

### Component Architecture

#### 1. Security Layer

**Purpose**: Defense-in-depth protection against unauthorized access and malicious input

**Components**:

- **IP Whitelist Middleware**: CIDR-based IP filtering with configurable allow/deny lists
- **Rate Limiting**: Sliding window rate limiting per IP address
- **Input Validation**: File type verification, size limits, content analysis
- **Request Integrity**: Malformed request detection and sanitization

#### 2. Application Layer

**Purpose**: HTTP request/response handling and routing

**Components**:

- **Express Server**: HTTP/HTTPS server with configurable ports and SSL
- **Middleware Stack**: Security, logging, parsing, and error handling
- **Route Handlers**: RESTful endpoints for conversion operations
- **File Upload Handling**: Multipart form-data processing with size limits

#### 3. Service Layer

**Purpose**: Business logic orchestration and service coordination

**Components**:

- **Document Service**: Main orchestrator routing conversion requests
- **Conversion Engine**: Core conversion logic and preprocessing coordination
- **LibreOffice Service**: LibreOffice process management and command execution
- **PDF/Image Services**: Specialized format handling

#### 4. Processing Layer

**Purpose**: Actual file conversion and preprocessing operations

**Components**:

- **Preprocessing Service**: Optional DOCX normalization (fonts, colors, styles)
- **LibreOffice Engine**: Headless document conversion execution
- **File Management**: Temporary file handling and cleanup
- **Performance Monitoring**: Resource usage tracking and limits

## Detailed Data Flow

### Conversion Request Flow

```
1. Client Request
   ↓
2. Security Validation
   ├── IP whitelist check
   ├── Rate limit verification
   └── Input sanitization
   ↓
3. Request Processing
   ├── File upload parsing
   ├── Format validation
   └── Metadata extraction
   ↓
4. Conversion Pipeline
   ├── Preprocessing (optional)
   │   ├── Font normalization
   │   ├── Color mapping
   │   └── Style cleanup
   ├── LibreOffice Conversion
   │   ├── Command construction
   │   ├── Process execution
   │   └── Output validation
   └── Post-processing
   ↓
5. Response Generation
   ├── File packaging
   ├── Metadata attachment
   └── HTTP response
```

### Error Handling Flow

```
Any Step Fails
   ↓
Error Handler
   ├── Error categorization (Critical/Warning/Info)
   ├── Context preservation
   ├── Recovery attempts (if applicable)
   ├── Audit logging
   └── Client response generation
```

## Security Architecture

### Defense-in-Depth Strategy

#### Network Security

- **IP Whitelisting**: CIDR-based access control with runtime configuration
- **Rate Limiting**: Prevents abuse with configurable thresholds
- **No External Calls**: Zero external API dependencies

#### Application Security

- **Input Validation**: Multi-layer file type and content verification
- **Secure File Handling**: Isolated temporary directories with cleanup
- **Error Information Leakage**: Sanitized error responses

#### Process Security

- **Bundled Dependencies**: Self-contained LibreOffice installation
- **Process Isolation**: Separate process execution for conversions
- **Resource Limits**: Memory and CPU usage constraints

### Threat Model

**Primary Threats Addressed**:

- Unauthorized access via IP spoofing or misconfiguration
- Malicious file uploads (viruses, exploits, oversized files)
- Denial of service through resource exhaustion
- Information leakage through error messages or logs

**Mitigation Strategies**:

- Strict input validation with multiple verification layers
- Resource monitoring and automatic process termination
- Comprehensive audit logging for security events
- Graceful degradation under attack conditions

## Deployment Architecture

### Single-Server Deployment

```
┌─────────────────────────────────────────────────┐
│              Windows Server                      │
├─────────────────────────────────────────────────┤
│  ┌─────────────────────────────────────────┐    │
│  │         File Conversion API             │    │
│  │  ┌─────────────────────────────────┐    │    │
│  │  │         Node.js Runtime         │    │    │
│  │  └─────────────────────────────────┘    │    │
│  │  ┌─────────────────────────────────┐    │    │
│  │  │      LibreOffice Bundle         │    │    │
│  │  └─────────────────────────────────┘    │    │
│  └─────────────────────────────────────────┘    │
├─────────────────────────────────────────────────┤
│  ┌─────────────────────────────────────────┐    │
│  │         Data Volumes                    │    │
│  │  ┌─────────────────────────────────┐    │    │
│  │  │      Temporary Files             │    │    │
│  │  │      Upload Storage              │    │    │
│  │  └─────────────────────────────────┘    │    │
│  └─────────────────────────────────────────┘    │
└─────────────────────────────────────────────────┘
```

### Multi-Server Deployment Considerations

For high-availability scenarios:

```
Load Balancer
     │
     ├── Server 1 (Primary)
     │    ├── API Instance
     │    ├── LibreOffice Bundle
     │    └── Local Storage
     │
     └── Server 2 (Secondary)
          ├── API Instance
          ├── LibreOffice Bundle
          └── Local Storage
```

**Load Balancing Strategy**: Round-robin with health checks
**Session Management**: Stateless design (no session persistence required)
**Data Synchronization**: Not required (each server processes independently)

## Performance and Scalability

### Performance Characteristics

**Throughput Metrics** (Single Server):

- Small documents (1-5 pages): 2-4 seconds
- Medium documents (10-20 pages): 3-6 seconds
- Large documents (50+ pages): 6-12 seconds
- Concurrent conversions: 2 simultaneous (configurable)

**Resource Utilization**:

- CPU: 10-30% during conversion operations
- Memory: 200-500MB base + 100-300MB per conversion
- Disk I/O: Moderate (temporary file operations)

### Scaling Considerations

#### Vertical Scaling

- **Memory**: Additional RAM improves concurrent conversion capacity
- **CPU**: Multi-core processors enable parallel processing
- **Storage**: Fast SSD storage reduces I/O bottlenecks

#### Horizontal Scaling

- **Stateless Design**: Enables load balancer distribution
- **Shared Storage**: Optional for centralized file management
- **Configuration Sync**: Environment variables for consistent settings

#### Performance Tuning

- **Concurrency Control**: Configurable max concurrent conversions
- **Timeout Management**: Automatic process termination for hung operations
- **Resource Monitoring**: Built-in performance tracking and alerting

## Monitoring and Observability

### Health Monitoring

**Endpoints**:

- `/health`: Basic service availability
- `/health/detailed`: Comprehensive system status
- `/health/errors`: Error metrics and trends

**Health Checks**:

- Service responsiveness
- LibreOffice availability
- Disk space availability
- Memory usage thresholds

### Logging and Telemetry

**Log Levels**: ERROR, WARN, INFO, DEBUG
**Structured Logging**: JSON format with correlation IDs
**Metrics Collected**:

- Conversion success/failure rates
- Processing times and throughput
- Resource utilization
- Security events and violations

### Alerting Triggers

**Critical Alerts**:

- Service unavailable
- High error rates (>5%)
- Resource exhaustion
- Security violations

**Warning Alerts**:

- Performance degradation
- Disk space low
- Memory usage high

## Technology Stack Rationale

### Core Technologies

**Node.js**: Chosen for:

- Cross-platform compatibility (Windows Server focus)
- Rich ecosystem for HTTP servers and file operations
- Single-threaded model prevents race conditions
- Excellent async/await support for I/O operations

**Express.js**: Selected for:

- Mature, battle-tested HTTP framework
- Middleware architecture matches security requirements
- Extensive ecosystem for security and monitoring
- Lightweight and performant

**LibreOffice**: Primary conversion engine because:

- Free, open-source, and widely supported
- Excellent fidelity for Office document formats
- Headless operation capability
- Cross-platform availability
- No licensing costs or external service dependencies

### Security Libraries

**Helmet.js**: Security headers and XSS protection
**Express Rate Limit**: Request throttling and DoS protection
**IP Address Validation**: CIDR-based whitelist enforcement

### File Processing Libraries

**Multer**: Robust multipart form-data handling
**Sharp**: High-performance image processing
**Adm-zip**: Office document manipulation
**PDF.js**: PDF structure analysis

## Risk Analysis and Mitigation

### Operational Risks

**LibreOffice Process Hangs**:

- Mitigation: Timeout enforcement, process monitoring, automatic cleanup
- Impact: Minimal (affects single conversion, service continues)

**Disk Space Exhaustion**:

- Mitigation: Size limits, cleanup policies, monitoring alerts
- Impact: Service degradation with automatic recovery

**Memory Leaks**:

- Mitigation: Process isolation, resource monitoring, restart policies
- Impact: Isolated to conversion processes

### Security Risks

**Malicious File Uploads**:

- Mitigation: Multi-layer validation, content analysis, sandboxed execution
- Impact: Files rejected at validation stage

**IP Whitelist Bypass**:

- Mitigation: CIDR validation, request logging, regular audits
- Impact: Logged and blocked at network layer

**Information Disclosure**:

- Mitigation: Sanitized error responses, minimal logging of sensitive data
- Impact: No sensitive information exposed

### Performance Risks

**Resource Exhaustion**:

- Mitigation: Configurable limits, monitoring, graceful degradation
- Impact: Service throttling rather than failure

**Large File Processing**:

- Mitigation: Size limits, timeout controls, progress monitoring
- Impact: Rejected at input validation

## Future Considerations

### Potential Enhancements

**Microservices Migration**:

- Split conversion services by format type
- Independent scaling per conversion type
- Improved fault isolation

**Containerization**:

- Docker deployment for easier management
- Kubernetes orchestration for scaling
- CI/CD pipeline integration

**Advanced Monitoring**:

- Distributed tracing (OpenTelemetry)
- Metrics collection (Prometheus)
- Centralized logging (ELK stack)

### Technology Evolution

**Node.js Updates**: Regular security updates and performance improvements
**LibreOffice Updates**: Format support improvements and bug fixes
**Security Enhancements**: Additional threat detection and response capabilities

## Conclusion

This architecture provides a robust, secure, and maintainable foundation for document conversion operations. The design emphasizes simplicity, security, and reliability while remaining flexible enough to accommodate future growth and enhancement. The single-engine approach using LibreOffice ensures consistent, high-quality conversions with minimal operational complexity.
