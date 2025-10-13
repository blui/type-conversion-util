/**
 * Spreadsheet Conversion Service
 *
 * Handles spreadsheet conversions (XLSX, CSV).
 * Supports multiple worksheets, streaming, and data preservation.
 *
 * Operations:
 * - XLSX to CSV (with multi-sheet support)
 * - CSV to XLSX (streaming for large files)
 */

const fs = require('fs');
const ExcelJS = require('exceljs');
const csvParser = require('csv-parser');
const { stringify } = require('csv-stringify/sync');

class SpreadsheetService {
  /**
   * Convert XLSX to CSV with multi-sheet support
   *
   * @param {string} inputPath - Path to XLSX file
   * @param {string} outputPath - Path for CSV file
   * @returns {Promise<Object>} Conversion result
   */
  async xlsxToCsv(inputPath, outputPath) {
    try {
      const workbook = new ExcelJS.Workbook();
      await workbook.xlsx.readFile(inputPath);

      const worksheets = workbook.worksheets;

      if (worksheets.length === 0) {
        throw new Error('No worksheets found in the Excel file');
      }

      if (worksheets.length > 1) {
        return await this._convertMultipleSheets(worksheets, outputPath);
      } else {
        return await this._convertSingleSheet(worksheets[0], outputPath);
      }
    } catch (error) {
      throw new Error(`XLSX to CSV conversion failed: ${error.message}`);
    }
  }

  /**
   * Convert single worksheet to CSV
   *
   * @param {Object} worksheet - ExcelJS worksheet
   * @param {string} outputPath - Path for CSV file
   * @returns {Promise<Object>} Conversion result
   * @private
   */
  async _convertSingleSheet(worksheet, outputPath) {
    const data = this.extractWorksheetData(worksheet);
    const csv = stringify(data, {
      quoted: true,
      quoted_empty: true,
      quoted_string: true
    });

    fs.writeFileSync(outputPath, csv);
    return { success: true, outputPath };
  }

  /**
   * Convert multiple worksheets to separate CSV files
   *
   * @param {Array} worksheets - ExcelJS worksheets
   * @param {string} outputPath - Base path for CSV files
   * @returns {Promise<Object>} Conversion result
   * @private
   */
  async _convertMultipleSheets(worksheets, outputPath) {
    const basePath = outputPath.replace('.csv', '');
    const results = [];
    const usedSheetNames = {};

    for (let i = 0; i < worksheets.length; i++) {
      const worksheet = worksheets[i];
      let baseSheetName = worksheet.name.replace(/[^a-zA-Z0-9]/g, '_');
      let sheetName = baseSheetName;
      let counter = 1;

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
        quoted_string: true
      });

      fs.writeFileSync(sheetPath, csv);
      results.push({
        path: sheetPath,
        name: worksheet.name,
        rows: sheetData.length
      });
    }

    const summaryPath = `${basePath}_summary.txt`;
    const summary = `Multiple worksheets converted to separate CSV files:\n\n${results
      .map((file, i) => `${i + 1}. ${file.name} -> ${file.path} (${file.rows} rows)`)
      .join('\n')}`;
    fs.writeFileSync(summaryPath, summary);

    return {
      success: true,
      outputPath: summaryPath,
      additionalFiles: results.map(f => f.path),
      worksheetCount: worksheets.length,
      message: `Converted ${worksheets.length} worksheets to separate CSV files`,
      details: results
    };
  }

  /**
   * Extract data from worksheet with formatting preservation
   *
   * @param {Object} worksheet - ExcelJS worksheet
   * @returns {Array} Extracted data rows
   */
  extractWorksheetData(worksheet) {
    const rows = [];
    const dimensions = worksheet.dimensions;

    if (!dimensions) {
      return [['No data found']];
    }

    const startRow = dimensions.top;
    const endRow = dimensions.bottom;
    const startCol = dimensions.left;
    const endCol = dimensions.right;

    for (let rowNum = startRow; rowNum <= endRow; rowNum++) {
      const row = worksheet.getRow(rowNum);
      const rowData = [];

      for (let colNum = startCol; colNum <= endCol; colNum++) {
        const cell = row.getCell(colNum);
        let cellValue = '';

        if (cell.value !== null && cell.value !== undefined) {
          cellValue = this._extractCellValue(cell);
        }

        rowData.push(cellValue);
      }

      rows.push(rowData);
    }

    return rows;
  }

  /**
   * Extract value from cell with type handling
   *
   * @param {Object} cell - ExcelJS cell
   * @returns {string} Extracted cell value
   * @private
   */
  _extractCellValue(cell) {
    if (typeof cell.value === 'object') {
      if (cell.value.text) {
        return cell.value.text;
      } else if (cell.value.result) {
        return cell.value.result;
      } else if (cell.value.richText) {
        return cell.value.richText.map(rt => rt.text).join('');
      } else {
        return JSON.stringify(cell.value);
      }
    } else if (typeof cell.value === 'number') {
      return cell.value.toString();
    } else if (typeof cell.value === 'boolean') {
      return cell.value ? 'TRUE' : 'FALSE';
    } else if (cell.value instanceof Date) {
      return cell.value.toISOString().split('T')[0];
    } else {
      return cell.value.toString();
    }
  }

  /**
   * Convert CSV to XLSX using streaming
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
          useSharedStrings: false
        });
        const worksheet = workbook.addWorksheet('Sheet1');

        let headers = null;

        parser
          .on('headers', (hdrs) => {
            headers = hdrs;
            worksheet.addRow(headers).commit();
          })
          .on('data', (row) => {
            if (!headers) headers = Object.keys(row);
            const values = headers.map(h => row[h] || '');
            worksheet.addRow(values).commit();
          })
          .on('end', async () => {
            try {
              await worksheet.commit();
              await workbook.commit();
              resolve({ success: true, outputPath });
            } catch (err) {
              reject(new Error(`CSV to XLSX conversion failed: ${err.message}`));
            }
          })
          .on('error', (err) => {
            reject(new Error(`CSV to XLSX conversion failed: ${err.message}`));
          });

        readStream.pipe(parser);
      });
    } catch (error) {
      throw new Error(`CSV to XLSX conversion failed: ${error.message}`);
    }
  }
}

module.exports = new SpreadsheetService();
