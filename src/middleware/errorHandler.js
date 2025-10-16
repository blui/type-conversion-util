/**
 * Error Handler Middleware
 *
 * Comprehensive error handling and fault tolerance for the file conversion application.
 * Provides structured error logging, categorization, and fault tolerance.
 *
 * Features:
 * - Structured error logging with telemetry data
 * - Error categorization (Critical, Warning, Info)
 * - Fault tolerance and recovery strategies
 * - System dependency verification with fallback handling
 * - Consistent error response formatting
 * - Performance monitoring and metrics
 * - Security-focused error messages
 * - Graceful degradation capabilities
 */

// Node.js built-in modules for file system operations
const fs = require("fs");
const path = require("path");

class ErrorHandler {
  // Error severity levels
  static ERROR_LEVELS = {
    CRITICAL: "CRITICAL", // System failure, data loss, security breach
    WARNING: "WARNING", // Degraded functionality, recoverable errors
    INFO: "INFO", // Normal operation, informational messages
    DEBUG: "DEBUG", // Development debugging information
  };

  // Error categories for classification and monitoring
  static ERROR_CATEGORIES = {
    SYSTEM: "SYSTEM", // OS, filesystem, network issues
    CONVERSION: "CONVERSION", // Document conversion failures
    SECURITY: "SECURITY", // Authentication, authorization failures
    VALIDATION: "VALIDATION", // Input validation errors
    DEPENDENCY: "DEPENDENCY", // Missing libraries, services
    PERFORMANCE: "PERFORMANCE", // Resource limits, timeouts
    CONFIGURATION: "CONFIGURATION", // Configuration errors
  };

  // Error metrics for monitoring
  static metrics = {
    totalErrors: 0,
    errorsByCategory: {},
    errorsByLevel: {},
    recentErrors: [],
  };

  /**
   * Log error information with detailed telemetry
   * Creates detailed error logs with telemetry data, categorization, and recovery suggestions
   *
   * @param {Error} error - The error object to log
   * @param {Object} req - Express request object (optional)
   * @param {string} category - Error category from ERROR_CATEGORIES
   * @param {string} level - Error level from ERROR_LEVELS
   * @param {Object} context - Additional context information
   */
  static logError(
    error,
    req = null,
    category = "SYSTEM",
    level = "WARNING",
    context = {}
  ) {
    const timestamp = new Date().toISOString();
    const errorId = this.generateErrorId();

    // Update error metrics
    this.metrics.totalErrors++;
    this.metrics.errorsByCategory[category] =
      (this.metrics.errorsByCategory[category] || 0) + 1;
    this.metrics.errorsByLevel[level] =
      (this.metrics.errorsByLevel[level] || 0) + 1;

    // Keep recent errors for monitoring (last 100)
    this.metrics.recentErrors.unshift({
      id: errorId,
      timestamp,
      category,
      level,
      message: error.message,
    });
    if (this.metrics.recentErrors.length > 100) {
      this.metrics.recentErrors.pop();
    }

    const logEntry = {
      errorId,
      timestamp,
      level,
      category,
      error: {
        message: error.message,
        stack: error.stack,
        code: error.code,
        name: error.name,
      },
      request: req
        ? {
            id: req.id,
            method: req.method,
            url: req.url,
            ip: req.ip,
            userAgent: req.headers["user-agent"],
            file: req.file
              ? {
                  originalname: req.file.originalname,
                  mimetype: req.file.mimetype,
                  size: req.file.size,
                }
              : null,
          }
        : null,
      context,
      recovery: this.getRecoveryStrategy(error, category),
      telemetry: {
        uptime: process.uptime(),
        memoryUsage: process.memoryUsage(),
        cpuUsage: process.cpuUsage(),
      },
    };

    // Log based on severity level
    switch (level) {
      case this.ERROR_LEVELS.CRITICAL:
        console.error("CRITICAL ERROR:", JSON.stringify(logEntry, null, 2));
        break;
      case this.ERROR_LEVELS.WARNING:
        console.warn("WARNING:", JSON.stringify(logEntry, null, 2));
        break;
      case this.ERROR_LEVELS.INFO:
        console.info("INFO:", JSON.stringify(logEntry, null, 2));
        break;
      case this.ERROR_LEVELS.DEBUG:
        if (process.env.LOG_LEVEL === "debug") {
          console.debug("DEBUG:", JSON.stringify(logEntry, null, 2));
        }
        break;
    }

    return errorId;
  }

  /**
   * Generate unique error ID for tracking
   * @returns {string} Unique error identifier
   */
  static generateErrorId() {
    return `ERR-${Date.now()}-${Math.random().toString(36).slice(2, 11)}`;
  }

