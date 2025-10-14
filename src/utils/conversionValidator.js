/**
 * Post-Conversion Validation Utility
 *
 * Performs quality checks on converted documents to detect:
 * - Page count discrepancies
 * - Missing content
 * - Font embedding issues
 * - File size anomalies
 * - Text extraction failures
 */

const fs = require('fs');
const path = require('path');
const pdfParse = require('pdf-parse');

class ConversionValidator {
  // Validation thresholds and constants
  static THRESHOLDS = {
    MIN_PDF_SIZE_BYTES: 10000, // Minimum PDF size (10KB) - smaller may indicate conversion failure
    MAX_PDF_SIZE_MULTIPLIER: 10, // PDF should not be more than 10x the source size
    MAX_TEXT_EXTRACTION_RATIO: 0.5, // Maximum ratio of extracted text to file size
  };

  /**
   * Validate a DOCX to PDF conversion
   *
   * @param {string} docxPath - Original DOCX file
   * @param {string} pdfPath - Generated PDF file
   * @returns {Promise<Object>} Validation result
   */
  async validateDocxToPdf(docxPath, pdfPath) {
    const issues = [];
    const warnings = [];
    const info = {};

    try {
      // Check files exist
      if (!fs.existsSync(docxPath)) {
        issues.push('Source DOCX file not found');
        return { valid: false, issues, warnings, info };
      }

      if (!fs.existsSync(pdfPath)) {
        issues.push('Output PDF file not found');
        return { valid: false, issues, warnings, info };
      }

      // Get file sizes
      const docxStats = fs.statSync(docxPath);
      const pdfStats = fs.statSync(pdfPath);

      info.docxSize = (docxStats.size / 1024).toFixed(2) + ' KB';
      info.pdfSize = (pdfStats.size / 1024).toFixed(2) + ' KB';

      // Parse PDF for detailed analysis
      const pdfBuffer = fs.readFileSync(pdfPath);
      const pdfData = await pdfParse(pdfBuffer);

      info.pdfPages = pdfData.numpages;
      info.pdfVersion = pdfData.version || 'Unknown';

      // Check if PDF is suspiciously small
      if (pdfStats.size < this.THRESHOLDS.MIN_PDF_SIZE_BYTES) {
        warnings.push('PDF file is very small - conversion may have failed');
      }

      // Check if PDF is suspiciously large
      if (pdfStats.size > docxStats.size * this.THRESHOLDS.MAX_PDF_SIZE_MULTIPLIER) {
        warnings.push('PDF is significantly larger than source - possible image quality issue');
      }

      // Extract text for content validation
      const extractedText = pdfData.text || '';
      const wordCount = extractedText.trim().split(/\s+/).length;

      info.extractedWordCount = wordCount;

      // Check if document appears empty
      if (wordCount < 10) {
        issues.push('Very little text extracted from PDF - conversion likely failed');
      } else if (wordCount < 50) {
        warnings.push('Low word count detected - verify conversion completeness');
      }

      // Check for common error indicators in text
      const errorPatterns = [
        /error/i,
        /failed/i,
        /exception/i,
        /could not/i,
        /unable to/i
      ];

      const firstPage = extractedText.substring(0, 500);
      for (const pattern of errorPatterns) {
        if (pattern.test(firstPage)) {
          warnings.push('Potential error message detected in PDF content');
          break;
        }
      }

      // Font embedding check (basic)
      // LibreOffice should embed fonts, but we can check file size as proxy
      const sizeRatio = pdfStats.size / docxStats.size;
      if (sizeRatio < 0.1) {
        warnings.push('PDF much smaller than DOCX - fonts may not be properly embedded');
      }

      // Page count estimation (very rough)
      // Average DOCX is ~20KB per page, PDF is ~50KB per page
      const estimatedDocxPages = Math.max(1, Math.round(docxStats.size / 20480));
      const actualPdfPages = pdfData.numpages;

      info.estimatedSourcePages = estimatedDocxPages;
      info.actualPdfPages = actualPdfPages;

      const pageDifference = Math.abs(actualPdfPages - estimatedDocxPages);
      const pageDiscrepancyPercent = (pageDifference / estimatedDocxPages) * 100;

      if (pageDiscrepancyPercent > 20) {
        warnings.push(`Page count may be off - estimated ${estimatedDocxPages} pages, got ${actualPdfPages} pages`);
      }

      // Check for metadata
      if (pdfData.info) {
        info.pdfProducer = pdfData.info.Producer || 'Unknown';
        info.pdfCreator = pdfData.info.Creator || 'Unknown';

        // Verify LibreOffice produced this
        if (info.pdfProducer && !info.pdfProducer.includes('LibreOffice')) {
          warnings.push('PDF not produced by LibreOffice - unexpected conversion method');
        }
      }

      // Determine overall validity
      const valid = issues.length === 0;

      return {
        valid,
        issues,
        warnings,
        info,
        recommendation: this._getRecommendation(valid, issues, warnings)
      };

    } catch (error) {
      issues.push(`Validation error: ${error.message}`);
      return {
        valid: false,
        issues,
        warnings,
        info,
        recommendation: 'Could not complete validation - check PDF file integrity'
      };
    }
  }

  /**
   * Get recommendation based on validation results
   *
   * @param {boolean} valid - Overall validity
   * @param {Array} issues - Critical issues
   * @param {Array} warnings - Non-critical warnings
   * @returns {string} Recommendation
   * @private
   */
  _getRecommendation(valid, issues, warnings) {
    if (issues.length > 0) {
      return 'REJECT: Critical issues detected - conversion failed';
    }

    if (warnings.length === 0) {
      return 'ACCEPT: Conversion appears successful with no warnings';
    }

    if (warnings.length <= 2) {
      return 'REVIEW: Minor warnings detected - manual review recommended';
    }

    return 'CAUTION: Multiple warnings detected - verify output quality';
  }

