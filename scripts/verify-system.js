/**
 * System Verification Script
 *
 * Verifies all components are operational.
 * Run before deployment to validate configuration.
 */

const path = require('path');
const fs = require('fs');
const conversionEngine = require('../src/services/conversionEngine');

console.log('System Verification\n');
console.log('='.repeat(50) + '\n');

let exitCode = 0;

// Check 1: LibreOffice
console.log('[1/4] LibreOffice');
const loPath = conversionEngine.getLibreOfficePath();
if (loPath && fs.existsSync(loPath)) {
  console.log('      Status: OPERATIONAL');
  console.log(`      Path: ${loPath}`);
} else {
  console.log('      Status: NOT FOUND');
  console.log('      Impact: Conversions will use fallback method (reduced fidelity)');
  console.log('      Action: Run scripts/bundle-libreoffice.js');
  exitCode = 1;
}
console.log('');

// Check 2: Edge Browser
console.log('[2/4] Microsoft Edge');
try {
  const edgePath = conversionEngine.getEdgePath();
  console.log('      Status: OPERATIONAL');
  console.log(`      Path: ${edgePath}`);
} catch (error) {
  console.log('      Status: NOT FOUND');
  console.log('      Impact: Fallback conversions will fail');
  console.log('      Action: Install Microsoft Edge');
  exitCode = 1;
}
console.log('');

// Check 3: Upload Directory
console.log('[3/4] Upload Directory');
const uploadsDir = path.join(__dirname, '..', 'uploads');
if (!fs.existsSync(uploadsDir)) {
  fs.mkdirSync(uploadsDir, { recursive: true });
  console.log('      Status: CREATED');
} else {
  console.log('      Status: EXISTS');
}
console.log(`      Path: ${uploadsDir}`);
console.log('');

// Check 4: Permissions
console.log('[4/4] Write Permissions');
const testFile = path.join(uploadsDir, '.write-test');
try {
  fs.writeFileSync(testFile, 'test');
  fs.unlinkSync(testFile);
  console.log('      Status: VERIFIED');
} catch (error) {
  console.log('      Status: FAILED');
  console.log('      Error: ' + error.message);
  exitCode = 1;
}
console.log('');

// Summary
console.log('='.repeat(50));
if (exitCode === 0) {
  console.log('System Status: READY');
  console.log('\nAll components operational. Ready for deployment.\n');
} else {
  console.log('System Status: DEGRADED');
  console.log('\nAddress issues above before deploying.\n');
}

process.exit(exitCode);
