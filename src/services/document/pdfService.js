/**
 * PDF Conversion Service
 *
 * PDF-related conversions including generation and extraction.
 * Specialized PDF operations for text, XML, and other formats.
 *
 * Operations:
 * - PDF to TXT (text extraction)
 * - TXT to PDF
 * - XML to PDF
 * - XLSX to PDF
 * - PPTX to PDF (limited support)
 */

const fs = require('fs');
const path = require('path');
const PDFDocument = require('pdfkit');
const pdfParse = require('pdf-parse');

class PdfService {
  /**
   * Extract text content from PDF file
   *
   * @param {string} inputPath - Path to PDF file
   * @param {string} outputPath - Path for TXT file
   * @returns {Promise<Object>} Conversion result
   */
  async extractText(inputPath, outputPath) {
    try {
      const pdfBuffer = fs.readFileSync(inputPath);
      const data = await pdfParse(pdfBuffer);
      fs.writeFileSync(outputPath, data.text);
      return { success: true, outputPath };
    } catch (error) {
      throw new Error(`PDF text extraction failed: ${error.message}`);
    }
  }

  /**
   * Generate PDF from plain text
   *
   * @param {string} inputPath - Path to TXT file
   * @param {string} outputPath - Path for PDF file
   * @returns {Promise<Object>} Conversion result
   */
  async fromText(inputPath, outputPath) {
    try {
      const textContent = fs.readFileSync(inputPath, 'utf8');
      const doc = new PDFDocument();
      doc.pipe(fs.createWriteStream(outputPath));

      doc.fontSize(12);
      doc.text(textContent, 50, 50, {
        width: 500,
        align: 'left'
      });

      doc.end();

      return new Promise((resolve, reject) => {
        doc.on('end', () => resolve({ success: true, outputPath }));
        doc.on('error', (error) => reject(new Error(`Text to PDF conversion failed: ${error.message}`)));
      });
    } catch (error) {
      throw new Error(`Text to PDF conversion failed: ${error.message}`);
    }
  }

  /**
   * Generate PDF from XML content
   *
   * @param {string} inputPath - Path to XML file
   * @param {string} outputPath - Path for PDF file
   * @returns {Promise<Object>} Conversion result
   */
  async fromXml(inputPath, outputPath) {
    try {
      const xmlContent = fs.readFileSync(inputPath, 'utf8');
      const doc = new PDFDocument();
      doc.pipe(fs.createWriteStream(outputPath));

      doc.fontSize(10);
      doc.text(xmlContent, 50, 50, {
        width: 500,
        align: 'left'
      });

      doc.end();

      return new Promise((resolve, reject) => {
        doc.on('end', () => resolve({ success: true, outputPath }));
        doc.on('error', (error) => reject(new Error(`XML to PDF conversion failed: ${error.message}`)));
      });
    } catch (error) {
      throw new Error(`XML to PDF conversion failed: ${error.message}`);
    }
  }

  /**
   * Generate PDF from spreadsheet data
   *
   * @param {Array} data - Spreadsheet data rows
   * @param {string} outputPath - Path for PDF file
   * @returns {Promise<Object>} Conversion result
   */
  async fromSpreadsheet(data, outputPath) {
    try {
      const doc = new PDFDocument({
        size: 'A4',
        layout: 'landscape',
        margins: { top: 36, bottom: 36, left: 36, right: 36 }
      });
      doc.pipe(fs.createWriteStream(outputPath));

      doc.fontSize(10);

      data.forEach((row, rowIndex) => {
        const rowText = row.join('  |  ');
        doc.text(rowText, {
          width: doc.page.width - 72,
          align: 'left'
        });

        if (rowIndex === 0) {
          doc.moveDown(0.5);
          doc.text('-'.repeat(80));
          doc.moveDown(0.5);
        }
      });

      doc.end();

      return new Promise((resolve, reject) => {
        doc.on('end', () => resolve({ success: true, outputPath }));
        doc.on('error', (error) => reject(new Error(`Spreadsheet to PDF conversion failed: ${error.message}`)));
      });
    } catch (error) {
      throw new Error(`Spreadsheet to PDF conversion failed: ${error.message}`);
    }
  }

  /**
   * Generate placeholder PDF for PPTX files
   * Limited support - creates informational PDF
   *
   * @param {string} inputPath - Path to PPTX file
   * @param {string} outputPath - Path for PDF file
   * @returns {Promise<Object>} Conversion result
   */
  async fromPresentation(inputPath, outputPath) {
    try {
      const doc = new PDFDocument();
      doc.pipe(fs.createWriteStream(outputPath));

      doc.fontSize(16);
      doc.text('PowerPoint Presentation', 50, 50);
      doc.fontSize(12);
      doc.text('Note: This is a simplified conversion from PPTX format.', 50, 100);
      doc.text('For full PPTX support, consider using a specialized service.', 50, 120);
      doc.text(`Original file: ${path.basename(inputPath)}`, 50, 160);

      doc.end();

      return new Promise((resolve, reject) => {
        doc.on('end', () => resolve({ success: true, outputPath }));
        doc.on('error', (error) => reject(new Error(`PPTX to PDF conversion failed: ${error.message}`)));
      });
    } catch (error) {
      throw new Error(`PPTX to PDF conversion failed: ${error.message}`);
    }
  }
}

module.exports = new PdfService();
