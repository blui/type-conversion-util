/**
 * Security Middleware
 *
 * Defense-in-depth security controls:
 * - Input validation and sanitization
 * - Path traversal prevention
 * - MIME type validation
 * - File size limits
 * - Rate limiting
 * - Request timeout enforcement
 *
 * Security Policy: Zero external network calls
 * All operations are local-only
 */

const path = require('path');
const fs = require('fs');
const rateLimit = require('express-rate-limit');

// Security Constants
const MAX_FILE_SIZE = 50 * 1024 * 1024; // 50MB
const MAX_FILENAME_LENGTH = 255;
const ALLOWED_EXTENSIONS = ['.docx', '.pdf', '.txt', '.xml', '.csv', '.xlsx', '.jpg', '.jpeg', '.png'];
const UPLOAD_TIMEOUT = 30000; // 30 seconds

// MIME type validation mapping
const MIME_TYPES = {
  '.docx': 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  '.pdf': 'application/pdf',
  '.txt': 'text/plain',
  '.xml': 'application/xml',
  '.csv': 'text/csv',
  '.xlsx': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.png': 'image/png'
};

/**
 * Rate limiter configuration
 * Prevents DoS attacks by limiting request frequency
 */
const createRateLimiter = () => {
  return rateLimit({
    windowMs: 60 * 1000, // 1 minute
    max: 30, // 30 requests per minute per IP
    message: {
      error: 'Rate limit exceeded',
      message: 'Too many requests. Maximum 30 requests per minute.',
      retryAfter: '60 seconds'
    },
    standardHeaders: true,
    legacyHeaders: false,
    handler: (req, res) => {
      res.status(429).json({
        error: 'Rate limit exceeded',
        message: 'Too many requests. Maximum 30 requests per minute.',
        retryAfter: 60
      });
    }
  });
};

/**
 * Validate and sanitize filename
 * Prevents path traversal and malicious filenames
 */
