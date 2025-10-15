/**
 * Health Check Routes
 *
 * Comprehensive health check endpoints for monitoring, alerting, and system reliability.
 * Provides system health monitoring and fault detection.
 *
 * Endpoints:
 * - GET /health - Basic health status for load balancers
 * - GET /health/detailed - Comprehensive system status with error metrics
 * - GET /health/errors - Error metrics and system health analysis
 *
 * Features:
 * - Memory usage monitoring and alerting
 * - System resource tracking with thresholds
 * - Temporary directory validation and recovery
 * - Environment information and configuration validation
 * - Performance metrics and telemetry
 * - Error categorization and recovery strategies
 * - Fault tolerance assessment
 */

// Express router for health check endpoints
const express = require("express");
const router = express.Router();

// Node.js built-in modules for system information
const fs = require("fs");
const os = require("os");
const path = require("path");

// Application modules
const config = require("../config/config");
const ErrorHandler = require("../middleware/errorHandler");

/**
 * Basic health check endpoint
 * Returns essential health status for load balancers and monitoring systems
 *
 * @param {Object} req - Express request object
 * @param {Object} res - Express response object
 */
router.get("/", (req, res) => {
  res.json({
    status: "healthy",
    timestamp: new Date().toISOString(),
    uptime: process.uptime(),
    version: require("../../package.json").version,
    environment: config.nodeEnv,
    requestId: req.id,
  });
});

/**
 * Detailed health check with system information
 * Returns full system status including memory usage, system resources,
 * and application configuration for monitoring and debugging
 *
 * @param {Object} req - Express request object
 * @param {Object} res - Express response object
 */
router.get("/detailed", async (req, res) => {
  try {
    // Calculate memory usage in megabytes for monitoring
    const memUsage = process.memoryUsage();
    const memUsageMB = {
      rss: Math.round(memUsage.rss / 1024 / 1024),
      heapTotal: Math.round(memUsage.heapTotal / 1024 / 1024),
      heapUsed: Math.round(memUsage.heapUsed / 1024 / 1024),
      external: Math.round(memUsage.external / 1024 / 1024),
    };

    // Collect system information for monitoring and debugging
    const systemInfo = {
      platform: os.platform(),
      arch: os.arch(),
      nodeVersion: process.version,
      cpus: os.cpus().length,
      totalMemory: Math.round(os.totalmem() / 1024 / 1024 / 1024),
      freeMemory: Math.round(os.freemem() / 1024 / 1024 / 1024),
      uptime: Math.round(os.uptime() / 3600),
    };

    // Check temporary directory status and writability
    const tempDirStatus = {
      path: config.tempDir,
      exists: fs.existsSync(config.tempDir),
      writable: false,
    };

    // Test temporary directory writability by creating and deleting a test file
    try {
      if (tempDirStatus.exists) {
        const testFile = path.join(config.tempDir, ".health-test");
        fs.writeFileSync(testFile, "test");
        fs.unlinkSync(testFile);
        tempDirStatus.writable = true;
      }
    } catch (error) {
      tempDirStatus.writable = false;
    }

    // Check output directory status and writability
    const outputDirStatus = {
      path: config.outputDir,
      exists: fs.existsSync(config.outputDir),
      writable: false,
    };

    // Test output directory writability by creating and deleting a test file
    try {
      if (outputDirStatus.exists) {
        const testFile = path.join(config.outputDir, ".health-test");
        fs.writeFileSync(testFile, "test");
        fs.unlinkSync(testFile);
        outputDirStatus.writable = true;
      }
    } catch (error) {
      outputDirStatus.writable = false;
    }

    // Get dependency status for health assessment
    const dependencyStatus = await ErrorHandler.checkSystemDependencies();

    // Get error metrics for health assessment
    const errorMetrics = ErrorHandler.getErrorMetrics();

    // Determine overall health status based on multiple factors
    let status = "healthy";
    let healthScore = 100;

    // Check critical health indicators
    const criticalIssues = [];

    if (!tempDirStatus.writable) {
      criticalIssues.push("Temp directory not writable");
      healthScore -= 30;
    }

    if (!outputDirStatus.writable) {
      criticalIssues.push("Output directory not writable");
      healthScore -= 30;
    }

    if (memUsageMB.heapUsed > 500) {
      criticalIssues.push("High memory usage");
      healthScore -= 20;
    }

    if (dependencyStatus.healthScore < 75) {
      criticalIssues.push("Critical dependencies unavailable");
      healthScore -= 40;
    }

    if (errorMetrics.systemHealth.score < 70) {
      criticalIssues.push("High error rate");
      healthScore -= 25;
    }

    // Determine status based on health score
    if (healthScore < 50) {
      status = "critical";
    } else if (healthScore < 75) {
      status = "warning";
    }

    res.json({
      status,
      healthScore,
      timestamp: new Date().toISOString(),
      uptime: process.uptime(),
      version: require("../../package.json").version,
      system: systemInfo,
      environment: {
        NODE_ENV: process.env.NODE_ENV,
        PORT: config.port,
        HOST: config.host,
      },
      resources: {
        memory: memUsageMB,
        tempDirectory: tempDirStatus,
        outputDirectory: outputDirStatus,
        concurrency: {
          max: config.concurrency.maxConcurrent,
          queue: config.concurrency.maxQueue,
        },
        rateLimit: {
          max: config.rateLimit.max,
          windowMs: config.rateLimit.windowMs,
        },
      },
      dependencies: dependencyStatus,
      errors: {
        systemHealth: errorMetrics.systemHealth,
        totalErrors: errorMetrics.totalErrors,
        recentErrors: errorMetrics.recentErrors.length,
      },
      criticalIssues: criticalIssues.length > 0 ? criticalIssues : null,
      endpoints: {
        api: "/api",
        documentation: "/api-docs",
        detailedHealth: "/health/detailed",
        errorMetrics: "/health/errors",
      },
    });
  } catch (error) {
    ErrorHandler.logError(
      error,
      req,
      ErrorHandler.ERROR_CATEGORIES.SYSTEM,
      ErrorHandler.ERROR_LEVELS.CRITICAL
    );
    res.status(500).json({
      error: "Health check failed",
      message: error.message,
      timestamp: new Date().toISOString(),
      errorId: ErrorHandler.generateErrorId(),
    });
  }
});

/**
 * Error metrics endpoint
 * Returns comprehensive error statistics for monitoring and alerting
 * Used by monitoring systems to track system health and error patterns
 *
 * @param {Object} req - Express request object
 * @param {Object} res - Express response object
 */
router.get("/errors", (req, res) => {
  try {
    const errorMetrics = ErrorHandler.getErrorMetrics();

    res.json({
      timestamp: new Date().toISOString(),
      errorMetrics,
      recommendations: ErrorHandler.generateHealthRecommendations(errorMetrics),
    });
  } catch (error) {
    ErrorHandler.logError(
      error,
      req,
      ErrorHandler.ERROR_CATEGORIES.SYSTEM,
      ErrorHandler.ERROR_LEVELS.CRITICAL
    );
    res.status(500).json({
      error: "Error metrics retrieval failed",
      message: error.message,
      timestamp: new Date().toISOString(),
      errorId: ErrorHandler.generateErrorId(),
    });
  }
});

module.exports = router;