  /**
   * Compare two PDFs (original vs converted)
   *
   * @param {string} expectedPdfPath - Original/expected PDF
   * @param {string} actualPdfPath - Converted PDF to validate
   * @returns {Promise<Object>} Comparison result
   */
  async comparePdfs(expectedPdfPath, actualPdfPath) {
    const differences = [];
    const info = {};

    try {
      if (!fs.existsSync(expectedPdfPath)) {
        throw new Error('Expected PDF not found');
      }

      if (!fs.existsSync(actualPdfPath)) {
        throw new Error('Actual PDF not found');
      }

      // Parse both PDFs
      const expectedBuffer = fs.readFileSync(expectedPdfPath);
      const actualBuffer = fs.readFileSync(actualPdfPath);

      const expectedData = await pdfParse(expectedBuffer);
      const actualData = await pdfParse(actualBuffer);

      info.expectedPages = expectedData.numpages;
      info.actualPages = actualData.numpages;

      // Page count comparison
      if (expectedData.numpages !== actualData.numpages) {
        const diff = actualData.numpages - expectedData.numpages;
        differences.push({
          type: 'page_count',
          severity: 'high',
          message: `Page count mismatch: expected ${expectedData.numpages}, got ${actualData.numpages} (${diff > 0 ? '+' : ''}${diff})`
        });
      }

      // Text extraction comparison (basic)
      const expectedText = expectedData.text || '';
      const actualText = actualData.text || '';

      const expectedWords = expectedText.trim().split(/\s+/).length;
      const actualWords = actualText.trim().split(/\s+/).length;

      info.expectedWords = expectedWords;
      info.actualWords = actualWords;

      const wordDiff = actualWords - expectedWords;
      const wordDiffPercent = Math.abs((wordDiff / expectedWords) * 100);

      if (wordDiffPercent > 5) {
        differences.push({
          type: 'word_count',
          severity: 'medium',
          message: `Word count differs by ${wordDiffPercent.toFixed(1)}%: expected ~${expectedWords}, got ~${actualWords}`
        });
      }

      // File size comparison
      const expectedSize = fs.statSync(expectedPdfPath).size;
      const actualSize = fs.statSync(actualPdfPath).size;

      info.expectedSize = (expectedSize / 1024).toFixed(2) + ' KB';
      info.actualSize = (actualSize / 1024).toFixed(2) + ' KB';

      const sizeDiffPercent = Math.abs(((actualSize - expectedSize) / expectedSize) * 100);

      if (sizeDiffPercent > 50) {
        differences.push({
          type: 'file_size',
          severity: 'low',
          message: `File size differs significantly: expected ${info.expectedSize}, got ${info.actualSize}`
        });
      }

      // Calculate similarity score (0-100)
      const pageScore = expectedData.numpages === actualData.numpages ? 100 : 0;
      const wordScore = Math.max(0, 100 - wordDiffPercent);
      const overallScore = (pageScore * 0.7 + wordScore * 0.3);

      info.similarityScore = overallScore.toFixed(1);

      return {
        matching: differences.length === 0,
        differences,
        info,
        summary: this._getComparisonSummary(overallScore, differences)
      };

    } catch (error) {
      return {
        matching: false,
        differences: [{ type: 'error', severity: 'critical', message: error.message }],
        info,
        summary: 'Comparison failed'
      };
    }
  }

  /**
   * Get comparison summary
   *
   * @param {number} score - Similarity score (0-100)
   * @param {Array} differences - List of differences
   * @returns {string} Summary text
   * @private
   */
  _getComparisonSummary(score, differences) {
    if (score >= 95) {
      return 'Excellent match - conversion is highly accurate';
    } else if (score >= 85) {
      return 'Good match - minor differences acceptable';
    } else if (score >= 70) {
      return 'Fair match - noticeable differences present';
    } else {
      return 'Poor match - significant differences detected';
    }
  }

  /**
   * Format validation report for console output
   *
   * @param {Object} result - Validation result
   * @returns {string} Formatted report
   */
  formatReport(result) {
    let report = '\n';
    report += '╔══════════════════════════════════════════════════════════╗\n';
    report += '║   Conversion Validation Report                           ║\n';
    report += '╚══════════════════════════════════════════════════════════╝\n\n';

    // Status
    const status = result.valid ? ' VALID' : ' INVALID';
    report += `Status: ${status}\n`;
    report += `Recommendation: ${result.recommendation}\n\n`;

    // Info
    if (Object.keys(result.info).length > 0) {
      report += 'Document Info:\n';
      for (const [key, value] of Object.entries(result.info)) {
        report += `  • ${key}: ${value}\n`;
      }
      report += '\n';
    }

    // Issues
    if (result.issues.length > 0) {
      report += ' Critical Issues:\n';
      result.issues.forEach(issue => {
        report += `  • ${issue}\n`;
      });
      report += '\n';
    }

    // Warnings
    if (result.warnings.length > 0) {
      report += '  Warnings:\n';
      result.warnings.forEach(warning => {
        report += `  • ${warning}\n`;
      });
      report += '\n';
    }

    if (result.issues.length === 0 && result.warnings.length === 0) {
      report += ' No issues detected\n\n';
    }

    return report;
  }
}

module.exports = new ConversionValidator();
