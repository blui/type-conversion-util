/**
 * Document Conversion Service
 *
 * Main orchestrator for document conversions.
 * Delegates to specialized services for focused operations.
 *
 * Architecture:
 * - pdfService: PDF generation and extraction
 * - docxService: DOCX operations
 * - spreadsheetService: XLSX/CSV operations
 * - conversionEngine: High-fidelity LibreOffice conversions
 *
 * Supported Conversions:
 * - PDF <-> DOCX (via LibreOffice)
 * - PDF -> TXT
 * - XLSX <-> CSV
 * - XLSX -> PDF
 * - PPTX -> PDF
 * - TXT -> PDF, DOCX
 * - XML -> PDF
 */

const pdfService = require('./document/pdfService');
const docxService = require('./document/docxService');
const spreadsheetService = require('./document/spreadsheetService');
const conversionEngine = require('./conversionEngine');
const ExcelJS = require('exceljs');

class DocumentService {
  /**
   * Route conversion request to appropriate handler
   *
   * @param {string} inputPath - Path to input file
   * @param {string} outputPath - Path for output file
   * @param {string} inputFormat - Input file format
   * @param {string} targetFormat - Target file format
   * @returns {Promise<Object>} Conversion result with success status
   */
  async convert(inputPath, outputPath, inputFormat, targetFormat) {
    console.log(`Converting ${inputFormat} to ${targetFormat}`);

    try {
      const conversionKey = `${inputFormat}-${targetFormat}`;
      const handler = this._getConversionHandler(conversionKey);

      if (!handler) {
        throw new Error(`Conversion from ${inputFormat} to ${targetFormat} is not supported`);
      }

      return await handler.call(this, inputPath, outputPath);
    } catch (error) {
      console.error('Document conversion error:', error);
      return { success: false, error: error.message };
    }
  }

  /**
   * Get conversion handler for format combination
   *
   * @param {string} conversionKey - Format combination key
   * @returns {Function|null} Handler function or null
   * @private
   */
  _getConversionHandler(conversionKey) {
    const handlers = {
      'docx-pdf': this._docxToPdf,
      'pdf-docx': this._pdfToDocx,
      'pdf-txt': this._pdfToTxt,
      'xlsx-csv': this._xlsxToCsv,
      'csv-xlsx': this._csvToXlsx,
      'xlsx-pdf': this._xlsxToPdf,
      'pptx-pdf': this._pptxToPdf,
      'txt-pdf': this._txtToPdf,
      'txt-docx': this._txtToDocx,
      'xml-pdf': this._xmlToPdf
    };

    return handlers[conversionKey] || null;
  }

  /**
   * DOCX to PDF conversion (high-fidelity via LibreOffice)
   */
  async _docxToPdf(inputPath, outputPath) {
    try {
      return await conversionEngine.docxToPdfEnhanced(inputPath, outputPath);
    } catch (error) {
      console.error('DOCX to PDF conversion failed:', error);
      throw new Error(`DOCX to PDF conversion failed: ${error.message}`);
    }
  }

  /**
   * PDF to DOCX conversion (high-fidelity via LibreOffice)
   */
  async _pdfToDocx(inputPath, outputPath) {
    try {
      return await conversionEngine.pdfToDocxEnhanced(inputPath, outputPath);
    } catch (error) {
      console.error('PDF to DOCX conversion failed:', error);
      throw new Error(`PDF to DOCX conversion failed: ${error.message}`);
    }
  }

  /**
   * PDF to TXT conversion (text extraction)
   */
  async _pdfToTxt(inputPath, outputPath) {
    return await pdfService.extractText(inputPath, outputPath);
  }

  /**
   * XLSX to CSV conversion (multi-sheet support)
   */
  async _xlsxToCsv(inputPath, outputPath) {
    return await spreadsheetService.xlsxToCsv(inputPath, outputPath);
  }

  /**
   * CSV to XLSX conversion (streaming)
   */
  async _csvToXlsx(inputPath, outputPath) {
    return await spreadsheetService.csvToXlsx(inputPath, outputPath);
  }

  /**
   * XLSX to PDF conversion
   */
  async _xlsxToPdf(inputPath, outputPath) {
    const workbook = new ExcelJS.Workbook();
    await workbook.xlsx.readFile(inputPath);
    const worksheet = workbook.getWorksheet(1);

    if (!worksheet) {
      throw new Error('No worksheets found in Excel file');
    }

    const data = spreadsheetService.extractWorksheetData(worksheet);
    return await pdfService.fromSpreadsheet(data, outputPath);
  }

  /**
   * PPTX to PDF conversion (limited support)
   */
  async _pptxToPdf(inputPath, outputPath) {
    return await pdfService.fromPresentation(inputPath, outputPath);
  }

  /**
   * TXT to PDF conversion
   */
  async _txtToPdf(inputPath, outputPath) {
    return await pdfService.fromText(inputPath, outputPath);
  }

  /**
   * TXT to DOCX conversion
   */
  async _txtToDocx(inputPath, outputPath) {
    return await docxService.fromText(inputPath, outputPath);
  }

  /**
   * XML to PDF conversion
   */
  async _xmlToPdf(inputPath, outputPath) {
    return await pdfService.fromXml(inputPath, outputPath);
  }
}

module.exports = new DocumentService();
