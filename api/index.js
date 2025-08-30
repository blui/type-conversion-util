/**
 * Vercel Serverless Function Entry Point
 *
 * This file serves as the main entry point for the Vercel serverless deployment.
 * It imports and exports the Express application configured for serverless execution.
 */

// Load environment variables (safe to call multiple times)
require("dotenv").config();

try {
  // Import the main Express application
  const app = require("../src/server");

  // Export the app as a Vercel serverless function
  module.exports = app;
} catch (error) {
  console.error("Error loading application:", error);

  // Create a minimal error handler app
  const express = require("express");
  const errorApp = express();

  errorApp.get("*", (req, res) => {
    res.status(500).json({
      error: "Server initialization failed",
      message: "Please check server logs for details",
      details:
        process.env.NODE_ENV === "development" ? error.message : undefined,
    });
  });

  module.exports = errorApp;
}
