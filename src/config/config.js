/**
 * Application Configuration
 *
 * Centralized configuration for the file conversion utility
 * Provides environment-specific settings with sensible defaults
 */

const config = {
  // Server Configuration
  port: process.env.PORT || 3000,
  host:
    process.env.HOST ||
    (process.env.NODE_ENV === "production" ? "0.0.0.0" : "localhost"),
  nodeEnv: process.env.NODE_ENV || "development",

  // Environment Detection
  isProduction: process.env.NODE_ENV === "production",
  isDevelopment: process.env.NODE_ENV === "development",
  isIntranet:
    process.env.INTRANET === "true" || process.env.NODE_ENV === "production",
  isServerless: process.env.VERCEL || process.env.AWS_LAMBDA_FUNCTION_NAME,

  // File Handling
  uploadLimit: process.env.UPLOAD_LIMIT || "50mb",
  maxFileSize: parseInt(process.env.MAX_FILE_SIZE) || 50 * 1024 * 1024,
  tempDir:
    process.env.TEMP_DIR ||
    (process.env.NODE_ENV === "production" ? "/tmp" : "./temp"),

  // Rate Limiting
  rateLimit: {
    windowMs: parseInt(process.env.RATE_LIMIT_WINDOW_MS) || 15 * 60 * 1000,
    max: parseInt(process.env.RATE_LIMIT_MAX_REQUESTS) || 100,
    message: "Too many requests from this IP, please try again later.",
  },

  // CORS Configuration
  cors: {
    origin:
      process.env.CORS_ORIGIN ||
      (process.env.INTRANET === "true" || process.env.NODE_ENV === "production"
        ? true
        : process.env.NODE_ENV === "development"
        ? true
        : false),
    credentials: true,
    methods: ["GET", "POST", "PUT", "DELETE", "OPTIONS"],
    allowedHeaders: ["Content-Type", "Authorization", "X-Requested-With"],
    maxAge: 86400,
  },

  // Security Headers
  helmet: {
    contentSecurityPolicy: {
      directives: {
        defaultSrc: ["'self'"],
        styleSrc: [
          "'self'",
          "'unsafe-inline'",
          "https://cdn.jsdelivr.net",
          "https://fonts.googleapis.com",
        ],
        scriptSrc: ["'self'", "'unsafe-inline'", "https://cdn.jsdelivr.net"],
        imgSrc: ["'self'", "data:", "blob:", "https://validator.swagger.io"],
        fontSrc: [
          "'self'",
          "data:",
          "https://fonts.gstatic.com",
          "https://cdn.jsdelivr.net",
        ],
        connectSrc: ["'self'", "ws:", "wss:"],
      },
    },
    hsts:
      process.env.NODE_ENV === "production"
        ? { maxAge: 31536000, includeSubDomains: true }
        : false,
    noSniff: true,
    referrerPolicy: { policy: "strict-origin-when-cross-origin" },
  },

  // Logging
  logging: {
    level: process.env.LOG_LEVEL || "info",
    format: process.env.NODE_ENV === "production" ? "combined" : "dev",
  },

  // Concurrency Control
  concurrency: {
    maxConcurrent: parseInt(process.env.MAX_CONCURRENCY) || 2,
    maxQueue: parseInt(process.env.MAX_QUEUE) || 10,
  },

  // Conversion Timeouts (in milliseconds)
  timeouts: {
    document: parseInt(process.env.DOCUMENT_TIMEOUT) || 60000,
    image: parseInt(process.env.IMAGE_TIMEOUT) || 30000,
  },

  // Quality Settings
  quality: {
    image: {
      jpeg: parseInt(process.env.IMAGE_JPEG_QUALITY) || 90,
      png: parseInt(process.env.IMAGE_PNG_COMPRESSION) || 6,
      tiff: process.env.IMAGE_TIFF_COMPRESSION || "lzw",
    },
  },

  // Network Configuration
  network: {
    trustProxy:
      process.env.NODE_ENV === "production" || process.env.INTRANET === "true",
    keepAlive: true,
    keepAliveTimeout: 65000,
    connectionTimeout: 30000,
  },

  // Health Check Configuration
  health: {
    path: "/health",
    detailedPath: "/health/detailed",
    puppeteerPath: "/health/puppeteer",
    timeout: 5000,
    includeSystemInfo:
      process.env.INTRANET === "true" || process.env.NODE_ENV === "production",
  },
};

module.exports = config;
