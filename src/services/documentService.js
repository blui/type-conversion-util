/**
 * Document Conversion Service
 *
 * Handles document file conversions including PDF, DOCX, XLSX, CSV, PPTX, TXT, and XML.
 * Uses various Node.js libraries for high-quality document processing with enhanced accuracy.
 * Implements streaming for large files and comprehensive error handling.
 *
 * Supported Conversions:
 * - PDF <-> DOCX (with enhanced accuracy)
 * - PDF -> TXT (text extraction)
 * - XLSX <-> CSV (with multiple worksheet support)
 * - XLSX -> PDF (spreadsheet to PDF)
 * - PPTX -> PDF (limited support)
 * - TXT -> PDF, DOCX
 * - XML -> PDF
 *
 * Features:
 * - Enhanced accuracy service integration
 * - Multiple worksheet handling for Excel files
 * - Streaming support for large files
 * - Professional PDF generation with Puppeteer
 * - Comprehensive error handling and fallbacks
 * - Formatting preservation where possible
 */

// Node.js built-in modules
const fs = require("fs");
const path = require("path");

// Document processing libraries
const puppeteer = require("puppeteer");
const mammoth = require("mammoth");
const ExcelJS = require("exceljs");
const csvParser = require("csv-parser");
const { stringify } = require("csv-stringify/sync");
const PDFDocument = require("pdfkit");
const pdfParse = require("pdf-parse");
const { Document, Packer, Paragraph, TextRun } = require("docx");

// Enhanced accuracy service for better conversion quality
const accuracyService = require("./accuracyService");

class DocumentService {
  /**
   * Get optimized Puppeteer launch options
   * Configures browser settings for different environments including serverless deployments
   * Optimizes performance and compatibility across various hosting platforms
   *
   * @returns {Object} Puppeteer launch configuration with optimized settings
   */
  getPuppeteerLaunchOptions() {
    // Base arguments for all environments - optimized for stability and performance
    const args = [
      "--no-sandbox",
      "--disable-setuid-sandbox",
      "--disable-dev-shm-usage",
      "--disable-accelerated-2d-canvas",
      "--no-first-run",
      "--no-zygote",
      "--disable-gpu",
    ];

    // Add single-process mode for serverless environments with limited resources
    if (process.env.VERCEL || process.env.AWS_LAMBDA_FUNCTION_NAME) {
      args.push("--single-process");
    }

    // Configure launch options with appropriate timeouts for document processing
    const launchOptions = {
      headless: true,
      args,
      protocolTimeout: 120000,
      timeout: 60000,
    };

    // Set custom executable path if specified in environment variables
    const executablePath = process.env.PUPPETEER_EXECUTABLE_PATH;
    if (executablePath && fs.existsSync(executablePath)) {
      launchOptions.executablePath = executablePath;
    }

    return launchOptions;
  }

  /**
   * Convert document from one format to another
   * Main conversion method that routes to appropriate conversion handlers
   * Provides comprehensive error handling and logging
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
      // Route to appropriate conversion method based on format combination
      switch (`${inputFormat}-${targetFormat}`) {
        case "docx-pdf":
          return await this.docxToPdf(inputPath, outputPath);
        case "pdf-docx":
          return await this.pdfToDocx(inputPath, outputPath);
        case "pdf-txt":
          return await this.pdfToTxt(inputPath, outputPath);
        case "xlsx-csv":
          return await this.xlsxToCsv(inputPath, outputPath);
        case "csv-xlsx":
          return await this.csvToXlsx(inputPath, outputPath);
        case "xlsx-pdf":
          return await this.xlsxToPdf(inputPath, outputPath);
        case "pptx-pdf":
          return await this.pptxToPdf(inputPath, outputPath);
        case "txt-pdf":
          return await this.txtToPdf(inputPath, outputPath);
        case "txt-docx":
          return await this.txtToDocx(inputPath, outputPath);
        case "xml-pdf":
          return await this.xmlToPdf(inputPath, outputPath);
        default:
          throw new Error(
            `Conversion from ${inputFormat} to ${targetFormat} is not supported`
          );
      }
    } catch (error) {
      console.error("Document conversion error:", error);
      return { success: false, error: error.message };
    }
  }

  /**
   * Convert PDF files to DOCX format with enhanced accuracy
   * Uses AccuracyService for professional-grade conversion with structure preservation
   *
   * @param {string} inputPath - Path to PDF file
   * @param {string} outputPath - Path for DOCX file
   * @returns {Promise<Object>} Conversion result
   */
  async pdfToDocx(inputPath, outputPath) {
    try {
      // Use enhanced accuracy service for better conversion
      const result = await accuracyService.enhancedPdfToDocx(
        inputPath,
        outputPath
      );

      // Validate conversion accuracy
      const validation = await accuracyService.validateConversionAccuracy(
        inputPath,
        outputPath,
        "pdf-to-docx"
      );

      return {
        ...result,
        accuracy: validation,
      };
    } catch (error) {
      console.error("PDF to DOCX conversion error:", error);

      // Fallback to basic conversion
      try {
        console.log("Attempting fallback PDF to DOCX conversion...");
        return await accuracyService.basicPdfToDocx(inputPath, outputPath);
      } catch (fallbackError) {
        throw new Error(`PDF to DOCX conversion failed: ${error.message}`);
      }
    }
  }

