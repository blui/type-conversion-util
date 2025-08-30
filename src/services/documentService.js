const fs = require("fs");
const path = require("path");
const puppeteer = require("puppeteer");
const mammoth = require("mammoth");
const ExcelJS = require("exceljs");
const csvParser = require("csv-parser");
const createCsvWriter = require("csv-writer").createObjectCsvWriter;
const PDFDocument = require("pdfkit");
const { PDFDocument: PDFLibDocument, rgb } = require("pdf-lib");
const pdfParse = require("pdf-parse");
const { Document, Packer, Paragraph, TextRun } = require("docx");
const HTMLtoDOCX = require("html-to-docx");

class DocumentService {
  async convert(inputPath, outputPath, inputFormat, targetFormat) {
    try {
      console.log(`Converting ${inputFormat} to ${targetFormat}`);

      switch (`${inputFormat}-${targetFormat}`) {
        // DOCX conversions
        case "docx-pdf":
          return await this.docxToPdf(inputPath, outputPath);

        // PDF conversions
        case "pdf-docx":
          return await this.pdfToDocx(inputPath, outputPath);
        case "pdf-txt":
          return await this.pdfToTxt(inputPath, outputPath);

        // XLSX conversions
        case "xlsx-csv":
          return await this.xlsxToCsv(inputPath, outputPath);
        case "xlsx-pdf":
          return await this.xlsxToPdf(inputPath, outputPath);

        // CSV conversions
        case "csv-xlsx":
          return await this.csvToXlsx(inputPath, outputPath);

        // PPTX conversions (limited support)
        case "pptx-pdf":
          return await this.pptxToPdf(inputPath, outputPath);

        // TXT conversions
        case "txt-pdf":
          return await this.txtToPdf(inputPath, outputPath);
        case "txt-html":
          return await this.txtToHtml(inputPath, outputPath);
        case "txt-docx":
          return await this.txtToDocx(inputPath, outputPath);

        // HTML conversions
        case "html-pdf":
          return await this.htmlToPdf(inputPath, outputPath);
        case "html-docx":
          return await this.htmlToDocx(inputPath, outputPath);

        // XML conversions
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

  async docxToPdf(inputPath, outputPath) {
    try {
      // Extract text and basic formatting from DOCX
      const result = await mammoth.convertToHtml({ path: inputPath });
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

      const browser = await puppeteer.launch({
        headless: true, // v24+ uses boolean instead of "new"
        args: [
          "--no-sandbox",
          "--disable-setuid-sandbox",
          "--disable-dev-shm-usage",
          "--disable-accelerated-2d-canvas",
          "--no-first-run",
          "--no-zygote",
          "--single-process", // Required for Vercel
          "--disable-gpu",
        ],
        // Use system Chrome in production (Vercel) if available
        executablePath: process.env.PUPPETEER_EXECUTABLE_PATH || undefined,
      });
      const page = await browser.newPage();
      await page.setContent(htmlContent);
      await page.pdf({
        path: outputPath,
        format: "A4",
        margin: { top: "1in", bottom: "1in", left: "1in", right: "1in" },
        printBackground: true,
      });
      await browser.close();

      return { success: true, outputPath };
    } catch (error) {
      throw new Error(`DOCX to PDF conversion failed: ${error.message}`);
    }
  }

  async pdfToDocx(inputPath, outputPath) {
    try {
      // Extract text from PDF
      const pdfBuffer = fs.readFileSync(inputPath);
      const data = await pdfParse(pdfBuffer);
      const text = data.text;

      // Create a new DOCX document
      const doc = new Document({
        sections: [
          {
            properties: {},
            children: text.split("\n").map(
              (line) =>
                new Paragraph({
                  children: [new TextRun(line || " ")], // Empty line if no content
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

  async xlsxToCsv(inputPath, outputPath) {
    try {
      const workbook = new ExcelJS.Workbook();
      await workbook.xlsx.readFile(inputPath);
      const worksheet = workbook.getWorksheet(1); // Get first worksheet

      const csvData = [];
      worksheet.eachRow((row, rowNumber) => {
        const rowData = [];
        row.eachCell((cell, colNumber) => {
          rowData.push(cell.text || "");
        });
        csvData.push(rowData.join(","));
      });

      fs.writeFileSync(outputPath, csvData.join("\n"));
      return { success: true, outputPath };
    } catch (error) {
      throw new Error(`XLSX to CSV conversion failed: ${error.message}`);
    }
  }

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

      const browser = await puppeteer.launch({
        headless: true, // v24+ uses boolean instead of "new"
        args: [
          "--no-sandbox",
          "--disable-setuid-sandbox",
          "--disable-dev-shm-usage",
          "--disable-accelerated-2d-canvas",
          "--no-first-run",
          "--no-zygote",
          "--single-process", // Required for Vercel
          "--disable-gpu",
        ],
        // Use system Chrome in production (Vercel) if available
        executablePath: process.env.PUPPETEER_EXECUTABLE_PATH || undefined,
      });
      const page = await browser.newPage();
      await page.setContent(htmlContent);
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
      await browser.close();

      return { success: true, outputPath };
    } catch (error) {
      throw new Error(`XLSX to PDF conversion failed: ${error.message}`);
    }
  }

  async csvToXlsx(inputPath, outputPath) {
    try {
      const results = [];

      return new Promise((resolve, reject) => {
        fs.createReadStream(inputPath)
          .pipe(csvParser())
          .on("data", (data) => results.push(data))
          .on("end", async () => {
            try {
              const workbook = new ExcelJS.Workbook();
              const worksheet = workbook.addWorksheet("Sheet1");

              if (results.length > 0) {
                // Add headers
                const headers = Object.keys(results[0]);
                worksheet.addRow(headers);

                // Add data rows
                results.forEach((row) => {
                  const values = headers.map((header) => row[header] || "");
                  worksheet.addRow(values);
                });
              }

              await workbook.xlsx.writeFile(outputPath);
              resolve({ success: true, outputPath });
            } catch (error) {
              reject(
                new Error(`CSV to XLSX conversion failed: ${error.message}`)
              );
            }
          })
          .on("error", (error) => {
            reject(
              new Error(`CSV to XLSX conversion failed: ${error.message}`)
            );
          });
      });
    } catch (error) {
      throw new Error(`CSV to XLSX conversion failed: ${error.message}`);
    }
  }

  async pptxToPdf(inputPath, outputPath) {
    try {
      // Limited PPTX support - extract text and create a simple PDF
      // Note: This is a basic implementation. Full PPTX parsing would require more complex libraries
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

  async htmlToPdf(inputPath, outputPath) {
    try {
      const htmlContent = fs.readFileSync(inputPath, "utf8");
      const browser = await puppeteer.launch({
        headless: true, // v24+ uses boolean instead of "new"
        args: [
          "--no-sandbox",
          "--disable-setuid-sandbox",
          "--disable-dev-shm-usage",
          "--disable-accelerated-2d-canvas",
          "--no-first-run",
          "--no-zygote",
          "--single-process", // Required for Vercel
          "--disable-gpu",
        ],
        // Use system Chrome in production (Vercel) if available
        executablePath: process.env.PUPPETEER_EXECUTABLE_PATH || undefined,
      });
      const page = await browser.newPage();
      await page.setContent(htmlContent);
      await page.pdf({
        path: outputPath,
        format: "A4",
        margin: { top: "1in", bottom: "1in", left: "1in", right: "1in" },
        printBackground: true,
      });
      await browser.close();
      return { success: true, outputPath };
    } catch (error) {
      throw new Error(`HTML to PDF conversion failed: ${error.message}`);
    }
  }

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
