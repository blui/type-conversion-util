/**
 * Vercel Serverless Function Entry Point
 *
 * This file serves as the main entry point for the Vercel serverless deployment.
 * It imports and exports the Express application configured for serverless execution.
 */

// Load environment variables
require("dotenv").config();

// Import the main Express application
const app = require("../src/server");

// Export the app as a Vercel serverless function
module.exports = app;