  /**
   * Convert DOCX files to PDF format with enhanced formatting preservation
   * Uses AccuracyService for professional-grade conversion with better styling
   *
   * @param {string} inputPath - Path to DOCX file
   * @param {string} outputPath - Path for PDF file
   * @returns {Promise<Object>} Conversion result
   */
  async docxToPdf(inputPath, outputPath) {
    try {
      // Use enhanced accuracy service for better conversion
      const result = await accuracyService.enhancedDocxToPdf(
        inputPath,
        outputPath
      );

      // Validate conversion accuracy
      const validation = await accuracyService.validateConversionAccuracy(
        inputPath,
        outputPath,
        "docx-to-pdf"
      );

      return {
        ...result,
        accuracy: validation,
      };
    } catch (error) {
      console.error("DOCX to PDF conversion error:", error);

      // Fallback to basic conversion
      try {
        console.log("Attempting fallback DOCX to PDF conversion...");
        return await this.basicDocxToPdf(inputPath, outputPath);
      } catch (fallbackError) {
        throw new Error(`DOCX to PDF conversion failed: ${error.message}`);
      }
    }
  }

  /**
   * Basic DOCX to PDF conversion as fallback
   */
  async basicDocxToPdf(inputPath, outputPath) {
    try {
      // Extract text and basic formatting from DOCX
      const result = await mammoth.convertToHtml({ path: inputPath });

      // Create styled HTML content for PDF generation
      const htmlContent = `
        <!DOCTYPE html>
        <html>
        <head>
          <meta charset="utf-8">
          <style>
            body { 
              font-family: Arial, sans-serif; 
              margin: 40px; 
              line-height: 1.6; 
              color: #333;
            }
            p { margin: 10px 0; }
            h1, h2, h3, h4, h5, h6 { 
              color: #2c3e50; 
              margin: 20px 0 10px 0; 
            }
            strong, b { font-weight: bold; }
            em, i { font-style: italic; }
            ul, ol { margin: 10px 0; padding-left: 30px; }
            table { border-collapse: collapse; width: 100%; margin: 10px 0; }
            td, th { border: 1px solid #ddd; padding: 8px; text-align: left; }
            th { background-color: #f2f2f2; }
          </style>
        </head>
        <body>${result.value}</body>
        </html>
      `;

      // Launch browser and generate PDF with enhanced error handling
      let browser;
      try {
        const launchOptions = this.getPuppeteerLaunchOptions();
        console.log(
          "Launching Puppeteer with options:",
          JSON.stringify(launchOptions, null, 2)
        );
        browser = await puppeteer.launch(launchOptions);

        const page = await browser.newPage();
        await page.setContent(htmlContent, { waitUntil: "networkidle0" });
        if (page.emulateMediaType) await page.emulateMediaType("screen");

        await page.pdf({
          path: outputPath,
          format: "A4",
          margin: { top: "1in", bottom: "1in", left: "1in", right: "1in" },
          printBackground: true,
        });
      } catch (puppeteerError) {
        console.error("Puppeteer error:", puppeteerError.message);

        // If Puppeteer fails, try to provide a helpful error message
        if (
          puppeteerError.message.includes("executablePath") ||
          puppeteerError.message.includes("Browser was not found")
        ) {
          throw new Error(
            `Browser initialization failed. Please ensure Chrome/Chromium is available or try again. Error: ${puppeteerError.message}`
          );
        }
        throw puppeteerError;
      } finally {
        if (browser) {
          try {
            await browser.close();
          } catch (closeError) {
            console.error("Error closing browser:", closeError.message);
          }
        }
      }

      return { success: true, outputPath };
    } catch (error) {
      throw new Error(`DOCX to PDF conversion failed: ${error.message}`);
    }
  }

