/**
 * Routes Configuration
 *
 * Centralized route setup for the Express application.
 * Configures API endpoints, static routes, and error handling.
 */

const path = require("path");

const conversionRoutes = require("../routes/conversion");
const healthRoutes = require("../routes/health");
const ErrorHandler = require("../middleware/errorHandler");
const { setupSwagger } = require("../config/swagger");

/**
 * Routes configuration utilities
 */
class RoutesSetup {
  /**
   * Configure all application routes
   *
   * @param {Object} app - Express application instance
   */
  static configureRoutes(app) {
    // API route configuration
    app.use("/api", conversionRoutes);

    // Health check routes for monitoring and load balancers
    app.use("/health", healthRoutes);
    app.use("/api/health", healthRoutes);

    // API documentation setup
    setupSwagger(app);

    // API discovery and information endpoint
    app.get("/api", (req, res) => {
      res.json({
        name: "File Conversion Utility API",
        version: require("../../package.json").version,
        description:
          "Document and image conversion service with enhanced accuracy",
        endpoints: {
          convert: "/api/convert",
          health: "/health",
          "supported-formats": "/api/supported-formats",
          documentation: "/api-docs",
        },
        environment: require("../config/config").nodeEnv,
        timestamp: new Date().toISOString(),
      });
    });

    // 404 error handler for unmatched routes
    app.use("*", (req, res) => {
      res.status(404).json({
        error: "Not found",
        message: "The requested resource was not found",
        availableEndpoints: ["/api", "/health", "/api-docs"],
        requestId: req.id,
      });
    });

    // Global error handling middleware
    app.use(ErrorHandler.handle);
  }
}

module.exports = RoutesSetup;
