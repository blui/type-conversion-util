/**
 * DOCX Conversion Service
 *
 * DOCX-related conversions using high-fidelity engine.
 * Delegates to conversionEngine for LibreOffice-based processing.
 *
 * Operations:
 * - DOCX to PDF (high-fidelity via LibreOffice)
 * - PDF to DOCX (high-fidelity via LibreOffice)
 * - TXT to DOCX
 */

const fs = require('fs');
const { Document, Packer, Paragraph, TextRun } = require('docx');

class DocxService {
  /**
   * Generate DOCX from plain text
   *
   * @param {string} inputPath - Path to TXT file
   * @param {string} outputPath - Path for DOCX file
   * @returns {Promise<Object>} Conversion result
   */
  async fromText(inputPath, outputPath) {
    try {
      const textContent = fs.readFileSync(inputPath, 'utf8');

      const doc = new Document({
        sections: [
          {
            properties: {},
            children: textContent.split('\n').map(line =>
              new Paragraph({
                children: [new TextRun(line || ' ')]
              })
            )
          }
        ]
      });

      const buffer = await Packer.toBuffer(doc);
      fs.writeFileSync(outputPath, buffer);

      return { success: true, outputPath };
    } catch (error) {
      throw new Error(`Text to DOCX conversion failed: ${error.message}`);
    }
  }
}

module.exports = new DocxService();
