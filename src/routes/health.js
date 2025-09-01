/**
 * Health Check Route Handler
 *
 * Provides health check endpoint for monitoring server status and availability.
 * Returns basic server information including uptime, version, and current timestamp.
 * Used by load balancers, monitoring systems, and health check services.
 */

const express = require("express");
const router = express.Router();
const puppeteer = require("puppeteer");
const fs = require("fs");

/**
 * @swagger
 * /health:
 *   get:
 *     tags:
 *       - Health
 *     summary: Health check endpoint
 *     description: Returns the current health status of the API server including uptime and version information
 *     operationId: getHealth
 *     responses:
 *       200:
 *         description: Server is healthy and operational
 *         content:
 *           application/json:
 *             schema:
 *               type: object
 *               required:
 *                 - status
 *                 - timestamp
 *                 - uptime
 *                 - version
 *               properties:
 *                 status:
 *                   type: string
 *                   enum: [healthy]
 *                   example: "healthy"
 *                 timestamp:
 *                   type: string
 *                   format: date-time
 *                   example: "2024-01-15T10:30:00.000Z"
 *                 uptime:
 *                   type: number
 *                   description: Server uptime in seconds
 *                   example: 3600.5
 *                 version:
 *                   type: string
 *                   example: "1.0.0"
 *       500:
 *         description: Server error
 *         content:
 *           application/json:
 *             schema:
 *               $ref: '#/components/schemas/ErrorResponse'
 */

/**
 * GET /health
 * Health check endpoint that returns server status information
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
  });
});

// Export the router for use in the main application
module.exports = router;

/**
 * @swagger
 * /health/puppeteer:
 *   get:
 *     tags:
 *       - Health
 *     summary: Puppeteer diagnostic endpoint
 *     description: Tests Puppeteer browser launch capability and returns diagnostic information
 *     operationId: getPuppeteerHealth
 *     responses:
 *       200:
 *         description: Puppeteer diagnostic information
 *         content:
 *           application/json:
 *             schema:
 *               type: object
 *               properties:
 *                 status:
 *                   type: string
 *                   enum: [available, unavailable]
 *                 executablePath:
 *                   type: string
 *                   nullable: true
 *                 error:
 *                   type: string
 *                   nullable: true
 *                 platform:
 *                   type: string
 *                 environment:
 *                   type: object
 *       500:
 *         description: Server error
 */

/**
 * GET /health/puppeteer
 * Puppeteer diagnostic endpoint to test browser launch capability
 *
 * @param {Object} req - Express request object
 * @param {Object} res - Express response object
 */
router.get("/puppeteer", async (req, res) => {
  try {
    // Get Puppeteer executable path
    let executablePath;
    try {
      executablePath = process.env.PUPPETEER_EXECUTABLE_PATH;
      if (!executablePath && typeof puppeteer.executablePath === "function") {
        executablePath = puppeteer.executablePath();
      }
    } catch (error) {
      executablePath = null;
    }

    // Check if executable path exists
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
