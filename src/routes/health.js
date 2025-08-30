/**
 * Health Check Route Handler
 *
 * Provides a simple health check endpoint for monitoring server status.
 * Returns basic server information including uptime, version, and current timestamp.
 * Used by load balancers, monitoring systems, and health check services.
 */

const express = require("express");
const router = express.Router();

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
