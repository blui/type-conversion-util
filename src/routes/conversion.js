/**
 * File Conversion Routes
 *
 * Handles all file conversion API endpoints including upload, validation,
 * conversion processing, and file download. Implements concurrency control
 * and comprehensive error handling for robust file processing.
 */

const express = require("express");
const multer = require("multer");
const path = require("path");
const fs = require("fs");
const { v4: uuidv4 } = require("uuid");
const FileType = require("file-type");
const Semaphore = require("../utils/semaphore");
const config = require("../config/config");

// Import conversion services
const documentService = require("../services/documentService");
const imageService = require("../services/imageService");
const audioVideoService = require("../services/audioVideoService");
const archiveService = require("../services/archiveService");

const router = express.Router();

// Initialize concurrency control semaphore
const semaphore = new Semaphore(
  config.concurrency.maxConcurrent,
  config.concurrency.maxQueue
);

/**
 * @swagger
 * /:
 *   get:
 *     tags:
 *       - API Info
 *     summary: API root endpoint
 *     description: Returns basic information about the File Conversion API
 *     operationId: getApiInfo
 *     responses:
 *       200:
 *         description: API information
 *         content:
 *           application/json:
 *             schema:
 *               type: object
 *               properties:
 *                 name:
 *                   type: string
 *                   example: "File Conversion Utility API"
 *                 version:
 *                   type: string
 *                   example: "1.0.0"
 *                 description:
 *                   type: string
 *                   example: "File conversion API using pure Node.js libraries"
 *                 documentation:
 *                   type: string
 *                   example: "/api-docs"
 *                 endpoints:
 *                   type: object
 *                   properties:
 *                     health:
 *                       type: string
 *                       example: "/api/health"
 *                     formats:
 *                       type: string
 *                       example: "/api/supported-formats"
 *                     convert:
 *                       type: string
 *                       example: "/api/convert"
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
    features: [
      "Zero external dependencies",
      "Enterprise security compliant",
      "Pure Node.js libraries",
      "OpenAPI 3.0 specification",
      "Swagger documentation",
    ],
  });
});

/**
 * Configure multer for file uploads with validation
 * Sets up storage, file filtering, and size limits
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

/**
 * File type filter for multer
 * Validates uploaded files against allowed MIME types
 */
const fileFilter = (req, file, cb) => {
  const allowedTypes = [
    // Documents
    "application/pdf",
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    "application/vnd.openxmlformats-officedocument.presentationml.presentation",
    "text/plain",
    "text/html",
    "text/csv",
    "application/xml",
    "text/xml",

    // Images
    "image/jpeg",
    "image/png",
    "image/gif",
    "image/bmp",
    "image/tiff",
    "image/svg+xml",
    "image/vnd.adobe.photoshop",

    // Audio
    "audio/mpeg",
    "audio/wav",

    // Video
    "video/mp4",
    "video/quicktime",
    "video/x-msvideo",

    // Archives
    "application/zip",
  ];

  if (allowedTypes.includes(file.mimetype)) {
    cb(null, true);
  } else {
    cb(new Error(`Unsupported file type: ${file.mimetype}`), false);
  }
};

// Configure multer upload middleware
const upload = multer({
  storage,
  fileFilter,
  limits: {
    fileSize: config.maxFileSize,
    files: 1,
  },
});

/**
 * GET /supported-formats
 * Returns comprehensive list of supported file formats and conversion mappings
 */
router.get("/supported-formats", (req, res) => {
  const formats = {
    documents: {
      input: ["pdf", "docx", "xlsx", "pptx", "txt", "html", "csv", "xml"],
      conversions: {
        docx: ["pdf"],
        pdf: ["docx", "txt"],
        xlsx: ["csv", "pdf"],
        csv: ["xlsx"],
        pptx: ["pdf"], // Limited support - creates placeholder
        txt: ["pdf", "html", "docx"],
        html: ["pdf", "docx"],
        xml: ["pdf", "html"],
      },
      notes: {
        pptx: "Limited support - creates simplified PDF",
        general: "All conversions use pure Node.js libraries",
      },
    },
    images: {
      input: ["jpg", "jpeg", "png", "gif", "bmp", "tif", "tiff", "svg", "psd"],
      conversions: {
        jpg: ["png", "gif", "bmp", "tiff"],
        jpeg: ["png", "gif", "bmp", "tiff"],
        png: ["jpg", "jpeg", "gif", "bmp", "tiff"],
        gif: ["png", "jpg", "jpeg"],
        bmp: ["png", "jpg", "jpeg"],
        tif: ["png", "jpg", "jpeg"],
        tiff: ["png", "jpg", "jpeg"],
        svg: ["png", "jpg", "jpeg"],
        psd: ["png", "jpg", "jpeg"],
      },
      notes: {
        general: "High-quality image processing using Sharp library",
      },
    },
    audio: {
      input: ["wav", "mp3"],
      conversions: {
        wav: ["mp3"],
        mp3: ["wav"], // Creates informational placeholder
      },
      notes: {
        mp3: "MP3 to WAV creates informational file - decoding requires additional libraries",
        wav: "WAV to MP3 fully supported using pure Node.js",
      },
    },
    video: {
      input: ["mp4", "mov", "avi"],
      conversions: {
        mp4: ["info"],
        mov: ["info"],
        avi: ["info"],
      },
      notes: {
        general:
          "Video conversion creates informational files - use cloud services for production video processing",
      },
    },
    archives: {
      input: ["zip"],
      conversions: {
        zip: ["extract"],
      },
      notes: {
        general: "Full ZIP extraction support using pure Node.js",
      },
    },
    system: {
      approach: "Pure Node.js libraries only",
      benefits: [
        "No external software dependencies",
        "Enterprise security friendly",
        "Easy deployment and scaling",
        "Consistent cross-platform behavior",
      ],
      limitations: [
        "Limited video processing (use cloud services)",
        "MP3 decoding requires additional libraries",
        "Complex PPTX layouts simplified",
      ],
    },
  };

  res.json(formats);
});

