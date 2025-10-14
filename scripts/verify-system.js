/**
 * System Verification Script
 *
 * Verifies all components are operational.
 * Run before deployment to validate configuration.
 */

const path = require("path");
const fs = require("fs");
const libreOfficeService = require("../src/services/libreOfficeService");

console.log("System Verification\n");
console.log("=".repeat(50) + "\n");

let exitCode = 0;

// Check 1: LibreOffice
console.log("[1/3] LibreOffice");
try {
  const loPath = libreOfficeService.getLibreOfficePath();
  if (loPath && fs.existsSync(loPath)) {
    console.log("      Status: OPERATIONAL");
    console.log(`      Path: ${loPath}`);
  } else {
    console.log("      Status: NOT FOUND");
    console.log("      Impact: DOCX to PDF conversions will fail");
    console.log("      Action: Run scripts/bundle-libreoffice.js");
    exitCode = 1;
  }
} catch (error) {
  console.log("      Status: ERROR");
  console.log(`      Error: ${error.message}`);
  console.log("      Impact: DOCX to PDF conversions will fail");
  exitCode = 1;
}
console.log("");

// Check 2: Upload Directory
console.log("[2/3] Upload Directory");
const uploadsDir = path.join(__dirname, "..", "uploads");
if (!fs.existsSync(uploadsDir)) {
  fs.mkdirSync(uploadsDir, { recursive: true });
  console.log("      Status: CREATED");
} else {
  console.log("      Status: EXISTS");
}
console.log(`      Path: ${uploadsDir}`);
console.log("");

// Check 3: Permissions
console.log("[3/3] Write Permissions");
const testFile = path.join(uploadsDir, ".write-test");
try {
  fs.writeFileSync(testFile, "test");
  fs.unlinkSync(testFile);
  console.log("      Status: VERIFIED");
} catch (error) {
  console.log("      Status: FAILED");
  console.log("      Error: " + error.message);
  exitCode = 1;
}
console.log("");

// Summary
console.log("=".repeat(50));
if (exitCode === 0) {
  console.log("System Status: READY");
  console.log("\nAll components operational. Ready for deployment.\n");
} else {
  console.log("System Status: DEGRADED");
  console.log("\nAddress issues above before deploying.\n");
}

process.exit(exitCode);
