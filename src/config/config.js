/**
 * Application Configuration
 *
 * Simplified configuration for the file conversion utility
 * Focused on essential settings for document and image conversion
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
    origin: true, // Allow all origins for simplicity
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
        imgSrc: ["'self'", "data:", "blob:"],
        fontSrc: [
          "'self'",
          "data:",
          "https://fonts.gstatic.com",
          "https://cdn.jsdelivr.net",
        ],
        connectSrc: ["'self'"],
      },
    },
    hsts: false, // Disabled for development
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

  // Network Configuration
  network: {
    trustProxy: process.env.NODE_ENV === "production",
    keepAlive: true,
    keepAliveTimeout: 65000,
  },

  // Health Check Configuration
  health: {
    path: "/health",
    detailedPath: "/health/detailed",
    puppeteerPath: "/health/puppeteer",
    timeout: 5000,
    includeSystemInfo: process.env.NODE_ENV === "production",
  },
};

module.exports = config;
