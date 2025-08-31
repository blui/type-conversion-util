/**
 * Error Handler Middleware
 *
 * Centralized error handling, logging, and file validation utilities.
 * Provides comprehensive error management for the file conversion application
 * including structured logging, file cleanup, and user-friendly error messages.
 */

const fs = require("fs");
const path = require("path");

class ErrorHandler {
  /**
   * Log error information to console with structured format
   * Creates detailed log entries with request context and error details
   *
   * @param {Error} error - The error object to log
   * @param {Object} req - Express request object (optional)
   */
  static logError(error, req = null) {
    const timestamp = new Date().toISOString();

    // Create structured log entry with error and request details
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

    // Output error to console for immediate visibility
    // In serverless environments (Vercel, AWS Lambda), use console logging only
    // as the file system is read-only except for /tmp
    console.error("Error occurred:", logEntry);

    // Note: File-based logging disabled for serverless compatibility
    // Use external logging services (e.g., Vercel Analytics, CloudWatch) for persistence
  }

  /**
   * Handle conversion-specific errors with file cleanup
   * Cleans up temporary files and provides user-friendly error messages
   *
   * @param {Error} error - The conversion error that occurred
   * @param {string} inputPath - Path to input file for cleanup
   * @param {string} outputPath - Path to output file for cleanup
   * @returns {Object} Formatted error response with status code and message
   */
  static handleConversionError(error, inputPath, outputPath) {
    // Attempt to clean up temporary files to prevent disk space issues
    try {
      if (inputPath && fs.existsSync(inputPath)) {
        fs.unlinkSync(inputPath);
      }
      if (outputPath && fs.existsSync(outputPath)) {
        fs.unlinkSync(outputPath);
      }
    } catch (cleanupError) {
      console.error("File cleanup error:", cleanupError);
    }

    // Analyze error type and determine appropriate response
    let userMessage = "Conversion failed";
    let statusCode = 500;

    // Map specific error types to user-friendly messages and HTTP status codes
    if (error.message.includes("ENOENT")) {
      userMessage =
        "Required system dependency not found. Please ensure all conversion tools are installed.";
      statusCode = 503;
    } else if (error.message.includes("LIMIT_FILE_SIZE")) {
      userMessage = "File size exceeds the maximum allowed limit.";
      statusCode = 413;
    } else if (error.message.includes("Unsupported")) {
      userMessage = error.message;
      statusCode = 400;
    } else if (error.message.includes("timeout")) {
      userMessage = "Conversion timed out. Please try with a smaller file.";
      statusCode = 408;
    }

    return {
      statusCode,
      error: "Conversion failed",
      message: userMessage,
      details:
        process.env.NODE_ENV === "development" ? error.message : undefined,
    };
  }

  /**
   * Validate uploaded file for security and compatibility
   * Performs comprehensive validation including size, type, and security checks
   *
   * @param {Object} file - Multer file object from upload
   * @returns {Array} Array of validation error messages (empty if valid)
   */
  static validateFile(file) {
    const errors = [];

    // Check if file was provided
    if (!file) {
      errors.push("No file provided");
      return errors;
    }

    // Validate file size against configured maximum
    const maxSize = parseInt(process.env.MAX_FILE_SIZE) || 50 * 1024 * 1024; // 50MB default
    if (file.size > maxSize) {
      errors.push(
        `File size (${this.formatFileSize(
          file.size
        )}) exceeds maximum allowed size (${this.formatFileSize(maxSize)})`
      );
    }

    // Define supported file extensions for validation
    const allowedExtensions = [
      // Document formats
      "pdf",
      "docx",
      "xlsx",
      "pptx",
      "txt",
      "html",
      "csv",
      "xml",
      // Image formats
      "jpg",
      "jpeg",
      "png",
      "gif",
      "bmp",
      "tif",
      "tiff",
      "svg",
      "psd",
      // Audio formats
      "mp3",
      "wav",
      // Video formats (informational support)
      "mp4",
      "mov",
      "avi",
      // Archive formats
      "zip",
    ];

    // Extract and validate file extension
    const fileExtension = path
      .extname(file.originalname)
      .toLowerCase()
      .slice(1);
    if (!allowedExtensions.includes(fileExtension)) {
      errors.push(`File type '${fileExtension}' is not supported`);
    }

    // Security check: block potentially dangerous file types
    const dangerousExtensions = [
      "exe",
      "bat",
      "cmd",
      "com",
      "pif",
      "scr",
      "vbs",
      "js",
    ];
    if (dangerousExtensions.includes(fileExtension)) {
      errors.push("File type not allowed for security reasons");
    }

    return errors;
  }

  /**
   * Format file size in human-readable format
   * Converts bytes to appropriate unit (Bytes, KB, MB, GB)
   *
   * @param {number} bytes - File size in bytes
   * @returns {string} Formatted file size string
   */
  static formatFileSize(bytes) {
    if (bytes === 0) return "0 Bytes";

    const k = 1024;
    const sizes = ["Bytes", "KB", "MB", "GB"];
    const i = Math.floor(Math.log(bytes) / Math.log(k));

    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + " " + sizes[i];
  }

  /**
   * Check availability of system dependencies
   * Verifies that required Node.js libraries are installed and accessible
   *
   * @returns {Promise<Object>} Object containing dependency availability status
   */
  static async checkSystemDependencies() {
    const dependencies = {
      puppeteer: false,
      sharp: false,
      nodeLibraries: true,
    };

    // Verify Puppeteer availability for PDF generation
    try {
      const puppeteer = require("puppeteer");
      dependencies.puppeteer = true;
    } catch (error) {
      console.warn("Puppeteer not available - PDF generation may be limited");
    }

    // Verify Sharp availability for image processing
    try {
      const sharp = require("sharp");
      dependencies.sharp = true;
    } catch (error) {
      console.warn("Sharp not available - image conversions may be limited");
    }

    console.log(
      "Using pure Node.js libraries - no external dependencies required"
    );
    return dependencies;
  }
}

// Export the ErrorHandler class for use throughout the application
module.exports = ErrorHandler;