/**
 * POST /convert
 * Main file conversion endpoint with concurrency control and comprehensive error handling
 */
router.post("/convert", upload.single("file"), async (req, res) => {
  // Acquire semaphore slot or return 429 if queue is saturated
  let release;
  try {
    release = await semaphore.acquire();
  } catch (error) {
    if (error && error.message === "queue_saturated") {
      res.setHeader("Retry-After", "10");
      return res.status(429).json({
        error: "Too many requests",
        message: "Conversion capacity is saturated. Please retry shortly.",
      });
    }
    return res
      .status(500)
      .json({ error: "Server busy", message: "Unable to queue request" });
  }

  try {
    // Validate required parameters
    if (!req.file) {
      return res.status(400).json({
        error: "No file uploaded",
        message: "Please provide a file to convert",
      });
    }

    const { targetFormat } = req.body;
    if (!targetFormat) {
      return res.status(400).json({
        error: "Missing target format",
        message: "Please specify the target format for conversion",
      });
    }

    // Prepare file paths and validate content type
    const inputPath = req.file.path;
    const inputExtension = path
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

    // Perform content-based type detection for security
    try {
      const detected = await FileType.fromFile(inputPath);
      if (detected && detected.mime !== req.file.mimetype) {
        console.warn(
          `MIME mismatch: declared=${req.file.mimetype} detected=${detected.mime}`
        );
      }
    } catch (sniffError) {
      console.warn("File type sniffing failed:", sniffError.message);
    }

    // Route to appropriate conversion service based on file type
    let conversionResult;
    if (
      ["pdf", "docx", "xlsx", "pptx", "txt", "html", "csv", "xml"].includes(
        inputExtension
      )
    ) {
      conversionResult = await documentService.convert(
        inputPath,
        outputPath,
        inputExtension,
        targetFormat
      );
    } else if (
      [
        "jpg",
        "jpeg",
        "png",
        "gif",
        "bmp",
        "tif",
        "tiff",
        "svg",
        "psd",
      ].includes(inputExtension)
    ) {
      conversionResult = await imageService.convert(
        inputPath,
        outputPath,
        inputExtension,
        targetFormat
      );
    } else if (["mp3", "wav", "mp4", "mov", "avi"].includes(inputExtension)) {
      conversionResult = await audioVideoService.convert(
        inputPath,
        outputPath,
        inputExtension,
        targetFormat
      );
    } else if (["zip"].includes(inputExtension)) {
      conversionResult = await archiveService.convert(
        inputPath,
        outputPath,
        inputExtension,
        targetFormat
      );
    } else {
      throw new Error(`Unsupported input format: ${inputExtension}`);
    }

    // Validate conversion result
    if (!conversionResult.success) {
      throw new Error(conversionResult.error);
    }

    // Send the converted file as download
    res.download(conversionResult.outputPath, outputFileName, (err) => {
      // Clean up files after download with timer unref to prevent open handles
      const timer = setTimeout(() => {
        try {
          if (fs.existsSync(inputPath)) fs.unlinkSync(inputPath);
          if (fs.existsSync(conversionResult.outputPath))
            fs.unlinkSync(conversionResult.outputPath);
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

    // Clean up input file on error
    if (req.file && fs.existsSync(req.file.path)) {
      try {
        fs.unlinkSync(req.file.path);
      } catch (cleanupError) {
        console.error("Cleanup error:", cleanupError);
      }
    }

    // Return standardized error response with request ID
    const requestId = req.headers["x-request-id"] || uuidv4();
    res.setHeader("X-Request-Id", requestId);
    res.status(500).json({
      error: "Conversion failed",
      message: error.message,
      requestId,
    });
  } finally {
    // Always release the semaphore slot
    if (typeof release === "function") release();
  }
});

module.exports = router;
