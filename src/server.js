/**
 * File Conversion Utility Server
 *
 * Main Express.js server application that provides file conversion services
 * through a RESTful API. Supports document, image, audio, and archive conversions
 * using pure Node.js libraries without external system dependencies.
 *
 * Features:
 * - Enterprise security with helmet, CORS, and rate limiting
 * - OpenAPI 3.0 documentation with Swagger UI
 * - Comprehensive error handling and logging
 * - File upload handling with size limits
 * - Temporary file management and cleanup
 */

// Load environment variables from .env file
require("dotenv").config();

// Core Express.js and middleware dependencies
const express = require("express");
const cors = require("cors");
const helmet = require("helmet");
const morgan = require("morgan");
const rateLimit = require("express-rate-limit");
const path = require("path");
const fs = require("fs");

// Application-specific imports
const config = require("./config/config");
const ErrorHandler = require("./middleware/errorHandler");
const { setupSwagger } = require("./config/swagger");

// Route handlers
const conversionRoutes = require("./routes/conversion");
const healthRoutes = require("./routes/health");

// Initialize Express application
const app = express();
const PORT = config.port;

/**
 * Initialize temporary directory for file processing
 * Creates the directory if it doesn't exist to ensure file uploads can be processed
 * In serverless environments, /tmp is automatically available
 */
if (config.tempDir !== "/tmp" && !fs.existsSync(config.tempDir)) {
  fs.mkdirSync(config.tempDir, { recursive: true });
}

/**
 * Verify system dependencies on startup
 * Checks that required Node.js libraries are available and logs the results
 */
ErrorHandler.checkSystemDependencies().then((dependencies) => {
  console.log("System dependencies verified:", dependencies);
});

/**
 * Security middleware configuration
 * Implements Content Security Policy and other security headers using Helmet
 * Allows specific external resources needed for the web interface
 */
app.use(
  helmet({
    contentSecurityPolicy: {
      directives: {
        defaultSrc: ["'self'"],
        styleSrc: ["'self'", "'unsafe-inline'", "https://cdn.jsdelivr.net"],
        scriptSrc: ["'self'", "https://cdn.jsdelivr.net"],
        imgSrc: ["'self'", "data:", "blob:"],
      },
    },
  })
);

/**
 * Rate limiting middleware
 * Prevents abuse by limiting requests per IP address
 * Allows 100 requests per 15-minute window per IP
 */
const limiter = rateLimit({
  windowMs: 15 * 60 * 1000, // 15 minutes in milliseconds
  max: 100, // Maximum requests per window per IP
  message: "Too many requests from this IP, please try again later.",
});
app.use("/api", limiter);

/**
 * Cross-Origin Resource Sharing (CORS) configuration
 * Allows cross-origin requests in development, restricts in production
 * Enables credentials for authenticated requests
 */
app.use(
  cors({
    origin: process.env.NODE_ENV === "production" ? false : true,
    credentials: true,
  })
);

/**
 * HTTP request logging middleware
 * Uses Morgan to log all HTTP requests in combined format
 * Useful for monitoring and debugging
 */
app.use(morgan("combined"));

/**
 * Request body parsing middleware
 * Handles JSON and URL-encoded request bodies
 * Sets upload limit based on environment variable or defaults to 50MB
 */
app.use(express.json({ limit: process.env.UPLOAD_LIMIT || "50mb" }));
app.use(
  express.urlencoded({
    extended: true,
    limit: process.env.UPLOAD_LIMIT || "50mb",
  })
);

/**
 * Static file serving middleware
 * Serves static assets (HTML, CSS, JS) from the public directory
 */
app.use(express.static(path.join(__dirname, "../public")));

/**
 * Initialize Swagger/OpenAPI documentation
 * Sets up interactive API documentation at /api-docs endpoint
 */
setupSwagger(app);

/**
 * API route configuration
 * Maps route handlers to their respective URL paths
 */
app.use("/api/health", healthRoutes);
app.use("/api", conversionRoutes);

/**
 * Main web interface route
 * Serves the primary HTML interface for file conversions
 */
app.get("/", (req, res) => {
  res.sendFile(path.join(__dirname, "../public/index.html"));
});

/**
 * Global error handling middleware
 * Catches and processes all application errors
 * Provides appropriate HTTP status codes and error messages
 * Logs errors for debugging and monitoring purposes
 */
app.use((err, req, res, next) => {
  // Log the error for debugging and monitoring
  ErrorHandler.logError(err, req);

  // Handle file size limit exceeded errors
  if (err.code === "LIMIT_FILE_SIZE") {
    return res.status(413).json({
      error: "File too large",
      message: `File size exceeds the limit of ${config.uploadLimit}`,
    });
  }

  // Handle unexpected file upload errors
  if (err.code === "LIMIT_UNEXPECTED_FILE") {
    return res.status(400).json({
      error: "Invalid file upload",
      message: "Unexpected file field",
    });
  }

  // Handle all other server errors
  res.status(500).json({
    error: "Internal server error",
    message:
      config.nodeEnv === "development" ? err.message : "Something went wrong",
  });
});

/**
 * 404 Not Found handler
 * Handles requests to non-existent routes
 * Returns consistent JSON error response
 */
app.use("*", (req, res) => {
  res.status(404).json({
    error: "Not found",
    message: "The requested resource was not found",
  });
});

/**
 * Graceful shutdown handler
 * Cleans up temporary files when the application is terminated
 * Ensures no orphaned files are left behind
 * Note: In serverless environments, this cleanup is handled automatically
 */
process.on("SIGINT", () => {
  console.log("Shutting down server and cleaning up temporary files...");
  if (config.tempDir !== "/tmp" && fs.existsSync(config.tempDir)) {
    fs.rmSync(config.tempDir, { recursive: true, force: true });
  }
  process.exit(0);
});

/**
 * Start the HTTP server (only in non-serverless environments)
 * In serverless environments like Vercel, the app is exported and handled by the platform
 */
if (!process.env.VERCEL && !process.env.AWS_LAMBDA_FUNCTION_NAME) {
  app.listen(PORT, () => {
    console.log(`File Conversion Server running on port ${PORT}`);
    console.log(`Temporary files directory: ${config.tempDir}`);
    console.log(`Web interface: http://localhost:${PORT}`);
    console.log(`API base URL: http://localhost:${PORT}/api`);
    console.log(`Environment: ${config.nodeEnv}`);
  });
}

// Export the Express application for serverless platforms and testing
module.exports = app;
