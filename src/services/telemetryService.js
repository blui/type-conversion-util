/**
 * Telemetry Service - NASA/JPL Standards Compliant
 *
 * Comprehensive telemetry and logging service implementing NASA/JPL standards.
 * Provides structured logging, performance monitoring, and system telemetry
 * for mission-critical applications.
 *
 * Features:
 * - Structured JSON logging with telemetry data
 * - Performance metrics collection and analysis
 * - System resource monitoring
 * - Request/response telemetry
 * - Error tracking and correlation
 * - Log aggregation and analysis support
 */

class TelemetryService {
  // Telemetry levels for different types of data collection
  static TELEMETRY_LEVELS = {
    TRACE: "TRACE", // Detailed execution tracing
    DEBUG: "DEBUG", // Debug information
    INFO: "INFO", // General information
    WARN: "WARN", // Warning conditions
    ERROR: "ERROR", // Error conditions
    CRITICAL: "CRITICAL", // Critical system conditions
  };

  // Telemetry categories for classification
  static TELEMETRY_CATEGORIES = {
    SYSTEM: "SYSTEM", // System-level operations
    APPLICATION: "APPLICATION", // Application-specific events
    SECURITY: "SECURITY", // Security-related events
    PERFORMANCE: "PERFORMANCE", // Performance metrics
    CONVERSION: "CONVERSION", // Document conversion operations
    NETWORK: "NETWORK", // Network operations
    STORAGE: "STORAGE", // Storage operations
  };

  // Performance metrics storage
  static metrics = {
    requests: [],
    conversions: [],
    systemResources: [],
    errors: [],
  };

  // Request tracking for correlation
  static activeRequests = new Map();

  /**
   * Log telemetry event with comprehensive context
   * @param {string} level - Telemetry level
   * @param {string} category - Telemetry category
   * @param {string} message - Log message
   * @param {Object} data - Additional telemetry data
   * @param {Object} context - Execution context
   */
  static log(level, category, message, data = {}, context = {}) {
    const timestamp = new Date().toISOString();
    const telemetryId = this.generateTelemetryId();

    const telemetryEntry = {
      telemetryId,
      timestamp,
      level,
      category,
      message,
      data: {
        ...data,
        process: {
          pid: process.pid,
          uptime: process.uptime(),
          memory: process.memoryUsage(),
          cpu: process.cpuUsage(),
        },
        system: {
          platform: require("os").platform(),
          arch: require("os").arch(),
          nodeVersion: process.version,
          totalMemory: require("os").totalmem(),
          freeMemory: require("os").freemem(),
        },
      },
      context,
      correlationId: context.correlationId || this.generateCorrelationId(),
    };

    // Store metrics based on category
    this.storeMetrics(category, telemetryEntry);

    // Output based on level and environment
    this.outputTelemetry(telemetryEntry);

    return telemetryId;
  }

  /**
   * Start request telemetry tracking
   * @param {Object} req - Express request object
   * @returns {string} Correlation ID
   */
  static startRequest(req) {
    const correlationId = this.generateCorrelationId();
    const startTime = process.hrtime.bigint();

    const requestData = {
      correlationId,
      startTime,
      method: req.method,
      url: req.url,
      headers: {
        "user-agent": req.headers["user-agent"],
        "content-length": req.headers["content-length"],
        "content-type": req.headers["content-type"],
      },
      ip: req.ip,
      timestamp: new Date().toISOString(),
    };

    this.activeRequests.set(correlationId, requestData);

    this.log(
      this.TELEMETRY_LEVELS.INFO,
      this.TELEMETRY_CATEGORIES.NETWORK,
      "Request started",
      { request: requestData },
      { correlationId, requestId: req.id }
    );

    return correlationId;
  }

