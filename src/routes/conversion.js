/**
 * File Conversion Routes
 *
 * Handles file conversion API endpoints including upload, validation,
 * conversion processing, and file download
 */

const express = require("express");
const multer = require("multer");
const path = require("path");
const fs = require("fs");
const { v4: uuidv4 } = require("uuid");
const FileType = require("file-type");
const Semaphore = require("../utils/semaphore");
const config = require("../config/config");

const documentService = require("../services/documentService");
const imageService = require("../services/imageService");

const router = express.Router();

// Initialize concurrency control
const semaphore = new Semaphore(
  config.concurrency.maxConcurrent,
  config.concurrency.maxQueue
);

/**
 * API root endpoint
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
 */
const storage = multer.diskStorage({
  destination: (req, file, cb) => {
    cb(null, config.tempDir);
  },
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
 */
router.get("/supported-formats", (req, res) => {
  const supportedFormats = {
    documents: {
      input: ["docx", "pdf", "xlsx", "csv", "pptx", "txt", "html", "xml"],
      conversions: {
        docx: ["pdf", "txt", "html"],
        pdf: ["docx", "txt", "html"],
        xlsx: ["csv", "pdf"],
        csv: ["xlsx", "pdf"],
        pptx: ["pdf"],
        txt: ["pdf", "html", "docx"],
        html: ["pdf", "docx"],
        xml: ["pdf", "html"],
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
 */
router.post("/convert", upload.single("file"), async (req, res) => {
  // Validate file upload
  if (!req.file) {
    return res.status(400).json({
      error: "No file uploaded",
      message: "Please provide a file to convert",
      requestId: req.id,
    });
  }

  // Validate target format
  if (!req.body.targetFormat) {
    return res.status(400).json({
      error: "Missing target format",
      message: "Please specify the target format for conversion",
      requestId: req.id,
    });
  }

  // Validate file size
  if (req.file.size > config.maxFileSize) {
    return res.status(413).json({
      error: "File too large",
      message: `File size exceeds the limit of ${
        config.maxFileSize / 1024 / 1024
      }MB`,
      requestId: req.id,
    });
  }

  // Validate file type using content-based detection (warning only)
  try {
    const fileType = await FileType.fromFile(req.file.path);
    if (!fileType) {
      console.warn(
        "Could not determine file type from content, proceeding with extension-based detection"
      );
    }
  } catch (error) {
    console.warn("File type detection failed:", error.message);
  }

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

  // Set request timeout
  const timeout =
    config.timeouts[getTimeoutCategory(inputFormat)] ||
    config.timeouts.document;
  const timeoutId = setTimeout(() => {
    if (!res.headersSent) {
      res.status(408).json({
        error: "Conversion timeout",
        message: `Conversion took longer than ${timeout / 1000} seconds`,
        requestId: req.id,
      });
    }
  }, timeout);

  try {
    // Acquire semaphore for concurrency control
    await semaphore.acquire();

    // Determine conversion service based on file type
    let result;
    if (isDocumentFormat(inputFormat)) {
      result = await documentService.convert(
        inputPath,
        outputPath,
        inputFormat,
        targetFormat
      );
    } else if (isImageFormat(inputFormat)) {
      result = await imageService.convert(
        inputPath,
        outputPath,
        inputFormat,
        targetFormat
      );
    } else {
      throw new Error(`Unsupported input format: ${inputFormat}`);
    }

    // Clear timeout since conversion completed
    clearTimeout(timeoutId);

    if (!result.success) {
      throw new Error(result.error);
    }

    // Send converted file as download
    res.download(result.outputPath, outputFileName, (err) => {
      // Clean up files after download
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
    // Clear timeout since error occurred
    clearTimeout(timeoutId);

    console.error("Conversion error:", error);

    // Clean up input file on error
    if (req.file && fs.existsSync(req.file.path)) {
      try {
        fs.unlinkSync(req.file.path);
      } catch (cleanupError) {
        console.error("Failed to cleanup input file:", cleanupError);
      }
    }

    res.status(500).json({
      error: "Conversion failed",
      message: error.message,
      requestId: req.id,
    });
  } finally {
    // Release semaphore
    semaphore.release();
  }
});

/**
 * Helper function to determine timeout category based on file format
 */
function getTimeoutCategory(format) {
  if (isDocumentFormat(format)) return "document";
  if (isImageFormat(format)) return "image";
  return "document"; // default
}

/**
 * Helper functions to determine file type categories
 */
function isDocumentFormat(format) {
  return ["pdf", "docx", "xlsx", "pptx", "txt", "html", "csv", "xml"].includes(
    format
  );
}

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