  /**
   * Get recovery strategy based on error type and category
   * @param {Error} error - The error object
   * @param {string} category - Error category
   * @returns {Object} Recovery strategy information
   */
  static getRecoveryStrategy(error, category) {
    const strategies = {
      [this.ERROR_CATEGORIES.SYSTEM]: {
        action: "RETRY",
        delay: 5000,
        maxRetries: 3,
        description: "System error - retry with exponential backoff",
      },
      [this.ERROR_CATEGORIES.CONVERSION]: {
        action: "FAILOVER",
        delay: 0,
        maxRetries: 0,
        description: "Conversion failed - no automatic recovery available",
      },
      [this.ERROR_CATEGORIES.SECURITY]: {
        action: "BLOCK",
        delay: 0,
        maxRetries: 0,
        description: "Security violation - request blocked",
      },
      [this.ERROR_CATEGORIES.VALIDATION]: {
        action: "REJECT",
        delay: 0,
        maxRetries: 0,
        description: "Validation failed - request rejected",
      },
      [this.ERROR_CATEGORIES.DEPENDENCY]: {
        action: "DEGRADE",
        delay: 0,
        maxRetries: 0,
        description: "Dependency unavailable - graceful degradation",
      },
      [this.ERROR_CATEGORIES.PERFORMANCE]: {
        action: "THROTTLE",
        delay: 1000,
        maxRetries: 1,
        description: "Performance limit exceeded - throttle request",
      },
      [this.ERROR_CATEGORIES.CONFIGURATION]: {
        action: "RESTART",
        delay: 0,
        maxRetries: 0,
        description: "Configuration error - requires system restart",
      },
    };

    return (
      strategies[category] || {
        action: "UNKNOWN",
        delay: 0,
        maxRetries: 0,
        description: "Unknown error type",
      }
    );
  }

  /**
   * Express error handling middleware
   * Processes errors and returns appropriate HTTP responses with fault tolerance
   * Handles file upload errors, validation errors, and general server errors
   * Implements graceful degradation and recovery strategies
   *
   * @param {Error} err - The error object
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   * @param {Function} next - Express next function
   */
  static handle(err, req, res, next) {
    // Determine error category and level
    let category = ErrorHandler.ERROR_CATEGORIES.SYSTEM;
    let level = ErrorHandler.ERROR_LEVELS.WARNING;

    if (
      err.code === "LIMIT_FILE_SIZE" ||
      err.code === "LIMIT_UNEXPECTED_FILE"
    ) {
      category = ErrorHandler.ERROR_CATEGORIES.VALIDATION;
      level = ErrorHandler.ERROR_LEVELS.INFO;
    } else if (
      err.message.includes("security") ||
      err.message.includes("unauthorized")
    ) {
      category = ErrorHandler.ERROR_CATEGORIES.SECURITY;
      level = ErrorHandler.ERROR_LEVELS.WARNING;
    } else if (
      err.message.includes("conversion") ||
      err.message.includes("LibreOffice")
    ) {
      category = ErrorHandler.ERROR_CATEGORIES.CONVERSION;
      level = ErrorHandler.ERROR_LEVELS.WARNING;
    } else if (
      err.message.includes("dependency") ||
      err.message.includes("module")
    ) {
      category = ErrorHandler.ERROR_CATEGORIES.DEPENDENCY;
      level = ErrorHandler.ERROR_LEVELS.CRITICAL;
    }

    // Log error with enhanced context
    const errorId = ErrorHandler.logError(err, req, category, level, {
      userAgent: req.headers["user-agent"],
      contentLength: req.headers["content-length"],
      origin: req.headers["origin"],
    });

    // Handle file size limit exceeded errors from multer
    if (err.code === "LIMIT_FILE_SIZE") {
      return res.status(413).json({
        error: "File too large",
        message: `File size exceeds the limit of ${
          process.env.UPLOAD_LIMIT || "50mb"
        }`,
        errorId,
        requestId: req.id,
        recovery:
          "Reduce file size or contact administrator for limit increase",
      });
    }

    // Handle unexpected file upload errors from multer
    if (err.code === "LIMIT_UNEXPECTED_FILE") {
      return res.status(400).json({
        error: "Invalid file upload",
        message: "Unexpected file field",
        errorId,
        requestId: req.id,
        recovery: "Ensure file is uploaded in the correct field",
      });
    }

    // Handle all other server errors with appropriate error messages
    const isDevelopment = process.env.NODE_ENV === "development";
    const statusCode = level === this.ERROR_LEVELS.CRITICAL ? 503 : 500;

    res.status(statusCode).json({
      error: "Internal server error",
      message: isDevelopment ? err.message : "Something went wrong",
      errorId,
      requestId: req.id,
      recovery: this.getRecoveryStrategy(err, category).description,
      ...(isDevelopment && { stack: err.stack }),
    });
  }

