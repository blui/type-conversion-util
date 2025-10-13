/**
 * Cloud Conversion Service
 *
 * Provides high-fidelity DOCX to PDF conversion using cloud APIs.
 * Uses CloudConvert API for professional-grade conversions.
 *
 * Free tier: 25 conversions/day
 * Paid tier: ~$0.01-0.02 per conversion
 *
 * Requires: CLOUDCONVERT_API_KEY in .env
 *
 * Benefits over LibreOffice:
 * - 99% fidelity (uses licensed conversion engines)
 * - Better font handling
 * - Accurate page counts
 * - Theme color preservation
 * - Complex formatting support
 */

const fs = require('fs');
const https = require('https');
const http = require('http');

class CloudConversionService {
  constructor() {
    this.apiKey = process.env.CLOUDCONVERT_API_KEY;
    this.apiUrl = 'https://api.cloudconvert.com/v2';
    this.enabled = !!this.apiKey;
  }

  /**
   * Check if cloud conversion is available
   *
   * @returns {boolean} True if API key is configured
   */
  isAvailable() {
    return this.enabled;
  }

  /**
   * Convert DOCX to PDF using CloudConvert
   *
   * @param {string} inputPath - Path to DOCX file
   * @param {string} outputPath - Path for output PDF
   * @returns {Promise<Object>} Conversion result
   */
  async convertDocxToPdf(inputPath, outputPath) {
    if (!this.enabled) {
      throw new Error('CloudConvert API key not configured. Set CLOUDCONVERT_API_KEY in .env');
    }

    try {
      console.log('Converting via CloudConvert API (high-fidelity)...');

      // Step 1: Create conversion job
      const job = await this._createJob(inputPath);

      // Step 2: Upload file
      await this._uploadFile(job.uploadUrl, inputPath);

      // Step 3: Wait for conversion
      const result = await this._waitForCompletion(job.jobId);

      // Step 4: Download result
      await this._downloadFile(result.downloadUrl, outputPath);

      console.log('CloudConvert conversion successful');

      return {
        success: true,
        outputPath,
        fidelity: '99%',
        method: 'cloudconvert-api',
        cost: result.credits || 'unknown'
      };

    } catch (error) {
      console.error('CloudConvert conversion error:', error);
      throw new Error(`Cloud conversion failed: ${error.message}`);
    }
  }

  /**
   * Create conversion job
   *
   * @param {string} inputPath - Input file path
   * @returns {Promise<Object>} Job details
   * @private
   */
  async _createJob(inputPath) {
    const jobData = {
      tasks: {
        'import-file': {
          operation: 'import/upload'
        },
        'convert-file': {
          operation: 'convert',
          input: 'import-file',
          input_format: 'docx',
          output_format: 'pdf',
          engine: 'office',  // Use office engine for best quality
          engine_version: 'latest',
          // PDF conversion options for maximum fidelity
          pdf_options: {
            embed_fonts: true,
            pdf_version: '1.7',
            image_quality: 100,
            image_dpi: 300
          }
        },
        'export-file': {
          operation: 'export/url',
          input: 'convert-file'
        }
      }
    };

    const response = await this._apiRequest('POST', '/jobs', jobData);

    if (!response || !response.data) {
      throw new Error('Failed to create conversion job');
    }

    const uploadTask = response.data.tasks.find(t => t.name === 'import-file');
    const jobId = response.data.id;

    return {
      jobId,
      uploadUrl: uploadTask.result.form.url,
      uploadParams: uploadTask.result.form.parameters
    };
  }