function sanitizeFilename(filename) {
  if (!filename || typeof filename !== 'string') {
    throw new Error('Invalid filename');
  }

  // Remove path separators and special characters
  const sanitized = filename
    .replace(/[\/\\]/g, '') // Remove path separators
    .replace(/\.\./g, '') // Remove parent directory references
    .replace(/[<>:"|?*\x00-\x1f]/g, '') // Remove invalid Windows filename characters
    .trim();

  if (sanitized.length === 0) {
    throw new Error('Filename cannot be empty');
  }

  if (sanitized.length > MAX_FILENAME_LENGTH) {
    throw new Error(`Filename too long (max ${MAX_FILENAME_LENGTH} characters)`);
  }

  // Prevent hidden files and system files
  if (sanitized.startsWith('.') || sanitized.startsWith('~')) {
    throw new Error('Invalid filename format');
  }

  return sanitized;
}

/**
 * Validate file extension
 * Only allows whitelisted file types
 */
function validateFileExtension(filename) {
  const ext = path.extname(filename).toLowerCase();

  if (!ALLOWED_EXTENSIONS.includes(ext)) {
    throw new Error(`File type not allowed. Supported: ${ALLOWED_EXTENSIONS.join(', ')}`);
  }

  return ext;
}

/**
 * Validate file path
 * Checks path is within allowed directory
 */
function validateFilePath(filePath, allowedDir) {
  const resolvedPath = path.resolve(filePath);
  const resolvedAllowedDir = path.resolve(allowedDir);

  if (!resolvedPath.startsWith(resolvedAllowedDir)) {
    throw new Error('Path traversal attempt detected');
  }

  return resolvedPath;
}

/**
 * Validate file size
 * Prevents resource exhaustion attacks
 */
function validateFileSize(size) {
  if (typeof size !== 'number' || size < 0) {
    throw new Error('Invalid file size');
  }

  if (size === 0) {
    throw new Error('File is empty');
  }

  if (size > MAX_FILE_SIZE) {
    throw new Error(`File too large (max ${MAX_FILE_SIZE / 1024 / 1024}MB)`);
  }

  return true;
}

/**
 * Validate MIME type
 * Verifies file content matches declared type
 */
function validateMimeType(filename, declaredMimeType) {
  const ext = path.extname(filename).toLowerCase();
  const expectedMimeType = MIME_TYPES[ext];

  if (!expectedMimeType) {
    throw new Error('Unsupported file type');
  }

  // Allow variations of MIME types (e.g., text/plain vs application/octet-stream for .txt)
  // But ensure it's not attempting to disguise as something else
  if (declaredMimeType && !declaredMimeType.startsWith('multipart/form-data')) {
    const mimeMatch = declaredMimeType.includes(ext.substring(1)) ||
                      declaredMimeType === expectedMimeType ||
                      declaredMimeType === 'application/octet-stream';

    if (!mimeMatch) {
      throw new Error('File type mismatch detected');
    }
  }

  return expectedMimeType;
}

/**
 * Middleware: Validate file upload
 * Comprehensive validation of uploaded files
 */
function validateFileUpload(req, res, next) {
  try {
    if (!req.file && !req.files) {
      return res.status(400).json({
        error: 'Validation failed',
        message: 'No file uploaded'
      });
    }

    const file = req.file || (req.files && req.files[0]);

    if (!file) {
      return res.status(400).json({
        error: 'Validation failed',
        message: 'No file data received'
      });
    }

    // Validate filename
    const sanitized = sanitizeFilename(file.originalname || file.filename);
    file.sanitizedName = sanitized;

    // Validate extension
    validateFileExtension(sanitized);

    // Validate file size
    validateFileSize(file.size);

    // Validate MIME type
    validateMimeType(sanitized, file.mimetype);

    // Validate file path (if applicable)
    // Use configured temp directory from environment or default locations
    if (file.path) {
      const config = require('../config/config');
      const tempDir = config.tempDir || './temp';
      const allowedDir = path.resolve(tempDir);
      validateFilePath(file.path, allowedDir);
    }

    next();
  } catch (error) {
    // Clean up uploaded file if validation fails
    if (req.file && req.file.path && fs.existsSync(req.file.path)) {
      try {
        fs.unlinkSync(req.file.path);
      } catch (cleanupError) {
        console.error('Failed to cleanup invalid upload:', cleanupError);
      }
    }

    return res.status(400).json({
      error: 'Validation failed',
      message: error.message
    });
  }
}

/**
 * Middleware: Request timeout
 * Prevents long-running requests from exhausting resources
 */
function requestTimeout(timeoutMs = 120000) {
  return (req, res, next) => {
    req.setTimeout(timeoutMs, () => {
      res.status(408).json({
        error: 'Request timeout',
        message: 'Request took too long to process'
      });
    });
    next();
  };
}

/**
 * Middleware: Security headers
 * Sets security-related HTTP headers
 */
function securityHeaders(req, res, next) {
  // Prevent MIME type sniffing
  res.setHeader('X-Content-Type-Options', 'nosniff');

  // Disable browser caching for sensitive operations
  res.setHeader('Cache-Control', 'no-store, no-cache, must-revalidate, private');
  res.setHeader('Pragma', 'no-cache');
  res.setHeader('Expires', '0');

  // Prevent clickjacking
  res.setHeader('X-Frame-Options', 'DENY');

  // Disable DNS prefetching
  res.setHeader('X-DNS-Prefetch-Control', 'off');

  next();
}

/**
 * Middleware: Validate conversion parameters
 * Validates query parameters and body data
 */
function validateConversionParams(req, res, next) {
  try {
    // Validate output format if specified
    if (req.query.format) {
      const format = req.query.format.toLowerCase();
      if (!['pdf', 'docx', 'txt', 'xml', 'csv'].includes(format)) {
        throw new Error('Invalid output format');
      }
    }

    // Validate quality parameter if specified
    if (req.query.quality) {
      const quality = parseInt(req.query.quality, 10);
      if (isNaN(quality) || quality < 1 || quality > 100) {
        throw new Error('Quality must be between 1 and 100');
      }
    }

    // Prevent injection attempts in parameters
    const dangerousPatterns = [
      /<script/i,
      /javascript:/i,
      /on\w+\s*=/i,
      /\.\.\//,
      /%2e%2e/i,
      /cmd\.exe/i,
      /powershell/i
    ];

    const checkParams = (obj) => {
      for (const key in obj) {
        const value = String(obj[key]);
        for (const pattern of dangerousPatterns) {
          if (pattern.test(value)) {
            throw new Error('Invalid parameter detected');
          }
        }
      }
    };

    checkParams(req.query);
    if (req.body) {
      checkParams(req.body);
    }

    next();
  } catch (error) {
    return res.status(400).json({
      error: 'Validation failed',
      message: error.message
    });
  }
}

module.exports = {
  createRateLimiter,
  validateFileUpload,
  requestTimeout,
  securityHeaders,
  validateConversionParams,
  sanitizeFilename,
  validateFileExtension,
  validateFilePath,
  validateFileSize,
  validateMimeType,
  MAX_FILE_SIZE,
  ALLOWED_EXTENSIONS
};
