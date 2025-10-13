/**
 * Self-Signed SSL Certificate Generator
 *
 * Generates self-signed SSL certificates for internal server deployment.
 * Certificates are valid for 365 days and suitable for intranet use only.
 *
 * Usage: node scripts/generate-ssl-cert.js
 *
 * Output:
 * - ssl/server.key: Private key (keep secure)
 * - ssl/server.cert: Self-signed certificate
 *
 * Configuration:
 * - Common Name (CN): Set via SSL_COMMON_NAME environment variable
 * - Organization: Set via SSL_ORGANIZATION environment variable
 * - Validity: 365 days (configurable via SSL_DAYS)
 *
 * Security Notes:
 * - Self-signed certificates are for internal use only
 * - Clients must trust the certificate or disable verification
 * - Private key file will have restrictive permissions (600)
 */

const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');
const os = require('os');

const SSL_DIR = path.join(__dirname, '..', 'ssl');
const KEY_PATH = path.join(SSL_DIR, 'server.key');
const CERT_PATH = path.join(SSL_DIR, 'server.cert');
const CSR_PATH = path.join(SSL_DIR, 'server.csr');

// Certificate configuration
const COMMON_NAME = process.env.SSL_COMMON_NAME || 'localhost';
const ORGANIZATION = process.env.SSL_ORGANIZATION || 'Internal Development';
const COUNTRY = process.env.SSL_COUNTRY || 'US';
const STATE = process.env.SSL_STATE || 'State';
const LOCALITY = process.env.SSL_LOCALITY || 'City';
const DAYS = parseInt(process.env.SSL_DAYS || '365', 10);

console.log('╔══════════════════════════════════════════════╗');
console.log('║   Self-Signed SSL Certificate Generator     ║');
console.log('╚══════════════════════════════════════════════╝\n');

/**
 * Check if OpenSSL is available
 */
function checkOpenSSL() {
  try {
    execSync('openssl version', { stdio: 'pipe' });
    return true;
  } catch (error) {
    return false;
  }
}

/**
 * Generate certificate using OpenSSL
 */
function generateWithOpenSSL() {
  console.log('[1/3] Generating private key...\n');

  // Generate 2048-bit RSA private key
  execSync(`openssl genrsa -out "${KEY_PATH}" 2048`, { stdio: 'inherit' });

  console.log('[2/3] Generating certificate signing request...\n');

  // Generate CSR
  const subject = `/C=${COUNTRY}/ST=${STATE}/L=${LOCALITY}/O=${ORGANIZATION}/CN=${COMMON_NAME}`;
  execSync(
    `openssl req -new -key "${KEY_PATH}" -out "${CSR_PATH}" -subj "${subject}"`,
    { stdio: 'inherit' }
  );

  console.log('[3/3] Generating self-signed certificate...\n');

  // Generate self-signed certificate
  execSync(
    `openssl x509 -req -days ${DAYS} -in "${CSR_PATH}" -signkey "${KEY_PATH}" -out "${CERT_PATH}"`,
    { stdio: 'inherit' }
  );

  // Clean up CSR
  if (fs.existsSync(CSR_PATH)) {
    fs.unlinkSync(CSR_PATH);
  }
}

/**
 * Generate certificate using PowerShell (Windows fallback)
 */
function generateWithPowerShell() {
  console.log('[1/2] Generating self-signed certificate with PowerShell...\n');

  const psScript = `
    $cert = New-SelfSignedCertificate \`
      -DnsName "${COMMON_NAME}" \`
      -CertStoreLocation "Cert:\\CurrentUser\\My" \`
      -NotAfter (Get-Date).AddDays(${DAYS}) \`
      -KeyAlgorithm RSA \`
      -KeyLength 2048

    $pwd = ConvertTo-SecureString -String "temp" -Force -AsPlainText

    $pfxPath = "${SSL_DIR}\\temp.pfx"
    Export-PfxCertificate -Cert "Cert:\\CurrentUser\\My\\$($cert.Thumbprint)" \`
      -FilePath $pfxPath \`
      -Password $pwd

    Remove-Item "Cert:\\CurrentUser\\My\\$($cert.Thumbprint)"

    Write-Output $pfxPath
  `;

  try {
    const pfxPath = execSync(`powershell -command "${psScript.replace(/\n/g, ' ')}"`, {
      encoding: 'utf8',
      stdio: 'pipe'
    }).trim();

    console.log('[2/2] Extracting key and certificate...\n');

    // Convert PFX to PEM using PowerShell or manual extraction
    console.log('Warning: Manual conversion required.');
    console.log(`PFX file created at: ${pfxPath}`);
    console.log('Use OpenSSL to extract key and certificate:');
    console.log(`  openssl pkcs12 -in "${pfxPath}" -nocerts -out "${KEY_PATH}" -nodes`);
    console.log(`  openssl pkcs12 -in "${pfxPath}" -clcerts -nokeys -out "${CERT_PATH}"`);
    return false;
  } catch (error) {
    throw new Error(`PowerShell certificate generation failed: ${error.message}`);
  }
}