  /**
   * Get error metrics for monitoring and alerting
   * Returns comprehensive error statistics for system health monitoring
   *
   * @returns {Object} Error metrics and statistics
   */
  static getErrorMetrics() {
    return {
      timestamp: new Date().toISOString(),
      totalErrors: this.metrics.totalErrors,
      errorsByCategory: this.metrics.errorsByCategory,
      errorsByLevel: this.metrics.errorsByLevel,
      recentErrors: this.metrics.recentErrors.slice(0, 10), // Last 10 errors
      systemHealth: this.calculateSystemHealth(),
    };
  }

  /**
   * Calculate system health score based on error patterns
   * @returns {Object} Health score and status
   */
  static calculateSystemHealth() {
    const criticalErrors =
      this.metrics.errorsByLevel[this.ERROR_LEVELS.CRITICAL] || 0;
    const warningErrors =
      this.metrics.errorsByLevel[this.ERROR_LEVELS.WARNING] || 0;
    const totalErrors = this.metrics.totalErrors;

    if (totalErrors === 0) {
      return {
        score: 100,
        status: "EXCELLENT",
        description: "No errors recorded",
      };
    }

    const criticalRatio = criticalErrors / totalErrors;
    const warningRatio = warningErrors / totalErrors;

    if (criticalRatio > 0.1) {
      return {
        score: 20,
        status: "CRITICAL",
        description: "High critical error rate",
      };
    } else if (criticalRatio > 0.05 || warningRatio > 0.3) {
      return {
        score: 50,
        status: "WARNING",
        description: "Elevated error rates",
      };
    } else if (warningRatio > 0.1) {
      return {
        score: 75,
        status: "GOOD",
        description: "Minor issues detected",
      };
    } else {
      return {
        score: 90,
        status: "EXCELLENT",
        description: "System operating normally",
      };
    }
  }

  /**
   * Generate health recommendations based on error metrics
   * Provides actionable recommendations for system health improvement
   *
   * @param {Object} errorMetrics - Error metrics from getErrorMetrics()
   * @returns {Array} Array of health recommendations
   */
  static generateHealthRecommendations(errorMetrics) {
    const recommendations = [];

    const healthScore = errorMetrics.systemHealth.score;
    const totalErrors = errorMetrics.totalErrors;
    const errorsByCategory = errorMetrics.errorsByCategory;

    // Health score based recommendations
    if (healthScore < 50) {
      recommendations.push({
        priority: "CRITICAL",
        category: "SYSTEM_HEALTH",
        recommendation:
          "Immediate attention required - system health critically low",
        actions: [
          "Review system logs for critical errors",
          "Check system resources and dependencies",
          "Consider system restart if issues persist",
          "Contact system administrator immediately",
        ],
      });
    } else if (healthScore < 75) {
      recommendations.push({
        priority: "HIGH",
        category: "SYSTEM_HEALTH",
        recommendation: "System health degraded - monitor closely",
        actions: [
          "Monitor error rates and system resources",
          "Review recent error patterns",
          "Check for dependency issues",
          "Plan maintenance window for investigation",
        ],
      });
    }

    // Error category specific recommendations
    if (errorsByCategory[this.ERROR_CATEGORIES.CONVERSION] > 10) {
      recommendations.push({
        priority: "HIGH",
        category: "CONVERSION_ERRORS",
        recommendation: "High conversion error rate detected",
        actions: [
          "Review LibreOffice installation and configuration",
          "Check document preprocessing pipeline",
          "Monitor system resources during conversions",
          "Validate input document formats",
        ],
      });
    }

    if (errorsByCategory[this.ERROR_CATEGORIES.SECURITY] > 0) {
      recommendations.push({
        priority: "CRITICAL",
        category: "SECURITY",
        recommendation: "Security-related errors detected",
        actions: [
          "Review security logs immediately",
          "Audit IP whitelist configuration",
          "Check for potential security breaches",
          "Update security policies if necessary",
        ],
      });
    }

    if (errorsByCategory[this.ERROR_CATEGORIES.DEPENDENCY] > 5) {
      recommendations.push({
        priority: "MEDIUM",
        category: "DEPENDENCIES",
        recommendation: "Dependency issues detected",
        actions: [
          "Run system dependency verification",
          "Check library installations",
          "Update or reinstall missing dependencies",
          "Review system compatibility",
        ],
      });
    }

    // Performance recommendations
    if (errorsByCategory[this.ERROR_CATEGORIES.PERFORMANCE] > 20) {
      recommendations.push({
        priority: "MEDIUM",
        category: "PERFORMANCE",
        recommendation: "Performance issues detected",
        actions: [
          "Monitor system resource usage",
          "Review concurrency settings",
          "Optimize conversion pipeline",
          "Consider system resource upgrades",
        ],
      });
    }

    // Default recommendation if no specific issues
    if (recommendations.length === 0 && totalErrors > 0) {
      recommendations.push({
        priority: "LOW",
        category: "GENERAL",
        recommendation: "Minor issues detected - continue monitoring",
        actions: [
          "Continue normal monitoring",
          "Review error logs periodically",
          "Maintain regular system health checks",
        ],
      });
    }

    return recommendations;
  }

