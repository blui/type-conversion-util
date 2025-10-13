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

const crypto = require("crypto");
const ipaddr = require("ipaddr.js");

class AdvancedSecurity {
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

    // Malicious pattern detection
    this.suspiciousPatterns = [
      /(\.\.)|(\/etc\/)|(\~\/)/gi, // Path traversal
      /<script|javascript:|onerror=/gi, // XSS attempts
      /(\bselect\b|\bunion\b|\binsert\b|\bdrop\b).*(\bfrom\b|\binto\b|\btable\b)/gi, // SQL injection
      /(\${|<%|<\?)/gi, // Template injection
      /(exec|eval|system|passthru)/gi, // Command injection
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
}

module.exports = new AdvancedSecurity();