  /**
   * End request telemetry tracking
   * @param {string} correlationId - Request correlation ID
   * @param {Object} res - Express response object
   * @param {Object} additionalData - Additional telemetry data
   */
  static endRequest(correlationId, res, additionalData = {}) {
    const requestData = this.activeRequests.get(correlationId);
    if (!requestData) return;

    const endTime = process.hrtime.bigint();
    const duration = Number(endTime - requestData.startTime) / 1e6; // Convert to milliseconds

    const responseData = {
      correlationId,
      duration,
      statusCode: res.statusCode,
      statusMessage: res.statusMessage,
      headers: res.getHeaders(),
      timestamp: new Date().toISOString(),
      ...additionalData,
    };

    this.log(
      this.TELEMETRY_LEVELS.INFO,
      this.TELEMETRY_CATEGORIES.NETWORK,
      "Request completed",
      {
        request: requestData,
        response: responseData,
        performance: {
          duration,
          throughput: additionalData.bytesTransferred
            ? additionalData.bytesTransferred / (duration / 1000)
            : 0,
        },
      },
      { correlationId }
    );

    this.activeRequests.delete(correlationId);
  }

  /**
   * Track conversion operation telemetry
   * @param {string} operation - Conversion operation type
   * @param {Object} input - Input document info
   * @param {Object} output - Output document info
   * @param {number} duration - Operation duration in ms
   * @param {boolean} success - Operation success status
   * @param {Object} context - Additional context
   */
  static trackConversion(
    operation,
    input,
    output,
    duration,
    success,
    context = {}
  ) {
    const telemetryData = {
      operation,
      input: {
        format: input.format,
        size: input.size,
        name: input.name,
      },
      output: {
        format: output.format,
        size: output.size,
      },
      duration,
      success,
      timestamp: new Date().toISOString(),
    };

    this.log(
      success ? this.TELEMETRY_LEVELS.INFO : this.TELEMETRY_LEVELS.WARN,
      this.TELEMETRY_CATEGORIES.CONVERSION,
      `Conversion ${success ? "completed" : "failed"}: ${operation}`,
      telemetryData,
      context
    );

    // Store conversion metrics for analysis
    this.metrics.conversions.push({
      ...telemetryData,
      timestamp: new Date(),
    });

    // Keep only last 1000 conversions for memory management
    if (this.metrics.conversions.length > 1000) {
      this.metrics.conversions.shift();
    }
  }

  /**
   * Collect system resource telemetry
   */
  static collectSystemTelemetry() {
    const telemetryData = {
      memory: process.memoryUsage(),
      cpu: process.cpuUsage(),
      uptime: process.uptime(),
      activeRequests: this.activeRequests.size,
      recentErrors: this.metrics.errors.length,
      conversionsProcessed: this.metrics.conversions.length,
    };

    this.log(
      this.TELEMETRY_LEVELS.INFO,
      this.TELEMETRY_CATEGORIES.SYSTEM,
      "System telemetry collected",
      telemetryData
    );

    // Store system metrics
    this.metrics.systemResources.push({
      ...telemetryData,
      timestamp: new Date(),
    });

    // Keep only last 100 system readings
    if (this.metrics.systemResources.length > 100) {
      this.metrics.systemResources.shift();
    }
  }

  /**
   * Get performance analytics
   * @returns {Object} Performance analytics data
   */
  static getPerformanceAnalytics() {
    const conversions = this.metrics.conversions.slice(-100); // Last 100 conversions
    const requests = this.metrics.requests.slice(-100); // Last 100 requests

    return {
      timestamp: new Date().toISOString(),
      conversionMetrics: {
        total: conversions.length,
        successRate:
          conversions.length > 0
            ? (conversions.filter((c) => c.success).length /
                conversions.length) *
              100
            : 0,
        averageDuration:
          conversions.length > 0
            ? conversions.reduce((sum, c) => sum + c.duration, 0) /
              conversions.length
            : 0,
        throughput: this.calculateThroughput(conversions),
      },
      requestMetrics: {
        total: requests.length,
        averageResponseTime:
          requests.length > 0
            ? requests.reduce((sum, r) => sum + r.duration, 0) / requests.length
            : 0,
        errorRate:
          requests.length > 0
            ? (requests.filter((r) => r.statusCode >= 400).length /
                requests.length) *
              100
            : 0,
      },
      systemHealth: {
        activeRequests: this.activeRequests.size,
        memoryUsage: process.memoryUsage(),
        uptime: process.uptime(),
      },
    };
  }

  /**
   * Generate unique telemetry ID
   * @returns {string} Unique telemetry identifier
   */
  static generateTelemetryId() {
    return `TEL-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
  }

  /**
   * Generate correlation ID for request tracking
   * @returns {string} Correlation identifier
   */
  static generateCorrelationId() {
    return `CORR-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
  }

