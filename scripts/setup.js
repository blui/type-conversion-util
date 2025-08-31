#!/usr/bin/env node

/**
 * Setup Script for File Conversion Utility
 *
 * This script initializes the application environment by creating necessary
 * directories, configuration files, and running initial security checks.
 * Should be run once after npm install to prepare the application for use.
 * Supports both traditional and serverless deployment environments.
 */

const fs = require("fs");
const path = require("path");

console.log("Setting up File Conversion Utility...\n");

/**
 * Create environment configuration file
 * Sets up .env file with default configuration values if it doesn't exist
 * Includes comprehensive settings for development and production environments
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

# Concurrency Control
MAX_CONCURRENCY=2
MAX_QUEUE=10

# Puppeteer Configuration (for PDF generation)
# For Vercel, uncomment these lines:
# PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium-browser
# PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=true
PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=false
PUPPETEER_EXECUTABLE_PATH=

# Logging Configuration
LOG_LEVEL=info

# Performance Settings
# Increase for high-volume environments
# MAX_CONCURRENCY=5
# MAX_QUEUE=20
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
 * This directory is used for file uploads and conversion processing
 */
const tempDir = path.join(__dirname, "..", "temp");
if (!fs.existsSync(tempDir)) {
  fs.mkdirSync(tempDir, { recursive: true });
  console.log("Created temp directory for local development");
} else {
  console.log("Note: Temp directory already exists");
}

/**
 * Create logs directory for traditional deployments
 * Note: File-based logging disabled for serverless compatibility
 * In serverless environments, use console logging and external services
 * for log persistence (e.g., Vercel Analytics, CloudWatch)
 */
const logsDir = path.join(__dirname, "..", "logs");
if (!fs.existsSync(logsDir)) {
  fs.mkdirSync(logsDir, { recursive: true });
  console.log("Created logs directory for traditional deployments");
} else {
  console.log("Note: Logs directory already exists");
}

console.log("Note: File-based logging disabled for serverless compatibility");
console.log("Use structured JSON logging for production environments");

console.log("\nSetup complete!");
console.log("\nNext steps:");
console.log("1. Review and modify .env file if needed");
console.log('2. Run "npm run dev" to start development server');
console.log('3. Run "npm run security:check" to verify security');
console.log("4. Visit http://localhost:3000 to test the application");
console.log("5. Explore API documentation at http://localhost:3000/api-docs\n");

/**
 * Run initial security audit
 * Checks for known vulnerabilities in dependencies
 * Provides recommendations for security improvements
 */
console.log("Running security check...");
const { exec } = require("child_process");
exec("npm audit --audit-level moderate", (error, stdout, stderr) => {
  if (error) {
    console.log('Security audit found issues. Run "npm audit fix" to resolve.');
    console.log("For production deployments, ensure zero vulnerabilities.");
  } else {
    console.log("No moderate or high security vulnerabilities found");
    console.log("Security status: PASSED");
  }

  console.log("\nSecurity recommendations:");
  console.log("- Run 'npm audit' regularly to check for vulnerabilities");
  console.log("- Keep dependencies updated with 'npm update'");
  console.log("- Use 'npm audit fix' to automatically resolve issues");
  console.log("- Monitor security advisories for critical packages");
});
