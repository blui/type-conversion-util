/**
 * SSL/TLS Configuration
 *
 * Manages SSL certificate loading and HTTPS server configuration.
 * Supports self-signed certificates for internal deployment.
 *
 * Certificate Setup:
 * 1. Place certificate files in project root or specify paths in .env
 * 2. Configure SSL_KEY_PATH and SSL_CERT_PATH environment variables
 * 3. For self-signed certs, set NODE_TLS_REJECT_UNAUTHORIZED=0 in clients
 *
 * Security Considerations:
 * - Self-signed certificates are suitable for internal networks only
 * - Production deployments should use CA-signed certificates
 * - Certificate files must have restrictive permissions (400 or 600)
 */

const fs = require('fs');
const path = require('path');

class SslConfig {
  constructor() {
    this.enabled = process.env.SSL_ENABLED === 'true';
    this.keyPath = process.env.SSL_KEY_PATH || path.join(__dirname, '../../ssl/server.key');
    this.certPath = process.env.SSL_CERT_PATH || path.join(__dirname, '../../ssl/server.cert');
    this.caPath = process.env.SSL_CA_PATH || null;
    this.passphrase = process.env.SSL_PASSPHRASE || null;

    // Accept self-signed certificates setting
    // If ACCEPT_SELF_SIGNED_CERTS is true, don't reject unauthorized certs
    this.acceptSelfSigned = process.env.ACCEPT_SELF_SIGNED_CERTS === 'true';
    this.rejectUnauthorized = !this.acceptSelfSigned;
  }

  /**
   * Load SSL credentials from filesystem
   *
   * @returns {Object|null} SSL credentials or null if disabled
   */
  getCredentials() {
    if (!this.enabled) {
      console.log('SSL/TLS: Disabled (using HTTP)');
      return null;
    }

    try {
      // Verify certificate files exist
      if (!fs.existsSync(this.keyPath)) {
        throw new Error(`SSL key file not found: ${this.keyPath}`);
      }

      if (!fs.existsSync(this.certPath)) {
        throw new Error(`SSL certificate file not found: ${this.certPath}`);
      }

      // Load credentials
      const credentials = {
        key: fs.readFileSync(this.keyPath, 'utf8'),
        cert: fs.readFileSync(this.certPath, 'utf8')
      };

      // Load CA bundle if specified (for certificate chains)
      if (this.caPath && fs.existsSync(this.caPath)) {
        credentials.ca = fs.readFileSync(this.caPath, 'utf8');
        console.log(`SSL/TLS: Loaded CA bundle from ${this.caPath}`);
      }

      // Add passphrase if specified
      if (this.passphrase) {
        credentials.passphrase = this.passphrase;
      }

      // Log certificate info
      console.log('SSL/TLS Configuration:');
      console.log(`  Key: ${this.keyPath}`);
      console.log(`  Certificate: ${this.certPath}`);
      console.log(`  Reject Unauthorized: ${this.rejectUnauthorized}`);

      return credentials;
    } catch (error) {
      console.error('SSL/TLS Configuration Error:', error.message);
      console.error('\nTo generate self-signed certificates:');
      console.error('  node scripts/generate-ssl-cert.js');
      throw error;
    }
  }

  /**
   * Get HTTPS server options
   *
   * @returns {Object|null} HTTPS options or null if disabled
   */
  getHttpsOptions() {
    const credentials = this.getCredentials();
    if (!credentials) {
      return null;
    }

    return {
      ...credentials,
      // Security options for self-signed certificates
      requestCert: false,
      rejectUnauthorized: this.rejectUnauthorized,
      // TLS version requirements
      minVersion: 'TLSv1.2',
      maxVersion: 'TLSv1.3',
      // Cipher suites (strong ciphers only)
      ciphers: [
        'ECDHE-RSA-AES256-GCM-SHA384',
        'ECDHE-RSA-AES128-GCM-SHA256',
        'ECDHE-RSA-AES256-SHA384',
        'ECDHE-RSA-AES128-SHA256'
      ].join(':'),
      // Prefer server cipher order
      honorCipherOrder: true
    };
  }

  /**
   * Validate certificate file permissions
   * Checks private key has restrictive permissions
   *
   * @returns {boolean} True if permissions are secure
   */
  validatePermissions() {
    if (!this.enabled) return true;

    try {
      const keyStats = fs.statSync(this.keyPath);
      const mode = (keyStats.mode & parseInt('777', 8)).toString(8);

      // Check if permissions are too permissive (should be 400 or 600)
      if (mode !== '400' && mode !== '600') {
        console.warn(`Warning: SSL key permissions are ${mode}, should be 400 or 600`);
        console.warn(`Fix with: chmod 600 ${this.keyPath}`);
        return false;
      }

      return true;
    } catch (error) {
      console.error('Permission validation failed:', error.message);
      return false;
    }
  }
}

module.exports = new SslConfig();
