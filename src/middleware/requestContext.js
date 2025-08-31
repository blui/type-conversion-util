/**
 * Request Context Middleware
 *
 * Adds request tracking and timing information to each HTTP request.
 * Provides unique request IDs and performance metrics for debugging and monitoring.
 * Implements structured logging for better observability.
 */

const { v4: uuidv4 } = require("uuid");

/**
 * Request context middleware function
 * Adds request ID and timing information to each request
 *
 * @param {Object} req - Express request object
 * @param {Object} res - Express response object
 * @param {Function} next - Express next function
 */
module.exports = function requestContext(req, res, next) {
  // Generate or use existing request ID
  const requestId = req.headers["x-request-id"] || uuidv4();
  req.requestId = requestId;
  res.setHeader("X-Request-Id", requestId);

  // Record start time for performance measurement
  const start = process.hrtime.bigint();

  // Log request completion with timing information
  res.on("finish", () => {
    try {
      const end = process.hrtime.bigint();
      const durationMs = Number(end - start) / 1e6;

      const logEntry = {
        requestId,
        method: req.method,
        url: req.originalUrl || req.url,
        status: res.statusCode,
        durationMs: Math.round(durationMs),
      };

      console.log("request", logEntry);
    } catch (error) {
      // Silently handle logging errors to avoid breaking request flow
    }
  });

  next();
};
