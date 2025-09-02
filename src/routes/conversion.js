/**
 * File Conversion Routes
 *
 * Handles file conversion API endpoints with comprehensive validation and processing.
 * Provides RESTful API for document and image conversions with enhanced accuracy.
 *
 * Endpoints:
 * - GET /api - API information and discovery
 * - GET /api/supported-formats - List of supported file formats and conversions
 * - POST /api/convert - File conversion endpoint
 *
 * Features:
 * - File upload handling with validation
 * - Format detection and validation
 * - Concurrency control with semaphore
 * - Comprehensive error handling
 * - File cleanup and resource management
 * - Rate limiting and security
 */

// Express and file handling imports
const express = require("express");
const multer = require("multer");
const path = require("path");
const fs = require("fs");

// Utility libraries
const { v4: uuidv4 } = require("uuid");

// Application modules
const Semaphore = require("../utils/semaphore");
const config = require("../config/config");

// Conversion service imports
const documentService = require("../services/documentService");
const imageService = require("../services/imageService");

// Initialize Express router
const router = express.Router();

// Initialize concurrency control for managing simultaneous conversions
const semaphore = new Semaphore(
  config.concurrency.maxConcurrent,
  config.concurrency.maxQueue
);

/**
 * API root endpoint
 * Provides API information, version, and available endpoints
 * Used for API discovery and documentation
 *
 * @param {Object} req - Express request object
 * @param {Object} res - Express response object
 */
router.get("/", (req, res) => {
  res.json({
    name: "File Conversion Utility API",
    version: require("../../package.json").version,
    description: "File conversion API using pure Node.js libraries",
    documentation: "/api-docs",
    endpoints: {
      health: "/api/health",
      formats: "/api/supported-formats",
      convert: "/api/convert",
    },
  });
});

/**
 * Configure multer for file uploads
 * Sets up file storage, size limits, and format validation
 * Ensures secure and controlled file uploads
 */
const storage = multer.diskStorage({
  // Set destination directory for uploaded files
  destination: (req, file, cb) => {
    cb(null, config.tempDir);
  },
  // Generate unique filename to prevent conflicts
  filename: (req, file, cb) => {
    const uniqueName = `${uuidv4()}-${file.originalname}`;
    cb(null, uniqueName);
  },
});

const upload = multer({
  storage,
  limits: {
    fileSize: config.maxFileSize,
  },
  // Validate file format and extension
  fileFilter: (req, file, cb) => {
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
    ];

    const fileExt = path.extname(file.originalname).toLowerCase().slice(1);
    if (allowedExtensions.includes(fileExt)) {
      cb(null, true);
    } else {
      cb(new Error(`File type '${fileExt}' is not supported`));
    }
  },
});

/**
 * Get supported file conversion formats
 * Returns comprehensive list of supported input formats and their conversion options
 * Used by frontend applications to display available conversion choices
 *
 * @param {Object} req - Express request object
 * @param {Object} res - Express response object
 */
router.get("/supported-formats", (req, res) => {
  const supportedFormats = {
    documents: {
      input: ["docx", "pdf", "xlsx", "csv", "pptx", "txt", "xml"],
      conversions: {
        docx: ["pdf", "txt"],
        pdf: ["docx", "txt"],
        xlsx: ["csv", "pdf"],
        csv: ["xlsx", "pdf"],
        pptx: ["pdf"],
        txt: ["pdf", "docx"],
        xml: ["pdf"],
      },
    },
    images: {
      input: ["jpg", "jpeg", "png", "gif", "bmp", "tif", "tiff", "svg", "psd"],
      conversions: {
        jpg: ["png", "gif", "bmp", "tiff"],
        jpeg: ["png", "gif", "bmp", "tiff"],
        png: ["jpg", "gif", "bmp", "tiff"],
        gif: ["png", "jpg", "bmp", "tiff"],
        bmp: ["png", "jpg", "gif", "tiff"],
        tif: ["jpg", "png", "gif", "bmp"],
        tiff: ["jpg", "png", "gif", "bmp"],
        svg: ["png", "jpg", "gif", "bmp", "tiff"],
        psd: ["png", "jpg", "gif", "bmp", "tiff"],
      },
    },
  };

  res.json(supportedFormats);
});

/**
 * Convert file endpoint
 * Main file conversion endpoint with comprehensive validation and processing
 * Handles file upload, format validation, conversion, and file delivery
 *
 * @param {Object} req - Express request object with uploaded file
 * @param {Object} res - Express response object
 */
