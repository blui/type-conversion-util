/**
 * Server Configuration
 *
 * Centralized server configuration and initialization utilities.
 * Handles startup logging, directory setup, and dependency verification.
 */

const fs = require("fs");
const path = require("path");
const config = require("../config/config");
const ErrorHandler = require("../middleware/errorHandler");

/**
 * Server configuration and initialization utilities
 */
class ServerConfig {
  /**
   * Log startup information and server configuration
   * Displays environment settings, network configuration, and service status
   */
  static logStartupInfo() {
    console.log("=".repeat(50));
    console.log("File Conversion Utility Server Starting");
    console.log("=".repeat(50));
    console.log(`Environment: ${config.nodeEnv}`);
    console.log(`Port: ${config.port}`);
    console.log(`Host: ${config.host}`);
    console.log(`Temp Directory: ${config.tempDir}`);
    console.log(`Max Concurrency: ${config.concurrency.maxConcurrent}`);
    console.log(
      `Rate Limit: ${config.rateLimit.max} requests per ${
        config.rateLimit.windowMs / 1000 / 60
      } minutes`
    );
    console.log("=".repeat(50));
  }

  /**
   * Initialize temporary directory for file processing
   * Creates temp directory if it doesn't exist (except for /tmp in production)
   */
  static initializeTempDirectory() {
    if (config.tempDir !== "/tmp" && !fs.existsSync(config.tempDir)) {
      try {
        fs.mkdirSync(config.tempDir, { recursive: true });
        console.log(`Created temp directory: ${config.tempDir}`);
      } catch (error) {
        console.error(`Failed to create temp directory: ${error.message}`);
        throw error;
      }
    }
  }

  /**
   * Verify system dependencies and conversion libraries
   * Checks for required libraries and logs results
   */
  static async verifyDependencies() {
    try {
      const dependencies = await ErrorHandler.checkSystemDependencies();
      console.log("System dependencies verified:", dependencies);
    } catch (error) {
      console.warn("Dependency check failed (non-fatal):", error.message);
    }
  }

  /**
   * Configure proxy trust for load balancers and reverse proxies
   * Important for correct IP address detection in production environments
   *
   * @param {Object} app - Express application instance
   */
  static configureProxyTrust(app) {
    if (config.network.trustProxy) {
      app.set("trust proxy", true);
      console.log("Trust proxy enabled for load balancer support");
    }
  }

  /**
   * Configure keep-alive settings for better connection management
   *
   * @param {Object} server - HTTP/HTTPS server instance
   */
  static configureKeepAlive(server) {
    if (config.network.keepAlive) {
      server.keepAliveTimeout = config.network.keepAliveTimeout;
      server.headersTimeout = config.network.keepAliveTimeout + 1000;
    }
  }
}

module.exports = ServerConfig;
