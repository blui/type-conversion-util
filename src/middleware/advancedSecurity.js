/**
 * Advanced Security Middleware
 *
 * Implements comprehensive security controls for file conversion service.
 * Provides defense-in-depth protection with IP whitelisting, input validation,
 * and threat detection following NASA/JPL standards.
 */

const crypto = require("crypto");
const ipaddr = require("ipaddr.js");

class AdvancedSecurity {
  // Security monitoring metrics
  static securityMetrics = {
    blockedRequests: 0,
    suspiciousPatternsDetected: 0,
    rateLimitHits: 0,
    invalidInputs: 0,
    recentSecurityEvents: [],
  };

  constructor() {
    // IP whitelist configuration
    this.ipWhitelist = this._parseIpWhitelist(process.env.IP_WHITELIST);
    // Auto-enable IP whitelist if IPs are configured
    this.ipWhitelistEnabled = this.ipWhitelist.length > 0;

    // Content-Type restrictions
    this.allowedContentTypes = [
      "multipart/form-data",
      "application/json",
      "application/x-www-form-urlencoded",
    ];

    // Enhanced malicious pattern detection
    this.suspiciousPatterns = [
      /(\.\.)|(\/etc\/)|(\~\/)/gi, // Path traversal
      /<script|javascript:|onerror=|onload=|onmouseover=/gi, // XSS attempts
      /(\bselect\b|\bunion\b|\binsert\b|\bdrop\b|\bdelete\b|\bupdate\b).*(\bfrom\b|\binto\b|\btable\b|\bwhere\b)/gi, // SQL injection
      /(\${|<%|<\?|<%=)/gi, // Template injection
      /(exec|eval|system|passthru|shell_exec|proc_open)/gi, // Command injection
      /(base64_decode|eval\(|gzinflate|str_rot13)/gi, // Code injection attempts
      /(\.\.\/|\.\.\\)/gi, // Directory traversal
      /\/(wp-admin|wp-content|administrator|admin|phpmyadmin)\//gi, // Common attack vectors
    ];

    // File security validation
    this.allowedFileExtensions = [
      ".docx",
      ".doc",
      ".pdf",
      ".xlsx",
      ".xls",
      ".csv",
      ".txt",
      ".xml",
      ".rtf",
    ];

    this.blockedFileExtensions = [
      ".exe",
      ".bat",
      ".cmd",
      ".scr",
      ".pif",
      ".com",
      ".jar",
      ".msi",
      ".deb",
      ".rpm",
    ];

    // Security headers configuration
    this.securityHeaderConfig = {
      "X-Content-Type-Options": "nosniff",
      "X-Frame-Options": "DENY",
      "X-XSS-Protection": "1; mode=block",
      "Referrer-Policy": "strict-origin-when-cross-origin",
      "Permissions-Policy": "geolocation=(), microphone=(), camera=()",
      "Cross-Origin-Embedder-Policy": "require-corp",
      "Cross-Origin-Opener-Policy": "same-origin",
      "Cross-Origin-Resource-Policy": "same-origin",
    };

    // Request rate monitoring (sliding window)
    this.requestHistory = new Map();
    this.rateLimitWindow = 15 * 60 * 1000; // 15 minutes
    this.maxRequestsPerWindow = parseInt(process.env.RATE_LIMIT_MAX) || 100;

    console.log("Advanced Security initialized with NASA/JPL standards");
    console.log(
      `   IP Whitelist: ${this.ipWhitelistEnabled ? "ENABLED" : "DISABLED"}`
    );
    console.log(
      `   Rate Limiting: ${this.maxRequestsPerWindow} requests per ${
        this.rateLimitWindow / 60000
      } minutes`
    );
  }

  /**
   * Parse IP whitelist from environment variable
   *
   * @param {string} whitelist - Comma-separated IP addresses or CIDR ranges
   * @returns {Array} Parsed IP list
   * @private
   */
  _parseIpWhitelist(whitelist) {
    if (!whitelist) return [];
    return whitelist
      .split(",")
      .map((ip) => ip.trim())
      .filter(Boolean);
  }

  /**
   * IP Whitelist Middleware
   * Restricts access to whitelisted IP addresses
   *
   * @returns {Function} Express middleware
   */
  ipWhitelistMiddleware() {
    return (req, res, next) => {
      if (!this.ipWhitelistEnabled) {
        return next();
      }

      const clientIp = req.ip || req.connection.remoteAddress;

      if (this.ipWhitelist.length === 0) {
        console.warn("IP whitelist enabled but no IPs configured");
        return next();
      }

      // Check if IP is whitelisted
      const isAllowed = this._checkIpWhitelist(clientIp, this.ipWhitelist);

      if (!isAllowed) {
        console.warn(`Blocked request from non-whitelisted IP: ${clientIp}`);
        return res.status(403).json({
          error: "Access Denied",
          message: "Your IP address is not authorized to access this service",
          requestId: req.id,
        });
      }

      next();
    };
  }

  /**
   * Check if client IP matches any entry in the whitelist
   * Handles both IPv4 and IPv6 addresses and CIDR ranges correctly
   *
   * Uses ipaddr.js for robust IP parsing and comparison:
   * - Handles all IPv6 formats (compressed, expanded, IPv4-mapped)
   * - Validates address formats
   * - Performs CIDR matching with version conversion
   *
   * @param {string} clientIpStr - Client IP address string
   * @param {Array<string>} whitelist - Array of allowed IPs or CIDR ranges
   * @returns {boolean} True if IP is whitelisted
   * @private
   */
  _checkIpWhitelist(clientIpStr, whitelist) {
    if (!clientIpStr || typeof clientIpStr !== "string") {
      return false;
    }

    let clientAddr;
    try {
      // ipaddr.process() handles IPv4, IPv6, and IPv4-mapped IPv6
      // It normalizes addresses to canonical form
      clientAddr = ipaddr.process(clientIpStr);
    } catch (err) {
      console.error(`Invalid client IP address: ${clientIpStr}`, err.message);
      return false;
    }

    // Check against each whitelist entry
    for (const entry of whitelist) {
      try {
        if (entry.includes("/")) {
          // CIDR range matching
          const [rangeStr, prefixLenStr] = entry.split("/");
          const prefixLen = parseInt(prefixLenStr, 10);

          if (isNaN(prefixLen)) {
            console.warn(`Invalid CIDR prefix length: ${entry}`);
            continue;
          }

          const rangeAddr = ipaddr.process(rangeStr);

          // Handle version mismatches (IPv4 client vs IPv6 range, etc.)
          if (this._matchCidr(clientAddr, rangeAddr, prefixLen)) {
            return true;
          }
        } else {
          // Exact IP match
          const allowedAddr = ipaddr.process(entry);

          // Compare canonical representations
          if (clientAddr.toString() === allowedAddr.toString()) {
            return true;
          }
        }
      } catch (err) {
        console.warn(`Invalid whitelist entry: ${entry}`, err.message);
        continue;
      }
    }

    return false;
  }

  /**
   * Check if client IP matches CIDR range
   * Handles IPv4/IPv6 version mismatches by converting when possible
   *
   * @param {Object} clientAddr - Parsed client IP (ipaddr.js IPv4 or IPv6 object)
   * @param {Object} rangeAddr - Parsed range IP (ipaddr.js IPv4 or IPv6 object)
   * @param {number} prefixLen - CIDR prefix length
   * @returns {boolean} True if IP is in range
   * @private
   */
  _matchCidr(clientAddr, rangeAddr, prefixLen) {
    // Direct match if same IP version
    if (clientAddr.kind() === rangeAddr.kind()) {
      return clientAddr.match(rangeAddr, prefixLen);
    }

    // Handle IPv4-mapped IPv6 addresses
    // If client is IPv6 and range is IPv4, check if client is IPv4-mapped
    if (clientAddr.kind() === "ipv6" && rangeAddr.kind() === "ipv4") {
      if (clientAddr.isIPv4MappedAddress()) {
        const clientAsV4 = clientAddr.toIPv4Address();
        return clientAsV4.match(rangeAddr, prefixLen);
      }
    }

    // If range is IPv6 and client is IPv4, convert client to IPv4-mapped IPv6
    if (clientAddr.kind() === "ipv4" && rangeAddr.kind() === "ipv6") {
      if (rangeAddr.isIPv4MappedAddress()) {
        const rangeAsV4 = rangeAddr.toIPv4Address();
        return clientAddr.match(rangeAsV4, prefixLen);
      }
      // Check if the IPv4 address would match when mapped to IPv6
      const clientAsV6 = clientAddr.toIPv4MappedAddress();
      return clientAsV6.match(rangeAddr, prefixLen);
    }

    // No version conversion possible - addresses are incompatible
    return false;
  }

  /**
   * Content-Type Enforcement Middleware
   * Validates Content-Type header for POST/PUT requests
   *
   * @returns {Function} Express middleware
   */
  contentTypeEnforcement() {
    return (req, res, next) => {
      // Skip GET and OPTIONS requests
      if (req.method === "GET" || req.method === "OPTIONS") {
        return next();
      }

      const contentType = req.get("Content-Type") || "";

      // Check if Content-Type is allowed
      const isAllowed = this.allowedContentTypes.some((type) =>
        contentType.toLowerCase().startsWith(type.toLowerCase())
      );

      if (!isAllowed && contentType !== "") {
        console.warn(
          `Blocked request with invalid Content-Type: ${contentType}`
        );
        return res.status(415).json({
          error: "Unsupported Media Type",
          message: "Content-Type header is not supported",
          allowed: this.allowedContentTypes,
          requestId: req.id,
        });
      }

      next();
    };
  }

  /**
   * Malicious Pattern Detection Middleware
   * Scans request for suspicious patterns
   * Skips binary file uploads to prevent false positives
   *
   * @returns {Function} Express middleware
   */
  maliciousPatternDetection() {
    return (req, res, next) => {
      // Skip pattern detection for file uploads (multipart/form-data)
      // File binary data can contain byte sequences matching patterns
      const contentType = req.get("Content-Type") || "";
      if (contentType.toLowerCase().includes("multipart/form-data")) {
        return next();
      }

      const targets = [
        req.path,
        JSON.stringify(req.query),
        JSON.stringify(req.body),
        ...Object.values(req.headers).filter((v) => typeof v === "string"),
      ];

      for (const target of targets) {
        if (!target) continue;

        for (const pattern of this.suspiciousPatterns) {
          if (pattern.test(target)) {
            console.error(
              `Malicious pattern detected in request from ${req.ip}`
            );
            console.error(
              `Pattern: ${pattern}, Target: ${target.substring(0, 100)}`
            );

            return res.status(400).json({
              error: "Bad Request",
              message: "Request contains suspicious patterns",
              requestId: req.id,
            });
          }
        }
      }

      next();
    };
  }

  /**
   * Audit Logging Middleware
   * Logs detailed request information for security audits
   *
   * @returns {Function} Express middleware
   */
  auditLogging() {
    return (req, res, next) => {
      const auditLog = {
        timestamp: new Date().toISOString(),
        requestId: req.id,
        method: req.method,
        path: req.path,
        ip: req.ip,
        userAgent: req.get("User-Agent"),
        contentLength: req.get("Content-Length"),
        referer: req.get("Referer"),
      };

      // Log on response finish
      res.on("finish", () => {
        auditLog.statusCode = res.statusCode;
        auditLog.duration = Date.now() - req.startTime;

        // Log suspicious activity
        if (res.statusCode >= 400) {
          console.warn("Audit Log:", JSON.stringify(auditLog));
        } else if (process.env.LOG_LEVEL === "debug") {
          console.log("Audit Log:", JSON.stringify(auditLog));
        }
      });

      next();
    };
  }

  /**
   * Request Integrity Validation Middleware
   * Validates request structure and parameters
   *
   * @returns {Function} Express middleware
   */
  requestIntegrityValidation() {
    return (req, res, next) => {
      // Validate URL length
      if (req.url.length > 2048) {
        return res.status(414).json({
          error: "URI Too Long",
          message: "Request URL exceeds maximum length",
          requestId: req.id,
        });
      }

      // Validate header count
      const headerCount = Object.keys(req.headers).length;
      if (headerCount > 100) {
        return res.status(400).json({
          error: "Bad Request",
          message: "Too many request headers",
          requestId: req.id,
        });
      }

      // Validate query parameter count
      const queryCount = Object.keys(req.query).length;
      if (queryCount > 50) {
        return res.status(400).json({
          error: "Bad Request",
          message: "Too many query parameters",
          requestId: req.id,
        });
      }

      next();
    };
  }

  /**
   * Slow Request Detection Middleware
   * Detects and logs unusually slow requests
   *
   * @param {number} threshold - Threshold in milliseconds
   * @returns {Function} Express middleware
   */
  slowRequestDetection(threshold = 10000) {
    return (req, res, next) => {
      req.startTime = Date.now();

      res.on("finish", () => {
        const duration = Date.now() - req.startTime;
        if (duration > threshold) {
          console.warn(
            `Slow request detected: ${req.method} ${req.path} (${duration}ms)`
          );
        }
      });

      next();
    };
  }

  /**
   * File Security Validation Middleware
   * Validates uploaded files for security threats
   *
   * @returns {Function} Express middleware
   */
  fileSecurityValidation() {
    return (req, res, next) => {
      if (!req.file) {
        return next();
      }

      const file = req.file;
      const filename = file.originalname.toLowerCase();
      const mimetype = file.mimetype.toLowerCase();

      // Check file extension
      const fileExtension = filename.substring(filename.lastIndexOf("."));
      if (this.blockedFileExtensions.includes(fileExtension)) {
        this._recordSecurityEvent("BLOCKED_FILE_EXTENSION", req, {
          extension: fileExtension,
        });
        return res.status(400).json({
          error: "Invalid File Type",
          message: "File type not allowed for security reasons",
          requestId: req.id,
        });
      }

      // Validate allowed extensions for our use case
      if (!this.allowedFileExtensions.includes(fileExtension)) {
        this._recordSecurityEvent("INVALID_FILE_EXTENSION", req, {
          extension: fileExtension,
        });
        return res.status(400).json({
          error: "Unsupported File Type",
          message: "File type not supported for conversion",
          requestId: req.id,
        });
      }

      // Check file size (additional validation beyond multer limits)
      const maxSize = this._parseFileSize(process.env.UPLOAD_LIMIT || "50mb");
      if (file.size > maxSize) {
        this._recordSecurityEvent("FILE_TOO_LARGE", req, {
          size: file.size,
          maxSize,
        });
        return res.status(413).json({
          error: "File Too Large",
          message: `File size ${file.size} exceeds limit of ${maxSize}`,
          requestId: req.id,
        });
      }

      // Check for suspicious filenames
      if (this._isSuspiciousFilename(filename)) {
        this._recordSecurityEvent("SUSPICIOUS_FILENAME", req, { filename });
        return res.status(400).json({
          error: "Invalid Filename",
          message: "Filename contains suspicious characters",
          requestId: req.id,
        });
      }

      // MIME type validation
      if (!this._isValidMimeType(mimetype, fileExtension)) {
        this._recordSecurityEvent("INVALID_MIME_TYPE", req, {
          mimetype,
          extension: fileExtension,
        });
        return res.status(400).json({
          error: "Invalid File Content",
          message: "File content does not match declared type",
          requestId: req.id,
        });
      }

      // Content-based validation for text files
      if (this._isTextFile(mimetype) && file.buffer) {
        const contentValidation = this._validateFileContent(
          file.buffer,
          filename
        );
        if (!contentValidation.valid) {
          this._recordSecurityEvent(
            "MALICIOUS_CONTENT",
            req,
            contentValidation.details
          );
          return res.status(400).json({
            error: "Invalid File Content",
            message: contentValidation.message,
            requestId: req.id,
          });
        }
      }

      next();
    };
  }

  /**
   * Security Headers Middleware
   * Enforces comprehensive security headers
   *
   * @returns {Function} Express middleware
   */
  securityHeaders() {
    return (req, res, next) => {
      // Set security headers
      Object.entries(this.securityHeaderConfig).forEach(([header, value]) => {
        res.setHeader(header, value);
      });

      // Additional dynamic headers
      res.setHeader("X-Request-ID", req.id || "unknown");
      res.setHeader("X-Content-Security-Policy", "default-src 'self'");
      res.setHeader(
        "Strict-Transport-Security",
        "max-age=31536000; includeSubDomains"
      );

      // Remove potentially dangerous headers
      res.removeHeader("X-Powered-By");
      res.removeHeader("Server");

      next();
    };
  }

  /**
   * Input Sanitization Middleware
   * Sanitizes user inputs to prevent injection attacks
   *
   * @returns {Function} Express middleware
   */
  inputSanitization() {
    return (req, res, next) => {
      // Sanitize query parameters
      if (req.query) {
        Object.keys(req.query).forEach((key) => {
          req.query[key] = this._sanitizeInput(req.query[key]);
        });
      }

      // Sanitize body parameters
      if (req.body && typeof req.body === "object") {
        this._sanitizeObject(req.body);
      }

      // Sanitize headers (selective)
      const headersToSanitize = ["referer", "user-agent", "accept-language"];
      headersToSanitize.forEach((header) => {
        if (req.headers[header]) {
          req.headers[header] = this._sanitizeInput(req.headers[header]);
        }
      });

      next();
    };
  }

  /**
   * Rate Limiting Middleware
   * Implements sliding window rate limiting
   *
   * @returns {Function} Express middleware
   */
  rateLimiting() {
    return (req, res, next) => {
      const clientId = this._getClientIdentifier(req);
      const now = Date.now();

      // Get or initialize request history for this client
      if (!this.requestHistory.has(clientId)) {
        this.requestHistory.set(clientId, []);
      }

      const clientRequests = this.requestHistory.get(clientId);

      // Remove requests outside the sliding window
      const windowStart = now - this.rateLimitWindow;
      const validRequests = clientRequests.filter(
        (timestamp) => timestamp > windowStart
      );

      // Check if rate limit exceeded
      if (validRequests.length >= this.maxRequestsPerWindow) {
        this._recordSecurityEvent("RATE_LIMIT_EXCEEDED", req, {
          clientId,
          requestCount: validRequests.length,
          windowStart: new Date(windowStart).toISOString(),
        });

        AdvancedSecurity.securityMetrics.rateLimitHits++;

        return res.status(429).json({
          error: "Too Many Requests",
          message: `Rate limit exceeded. Maximum ${
            this.maxRequestsPerWindow
          } requests per ${this.rateLimitWindow / 60000} minutes`,
          retryAfter: Math.ceil(
            (validRequests[0] + this.rateLimitWindow - now) / 1000
          ),
          requestId: req.id,
        });
      }

      // Add current request to history
      validRequests.push(now);
      this.requestHistory.set(clientId, validRequests);

      // Clean up old entries periodically
      if (this.requestHistory.size > 10000) {
        this._cleanupRequestHistory();
      }

      next();
    };
  }

  /**
   * Request Anomaly Detection Middleware
   * Detects unusual request patterns
   *
   * @returns {Function} Express middleware
   */
  anomalyDetection() {
    return (req, res, next) => {
      const anomalies = [];

      // Check for unusual header combinations
      if (this._hasSuspiciousHeaders(req)) {
        anomalies.push("suspicious_headers");
      }

      // Check for unusual request patterns
      if (this._hasUnusualRequestPattern(req)) {
        anomalies.push("unusual_pattern");
      }

      // Check for potential scanning activity
      if (this._isPotentialScanner(req)) {
        anomalies.push("potential_scanner");
      }

      if (anomalies.length > 0) {
        this._recordSecurityEvent("REQUEST_ANOMALY", req, { anomalies });

        // Log but don't block - monitor for patterns
        console.warn(`Request anomaly detected: ${anomalies.join(", ")}`, {
          requestId: req.id,
          ip: req.ip,
          userAgent: req.headers["user-agent"],
        });
      }

      next();
    };
  }

  // Private helper methods

  /**
   * Parse file size string to bytes
   * @param {string} sizeStr - Size string like "50mb"
   * @returns {number} Size in bytes
   */
  _parseFileSize(sizeStr) {
    const units = { b: 1, kb: 1024, mb: 1024 * 1024, gb: 1024 * 1024 * 1024 };
    const match = sizeStr
      .toLowerCase()
      .match(/^(\d+(?:\.\d+)?)\s*(b|kb|mb|gb)?$/);
    if (!match) return 50 * 1024 * 1024; // Default 50MB

    const size = parseFloat(match[1]);
    const unit = match[2] || "mb";
    return Math.round(size * units[unit]);
  }

  /**
   * Check if filename is suspicious
   * @param {string} filename - Filename to check
   * @returns {boolean} True if suspicious
   */
  _isSuspiciousFilename(filename) {
    const suspiciousPatterns = [
      /\.\./, // Directory traversal
      /[<>:|?*]/, // Invalid filename characters
      /^[.-]/, // Starts with dot or dash
      /\s{2,}/, // Multiple spaces
      /[\x00-\x1F\x7F-\x9F]/, // Control characters
    ];

    return suspiciousPatterns.some((pattern) => pattern.test(filename));
  }

  /**
   * Validate MIME type against file extension
   * @param {string} mimetype - MIME type
   * @param {string} extension - File extension
   * @returns {boolean} True if valid combination
   */
  _isValidMimeType(mimetype, extension) {
    const mimeMap = {
      ".docx": [
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
      ],
      ".doc": ["application/msword"],
      ".pdf": ["application/pdf"],
      ".xlsx": [
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
      ],
      ".xls": ["application/vnd.ms-excel"],
      ".csv": ["text/csv", "application/csv"],
      ".txt": ["text/plain"],
      ".xml": ["text/xml", "application/xml"],
      ".rtf": ["text/rtf", "application/rtf"],
    };

    return mimeMap[extension]?.includes(mimetype) || false;
  }

  /**
   * Check if file is a text-based format
   * @param {string} mimetype - MIME type
   * @returns {boolean} True if text file
   */
  _isTextFile(mimetype) {
    return (
      mimetype.startsWith("text/") ||
      mimetype.includes("xml") ||
      mimetype.includes("csv")
    );
  }

  /**
   * Validate file content for malicious patterns
   * @param {Buffer} content - File content
   * @param {string} filename - Filename
   * @returns {Object} Validation result
   */
  _validateFileContent(content, filename) {
    const textContent = content.toString(
      "utf8",
      0,
      Math.min(content.length, 1024)
    ); // First 1KB

    // Check for malicious patterns in content
    for (const pattern of this.suspiciousPatterns) {
      if (pattern.test(textContent)) {
        return {
          valid: false,
          message: "File content contains suspicious patterns",
          details: { pattern: pattern.source, filename },
        };
      }
    }

    // Check for binary content in text files
    const nullBytes = (textContent.match(/\x00/g) || []).length;
    if (nullBytes > 5) {
      return {
        valid: false,
        message: "Text file contains binary content",
        details: { nullBytes, filename },
      };
    }

    return { valid: true };
  }

  /**
   * Sanitize input string
   * @param {string} input - Input to sanitize
   * @returns {string} Sanitized input
   */
  _sanitizeInput(input) {
    if (typeof input !== "string") return input;

    return input
      .replace(/<script[^>]*>.*?<\/script>/gi, "") // Remove script tags
      .replace(/javascript:/gi, "") // Remove javascript: URLs
      .replace(/on\w+\s*=/gi, "") // Remove event handlers
      .replace(/[<>"'&]/g, (match) => {
        // Escape HTML entities
        const entities = {
          "<": "&lt;",
          ">": "&gt;",
          '"': "&quot;",
          "'": "&#x27;",
          "&": "&amp;",
        };
        return entities[match];
      })
      .substring(0, 10000); // Limit length
  }

  /**
   * Sanitize object recursively
   * @param {Object} obj - Object to sanitize
   */
  _sanitizeObject(obj) {
    for (const key in obj) {
      if (typeof obj[key] === "string") {
        obj[key] = this._sanitizeInput(obj[key]);
      } else if (typeof obj[key] === "object" && obj[key] !== null) {
        this._sanitizeObject(obj[key]);
      }
    }
  }

  /**
   * Get client identifier for rate limiting
   * @param {Object} req - Express request object
   * @returns {string} Client identifier
   */
  _getClientIdentifier(req) {
    // Use IP + User-Agent for identification
    return `${req.ip}:${req.headers["user-agent"] || "unknown"}`;
  }

  /**
   * Clean up old request history entries
   */
  _cleanupRequestHistory() {
    const cutoff = Date.now() - this.rateLimitWindow;
    for (const [clientId, requests] of this.requestHistory) {
      const validRequests = requests.filter((timestamp) => timestamp > cutoff);
      if (validRequests.length === 0) {
        this.requestHistory.delete(clientId);
      } else {
        this.requestHistory.set(clientId, validRequests);
      }
    }
  }

  /**
   * Check for suspicious headers
   * @param {Object} req - Express request object
   * @returns {boolean} True if suspicious headers detected
   */
  _hasSuspiciousHeaders(req) {
    const suspiciousHeaders = [
      "x-forwarded-for",
      "x-real-ip",
      "x-client-ip", // Spoofed IP headers
      "x-forwarded-host",
      "x-forwarded-proto", // Forwarded headers
      "x-custom-header", // Custom headers that shouldn't be there
    ];

    return suspiciousHeaders.some(
      (header) => req.headers[header.toLowerCase()]
    );
  }

  /**
   * Check for unusual request patterns
   * @param {Object} req - Express request object
   * @returns {boolean} True if unusual pattern detected
   */
  _hasUnusualRequestPattern(req) {
    // Check for extremely long query strings
    const queryString = req.url.split("?")[1] || "";
    if (queryString.length > 1000) return true;

    // Check for unusual characters in path
    if (/[^\w\-\/\.\?&=]/.test(req.path)) return true;

    // Check for too many parameters
    const paramCount = (queryString.match(/&/g) || []).length;
    if (paramCount > 20) return true;

    return false;
  }

  /**
   * Check if request appears to be from a scanner
   * @param {Object} req - Express request object
   * @returns {boolean} True if potential scanner detected
   */
  _isPotentialScanner(req) {
    const scannerPatterns = [
      /sqlmap|nessus|acunetix|nmap|nikto|dirbuster|gobuster/i,
      /\/(phpmyadmin|admin|administrator|wp-admin|wp-content|backup)/i,
      /\.(bak|old|orig|backup|save)$/i,
    ];

    const userAgent = req.headers["user-agent"] || "";
    const path = req.path;

    return scannerPatterns.some(
      (pattern) => pattern.test(userAgent) || pattern.test(path)
    );
  }

  /**
   * Record security event for monitoring
   * @param {string} eventType - Type of security event
   * @param {Object} req - Express request object
   * @param {Object} details - Event details
   */
  _recordSecurityEvent(eventType, req, details = {}) {
    const event = {
      type: eventType,
      timestamp: new Date().toISOString(),
      requestId: req.id,
      ip: req.ip,
      userAgent: req.headers["user-agent"],
      url: req.url,
      details,
    };

    // Update metrics
    switch (eventType) {
      case "BLOCKED_REQUEST":
        AdvancedSecurity.securityMetrics.blockedRequests++;
        break;
      case "SUSPICIOUS_PATTERN":
        AdvancedSecurity.securityMetrics.suspiciousPatternsDetected++;
        break;
      case "RATE_LIMIT_EXCEEDED":
        AdvancedSecurity.securityMetrics.rateLimitHits++;
        break;
      case "INVALID_INPUT":
        AdvancedSecurity.securityMetrics.invalidInputs++;
        break;
    }

    // Keep recent events
    AdvancedSecurity.securityMetrics.recentSecurityEvents.unshift(event);
    if (AdvancedSecurity.securityMetrics.recentSecurityEvents.length > 100) {
      AdvancedSecurity.securityMetrics.recentSecurityEvents.pop();
    }

    // Log security event
    console.warn(
      `Security Event: ${eventType}`,
      JSON.stringify(event, null, 2)
    );
  }

  /**
   * Get security metrics for monitoring
   * @returns {Object} Security metrics
   */
  static getSecurityMetrics() {
    return {
      timestamp: new Date().toISOString(),
      ...AdvancedSecurity.securityMetrics,
      summary: {
        totalSecurityEvents: Object.values(AdvancedSecurity.securityMetrics)
          .filter((v) => typeof v === "number")
          .reduce((sum, val) => sum + val, 0),
        mostRecentEvent:
          AdvancedSecurity.securityMetrics.recentSecurityEvents[0] || null,
      },
    };
  }
}

module.exports = new AdvancedSecurity();