router.post("/convert", upload.single("file"), async (req, res) => {
  // Validate file upload - ensure file was provided
  if (!req.file) {
    return res.status(400).json({
      error: "No file uploaded",
      message: "Please provide a file to convert",
      requestId: req.id,
    });
  }

  // Validate target format - ensure conversion target is specified
  if (!req.body.targetFormat) {
    return res.status(400).json({
      error: "Missing target format",
      message: "Please specify the target format for conversion",
      requestId: req.id,
    });
  }

  // Extract file information and prepare for conversion
  const inputPath = req.file.path;
  const targetFormat = req.body.targetFormat.toLowerCase();
  const inputFormat = path
    .extname(req.file.originalname)
    .toLowerCase()
    .slice(1);
  const outputFileName = `${
    path.parse(req.file.originalname).name
  }.${targetFormat}`;
  const outputPath = path.join(
    path.dirname(inputPath),
    `converted-${uuidv4()}-${outputFileName}`
  );

  // Set request timeout based on file type and conversion complexity
  const timeout =
    config.timeouts[getTimeoutCategory(inputFormat)] ||
    config.timeouts.document;

  // Create a promise that rejects after timeout
  const timeoutPromise = new Promise((_, reject) => {
    setTimeout(() => {
      reject(
        new Error(
          `Conversion timeout: took longer than ${timeout / 1000} seconds`
        )
      );
    }, timeout);
  });

  try {
    // Acquire semaphore for concurrency control to prevent resource overload
    await semaphore.acquire();

    // Race between conversion and timeout
    const result = await Promise.race([
      (async () => {
        // Determine appropriate conversion service based on file type
        if (isDocumentFormat(inputFormat)) {
          return await documentService.convert(
            inputPath,
            outputPath,
            inputFormat,
            targetFormat
          );
        } else if (isImageFormat(inputFormat)) {
          return await imageService.convert(
            inputPath,
            outputPath,
            inputFormat,
            targetFormat
          );
        } else {
          throw new Error(`Unsupported input format: ${inputFormat}`);
        }
      })(),
      timeoutPromise,
    ]);

    if (!result.success) {
      throw new Error(result.error);
    }

    // Send converted file as download with cleanup handling
    res.download(result.outputPath, outputFileName, (err) => {
      // Clean up temporary files after download completion
      const timer = setTimeout(() => {
        try {
          if (fs.existsSync(inputPath)) fs.unlinkSync(inputPath);
          if (fs.existsSync(result.outputPath))
            fs.unlinkSync(result.outputPath);
        } catch (cleanupError) {
          console.error("Cleanup error:", cleanupError);
        }
      }, 1000);
      if (typeof timer.unref === "function") timer.unref();

      if (err) {
        console.error("Download error:", err);
        if (!res.headersSent) {
          res.status(500).json({
            error: "Download failed",
            message: "Failed to send converted file",
          });
        }
      }
    });
  } catch (error) {
    console.error("Conversion error:", error);

    // Clean up input file on error to prevent disk space issues
    if (req.file && fs.existsSync(req.file.path)) {
      try {
        fs.unlinkSync(req.file.path);
      } catch (cleanupError) {
        console.error("Failed to cleanup input file:", cleanupError);
      }
    }

    // Handle timeout errors specifically
    if (error.message.includes("Conversion timeout")) {
      res.status(408).json({
        error: "Conversion timeout",
        message: error.message,
        requestId: req.id,
      });
    } else {
      res.status(500).json({
        error: "Conversion failed",
        message: error.message,
        requestId: req.id,
      });
    }
  } finally {
    // Release semaphore to allow other conversions to proceed
    semaphore.release();
  }
});

/**
 * Helper function to determine timeout category based on file format
 * Returns appropriate timeout configuration for different file types
 *
 * @param {string} format - File format extension
 * @returns {string} Timeout category (document, image, or default)
 */
function getTimeoutCategory(format) {
  if (isDocumentFormat(format)) return "document";
  if (isImageFormat(format)) return "image";
  return "document"; // default
}

/**
 * Helper functions to determine file type categories
 * Classify file formats for appropriate service routing
 */
/**
 * Check if file format is a document type
 *
 * @param {string} format - File format extension
 * @returns {boolean} True if document format, false otherwise
 */
function isDocumentFormat(format) {
  return ["pdf", "docx", "xlsx", "pptx", "txt", "csv", "xml"].includes(format);
}

/**
 * Check if file format is an image type
 *
 * @param {string} format - File format extension
 * @returns {boolean} True if image format, false otherwise
 */
function isImageFormat(format) {
  return [
    "jpg",
    "jpeg",
    "png",
    "gif",
    "bmp",
    "tif",
    "tiff",
    "svg",
    "psd",
  ].includes(format);
}

module.exports = router;