  /**
   * Upload file to CloudConvert
   *
   * @param {string} uploadUrl - Upload URL from job creation
   * @param {string} filePath - File to upload
   * @returns {Promise<void>}
   * @private
   */
  async _uploadFile(uploadUrl, filePath) {
    return new Promise((resolve, reject) => {
      const fileStream = fs.createReadStream(filePath);
      const url = new URL(uploadUrl);

      const options = {
        method: 'POST',
        hostname: url.hostname,
        path: url.pathname + url.search,
        headers: {
          'Content-Type': 'application/octet-stream',
          'Content-Length': fs.statSync(filePath).size
        }
      };

      const protocol = url.protocol === 'https:' ? https : http;
      const req = protocol.request(options, (res) => {
        if (res.statusCode >= 200 && res.statusCode < 300) {
          resolve();
        } else {
          reject(new Error(`Upload failed with status ${res.statusCode}`));
        }
      });

      req.on('error', reject);
      fileStream.pipe(req);
    });
  }

  /**
   * Wait for conversion to complete
   *
   * @param {string} jobId - Job ID
   * @returns {Promise<Object>} Job result
   * @private
   */
  async _waitForCompletion(jobId) {
    const maxAttempts = 60; // 5 minutes max (5 second intervals)
    let attempts = 0;

    while (attempts < maxAttempts) {
      const response = await this._apiRequest('GET', `/jobs/${jobId}`);

      if (!response || !response.data) {
        throw new Error('Failed to check job status');
      }

      const job = response.data;

      if (job.status === 'finished') {
        const exportTask = job.tasks.find(t => t.name === 'export-file');

        if (!exportTask || !exportTask.result || !exportTask.result.files[0]) {
          throw new Error('Export task failed or no output file');
        }

        return {
          downloadUrl: exportTask.result.files[0].url,
          credits: job.credits
        };
      }

      if (job.status === 'error') {
        const errorTask = job.tasks.find(t => t.status === 'error');
        const errorMsg = errorTask ? errorTask.message : 'Unknown error';
        throw new Error(`Conversion failed: ${errorMsg}`);
      }

      // Wait 5 seconds before next check
      await new Promise(resolve => setTimeout(resolve, 5000));
      attempts++;
    }

    throw new Error('Conversion timeout - job did not complete in time');
  }

  /**
   * Download converted file
   *
   * @param {string} downloadUrl - Download URL
   * @param {string} outputPath - Output file path
   * @returns {Promise<void>}
   * @private
   */
  async _downloadFile(downloadUrl, outputPath) {
    return new Promise((resolve, reject) => {
      const file = fs.createWriteStream(outputPath);
      const url = new URL(downloadUrl);

      const protocol = url.protocol === 'https:' ? https : http;

      protocol.get(downloadUrl, (response) => {
        if (response.statusCode !== 200) {
          reject(new Error(`Download failed with status ${response.statusCode}`));
          return;
        }

        response.pipe(file);

        file.on('finish', () => {
          file.close();
          resolve();
        });

        file.on('error', (err) => {
          fs.unlink(outputPath, () => {});
          reject(err);
        });
      }).on('error', reject);
    });
  }

  /**
   * Make API request to CloudConvert
   *
   * @param {string} method - HTTP method
   * @param {string} endpoint - API endpoint
   * @param {Object} data - Request data
   * @returns {Promise<Object>} Response data
   * @private
   */
  async _apiRequest(method, endpoint, data = null) {
    return new Promise((resolve, reject) => {
      const options = {
        hostname: 'api.cloudconvert.com',
        port: 443,
        path: `/v2${endpoint}`,
        method: method,
        headers: {
          'Authorization': `Bearer ${this.apiKey}`,
          'Content-Type': 'application/json'
        }
      };

      const req = https.request(options, (res) => {
        let responseData = '';

        res.on('data', (chunk) => {
          responseData += chunk;
        });

        res.on('end', () => {
          try {
            const parsed = JSON.parse(responseData);

            if (res.statusCode >= 200 && res.statusCode < 300) {
              resolve(parsed);
            } else {
              reject(new Error(`API error: ${parsed.message || responseData}`));
            }
          } catch (err) {
            reject(new Error(`Invalid API response: ${responseData}`));
          }
        });
      });

      req.on('error', reject);

      if (data) {
        req.write(JSON.stringify(data));
      }

      req.end();
    });
  }
}

module.exports = new CloudConversionService();