  /**
   * Check system dependencies and conversion libraries
   * Verifies that required libraries are available with fallback handling
   * Implements graceful degradation for missing dependencies
   *
   * @returns {Promise<Object>} Object containing dependency status and health
   */
  static async checkSystemDependencies() {
    try {
      const dependencies = {
        sharp: {
          available: false,
          required: false,
          description: "Image processing library",
        },
        pdfkit: {
          available: false,
          required: true,
          description: "PDF generation library",
        },
        nodeLibraries: {
          available: true,
          required: true,
          description: "Core Node.js libraries",
        },
        libreOffice: {
          available: false,
          required: true,
          description: "Document conversion engine",
        },
      };

      let criticalDependencies = 0;
      let availableDependencies = 0;

      // Check Sharp availability for image processing (optional)
      try {
        const sharp = require("sharp");
        dependencies.sharp.available = true;
        availableDependencies++;
        console.info(`[INFO] Sharp library loaded successfully`, {
          category: this.ERROR_CATEGORIES.DEPENDENCY,
          level: this.ERROR_LEVELS.INFO,
          library: "sharp",
          version: sharp.version,
        });
      } catch (error) {
        this.logError(
          error,
          null,
          this.ERROR_CATEGORIES.DEPENDENCY,
          this.ERROR_LEVELS.WARNING,
          { library: "sharp", impact: "Image processing unavailable" }
        );
      }

      // Check PDFKit availability for PDF generation (required)
      try {
        const pdfkit = require("pdfkit");
        dependencies.pdfkit.available = true;
        availableDependencies++;
        criticalDependencies++;
      } catch (error) {
        this.logError(
          error,
          null,
          this.ERROR_CATEGORIES.DEPENDENCY,
          this.ERROR_LEVELS.CRITICAL,
          { library: "pdfkit", impact: "PDF generation unavailable" }
        );
      }

      // Check LibreOffice availability (required for core functionality)
      try {
        const libreOfficeService = require("../services/libreOfficeService");
        const loPath = libreOfficeService.getLibreOfficePath();
        dependencies.libreOffice.available =
          loPath && require("fs").existsSync(loPath);
        if (dependencies.libreOffice.available) {
          availableDependencies++;
          criticalDependencies++;
        }
      } catch (error) {
        this.logError(
          error,
          null,
          this.ERROR_CATEGORIES.DEPENDENCY,
          this.ERROR_LEVELS.CRITICAL,
          { library: "libreoffice", impact: "Document conversion unavailable" }
        );
      }

      const healthScore =
        criticalDependencies /
        Object.values(dependencies).filter((d) => d.required).length;

      console.log("Dependency check complete");
      this.logError(
        new Error(
          `System dependency check completed: ${availableDependencies}/${
            Object.keys(dependencies).length
          } available`
        ),
        null,
        this.ERROR_CATEGORIES.SYSTEM,
        this.ERROR_LEVELS.INFO,
        { dependencies, healthScore }
      );

      return {
        dependencies,
        healthScore: Math.round(healthScore * 100),
        criticalDependencies,
        totalDependencies: Object.keys(dependencies).length,
      };
    } catch (error) {
      this.logError(
        error,
        null,
        this.ERROR_CATEGORIES.SYSTEM,
        this.ERROR_LEVELS.CRITICAL,
        { operation: "dependency_check" }
      );
      return {
        dependencies: {
          sharp: false,
          pdfkit: false,
          nodeLibraries: false,
          libreOffice: false,
        },
        healthScore: 0,
        criticalDependencies: 0,
        totalDependencies: 4,
        error: error.message,
      };
    }
  }
}

module.exports = ErrorHandler;