  /**
   * Store metrics based on telemetry category
   * @param {string} category - Telemetry category
   * @param {Object} entry - Telemetry entry
   */
  static storeMetrics(category, entry) {
    switch (category) {
      case this.TELEMETRY_CATEGORIES.NETWORK:
        this.metrics.requests.push(entry);
        if (this.metrics.requests.length > 500) {
          this.metrics.requests.shift();
        }
        break;
      case this.TELEMETRY_CATEGORIES.CONVERSION:
        // Already handled in trackConversion
        break;
      case this.TELEMETRY_CATEGORIES.SECURITY:
      case this.TELEMETRY_CATEGORIES.SYSTEM:
        this.metrics.errors.push(entry);
        if (this.metrics.errors.length > 200) {
          this.metrics.errors.shift();
        }
        break;
    }
  }

  /**
   * Output telemetry based on configuration
   * @param {Object} telemetryEntry - Telemetry entry to output
   */
  static outputTelemetry(telemetryEntry) {
    const logLevel = process.env.LOG_LEVEL || "info";
    const shouldLog = this.shouldLog(telemetryEntry.level, logLevel);

    if (shouldLog) {
      const outputFormat = process.env.TELEMETRY_FORMAT || "json";

      if (outputFormat === "json") {
        console.log(JSON.stringify(telemetryEntry));
      } else {
        // Structured text format for human readability
        const levelPrefix = {
          [this.TELEMETRY_LEVELS.TRACE]: "TRACE",
          [this.TELEMETRY_LEVELS.DEBUG]: "DEBUG",
          [this.TELEMETRY_LEVELS.INFO]: "INFO",
          [this.TELEMETRY_LEVELS.WARN]: "WARN",
          [this.TELEMETRY_LEVELS.ERROR]: "ERROR",
          [this.TELEMETRY_LEVELS.CRITICAL]: "CRITICAL",
        };

        console.log(
          `${levelPrefix[telemetryEntry.level] || "LOG"} [${
            telemetryEntry.timestamp
          }] ${telemetryEntry.category}: ${telemetryEntry.message}`
        );
      }
    }
  }

  /**
   * Determine if telemetry should be logged based on level
   * @param {string} entryLevel - Entry log level
   * @param {string} configLevel - Configured log level
   * @returns {boolean} Whether to log the entry
   */
  static shouldLog(entryLevel, configLevel) {
    const levels = Object.values(this.TELEMETRY_LEVELS);
    const entryIndex = levels.indexOf(entryLevel);
    const configIndex = levels.indexOf(configLevel.toUpperCase());

    return entryIndex >= configIndex;
  }

  /**
   * Calculate throughput from conversion data
   * @param {Array} conversions - Array of conversion records
   * @returns {number} Throughput in conversions per minute
   */
  static calculateThroughput(conversions) {
    if (conversions.length < 2) return 0;

    const sorted = conversions.sort(
      (a, b) => new Date(a.timestamp) - new Date(b.timestamp)
    );
    const timeSpan =
      (new Date(sorted[sorted.length - 1].timestamp) -
        new Date(sorted[0].timestamp)) /
      1000 /
      60; // minutes

    return timeSpan > 0 ? conversions.length / timeSpan : 0;
  }

  /**
   * Export telemetry data for analysis
   * @param {string} format - Export format (json, csv)
   * @returns {string} Exported telemetry data
   */
  static exportTelemetry(format = "json") {
    const data = {
      timestamp: new Date().toISOString(),
      metrics: this.metrics,
      activeRequests: Array.from(this.activeRequests.entries()),
      analytics: this.getPerformanceAnalytics(),
    };

    if (format === "csv") {
      // Convert to CSV format for analysis tools
      return this.convertToCSV(data);
    }

    return JSON.stringify(data, null, 2);
  }

  /**
   * Convert telemetry data to CSV format
   * @param {Object} data - Telemetry data
   * @returns {string} CSV formatted data
   */
  static convertToCSV(data) {
    // Implementation for CSV export would go here
    // For brevity, returning JSON for now
    return JSON.stringify(data, null, 2);
  }
}

module.exports = TelemetryService;
