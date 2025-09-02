/**
 * Health Check Routes
 *
 * Provides health check endpoints for monitoring and load balancers
 */

const express = require("express");
const router = express.Router();
const puppeteer = require("puppeteer");
const fs = require("fs");
const os = require("os");
const config = require("../config/config");
const path = require("path");

/**
 * Basic health check endpoint
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
 */
router.get("/detailed", (req, res) => {
  try {
    // Memory usage
    const memUsage = process.memoryUsage();
    const memUsageMB = {
      rss: Math.round(memUsage.rss / 1024 / 1024),
      heapTotal: Math.round(memUsage.heapTotal / 1024 / 1024),
      heapUsed: Math.round(memUsage.heapUsed / 1024 / 1024),
      external: Math.round(memUsage.external / 1024 / 1024),
    };

    // System information
    const systemInfo = {
      platform: os.platform(),
      arch: os.arch(),
      nodeVersion: process.version,
      cpus: os.cpus().length,
      totalMemory: Math.round(os.totalmem() / 1024 / 1024 / 1024),
      freeMemory: Math.round(os.freemem() / 1024 / 1024 / 1024),
      loadAverage: os.loadavg(),
      uptime: Math.round(os.uptime() / 3600),
    };

    // Temp directory status
    const tempDirStatus = {
      path: config.tempDir,
      exists: fs.existsSync(config.tempDir),
      writable: false,
    };

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

    // Determine health status
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
        INTRANET: config.isIntranet,
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
        puppeteer: "/health/puppeteer",
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

/**
 * Puppeteer diagnostic endpoint
 */
router.get("/puppeteer", async (req, res) => {
  try {
    // Get executable path
    let executablePath;
    try {
      executablePath = process.env.PUPPETEER_EXECUTABLE_PATH;
      if (!executablePath && typeof puppeteer.executablePath === "function") {
        executablePath = puppeteer.executablePath();
      }
    } catch (error) {
      executablePath = null;
    }

    // Check if executable exists
    let executableExists = false;
    if (executablePath) {
      try {
        executableExists = fs.existsSync(executablePath);
      } catch (error) {
        executableExists = false;
      }
    }

    // Try to launch browser
    let browser = null;
    let launchSuccess = false;
    let launchError = null;

    try {
      const launchOptions = {
        headless: true,
        args: [
          "--no-sandbox",
          "--disable-setuid-sandbox",
          "--disable-dev-shm-usage",
        ],
        timeout: 30000,
      };

      if (executablePath && executableExists) {
        launchOptions.executablePath = executablePath;
      }

      browser = await puppeteer.launch(launchOptions);
      launchSuccess = true;
    } catch (error) {
      launchError = error.message;
    } finally {
      if (browser) {
        try {
          await browser.close();
        } catch (error) {
          // Ignore close errors
        }
      }
    }

    res.json({
      status: launchSuccess ? "available" : "unavailable",
      executablePath,
      executableExists,
      error: launchError,
      platform: process.platform,
      environment: {
        NODE_ENV: process.env.NODE_ENV,
        VERCEL: process.env.VERCEL,
        INTRANET: config.isIntranet,
        PUPPETEER_SKIP_CHROMIUM_DOWNLOAD:
          process.env.PUPPETEER_SKIP_CHROMIUM_DOWNLOAD,
        PUPPETEER_CACHE_DIR: process.env.PUPPETEER_CACHE_DIR,
      },
    });
  } catch (error) {
    res.status(500).json({
      error: "Diagnostic failed",
      message: error.message,
    });
  }
});

module.exports = router;