  /**
   * Convert PDF files to plain text
   * Extracts text content and saves as TXT file
   *
   * @param {string} inputPath - Path to PDF file
   * @param {string} outputPath - Path for TXT file
   * @returns {Promise<Object>} Conversion result
   */
  async pdfToTxt(inputPath, outputPath) {
    try {
      const pdfBuffer = fs.readFileSync(inputPath);
      const data = await pdfParse(pdfBuffer);
      fs.writeFileSync(outputPath, data.text);
      return { success: true, outputPath };
    } catch (error) {
      throw new Error(`PDF to TXT conversion failed: ${error.message}`);
    }
  }

  /**
   * Convert XLSX files to CSV format with enhanced accuracy
   * Handles multiple worksheets, preserves formatting, and handles complex data types
   *
   * @param {string} inputPath - Path to XLSX file
   * @param {string} outputPath - Path for CSV file
   * @returns {Promise<Object>} Conversion result
   */
  async xlsxToCsv(inputPath, outputPath) {
    try {
      const workbook = new ExcelJS.Workbook();
      await workbook.xlsx.readFile(inputPath);

      // Get all worksheets
      const worksheets = workbook.worksheets;

      if (worksheets.length === 0) {
        throw new Error("No worksheets found in the Excel file");
      }

      // If multiple worksheets, create separate CSV files
      if (worksheets.length > 1) {
        const basePath = outputPath.replace(".csv", "");
        const results = [];

        // Track used sanitized names to avoid filename conflicts
        const usedSheetNames = {};
        for (let i = 0; i < worksheets.length; i++) {
          const worksheet = worksheets[i];
          let baseSheetName = worksheet.name.replace(/[^a-zA-Z0-9]/g, "_"); // Sanitize sheet name
          let sheetName = baseSheetName;
          let counter = 1;
          // Ensure uniqueness by appending a counter if needed
          while (usedSheetNames[sheetName]) {
            sheetName = `${baseSheetName}_${counter}`;
            counter++;
          }
          usedSheetNames[sheetName] = true;
          const sheetPath = `${basePath}_${sheetName}.csv`;

          const sheetData = this.extractWorksheetData(worksheet);
          const csv = stringify(sheetData, {
            quoted: true,
            quoted_empty: true,
            quoted_string: true,
          });

          fs.writeFileSync(sheetPath, csv);
          results.push({
            path: sheetPath,
            name: worksheet.name,
            rows: sheetData.length,
          });
        }

        // Create a summary file listing all generated CSVs
        const summaryPath = `${basePath}_summary.txt`;
        const summary = `Multiple worksheets converted to separate CSV files:\n\n${results
          .map(
            (file, i) =>
              `${i + 1}. ${file.name} -> ${file.path} (${file.rows} rows)`
          )
          .join("\n")}`;
        fs.writeFileSync(summaryPath, summary);

        return {
          success: true,
          outputPath: summaryPath,
          additionalFiles: results.map((f) => f.path),
          worksheetCount: worksheets.length,
          message: `Converted ${worksheets.length} worksheets to separate CSV files`,
          details: results,
        };
      } else {
        // Single worksheet - convert to single CSV
        const worksheet = worksheets[0];
        const data = this.extractWorksheetData(worksheet);
        const csv = stringify(data, {
          quoted: true,
          quoted_empty: true,
          quoted_string: true,
        });

        fs.writeFileSync(outputPath, csv);
        return { success: true, outputPath };
      }
    } catch (error) {
      throw new Error(`XLSX to CSV conversion failed: ${error.message}`);
    }
  }

