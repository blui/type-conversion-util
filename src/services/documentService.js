/**
 * Document Conversion Service
 *
 * Handles document file conversions including PDF, DOCX, XLSX, CSV, PPTX, TXT, HTML, and XML.
 * Uses various Node.js libraries for high-quality document processing.
 * Implements streaming for large files and comprehensive error handling.
 */

const fs = require("fs");
const path = require("path");
const puppeteer = require("puppeteer");
const mammoth = require("mammoth");
const ExcelJS = require("exceljs");
const csvParser = require("csv-parser");
const { stringify } = require("csv-stringify/sync");
const PDFDocument = require("pdfkit");
const pdfParse = require("pdf-parse");
const { Document, Packer, Paragraph, TextRun } = require("docx");
const HTMLtoDOCX = require("html-to-docx");

class DocumentService {
  /**
   * Get optimized Puppeteer launch options for different environments
   * Configures browser settings for serverless and production environments
   *
   * @returns {Object} Puppeteer launch configuration
   */
  getPuppeteerLaunchOptions() {
    // Try to get executable path from environment or Puppeteer
    let executablePath;
    try {
      executablePath = process.env.PUPPETEER_EXECUTABLE_PATH;
      if (!executablePath && typeof puppeteer.executablePath === "function") {
        executablePath = puppeteer.executablePath();
      }
    } catch (error) {
      console.log(
        "Could not determine Puppeteer executable path, using default"
      );
      executablePath = undefined;
    }

    // Base arguments for all environments
    const baseArgs = [
      "--no-sandbox",
      "--disable-setuid-sandbox",
      "--disable-dev-shm-usage",
      "--disable-accelerated-2d-canvas",
      "--no-first-run",
      "--no-zygote",
      "--disable-gpu",
      "--disable-web-security",
      "--disable-features=VizDisplayCompositor",
    ];

    // Use single-process only in constrained environments
    const isConstrainedLinux =
      process.env.VERCEL ||
      process.env.AWS_LAMBDA_FUNCTION_NAME ||
      (process.platform === "linux" && process.env.NODE_ENV === "production");

    const args = isConstrainedLinux
      ? [...baseArgs, "--single-process"]
      : baseArgs;

    const launchOptions = {
      headless: true,
      args,
      protocolTimeout: 120000,
      timeout: 60000,
    };

    // Only set executablePath if it exists and is accessible
    if (executablePath) {
      try {
        const fs = require("fs");
        if (fs.existsSync(executablePath)) {
          launchOptions.executablePath = executablePath;
        } else {
          console.log(`Puppeteer executable not found at: ${executablePath}`);
        }
      } catch (error) {
        console.log("Could not verify executable path, using default");
      }
    }

    return launchOptions;
  }

