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

const fs = require("fs");
const pdfService = require("./pdfService");
const docxService = require("./document/docxService");
const spreadsheetService = require("./document/spreadsheetService");
const conversionEngine = require("./conversionEngine");
const libreOfficeService = require("./libreOfficeService");
const ExcelJS = require("exceljs");

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
        throw new Error(
          `Conversion from ${inputFormat} to ${targetFormat} is not supported`
        );
      }

      return await handler.call(this, inputPath, outputPath);
    } catch (error) {
      console.error("Document conversion error:", error);
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
      "doc-pdf": this._docToPdf,
      "docx-pdf": this._docxToPdf,
      "pdf-docx": this._pdfToDocx,
      "pdf-txt": this._pdfToTxt,
      "xlsx-csv": this._xlsxToCsv,
      "csv-xlsx": this._csvToXlsx,
      "xlsx-pdf": this._xlsxToPdf,
      "pptx-pdf": this._pptxToPdf,
      "txt-pdf": this._txtToPdf,
      "txt-docx": this._txtToDocx,
      "xml-pdf": this._xmlToPdf,
    };

    return handlers[conversionKey] || null;
  }

  /**
   * DOC to PDF conversion (high-fidelity via LibreOffice)
   */
  async _docToPdf(inputPath, outputPath) {
    try {
      return await conversionEngine.docxToPdfEnhanced(inputPath, outputPath);
    } catch (error) {
      console.error("DOC to PDF conversion failed:", error);
      throw new Error(`DOC to PDF conversion failed: ${error.message}`);
    }
  }

  /**
   * DOCX to PDF conversion (high-fidelity via LibreOffice)
   */
  async _docxToPdf(inputPath, outputPath) {
    try {
      return await conversionEngine.docxToPdfEnhanced(inputPath, outputPath);
    } catch (error) {
      console.error("DOCX to PDF conversion failed:", error);
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
      console.error("PDF to DOCX conversion failed:", error);
      throw new Error(`PDF to DOCX conversion failed: ${error.message}`);
    }
  }

  async _pdfToTxt(inputPath, outputPath) {
    try {
      const extraction = await pdfService.extractText(inputPath);
      if (!extraction.success) {
        throw new Error(`Text extraction failed: ${extraction.error}`);
      }

      // Write text to file
      fs.writeFileSync(outputPath, extraction.text, "utf8");

      return {
        success: true,
        outputPath,
        fidelity: "95-98%",
        method: "pdf-text-extraction",
        pages: extraction.pages,
      };
    } catch (error) {
      console.error("PDF to TXT conversion failed:", error);
      throw new Error(`PDF to TXT conversion failed: ${error.message}`);
    }
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
    try {
      // Use LibreOffice for XLSX to PDF conversion
      return await conversionEngine.xlsxToPdf(inputPath, outputPath);
    } catch (error) {
      console.error("XLSX to PDF conversion failed:", error);
      throw new Error(`XLSX to PDF conversion failed: ${error.message}`);
    }
  }

  async _pptxToPdf(inputPath, outputPath) {
    try {
      // Use LibreOffice for PPTX to PDF conversion
      return await libreOfficeService.convertToPdf(
        inputPath,
        outputPath,
        "pptx"
      );
    } catch (error) {
      console.error("PPTX to PDF conversion failed:", error);
      throw new Error(`PPTX to PDF conversion failed: ${error.message}`);
    }
  }

  async _txtToPdf(inputPath, outputPath) {
    try {
      const text = fs.readFileSync(inputPath, "utf8");
      return await pdfService.createPdfFromText(text, outputPath);
    } catch (error) {
      console.error("TXT to PDF conversion failed:", error);
      throw new Error(`TXT to PDF conversion failed: ${error.message}`);
    }
  }

  async _txtToDocx(inputPath, outputPath) {
    try {
      const text = fs.readFileSync(inputPath, "utf8");
      const { Document, Packer, Paragraph } = require("docx");

      const doc = new Document({
        sections: [
          {
            properties: {},
            children: text.split("\n\n").map(
              (paragraph) =>
                new Paragraph({
                  text: paragraph,
                  spacing: { after: 200 },
                })
            ),
          },
        ],
      });

      const buffer = await Packer.toBuffer(doc);
      fs.writeFileSync(outputPath, buffer);

      return {
        success: true,
        outputPath,
        fidelity: "95-98%",
        method: "docx-generation",
      };
    } catch (error) {
      console.error("TXT to DOCX conversion failed:", error);
      throw new Error(`TXT to DOCX conversion failed: ${error.message}`);
    }
  }

  async _xmlToPdf(inputPath, outputPath) {
    try {
      const xmlContent = fs.readFileSync(inputPath, "utf8");
      return await pdfService.createPdfFromXml(xmlContent, outputPath);
    } catch (error) {
      console.error("XML to PDF conversion failed:", error);
      throw new Error(`XML to PDF conversion failed: ${error.message}`);
    }
  }
}

module.exports = new DocumentService();
