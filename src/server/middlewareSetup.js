/**
 * Middleware Configuration
 *
 * Centralized middleware setup for the Express application.
 * Configures security, logging, parsing, and other middleware layers.
 */

const express = require("express");
const cors = require("cors");
const helmet = require("helmet");
const morgan = require("morgan");

const config = require("../config/config");
const security = require("../middleware/security");
const advancedSecurity = require("../middleware/advancedSecurity");
const requestContext = require("../middleware/requestContext");

/**
 * Middleware configuration utilities
 */
class MiddlewareSetup {
  /**
   * Configure all security middleware - NASA/JPL Standards Compliant
   * Implements comprehensive security controls with defense-in-depth
   *
   * @param {Object} app - Express application instance
   */
  static configureSecurity(app) {
    // Helmet: Security headers for protection against common vulnerabilities
    app.use(helmet(config.helmet));

    // NASA/JPL Enhanced security headers (includes additional protections)
    app.use(advancedSecurity.securityHeaders());

    // Advanced security controls - Defense in Depth
    app.use(advancedSecurity.ipWhitelistMiddleware());
    app.use(advancedSecurity.requestIntegrityValidation());
    app.use(advancedSecurity.maliciousPatternDetection());
    app.use(advancedSecurity.contentTypeEnforcement());

    // Input sanitization and validation
    app.use(advancedSecurity.inputSanitization());

    // Rate limiting with sliding window algorithm
    app.use(advancedSecurity.rateLimiting());

    // Request anomaly detection
    app.use(advancedSecurity.anomalyDetection());

    // Audit logging for compliance
    app.use(advancedSecurity.auditLogging());

    // File security validation for uploads
    app.use(advancedSecurity.fileSecurityValidation());

    // Performance monitoring for slow requests
    app.use(advancedSecurity.slowRequestDetection(5000));

    // Legacy security middleware (maintained for compatibility)
    app.use(security.securityHeaders);
    app.use(security.createRateLimiter());
    app.use(security.requestTimeout(300000)); // Increased for complex conversions

    console.log(
      "NASA/JPL Security middleware configured with defense-in-depth protections"
    );
  }

  /**
   * Configure basic middleware (CORS, logging, parsing)
   *
   * @param {Object} app - Express application instance
   */
  static configureBasicMiddleware(app) {
    // CORS: Cross-origin resource sharing configuration
    app.use(cors(config.cors));
    app.options("*", cors(config.cors));

    // Logging: HTTP request logging with configurable format
    app.use(morgan(config.logging.format));

    // Request context: Add unique request IDs and timing information
    app.use(requestContext);

    // Body parsing: JSON and URL-encoded data with size limits
    app.use(express.json({ limit: config.uploadLimit }));
    app.use(express.urlencoded({ extended: true, limit: config.uploadLimit }));
  }
}

module.exports = MiddlewareSetup;