  /**
   * Convert document from one format to another
   */
  async convert(inputPath, outputPath, inputFormat, targetFormat) {
    console.log(`Converting ${inputFormat} to ${targetFormat}`);

    try {
      // Route to appropriate conversion method
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
        case "txt-html":
          return await this.txtToHtml(inputPath, outputPath);
        case "txt-docx":
          return await this.txtToDocx(inputPath, outputPath);
        case "html-pdf":
          return await this.htmlToPdf(inputPath, outputPath);
        case "html-docx":
          return await this.htmlToDocx(inputPath, outputPath);
        case "xml-pdf":
          return await this.xmlToPdf(inputPath, outputPath);
        case "xml-html":
          return await this.xmlToHtml(inputPath, outputPath);
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
   * Convert DOCX files to PDF format
   * Uses Mammoth to extract content and Puppeteer for PDF generation
   *
   * @param {string} inputPath - Path to DOCX file
   * @param {string} outputPath - Path for PDF file
   * @returns {Promise<Object>} Conversion result
   */
  async docxToPdf(inputPath, outputPath) {
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
   * Convert PDF files to DOCX format
   * Extracts text content and creates a new DOCX document
   *
   * @param {string} inputPath - Path to PDF file
   * @param {string} outputPath - Path for DOCX file
   * @returns {Promise<Object>} Conversion result
   */
  async pdfToDocx(inputPath, outputPath) {
    try {
      // Extract text from PDF
      const pdfBuffer = fs.readFileSync(inputPath);
      const data = await pdfParse(pdfBuffer);
      const text = data.text;

      // Create a new DOCX document with extracted text
      const doc = new Document({
        sections: [
          {
            properties: {},
            children: text.split("\n").map(
              (line) =>
                new Paragraph({
                  children: [new TextRun(line || " ")],
                })
            ),
          },
        ],
      });

      // Generate and save the document
      const buffer = await Packer.toBuffer(doc);
      fs.writeFileSync(outputPath, buffer);

      return { success: true, outputPath };
    } catch (error) {
      throw new Error(`PDF to DOCX conversion failed: ${error.message}`);
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
   * Convert XLSX files to CSV format
   * Extracts data from first worksheet and creates CSV file
   *
   * @param {string} inputPath - Path to XLSX file
   * @param {string} outputPath - Path for CSV file
   * @returns {Promise<Object>} Conversion result
   */
  async xlsxToCsv(inputPath, outputPath) {
    try {
      const workbook = new ExcelJS.Workbook();
      await workbook.xlsx.readFile(inputPath);
      const worksheet = workbook.getWorksheet(1); // Get first worksheet

      // Extract all data from worksheet
      const rows = [];
      worksheet.eachRow((row) => {
        const rowData = [];
        row.eachCell((cell) => {
          rowData.push(cell.text || "");
        });
        rows.push(rowData);
      });

      // Generate CSV with proper quoting
      const csv = stringify(rows, { quoted: true });
      fs.writeFileSync(outputPath, csv);
      return { success: true, outputPath };
    } catch (error) {
      throw new Error(`XLSX to CSV conversion failed: ${error.message}`);
    }
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
   * Convert TXT files to HTML format
   * Creates HTML document with formatted text content
   *
   * @param {string} inputPath - Path to TXT file
   * @param {string} outputPath - Path for HTML file
   * @returns {Promise<Object>} Conversion result
   */
  async txtToHtml(inputPath, outputPath) {
    try {
      const textContent = fs.readFileSync(inputPath, "utf8");
      const htmlContent = `
        <!DOCTYPE html>
        <html>
        <head>
          <meta charset="utf-8">
          <title>Converted Text</title>
          <style>
            body { font-family: Arial, sans-serif; margin: 40px; line-height: 1.6; }
            pre { white-space: pre-wrap; word-wrap: break-word; }
          </style>
        </head>
        <body>
          <pre>${textContent.replace(/</g, "&lt;").replace(/>/g, "&gt;")}</pre>
        </body>
        </html>
      `;
      fs.writeFileSync(outputPath, htmlContent);
      return { success: true, outputPath };
    } catch (error) {
      throw new Error(`TXT to HTML conversion failed: ${error.message}`);
    }
  }

  /**
   * Convert HTML files to PDF format
   * Uses Puppeteer to render HTML and generate PDF
   *
   * @param {string} inputPath - Path to HTML file
   * @param {string} outputPath - Path for PDF file
   * @returns {Promise<Object>} Conversion result
   */
  async htmlToPdf(inputPath, outputPath) {
    try {
      const htmlContent = fs.readFileSync(inputPath, "utf8");
      const browser = await puppeteer.launch(this.getPuppeteerLaunchOptions());
      try {
        const page = await browser.newPage();
        await page.setContent(htmlContent, { waitUntil: "networkidle0" });
        if (page.emulateMediaType) await page.emulateMediaType("screen");
        await page.pdf({
          path: outputPath,
          format: "A4",
          margin: { top: "1in", bottom: "1in", left: "1in", right: "1in" },
          printBackground: true,
        });
      } finally {
        await browser.close();
      }
      return { success: true, outputPath };
    } catch (error) {
      throw new Error(`HTML to PDF conversion failed: ${error.message}`);
    }
  }

  /**
   * Convert HTML files to DOCX format
   * Uses html-to-docx library for conversion
   *
   * @param {string} inputPath - Path to HTML file
   * @param {string} outputPath - Path for DOCX file
   * @returns {Promise<Object>} Conversion result
   */
  async htmlToDocx(inputPath, outputPath) {
    try {
      const htmlContent = fs.readFileSync(inputPath, "utf8");
      const docxBuffer = await HTMLtoDOCX(htmlContent, null, {
        table: { row: { cantSplit: true } },
        footer: true,
        pageNumber: true,
      });
      fs.writeFileSync(outputPath, docxBuffer);
      return { success: true, outputPath };
    } catch (error) {
      throw new Error(`HTML to DOCX conversion failed: ${error.message}`);
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

  /**
   * Convert XML files to HTML format
   * Creates HTML document with formatted XML content
   *
   * @param {string} inputPath - Path to XML file
   * @param {string} outputPath - Path for HTML file
   * @returns {Promise<Object>} Conversion result
   */
  async xmlToHtml(inputPath, outputPath) {
    try {
      const xmlContent = fs.readFileSync(inputPath, "utf8");
      const htmlContent = `
        <!DOCTYPE html>
        <html>
        <head>
          <meta charset="utf-8">
          <title>XML Content</title>
          <style>
            body { font-family: monospace; margin: 40px; line-height: 1.4; }
            pre { white-space: pre-wrap; word-wrap: break-word; background: #f5f5f5; padding: 20px; border-radius: 5px; }
          </style>
        </head>
        <body>
          <pre>${xmlContent.replace(/</g, "&lt;").replace(/>/g, "&gt;")}</pre>
        </body>
        </html>
      `;
      fs.writeFileSync(outputPath, htmlContent);
      return { success: true, outputPath };
    } catch (error) {
      throw new Error(`XML to HTML conversion failed: ${error.message}`);
    }
  }
}

module.exports = new DocumentService();
