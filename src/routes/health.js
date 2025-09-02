/**
 * Health Check Routes
 *
 * Provides health check endpoints for monitoring and load balancer integration.
 * Includes basic health status, detailed system information, and resource monitoring.
 *
 * Endpoints:
 * - GET /health - Basic health status
 * - GET /health/detailed - Detailed system information and resource usage
 *
 * Features:
 * - Memory usage monitoring
 * - System resource tracking
 * - Temporary directory validation
 * - Environment information
 * - Performance metrics
 */

// Express router for health check endpoints
const express = require("express");
const router = express.Router();

// Node.js built-in modules for system information
const fs = require("fs");
const os = require("os");
const path = require("path");

// Application configuration
const config = require("../config/config");

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
 * Provides comprehensive system status including memory usage, system resources,
 * and application configuration for detailed monitoring and debugging
 *
 * @param {Object} req - Express request object
 * @param {Object} res - Express response object
 */
router.get("/detailed", (req, res) => {
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

    // Determine overall health status based on system conditions
    let status = "healthy";
    if (!tempDirStatus.writable || memUsageMB.heapUsed > 500) {
      status = "warning";
    }

    res.json({
      status,
      timestamp: new Date().toISOString(),
      uptime: process.uptime(),
      version: require("../../package.json").version,
      system: systemInfo,
      environment: {
        NODE_ENV: process.env.NODE_ENV,
        SERVERLESS: config.isServerless,
        PORT: config.port,
        HOST: config.host,
      },
      resources: {
        memory: memUsageMB,
        tempDirectory: tempDirStatus,
        concurrency: {
          max: config.concurrency.maxConcurrent,
          queue: config.concurrency.maxQueue,
        },
        rateLimit: {
          max: config.rateLimit.max,
          windowMs: config.rateLimit.windowMs,
        },
      },
      endpoints: {
        api: "/api",
        documentation: "/api-docs",
      },
    });
  } catch (error) {
    res.status(500).json({
      error: "Health check failed",
      message: error.message,
      timestamp: new Date().toISOString(),
    });
  }
});

module.exports = router;
