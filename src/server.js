/**
 * File Conversion Utility Server
 *
 * Express server providing file conversion API with enhanced accuracy features.
 * Supports document and image conversions with professional-grade formatting preservation.
 */

// Load environment variables
require("dotenv").config();

// Core dependencies
const express = require("express");
const config = require("./config/config");
const sslConfig = require("./config/ssl");

// Server setup utilities
const ServerConfig = require("./server/serverConfig");
const MiddlewareSetup = require("./server/middlewareSetup");
const RoutesSetup = require("./server/routesSetup");

// Initialize Express application
const app = express();

// Server configuration
const PORT = config.port;
const HOST = config.host;

// Initialize server configuration
async function initializeServer() {
  // Configure proxy trust
  ServerConfig.configureProxyTrust(app);

  // Initialize directories for file processing and output
  ServerConfig.initializeDirectories();

  // Verify system dependencies
  await ServerConfig.verifyDependencies();

  // Configure middleware
  MiddlewareSetup.configureSecurity(app);
  MiddlewareSetup.configureBasicMiddleware(app);

  // Configure routes
  RoutesSetup.configureRoutes(app);
}

// Graceful shutdown handlers
process.on("SIGTERM", () => {
  console.log("SIGTERM received, shutting down gracefully...");
  process.exit(0);
});

process.on("SIGINT", () => {
  console.log("SIGINT received, shutting down gracefully...");
  process.exit(0);
});

// Server startup
async function startServer() {
  try {
    // Initialize all server components
    await initializeServer();

    // Start HTTP or HTTPS server
    const httpsOptions = sslConfig.getHttpsOptions();
    let server;

    if (httpsOptions) {
      const https = require("https");
      server = https.createServer(httpsOptions, app);
    } else {
      const http = require("http");
      server = http.createServer(app);
    }

    // Configure keep-alive settings
    ServerConfig.configureKeepAlive(server);

    // Start listening
    server.listen(PORT, HOST, () => {
      ServerConfig.logStartupInfo();
      const protocol = httpsOptions ? "https" : "http";
      console.log(`Server running on ${protocol}://${HOST}:${PORT}`);
      console.log(`API Documentation: ${protocol}://${HOST}:${PORT}/api-docs`);
      console.log(`Health Check: ${protocol}://${HOST}:${PORT}/health`);
    });
  } catch (error) {
    console.error("Failed to start server:", error);
    process.exit(1);
  }
}

// Start the server
startServer();

module.exports = app;
