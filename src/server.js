/**
 * File Conversion Utility Server
 *
 * Express server providing file conversion API
 * Supports documents, images, audio, and archives
 */

require("dotenv").config();

const express = require("express");
const cors = require("cors");
const helmet = require("helmet");
const morgan = require("morgan");
const rateLimit = require("express-rate-limit");
const path = require("path");
const fs = require("fs");
const os = require("os");

const config = require("./config/config");
const ErrorHandler = require("./middleware/errorHandler");
const requestContext = require("./middleware/requestContext");
const { setupSwagger } = require("./config/swagger");

const conversionRoutes = require("./routes/conversion");
const healthRoutes = require("./routes/health");

const app = express();
const PORT = config.port;
const HOST = config.host;

/**
 * Log startup information
 */
function logStartupInfo() {
  console.log("=".repeat(60));
  console.log("File Conversion Utility Server Starting");
  console.log("=".repeat(60));
  console.log(`Environment: ${config.nodeEnv}`);
  console.log(`Port: ${PORT}`);
  console.log(`Host: ${HOST}`);
  console.log(`Intranet Mode: ${config.isIntranet ? "Enabled" : "Disabled"}`);
  console.log(`Serverless: ${config.isServerless ? "Yes" : "No"}`);

  if (config.isIntranet) {
    console.log("\nNetwork Interfaces:");
    const networkInterfaces = os.networkInterfaces();
    Object.keys(networkInterfaces).forEach((name) => {
      networkInterfaces[name].forEach((iface) => {
        if (iface.family === "IPv4" && !iface.internal) {
          console.log(`  ${name}: ${iface.address}`);
        }
      });
    });
  }

  console.log(`\nTemp Directory: ${config.tempDir}`);
  console.log(
    `CORS Origin: ${
      config.cors.origin === true ? "All Origins" : config.cors.origin
    }`
  );
  console.log(`Max Concurrency: ${config.concurrency.maxConcurrent}`);
  console.log(
    `Rate Limit: ${config.rateLimit.max} requests per ${
      config.rateLimit.windowMs / 1000 / 60
    } minutes`
  );
  console.log("=".repeat(60));
}

// Configure proxy trust for load balancers
if (config.network.trustProxy) {
  app.set("trust proxy", true);
  console.log("Trust proxy enabled for load balancer support");
}

// Initialize temp directory
if (config.tempDir !== "/tmp" && !fs.existsSync(config.tempDir)) {
  try {
    fs.mkdirSync(config.tempDir, { recursive: true });
    console.log(`Created temp directory: ${config.tempDir}`);
  } catch (error) {
    console.error(`Failed to create temp directory: ${error.message}`);
  }
}

// Verify system dependencies
ErrorHandler.checkSystemDependencies()
  .then((dependencies) => {
    console.log("System dependencies verified:", dependencies);
  })
  .catch((error) => {
    console.warn("Dependency check failed (non-fatal):", error.message);
  });

// Middleware
app.use(helmet(config.helmet));
app.use(rateLimit(config.rateLimit));
app.use(cors(config.cors));
app.options("*", cors(config.cors));
app.use(morgan(config.logging.format));
app.use(requestContext);
app.use(express.json({ limit: config.uploadLimit }));
app.use(express.urlencoded({ extended: true, limit: config.uploadLimit }));

// Static file serving
if (!config.isServerless) {
  app.use(express.static(path.join(__dirname, "../")));
} else {
  app.use("/", express.static(path.join(__dirname, "../")));
}

// API Routes
app.use("/api", conversionRoutes);
app.use("/health", healthRoutes);
app.use("/api/health", healthRoutes);

// Serve the main HTML file
app.get("/", (req, res) => {
  res.sendFile(path.join(__dirname, "../index.html"));
});

// Swagger documentation
setupSwagger(app);

// API discovery endpoint
app.get("/api", (req, res) => {
  res.json({
    name: "File Conversion Utility API",
    version: require("../package.json").version,
    description: "Comprehensive file conversion service",
    endpoints: {
      convert: "/api/convert",
      health: "/health",
      "health-detailed": "/health/detailed",
      "health-puppeteer": "/health/puppeteer",
      "supported-formats": "/api/supported-formats",
      documentation: "/api-docs",
    },
    environment: config.nodeEnv,
    intranet: config.isIntranet,
    timestamp: new Date().toISOString(),
  });
});

// 404 handler
app.use("*", (req, res) => {
  res.status(404).json({
    error: "Not found",
    message: "The requested resource was not found",
    availableEndpoints: ["/api", "/health", "/api-docs", "/"],
    requestId: req.id,
  });
});

// Error handler
app.use(ErrorHandler.handle);

// Graceful shutdown
process.on("SIGTERM", () => {
  console.log("SIGTERM received, shutting down gracefully...");
  process.exit(0);
});

process.on("SIGINT", () => {
  console.log("SIGINT received, shutting down gracefully...");
  process.exit(0);
});

// Start server
if (!config.isServerless) {
  const server = app.listen(PORT, HOST, () => {
    logStartupInfo();
    console.log(`Server running on http://${HOST}:${PORT}`);
    console.log(`Web Interface: http://${HOST}:${PORT}`);
    console.log(`API Documentation: http://${HOST}:${PORT}/api-docs`);
    console.log(`Health Check: http://${HOST}:${PORT}/health`);

    if (config.isIntranet) {
      console.log("\nIntranet Access URLs:");
      const networkInterfaces = os.networkInterfaces();
      Object.keys(networkInterfaces).forEach((name) => {
        networkInterfaces[name].forEach((iface) => {
          if (iface.family === "IPv4" && !iface.internal) {
            console.log(`  ${name}: http://${iface.address}:${PORT}`);
          }
        });
      });
    }
  });

  if (config.network.keepAlive) {
    server.keepAliveTimeout = config.network.keepAliveTimeout;
    server.headersTimeout = config.network.keepAliveTimeout + 1000;
  }
} else {
  console.log("Serverless environment detected, server not started");
}

module.exports = app;
