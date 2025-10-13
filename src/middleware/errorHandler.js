/**
 * Error Handler Middleware
 *
 * Centralized error handling and logging for the file conversion application.
 * Structured error logging, system dependency checking, and
 * consistent error responses across the application.
 *
 * Features:
 * - Structured error logging with request context
 * - System dependency verification
 * - Consistent error response formatting
 * - File upload error handling
 * - Security-focused error messages
 */

// Node.js built-in modules for file system operations
const fs = require("fs");
const path = require("path");

class ErrorHandler {
  /**
   * Log error information to console with structured format
   * Creates detailed error logs including timestamp, error details, and request context
   *
   * @param {Error} error - The error object to log
   * @param {Object} req - Express request object (optional)
   */
  static logError(error, req = null) {
    const timestamp = new Date().toISOString();
    const logEntry = {
      timestamp,
      error: {
        message: error.message,
        stack: error.stack,
        code: error.code,
      },
      request: req
        ? {
            method: req.method,
            url: req.url,
            headers: req.headers,
            body: req.body,
            file: req.file
              ? {
                  originalname: req.file.originalname,
                  mimetype: req.file.mimetype,
                  size: req.file.size,
                }
              : null,
          }
        : null,
    };

    console.error("Error occurred:", logEntry);
  }

  /**
   * Express error handling middleware
   * Processes errors and returns appropriate HTTP responses
   * Handles file upload errors, validation errors, and general server errors
   *
   * @param {Error} err - The error object
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   * @param {Function} next - Express next function
   */
  static handle(err, req, res, next) {
    this.logError(err, req);

    // Handle file size limit exceeded errors from multer
    if (err.code === "LIMIT_FILE_SIZE") {
      return res.status(413).json({
        error: "File too large",
        message: `File size exceeds the limit of ${
          process.env.UPLOAD_LIMIT || "50mb"
        }`,
        requestId: req.id,
      });
    }

    // Handle unexpected file upload errors from multer
    if (err.code === "LIMIT_UNEXPECTED_FILE") {
      return res.status(400).json({
        error: "Invalid file upload",
        message: "Unexpected file field",
        requestId: req.id,
      });
    }

    // Handle all other server errors with appropriate error messages
    res.status(500).json({
      error: "Internal server error",
      message:
        process.env.NODE_ENV === "development"
          ? err.message
          : "Something went wrong",
      requestId: req.id,
    });
  }

  /**
   * Check system dependencies and conversion libraries
   * Verifies that required libraries (Sharp, PDFKit, etc.) are available
   * Returns dependency status for monitoring and debugging
   *
   * @returns {Promise<Object>} Object containing dependency status
   */
  static async checkSystemDependencies() {
    try {
      const dependencies = {
        sharp: false,
        pdfkit: false,
        nodeLibraries: true,
      };

      // Check Sharp availability for image processing
      try {
        const sharp = require("sharp");
        dependencies.sharp = true;
      } catch (error) {
        console.warn("Sharp not available:", error.message);
      }

      // Check PDFKit availability for PDF generation
      try {
        const pdfkit = require("pdfkit");
        dependencies.pdfkit = true;
      } catch (error) {
        console.warn("PDFKit not available:", error.message);
      }

      console.log("Dependency check complete");
      return dependencies;
    } catch (error) {
      console.error("Dependency check failed:", error.message);
      return { sharp: false, pdfkit: false, nodeLibraries: false };
    }
  }
}

module.exports = ErrorHandler;
