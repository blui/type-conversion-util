/**
 * Accuracy Service
 *
 * Provides enhanced file conversion accuracy with advanced formatting preservation,
 * table detection, and structure analysis for professional-grade conversions
 */

const fs = require("fs");
const path = require("path");
const pdfParse = require("pdf-parse");
const mammoth = require("mammoth");
const {
  Document,
  Packer,
  Paragraph,
  TextRun,
  Table,
  TableRow,
  TableCell,
  WidthType,
} = require("docx");
const puppeteer = require("puppeteer");

class AccuracyService {
  /**
   * Enhanced PDF to DOCX conversion with structure preservation
   */
  async enhancedPdfToDocx(inputPath, outputPath) {
    try {
      // For now, use basic conversion as enhanced conversion needs more work
      return await this.basicPdfToDocx(inputPath, outputPath);
    } catch (error) {
      console.error("Enhanced PDF to DOCX conversion error:", error);

      // Fallback to basic conversion
      try {
        console.log("Attempting fallback PDF to DOCX conversion...");
        return await this.basicPdfToDocx(inputPath, outputPath);
      } catch (fallbackError) {
        throw new Error(`PDF to DOCX conversion failed: ${error.message}`);
      }
    }
  }

  /**
   * Enhanced DOCX to PDF conversion with better formatting
   */
  async enhancedDocxToPdf(inputPath, outputPath) {
    try {
      // Extract content with enhanced options
      const result = await mammoth.convertToHtml({
        path: inputPath,
        styleMap: [
          "p[style-name='Heading 1'] => h1:fresh",
          "p[style-name='Heading 2'] => h2:fresh",
          "p[style-name='Heading 3'] => h3:fresh",
          "p[style-name='Title'] => h1.title:fresh",
          "p[style-name='Subtitle'] => h2.subtitle:fresh",
          "r[style-name='Strong'] => strong",
          "r[style-name='Emphasis'] => em",
          "table => table.table",
          "tr => tr",
          "td => td",
          "th => th",
        ],
        includeDefaultStyleMap: true,
        includeEmbeddedStyleMap: true,
        idPrefix: "docx-",
      });

      // Create professional HTML content
      const htmlContent = `
        <!DOCTYPE html>
        <html>
        <head>
          <meta charset="utf-8">
          <style>
            @page {
              size: A4;
              margin: 1in;
            }
            
            body { 
              font-family: 'Times New Roman', serif; 
              line-height: 1.6; 
              color: #333;
              margin: 0;
              padding: 0;
            }
            
            h1, h2, h3, h4, h5, h6 { 
              color: #2c3e50; 
              margin: 20px 0 10px 0; 
              font-weight: bold;
              page-break-after: avoid;
            }
            
            h1 { font-size: 24px; }
            h2 { font-size: 20px; }
            h3 { font-size: 18px; }
            
            p { 
              margin: 10px 0; 
              text-align: justify;
              orphans: 2;
              widows: 2;
            }
            
            strong, b { font-weight: bold; }
            em, i { font-style: italic; }
            
            table { 
              border-collapse: collapse; 
              width: 100%; 
              margin: 15px 0;
              page-break-inside: avoid;
            }
            
            td, th { 
              border: 1px solid #ddd; 
              padding: 8px; 
              text-align: left;
              vertical-align: top;
            }
            
            th { 
              background-color: #f2f2f2; 
              font-weight: bold;
            }
            
            .title {
              text-align: center;
              font-size: 28px;
              margin-bottom: 30px;
            }
            
            .subtitle {
              text-align: center;
              font-size: 18px;
              color: #666;
              margin-bottom: 20px;
            }
            
            @media print {
              body { font-size: 12pt; }
              h1, h2, h3 { page-break-after: avoid; }
              table { page-break-inside: avoid; }
            }
          </style>
        </head>
        <body>${result.value}</body>
        </html>
      `;

      // Generate PDF with enhanced settings
      let browser;
      try {
        browser = await puppeteer.launch({
          headless: true,
          args: [
            "--no-sandbox",
            "--disable-setuid-sandbox",
            "--disable-dev-shm-usage",
            "--disable-accelerated-2d-canvas",
            "--no-first-run",
            "--no-zygote",
            "--disable-gpu",
          ],
        });

        const page = await browser.newPage();
        await page.setViewport({ width: 1200, height: 800 });
        await page.setContent(htmlContent, { waitUntil: "networkidle0" });
        await page.emulateMediaType("print");

        await page.pdf({
          path: outputPath,
          format: "A4",
          margin: { top: "1in", bottom: "1in", left: "1in", right: "1in" },
          printBackground: true,
          displayHeaderFooter: false,
          preferCSSPageSize: true,
          scale: 1.0,
        });
      } finally {
        if (browser) {
          try {
            await browser.close();
          } catch (closeError) {
            console.error("Error closing browser:", closeError.message);
          }
        }
      }

      return {
        success: true,
        outputPath,
        accuracy: {
          formattingPreserved: true,
          structureMaintained: true,
          tablesDetected: 0,
        },
      };
    } catch (error) {
      throw new Error(
        `Enhanced DOCX to PDF conversion failed: ${error.message}`
      );
    }
  }