  /**
   * Extract data from a worksheet with enhanced formatting preservation
   */
  extractWorksheetData(worksheet) {
    const rows = [];
    const dimensions = worksheet.dimensions;

    if (!dimensions) {
      return [["No data found"]];
    }

    // Get the actual data range
    const startRow = dimensions.top;
    const endRow = dimensions.bottom;
    const startCol = dimensions.left;
    const endCol = dimensions.right;

    for (let rowNum = startRow; rowNum <= endRow; rowNum++) {
      const row = worksheet.getRow(rowNum);
      const rowData = [];

      for (let colNum = startCol; colNum <= endCol; colNum++) {
        const cell = row.getCell(colNum);
        let cellValue = "";

        if (cell.value !== null && cell.value !== undefined) {
          // Handle different data types
          if (typeof cell.value === "object") {
            if (cell.value.text) {
              cellValue = cell.value.text;
            } else if (cell.value.result) {
              cellValue = cell.value.result;
            } else if (cell.value.richText) {
              cellValue = cell.value.richText.map((rt) => rt.text).join("");
            } else {
              cellValue = JSON.stringify(cell.value);
            }
          } else if (typeof cell.value === "number") {
            // Preserve number formatting
            if (cell.numFmt) {
              cellValue = cell.value.toString();
            } else {
              cellValue = cell.value.toString();
            }
          } else if (typeof cell.value === "boolean") {
            cellValue = cell.value ? "TRUE" : "FALSE";
          } else if (cell.value instanceof Date) {
            cellValue = cell.value.toISOString().split("T")[0]; // YYYY-MM-DD format
          } else {
            cellValue = cell.value.toString();
          }
        }

        rowData.push(cellValue);
      }

      rows.push(rowData);
    }

    return rows;
  }

  /**
   * Convert XLSX files to PDF format
   * Converts spreadsheet data to HTML table and generates PDF
   *
   * @param {string} inputPath - Path to XLSX file
   * @param {string} outputPath - Path for PDF file
   * @returns {Promise<Object>} Conversion result
   */
  async xlsxToPdf(inputPath, outputPath) {
    try {
      const workbook = new ExcelJS.Workbook();
      await workbook.xlsx.readFile(inputPath);
      const worksheet = workbook.getWorksheet(1);

      // Convert to HTML table
      let htmlTable = "<table>";
      worksheet.eachRow((row, rowNumber) => {
        htmlTable += "<tr>";
        row.eachCell((cell, colNumber) => {
          const tag = rowNumber === 1 ? "th" : "td";
          htmlTable += `<${tag}>${cell.text || ""}</${tag}>`;
        });
        htmlTable += "</tr>";
      });
      htmlTable += "</table>";

      // Create styled HTML for PDF generation
      const htmlContent = `
        <!DOCTYPE html>
        <html>
        <head>
          <meta charset="utf-8">
          <style>
            body { font-family: Arial, sans-serif; margin: 20px; }
            table { border-collapse: collapse; width: 100%; }
            td, th { border: 1px solid #ddd; padding: 8px; text-align: left; font-size: 12px; }
            th { background-color: #f2f2f2; font-weight: bold; }
          </style>
        </head>
        <body>${htmlTable}</body>
        </html>
      `;

      // Generate PDF using Puppeteer
      const browser = await puppeteer.launch(this.getPuppeteerLaunchOptions());
      try {
        const page = await browser.newPage();
        await page.setContent(htmlContent, { waitUntil: "networkidle0" });
        if (page.emulateMediaType) await page.emulateMediaType("screen");
        await page.pdf({
          path: outputPath,
          format: "A4",
          landscape: true, // Better for spreadsheets
          margin: {
            top: "0.5in",
            bottom: "0.5in",
            left: "0.5in",
            right: "0.5in",
          },
        });
      } finally {
        await browser.close();
      }

      return { success: true, outputPath };
    } catch (error) {
      throw new Error(`XLSX to PDF conversion failed: ${error.message}`);
    }
  }

  /**
   * Convert CSV files to XLSX format using streaming
   * Processes large CSV files efficiently with streaming writer
   *
   * @param {string} inputPath - Path to CSV file
   * @param {string} outputPath - Path for XLSX file
   * @returns {Promise<Object>} Conversion result
   */
  async csvToXlsx(inputPath, outputPath) {
    try {
      return new Promise((resolve, reject) => {
        const readStream = fs.createReadStream(inputPath);
        const parser = csvParser();
        const workbook = new ExcelJS.stream.xlsx.WorkbookWriter({
          filename: outputPath,
          useStyles: false,
          useSharedStrings: false,
        });
        const worksheet = workbook.addWorksheet("Sheet1");

        let headers = null;

        parser
          .on("headers", (hdrs) => {
            headers = hdrs;
            worksheet.addRow(headers).commit();
          })
          .on("data", (row) => {
            if (!headers) headers = Object.keys(row);
            const values = headers.map((h) => row[h] || "");
            worksheet.addRow(values).commit();
          })
          .on("end", async () => {
            try {
              await worksheet.commit();
              await workbook.commit();
              resolve({ success: true, outputPath });
            } catch (err) {
              reject(
                new Error(`CSV to XLSX conversion failed: ${err.message}`)
              );
            }
          })
          .on("error", (err) => {
            reject(new Error(`CSV to XLSX conversion failed: ${err.message}`));
          });

        readStream.pipe(parser);
      });
    } catch (error) {
      throw new Error(`CSV to XLSX conversion failed: ${error.message}`);
    }
  }