/**
 * Set secure file permissions (Unix-like systems)
 */
function setPermissions() {
  if (os.platform() !== 'win32') {
    try {
      fs.chmodSync(KEY_PATH, 0o600);
      console.log('Set private key permissions to 600\n');
    } catch (error) {
      console.warn('Warning: Could not set file permissions:', error.message);
    }
  }
}

/**
 * Display certificate information
 */
function displayInfo() {
  console.log('╔══════════════════════════════════════════════╗');
  console.log('║   Certificate Generated Successfully         ║');
  console.log('╚══════════════════════════════════════════════╝\n');

  console.log('Certificate Details:');
  console.log(`  Common Name: ${COMMON_NAME}`);
  console.log(`  Organization: ${ORGANIZATION}`);
  console.log(`  Valid for: ${DAYS} days`);
  console.log('');

  console.log('Files Created:');
  console.log(`  Private Key: ${KEY_PATH}`);
  console.log(`  Certificate: ${CERT_PATH}`);
  console.log('');

  console.log('Next Steps:');
  console.log('');
  console.log('1. Enable SSL in .env:');
  console.log('   SSL_ENABLED=true');
  console.log('   SSL_KEY_PATH=ssl/server.key');
  console.log('   SSL_CERT_PATH=ssl/server.cert');
  console.log('');
  console.log('2. Start server:');
  console.log('   npm start');
  console.log('');
  console.log('3. Access via HTTPS:');
  console.log(`   https://${COMMON_NAME}:3000`);
  console.log('');
  console.log('Note: Browsers will show security warnings for self-signed certificates.');
  console.log('For internal use, add exception or import certificate to trusted store.');
  console.log('');
}

/**
 * Main execution
 */
function main() {
  try {
    // Create SSL directory
    if (!fs.existsSync(SSL_DIR)) {
      fs.mkdirSync(SSL_DIR, { recursive: true });
      console.log(`Created SSL directory: ${SSL_DIR}\n`);
    }

    // Check if certificates already exist
    if (fs.existsSync(KEY_PATH) && fs.existsSync(CERT_PATH)) {
      console.log('SSL certificates already exist.\n');
      console.log('To regenerate, delete existing certificates:');
      console.log(`  ${KEY_PATH}`);
      console.log(`  ${CERT_PATH}\n`);
      process.exit(0);
    }

    // Generate certificates
    if (checkOpenSSL()) {
      console.log('Using OpenSSL for certificate generation\n');
      generateWithOpenSSL();
      setPermissions();
      displayInfo();
    } else if (os.platform() === 'win32') {
      console.log('OpenSSL not found. Using PowerShell (Windows)\n');
      const success = generateWithPowerShell();
      if (!success) {
        console.log('\nInstall OpenSSL for automatic certificate generation:');
        console.log('  https://slproweb.com/products/Win32OpenSSL.html');
        process.exit(1);
      }
      displayInfo();
    } else {
      throw new Error('OpenSSL not found. Please install OpenSSL to generate certificates.');
    }

  } catch (error) {
    console.error('\nCertificate generation failed:', error.message);
    console.error('\nManual generation:');
    console.error('  openssl req -x509 -newkey rsa:2048 -keyout server.key -out server.cert -days 365 -nodes');
    process.exit(1);
  }
}

main();
