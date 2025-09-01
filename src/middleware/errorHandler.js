/**
 * Error Handler Middleware
 *
 * Centralized error handling, logging, and file validation utilities
 * Provides comprehensive error management for the file conversion application
 */

const fs = require("fs");
const path = require("path");

class ErrorHandler {
  /**
   * Log error information to console with structured format
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
   * Handle conversion-specific errors with file cleanup
   */
  static handleConversionError(error, inputPath, outputPath) {
    // Clean up temporary files
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

    // Determine appropriate response based on error type
    let userMessage = "Conversion failed";
    let statusCode = 500;

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
      userMessage,
      technicalDetails:
        process.env.NODE_ENV === "development" ? error.message : undefined,
    };
  }

  /**
   * Express error handling middleware
   */
  static handle(err, req, res, next) {
    this.logError(err, req);

    // Handle file size limit exceeded errors
    if (err.code === "LIMIT_FILE_SIZE") {
      return res.status(413).json({
        error: "File too large",
        message: `File size exceeds the limit of ${
          process.env.UPLOAD_LIMIT || "50mb"
        }`,
        requestId: req.id,
      });
    }

    // Handle unexpected file upload errors
    if (err.code === "LIMIT_UNEXPECTED_FILE") {
      return res.status(400).json({
        error: "Invalid file upload",
        message: "Unexpected file field",
        requestId: req.id,
      });
    }

    // Handle all other server errors
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
   * Validate file type and security
   */
  static validateFile(file) {
    if (!file) {
      throw new Error("No file provided");
    }

    const allowedExtensions = [
      "pdf",
      "docx",
      "xlsx",
      "pptx",
      "txt",
      "html",
      "csv",
      "xml",
      "jpg",
      "jpeg",
      "png",
      "gif",
      "bmp",
      "tif",
      "tiff",
      "svg",
      "psd",
      "mp3",
      "wav",
      "mp4",
      "mov",
      "avi",
      "zip",
    ];

    const dangerousExtensions = [
      "exe",
      "bat",
      "cmd",
      "com",
      "pif",
      "scr",
      "vbs",
      "js",
      "jar",
    ];

    const fileExt = path.extname(file.originalname).toLowerCase().slice(1);

    if (dangerousExtensions.includes(fileExt)) {
      throw new Error(
        `File type '${fileExt}' is not allowed for security reasons`
      );
    }

    if (!allowedExtensions.includes(fileExt)) {
      throw new Error(`File type '${fileExt}' is not supported`);
    }

    return true;
  }

  /**
   * Check system dependencies
   */
  static async checkSystemDependencies() {
    try {
      const dependencies = {
        puppeteer: false,
        sharp: false,
        nodeLibraries: true,
      };

      // Check Puppeteer
      try {
        const puppeteer = require("puppeteer");
        dependencies.puppeteer = true;
      } catch (error) {
        console.warn("Puppeteer not available:", error.message);
      }

      // Check Sharp
      try {
        const sharp = require("sharp");
        dependencies.sharp = true;
      } catch (error) {
        console.warn("Sharp not available:", error.message);
      }

      console.log(
        "Using pure Node.js libraries - no external dependencies required"
      );
      return dependencies;
    } catch (error) {
      console.error("Dependency check failed:", error.message);
      return { puppeteer: false, sharp: false, nodeLibraries: false };
    }
  }
}

module.exports = ErrorHandler;