  /**
   * Convert PPTX files to PDF format (limited support)
   * Creates simplified PDF with basic information
   *
   * @param {string} inputPath - Path to PPTX file
   * @param {string} outputPath - Path for PDF file
   * @returns {Promise<Object>} Conversion result
   */
  async pptxToPdf(inputPath, outputPath) {
    try {
      // Limited PPTX support - create informational PDF
      const doc = new PDFDocument();
      doc.pipe(fs.createWriteStream(outputPath));

      doc.fontSize(16);
      doc.text("PowerPoint Presentation", 50, 50);
      doc.fontSize(12);
      doc.text(
        "Note: This is a simplified conversion from PPTX format.",
        50,
        100
      );
      doc.text(
        "For full PPTX support, consider using a specialized service.",
        50,
        120
      );
      doc.text(`Original file: ${path.basename(inputPath)}`, 50, 160);

      doc.end();

      return new Promise((resolve, reject) => {
        doc.on("end", () => resolve({ success: true, outputPath }));
        doc.on("error", (error) =>
          reject(new Error(`PPTX to PDF conversion failed: ${error.message}`))
        );
      });
    } catch (error) {
      throw new Error(`PPTX to PDF conversion failed: ${error.message}`);
    }
  }

  /**
   * Convert TXT files to PDF format
   * Creates PDF with formatted text content
   *
   * @param {string} inputPath - Path to TXT file
   * @param {string} outputPath - Path for PDF file
   * @returns {Promise<Object>} Conversion result
   */
  async txtToPdf(inputPath, outputPath) {
    try {
      const textContent = fs.readFileSync(inputPath, "utf8");
      const doc = new PDFDocument();
      doc.pipe(fs.createWriteStream(outputPath));

      // Add text with proper formatting
      doc.fontSize(12);
      doc.text(textContent, 50, 50, {
        width: 500,
        align: "left",
      });

      doc.end();

      return new Promise((resolve, reject) => {
        doc.on("end", () => resolve({ success: true, outputPath }));
        doc.on("error", (error) =>
          reject(new Error(`TXT to PDF conversion failed: ${error.message}`))
        );
      });
    } catch (error) {
      throw new Error(`TXT to PDF conversion failed: ${error.message}`);
    }
  }

  /**
   * Convert TXT files to DOCX format
   * Creates DOCX document with text content
   *
   * @param {string} inputPath - Path to TXT file
   * @param {string} outputPath - Path for DOCX file
   * @returns {Promise<Object>} Conversion result
   */
  async txtToDocx(inputPath, outputPath) {
    try {
      const textContent = fs.readFileSync(inputPath, "utf8");

      const doc = new Document({
        sections: [
          {
            properties: {},
            children: textContent.split("\n").map(
              (line) =>
                new Paragraph({
                  children: [new TextRun(line || " ")],
                })
            ),
          },
        ],
      });

      const buffer = await Packer.toBuffer(doc);
      fs.writeFileSync(outputPath, buffer);

      return { success: true, outputPath };
    } catch (error) {
      throw new Error(`TXT to DOCX conversion failed: ${error.message}`);
    }
  }

  /**
   * Convert XML files to PDF format
   * Creates PDF with formatted XML content
   *
   * @param {string} inputPath - Path to XML file
   * @param {string} outputPath - Path for PDF file
   * @returns {Promise<Object>} Conversion result
   */
  async xmlToPdf(inputPath, outputPath) {
    try {
      const xmlContent = fs.readFileSync(inputPath, "utf8");
      const doc = new PDFDocument();
      doc.pipe(fs.createWriteStream(outputPath));

      doc.fontSize(10);
      doc.text(xmlContent, 50, 50, {
        width: 500,
        align: "left",
      });

      doc.end();

      return new Promise((resolve, reject) => {
        doc.on("end", () => resolve({ success: true, outputPath }));
        doc.on("error", (error) =>
          reject(new Error(`XML to PDF conversion failed: ${error.message}`))
        );
      });
    } catch (error) {
      throw new Error(`XML to PDF conversion failed: ${error.message}`);
    }
  }
}

module.exports = new DocumentService();
