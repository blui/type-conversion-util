/**
 * Advanced Security Middleware
 *
 * Additional security controls for internal server deployment.
 * Defense-in-depth protection beyond basic security middleware.
 *
 * Features:
 * - IP whitelisting for restricted access
 * - Content-Type enforcement
 * - Request integrity validation
 * - Audit logging
 * - Malicious pattern detection
 */

const crypto = require('crypto');

class AdvancedSecurity {
  constructor() {
    // IP whitelist configuration
    this.ipWhitelist = this._parseIpWhitelist(process.env.IP_WHITELIST);
    // Auto-enable IP whitelist if IPs are configured
    this.ipWhitelistEnabled = this.ipWhitelist.length > 0;

    // Content-Type restrictions
    this.allowedContentTypes = [
      'multipart/form-data',
      'application/json',
      'application/x-www-form-urlencoded'
    ];

    // Malicious pattern detection
    this.suspiciousPatterns = [
      /(\.\.)|(\/etc\/)|(\~\/)/gi,  // Path traversal
      /<script|javascript:|onerror=/gi,  // XSS attempts
      /(\bselect\b|\bunion\b|\binsert\b|\bdrop\b).*(\bfrom\b|\binto\b|\btable\b)/gi,  // SQL injection
      /(\${|<%|<\?)/gi,  // Template injection
      /(exec|eval|system|passthru)/gi  // Command injection
    ];
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
    return whitelist.split(',').map(ip => ip.trim()).filter(Boolean);
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
        console.warn('IP whitelist enabled but no IPs configured');
        return next();
      }

      // Check if IP is whitelisted
      const isAllowed = this.ipWhitelist.some(allowedIp => {
        if (allowedIp.includes('/')) {
          // CIDR range matching would go here
          return this._isIpInCidr(clientIp, allowedIp);
        }
        return clientIp === allowedIp || clientIp.endsWith(allowedIp);
      });

      if (!isAllowed) {
        console.warn(`Blocked request from non-whitelisted IP: ${clientIp}`);
        return res.status(403).json({
          error: 'Access Denied',
          message: 'Your IP address is not authorized to access this service',
          requestId: req.id
        });
      }

      next();
    };
  }

  /**
   * Check if IP is in CIDR range
   * Simplified implementation for common cases
   *
   * @param {string} ip - Client IP address
   * @param {string} cidr - CIDR range (e.g., 192.168.1.0/24)
   * @returns {boolean} True if IP is in range
   * @private
   */
  _isIpInCidr(ip, cidr) {
    // Simplified CIDR matching for IPv4
    const [range, bits] = cidr.split('/');
    if (!bits) return ip === range;

    const mask = ~(2 ** (32 - parseInt(bits, 10)) - 1);
    const ipInt = this._ipToInt(ip);
    const rangeInt = this._ipToInt(range);

    return (ipInt & mask) === (rangeInt & mask);
  }

  /**
   * Convert IP address to integer
   *
   * @param {string} ip - IP address
   * @returns {number} Integer representation
   * @private
   */
  _ipToInt(ip) {
    return ip.split('.').reduce((int, octet) => (int << 8) + parseInt(octet, 10), 0) >>> 0;
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
      if (req.method === 'GET' || req.method === 'OPTIONS') {
        return next();
      }

      const contentType = req.get('Content-Type') || '';

      // Check if Content-Type is allowed
      const isAllowed = this.allowedContentTypes.some(type =>
        contentType.toLowerCase().startsWith(type.toLowerCase())
      );

      if (!isAllowed && contentType !== '') {
        console.warn(`Blocked request with invalid Content-Type: ${contentType}`);
        return res.status(415).json({
          error: 'Unsupported Media Type',
          message: 'Content-Type header is not supported',
          allowed: this.allowedContentTypes,
          requestId: req.id
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
      const contentType = req.get('Content-Type') || '';
      if (contentType.toLowerCase().includes('multipart/form-data')) {
        return next();
      }

      const targets = [
        req.path,
        JSON.stringify(req.query),
        JSON.stringify(req.body),
        ...Object.values(req.headers).filter(v => typeof v === 'string')
      ];

      for (const target of targets) {
        if (!target) continue;

        for (const pattern of this.suspiciousPatterns) {
          if (pattern.test(target)) {
            console.error(`Malicious pattern detected in request from ${req.ip}`);
            console.error(`Pattern: ${pattern}, Target: ${target.substring(0, 100)}`);

            return res.status(400).json({
              error: 'Bad Request',
              message: 'Request contains suspicious patterns',
              requestId: req.id
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
        userAgent: req.get('User-Agent'),
        contentLength: req.get('Content-Length'),
        referer: req.get('Referer')
      };

      // Log on response finish
      res.on('finish', () => {
        auditLog.statusCode = res.statusCode;
        auditLog.duration = Date.now() - req.startTime;

        // Log suspicious activity
        if (res.statusCode >= 400) {
          console.warn('Audit Log:', JSON.stringify(auditLog));
        } else if (process.env.LOG_LEVEL === 'debug') {
          console.log('Audit Log:', JSON.stringify(auditLog));
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
          error: 'URI Too Long',
          message: 'Request URL exceeds maximum length',
          requestId: req.id
        });
      }

      // Validate header count
      const headerCount = Object.keys(req.headers).length;
      if (headerCount > 100) {
        return res.status(400).json({
          error: 'Bad Request',
          message: 'Too many request headers',
          requestId: req.id
        });
      }

      // Validate query parameter count
      const queryCount = Object.keys(req.query).length;
      if (queryCount > 50) {
        return res.status(400).json({
          error: 'Bad Request',
          message: 'Too many query parameters',
          requestId: req.id
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

      res.on('finish', () => {
        const duration = Date.now() - req.startTime;
        if (duration > threshold) {
          console.warn(`Slow request detected: ${req.method} ${req.path} (${duration}ms)`);
        }
      });

      next();
    };
  }
}

module.exports = new AdvancedSecurity();
