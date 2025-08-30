#!/usr/bin/env node

/**
 * Setup Script for File Conversion Utility
 *
 * This script initializes the application environment by creating necessary
 * directories, configuration files, and running initial security checks.
 * Should be run once after npm install to prepare the application for use.
 */

const fs = require("fs");
const path = require("path");

console.log("Setting up File Conversion Utility...\n");

/**
 * Create environment configuration file
 * Sets up .env file with default configuration values if it doesn't exist
 */
const envPath = path.join(__dirname, "..", ".env");
const envExampleContent = `# File Conversion Utility Configuration
# For Vercel deployment, set these as environment variables in the dashboard

# Server Configuration
PORT=3000
NODE_ENV=development

# File Upload Configuration
UPLOAD_LIMIT=50mb
MAX_FILE_SIZE=52428800
# TEMP_DIR automatically uses /tmp in production/serverless environments
TEMP_DIR=./temp

# Security Configuration
RATE_LIMIT_WINDOW_MS=900000
RATE_LIMIT_MAX_REQUESTS=100

# Puppeteer Configuration (for PDF generation)
# For Vercel, uncomment these lines:
# PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium-browser
# PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=true
PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=false
PUPPETEER_EXECUTABLE_PATH=

# Logging
LOG_LEVEL=info
`;

if (!fs.existsSync(envPath)) {
  fs.writeFileSync(envPath, envExampleContent);
  console.log("Created .env file with default configuration");
} else {
  console.log("Note: .env file already exists");
}

/**
 * Create temporary files directory for local development
 * In serverless environments (Vercel), /tmp is automatically available
 */
const tempDir = path.join(__dirname, "..", "temp");
if (!fs.existsSync(tempDir)) {
  fs.mkdirSync(tempDir, { recursive: true });
  console.log("Created temp directory for local development");
} else {
  console.log("Note: Temp directory already exists");
}

/**
 * Note: Logs directory not created for serverless compatibility
 * In serverless environments, use console logging and external services
 * for log persistence (e.g., Vercel Analytics, CloudWatch)
 */
console.log("Note: File-based logging disabled for serverless compatibility");

console.log("\nSetup complete!");
console.log("\nNext steps:");
console.log("1. Review and modify .env file if needed");
console.log('2. Run "npm run dev" to start development server');
console.log('3. Run "npm run security:check" to verify security');
console.log("4. Visit http://localhost:3000 to test the application\n");

/**
 * Run initial security audit
 * Checks for known vulnerabilities in dependencies
 */
console.log("Running security check...");
const { exec } = require("child_process");
exec("npm audit --audit-level moderate", (error, stdout, stderr) => {
  if (error) {
    console.log('Security audit found issues. Run "npm audit fix" to resolve.');
  } else {
    console.log("No moderate or high security vulnerabilities found");
  }
});
