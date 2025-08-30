/**
 * Application Configuration
 *
 * Centralized configuration object that manages all application settings
 * including server configuration, file handling, security, and conversion parameters.
 * Uses environment variables with sensible defaults for flexibility across environments.
 */

const path = require("path");

const config = {
  /**
   * Server Configuration
   * Basic server settings including port and environment
   */
  port: process.env.PORT || 3000,
  nodeEnv: process.env.NODE_ENV || "development",

  /**
   * File Upload Configuration
   * Settings for handling file uploads including size limits and temporary storage
   * Uses /tmp for serverless compatibility (Vercel, AWS Lambda, etc.)
   */
  uploadLimit: process.env.UPLOAD_LIMIT || "50mb",
  maxFileSize: parseInt(process.env.MAX_FILE_SIZE) || 50 * 1024 * 1024, // 50MB in bytes
  tempDir:
    process.env.TEMP_DIR ||
    (process.env.NODE_ENV === "production" ? "/tmp" : "./temp"),

  /**
   * Supported MIME Types
   * Maps MIME types to file extensions for upload validation
   * Organized by category for better maintainability
   */
  supportedMimeTypes: {
    // Document formats
    "application/pdf": "pdf",
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document":
      "docx",
    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": "xlsx",
    "application/vnd.openxmlformats-officedocument.presentationml.presentation":
      "pptx",
    "text/plain": "txt",
    "text/html": "html",
    "text/csv": "csv",
    "application/xml": "xml",
    "text/xml": "xml",

    // Image formats
    "image/jpeg": "jpg",
    "image/png": "png",
    "image/gif": "gif",
    "image/bmp": "bmp",
    "image/tiff": "tiff",
    "image/svg+xml": "svg",
    "image/vnd.adobe.photoshop": "psd",

    // Audio formats
    "audio/mpeg": "mp3",
    "audio/wav": "wav",

    // Video formats (informational support only)
    "video/mp4": "mp4",
    "video/quicktime": "mov",
    "video/x-msvideo": "avi",

    // Archive formats
    "application/zip": "zip",
  },

  /**
   * File Conversion Mappings
   * Defines which file formats can be converted to which target formats
   * Organized by category for easy maintenance and validation
   */
  conversionMappings: {
    documents: {
      docx: ["pdf"],
      pdf: ["docx", "txt"],
      xlsx: ["csv", "pdf"],
      csv: ["xlsx"],
      pptx: ["pdf"],
      txt: ["pdf", "html", "docx"],
      html: ["pdf", "docx"],
      xml: ["pdf", "html"],
    },
    images: {
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
    audio: {
      wav: ["mp3"],
      mp3: ["wav"], // Creates informational file
    },
    video: {
      mp4: ["mov", "avi"], // Informational support only
      mov: ["mp4", "avi"], // Informational support only
      avi: ["mp4", "mov"], // Informational support only
    },
    archives: {
      zip: ["extract"],
    },
  },

  /**
   * Rate Limiting Configuration
   * Prevents API abuse by limiting requests per IP address
   */
  rateLimit: {
    windowMs: 15 * 60 * 1000, // 15 minutes in milliseconds
    max: 100, // Maximum requests per window per IP
    message: "Too many requests from this IP, please try again later.",
  },

  /**
   * Cross-Origin Resource Sharing (CORS) Configuration
   * Controls which origins can access the API
   */
  cors: {
    origin: process.env.NODE_ENV === "production" ? false : true,
    credentials: true,
  },

  /**
   * Security Headers Configuration
   * Content Security Policy and other security headers via Helmet
   */
  helmet: {
    contentSecurityPolicy: {
      directives: {
        defaultSrc: ["'self'"],
        styleSrc: ["'self'", "'unsafe-inline'", "https://cdn.jsdelivr.net"],
        scriptSrc: ["'self'", "https://cdn.jsdelivr.net"],
        imgSrc: ["'self'", "data:", "blob:"],
      },
    },
  },

  /**
   * Logging Configuration
   * Controls log level and format based on environment
   */
  logging: {
    level: process.env.LOG_LEVEL || "info",
    format: process.env.NODE_ENV === "production" ? "combined" : "dev",
  },

  /**
   * Conversion Timeout Settings
   * Maximum time allowed for different types of conversions (in milliseconds)
   */
  timeouts: {
    document: 60000, // 1 minute
    image: 30000, // 30 seconds
    audio: 120000, // 2 minutes
    video: 300000, // 5 minutes (informational only)
    archive: 60000, // 1 minute
  },

  /**
   * Quality Settings for Conversions
   * Default quality parameters for various output formats
   */
  quality: {
    image: {
      jpeg: 90, // JPEG quality (0-100)
      png: 6, // PNG compression level (0-9)
      tiff: "lzw", // TIFF compression type
    },
    audio: {
      mp3: "192k", // MP3 bitrate
      aac: "128k", // AAC bitrate
    },
    video: {
      bitrate: "1000k", // Video bitrate (informational)
      audioBitrate: "128k", // Audio bitrate (informational)
    },
  },
};

// Export the configuration object for use throughout the application
module.exports = config;
