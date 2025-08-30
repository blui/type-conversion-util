// Test setup file
const fs = require("fs");
const path = require("path");

// Set test environment variables
process.env.NODE_ENV = "test";
process.env.PORT = "0"; // Use random port for tests
process.env.TEMP_DIR = "./temp-test";

// Create test temp directory
const testTempDir = "./temp-test";
if (!fs.existsSync(testTempDir)) {
  fs.mkdirSync(testTempDir, { recursive: true });
}

// Global test cleanup
afterAll(() => {
  // Clean up test temp directory
  if (fs.existsSync(testTempDir)) {
    try {
      fs.rmSync(testTempDir, { recursive: true, force: true });
    } catch (error) {
      console.warn("Failed to clean up test temp directory:", error);
    }
  }
});