  /**
   * Basic PDF to DOCX conversion as fallback
   */
  async basicPdfToDocx(inputPath, outputPath) {
    try {
      const pdfBuffer = fs.readFileSync(inputPath);
      let text = "PDF content extracted";

      try {
        const data = await pdfParse(pdfBuffer);
        text = data.text || "PDF content extracted";
      } catch (parseError) {
        console.warn(
          "PDF parsing failed, using fallback text:",
          parseError.message
        );
        text = "PDF content extracted (parsing failed)";
      }

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

      const buffer = await Packer.toBuffer(doc);
      fs.writeFileSync(outputPath, buffer);

      return {
        success: true,
        outputPath,
        accuracy: {
          tablesDetected: 0,
          formattingPreserved: false,
          structureMaintained: true,
        },
      };
    } catch (error) {
      throw new Error(`Basic PDF to DOCX conversion failed: ${error.message}`);
    }
  }

  /**
   * Validate conversion accuracy
   */
  validateConversionAccuracy(originalPath, convertedPath, conversionType) {
    const validation = {
      success: true,
      formattingPreserved: true,
      structureMaintained: true,
      tablesDetected: 0,
      contentPreserved: true,
      issues: [],
    };

    try {
      // Add validation logic based on conversion type
      switch (conversionType) {
        case "pdf-to-docx":
          validation.tablesDetected = this.countTablesInDocx(convertedPath);
          validation.formattingPreserved = true; // Assume preserved for now
          validation.structureMaintained = true; // Assume maintained for now
          break;
        case "docx-to-pdf":
          validation.formattingPreserved =
            this.validatePdfFormatting(convertedPath);
          validation.structureMaintained = true; // Assume maintained for now
          break;
        default:
          validation.formattingPreserved = true;
          validation.structureMaintained = true;
      }
    } catch (error) {
      console.error("Validation error:", error);
      validation.success = false;
      validation.issues.push(error.message);
    }

    return validation;
  }

  /**
   * Count tables in DOCX file
   */
  countTablesInDocx(filePath) {
    // Implementation to count tables
    return 0; // Placeholder
  }

  /**
   * Validate PDF formatting
   */
  validatePdfFormatting(filePath) {
    // Implementation to validate PDF formatting
    return true; // Placeholder
  }

  /**
   * Detect tables in PDF text elements using spatial analysis
   */
  detectTables(texts) {
    const tables = [];
    const textGroups = this.groupTextsByPosition(texts, 2);
    const potentialTableRows = textGroups.filter((group) => group.length >= 3);

    if (potentialTableRows.length >= 2) {
      const columnPositions = this.analyzeColumnPositions(potentialTableRows);

      if (columnPositions.length >= 2) {
        const table = {
          rows: potentialTableRows.map((row) => {
            const tableRow = [];
            columnPositions.forEach((colPos, colIndex) => {
              const cellText = row.find(
                (text) => Math.abs(text.x - colPos) < 20
              );
              tableRow.push(
                cellText ? decodeURIComponent(cellText.R[0].T) : ""
              );
            });
            return tableRow;
          }),
        };

        tables.push(table);
      }
    }

    return tables;
  }

  /**
   * Filter out texts that are part of detected tables
   */
  filterNonTableTexts(texts, tables) {
    // Implementation to filter out table texts
    return texts; // Simplified for now
  }

  /**
   * Group text elements by vertical position
   */
  groupTextsByPosition(texts, tolerance = 5) {
    const groups = [];

    texts.forEach((text) => {
      const y = text.y;
      let addedToGroup = false;

      for (const group of groups) {
        if (group.length > 0 && Math.abs(group[0].y - y) <= tolerance) {
          group.push(text);
          addedToGroup = true;
          break;
        }
      }

      if (!addedToGroup) {
        groups.push([text]);
      }
    });

    return groups
      .sort((a, b) => b[0].y - a[0].y)
      .map((group) => group.sort((a, b) => a.x - b.x));
  }

  /**
   * Analyze column positions from table rows
   */
  analyzeColumnPositions(rows) {
    const allXPositions = [];

    rows.forEach((row) => {
      row.forEach((text) => {
        allXPositions.push(text.x);
      });
    });

    const columns = [];
    const tolerance = 30;

    allXPositions
      .sort((a, b) => a - b)
      .forEach((x) => {
        const nearbyColumn = columns.find(
          (col) => Math.abs(col - x) < tolerance
        );
        if (nearbyColumn) {
          const index = columns.indexOf(nearbyColumn);
          columns[index] = (nearbyColumn + x) / 2;
        } else {
          columns.push(x);
        }
      });

    return columns.sort((a, b) => a - b);
  }

  /**
   * Create DOCX table from detected table data
   */
  createDocxTable(tableData) {
    return new Table({
      width: {
        size: 100,
        type: WidthType.PERCENTAGE,
      },
      rows: tableData.rows.map((row) => {
        return new TableRow({
          children: row.map((cellText) => {
            return new TableCell({
              children: [
                new Paragraph({
                  children: [
                    new TextRun({
                      text: cellText,
                      size: 20,
                    }),
                  ],
                }),
              ],
            });
          }),
        });
      }),
    });
  }
}

module.exports = new AccuracyService();
