/**
 * File Conversion Utility Server
 *
 * Express server providing file conversion API with enhanced accuracy features.
 * Supports document and image conversions with professional-grade formatting preservation.
 *
 * Features:
 * - Document conversions: PDF, DOCX, XLSX, CSV, PPTX, TXT, XML
 * - Image conversions: JPG, PNG, GIF, BMP, TIFF, SVG, PSD
 * - RESTful API with comprehensive documentation
 * - Health monitoring and system diagnostics
 * - Rate limiting and security middleware
 */

// Load environment variables from .env file
require("dotenv").config();

// Core Express and middleware imports
const express = require("express");
const cors = require("cors");
const helmet = require("helmet");
const morgan = require("morgan");
const rateLimit = require("express-rate-limit");

// Node.js built-in modules
const path = require("path");
const fs = require("fs");
const os = require("os");

// Application configuration and utilities
const config = require("./config/config");
const sslConfig = require("./config/ssl");
const ErrorHandler = require("./middleware/errorHandler");
const requestContext = require("./middleware/requestContext");
const security = require("./middleware/security");
const advancedSecurity = require("./middleware/advancedSecurity");
const { setupSwagger } = require("./config/swagger");

// Route handlers
const conversionRoutes = require("./routes/conversion");
const healthRoutes = require("./routes/health");

// Initialize Express application
const app = express();

// Server configuration from environment
const PORT = config.port;
const HOST = config.host;

/**
 * Log startup information and server configuration
 * Displays environment settings, network configuration, and service status
 */
function logStartupInfo() {
  console.log("=".repeat(50));
  console.log("File Conversion Utility Server Starting");
  console.log("=".repeat(50));
  console.log(`Environment: ${config.nodeEnv}`);
  console.log(`Port: ${PORT}`);
  console.log(`Host: ${HOST}`);
  console.log(`Temp Directory: ${config.tempDir}`);
  console.log(`Max Concurrency: ${config.concurrency.maxConcurrent}`);
  console.log(
    `Rate Limit: ${config.rateLimit.max} requests per ${
      config.rateLimit.windowMs / 1000 / 60
    } minutes`
  );
  console.log("=".repeat(50));
}

// Configure proxy trust for load balancers and reverse proxies
// This is important for correct IP address detection in production environments
if (config.network.trustProxy) {
  app.set("trust proxy", true);
  console.log("Trust proxy enabled for load balancer support");
}

// Initialize temporary directory for file processing
// Creates temp directory if it doesn't exist (except for /tmp in production)
if (config.tempDir !== "/tmp" && !fs.existsSync(config.tempDir)) {
  try {
    fs.mkdirSync(config.tempDir, { recursive: true });
    console.log(`Created temp directory: ${config.tempDir}`);
  } catch (error) {
    console.error(`Failed to create temp directory: ${error.message}`);
  }
}

// Verify system dependencies and conversion libraries
// Checks for Puppeteer, Sharp, and other required libraries
ErrorHandler.checkSystemDependencies()
  .then((dependencies) => {
    console.log("System dependencies verified:", dependencies);
  })
  .catch((error) => {
    console.warn("Dependency check failed (non-fatal):", error.message);
  });

// Security and middleware configuration
// Helmet: Security headers for protection against common vulnerabilities
app.use(helmet(config.helmet));

// Enhanced security headers
app.use(security.securityHeaders);

// Advanced security controls
app.use(advancedSecurity.ipWhitelistMiddleware());
app.use(advancedSecurity.requestIntegrityValidation());
app.use(advancedSecurity.maliciousPatternDetection());
app.use(advancedSecurity.contentTypeEnforcement());
app.use(advancedSecurity.auditLogging());
app.use(advancedSecurity.slowRequestDetection(10000));

// Rate limiting: Prevent abuse and ensure fair usage (enhanced version)
app.use(security.createRateLimiter());

// Request timeout enforcement
app.use(security.requestTimeout(120000));

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

// Static file serving configuration
// Serve static files from the project root directory
app.use(express.static(path.join(__dirname, "../")));

// API route configuration
// Mount conversion routes under /api prefix
app.use("/api", conversionRoutes);

// Health check routes for monitoring and load balancers
app.use("/health", healthRoutes);
app.use("/api/health", healthRoutes);

// Serve the main web interface
// Returns the HTML file for the file conversion web application
app.get("/", (req, res) => {
  res.sendFile(path.join(__dirname, "../index.html"));
});

// API documentation setup
// Configure Swagger/OpenAPI documentation for the REST API
setupSwagger(app);

// API discovery and information endpoint
// Provides metadata about the API including available endpoints and version
app.get("/api", (req, res) => {
  res.json({
    name: "File Conversion Utility API",
    version: require("../package.json").version,
    description: "Document and image conversion service with enhanced accuracy",
    endpoints: {
      convert: "/api/convert",
      health: "/health",
      "supported-formats": "/api/supported-formats",
      documentation: "/api-docs",
    },
    environment: config.nodeEnv,
    timestamp: new Date().toISOString(),
  });
});

// 404 error handler for unmatched routes
// Returns a structured error response with available endpoints
app.use("*", (req, res) => {
  res.status(404).json({
    error: "Not found",
    message: "The requested resource was not found",
    availableEndpoints: ["/api", "/health", "/api-docs", "/"],
    requestId: req.id,
  });
});

// Global error handling middleware
// Catches and processes all unhandled errors in the application
app.use(ErrorHandler.handle);

// Graceful shutdown handlers
// Handle system signals for clean application termination
process.on("SIGTERM", () => {
  console.log("SIGTERM received, shutting down gracefully...");
  process.exit(0);
});

process.on("SIGINT", () => {
  console.log("SIGINT received, shutting down gracefully...");
  process.exit(0);
});

// Server startup configuration
// Supports both HTTP and HTTPS based on SSL configuration
let server;
const httpsOptions = sslConfig.getHttpsOptions();

if (httpsOptions) {
  // Start HTTPS server
  const https = require('https');
  server = https.createServer(httpsOptions, app);

  server.listen(PORT, HOST, () => {
    logStartupInfo();
    console.log(`Secure server running on https://${HOST}:${PORT}`);
    console.log(`Web Interface: https://${HOST}:${PORT}`);
    console.log(`API Documentation: https://${HOST}:${PORT}/api-docs`);
    console.log(`Health Check: https://${HOST}:${PORT}/health`);
  });
} else {
  // Start HTTP server
  const http = require('http');
  server = http.createServer(app);

  server.listen(PORT, HOST, () => {
    logStartupInfo();
    console.log(`Server running on http://${HOST}:${PORT}`);
    console.log(`Web Interface: http://${HOST}:${PORT}`);
    console.log(`API Documentation: http://${HOST}:${PORT}/api-docs`);
    console.log(`Health Check: http://${HOST}:${PORT}/health`);
  });
}

// Configure keep-alive settings for better connection management
if (config.network.keepAlive) {
  server.keepAliveTimeout = config.network.keepAliveTimeout;
  server.headersTimeout = config.network.keepAliveTimeout + 1000;
}

module.exports = app;
