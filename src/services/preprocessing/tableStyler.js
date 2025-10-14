/**
 * Table Style Utilities
 *
 * Advanced table structure optimization for DOCX preprocessing.
 * Handles table border styling, cell spacing, and layout preservation.
 */

class TableStyler {
  /**
   * Initialize table style optimization mappings
   * @returns {Object} Table style mappings for compatibility optimization
   */
  initializeTableStyleMappings() {
    return {
      // Microsoft Office table styles with LibreOffice compatibility
      "Table Grid": {
        borders: "all",
        borderStyle: "single",
        borderWidth: "4",
        cellSpacing: "0",
      },
      "Light Shading": {
        background: "f2f2f2",
        borders: "all",
        borderStyle: "single",
        borderWidth: "1",
      },
      "Light Shading Accent 1": {
        background: "dbe5f1",
        borders: "all",
        borderStyle: "single",
        borderWidth: "1",
      },
      "Light Shading Accent 2": {
        background: "fde9d9",
        borders: "all",
        borderStyle: "single",
        borderWidth: "1",
      },
      "Medium Shading 1": {
        background: "a6a6a6",
        borders: "all",
        borderStyle: "single",
        borderWidth: "2",
      },
      "Medium Shading 2": {
        background: "d9d9d9",
        borders: "all",
        borderStyle: "single",
        borderWidth: "2",
      },

      // Professional table styles
      "Plain Table 1": {
        borders: "all",
        borderStyle: "single",
        borderWidth: "2",
        cellPadding: "108", // 0.08 inches in twips
      },
      "Plain Table 2": {
        borders: "all",
        borderStyle: "single",
        borderWidth: "1",
        background: "ffffff",
      },
      "Plain Table 3": {
        borders: "all",
        borderStyle: "single",
        borderWidth: "3",
        cellSpacing: "60", // 0.04 inches in twips
      },
    };
  }

  /**
   * Apply table style optimizations to XML content
   * @param {string} xmlContent - XML content to process
   * @returns {string} Processed XML with optimized table styles
   */
  applyTableStyleOptimizations(xmlContent) {
    let processedXml = xmlContent;

    // Ensure all tables have proper border definitions
    processedXml = this.ensureTableBorders(processedXml);

    // Normalize table cell spacing and padding
    processedXml = this.normalizeTableSpacing(processedXml);

    // Fix table width specifications
    processedXml = this.fixTableWidths(processedXml);

    // Optimize table layout for LibreOffice compatibility
    processedXml = this.optimizeTableLayout(processedXml);

    return processedXml;
  }

  /**
   * Ensure all tables have proper border definitions
   * @param {string} xmlContent - XML content to process
   * @returns {string} XML with ensured table borders
   */
  ensureTableBorders(xmlContent) {
    let processedXml = xmlContent;

    // Add missing table borders where they should exist
    const tableRegex = /<w:tbl[^>]*>(.*?)<\/w:tbl>/gs;
    processedXml = processedXml.replace(tableRegex, (match, tableContent) => {
      // Check if table has border properties
      if (!tableContent.includes("<w:tblBorders>")) {
        // Add default table borders
        const borderProperties = `
          <w:tblPr>
            <w:tblBorders>
              <w:top w:val="single" w:sz="4" w:space="0" w:color="000000"/>
              <w:left w:val="single" w:sz="4" w:space="0" w:color="000000"/>
              <w:bottom w:val="single" w:sz="4" w:space="0" w:color="000000"/>
              <w:right w:val="single" w:sz="4" w:space="0" w:color="000000"/>
              <w:insideH w:val="single" w:sz="4" w:space="0" w:color="000000"/>
              <w:insideV w:val="single" w:sz="4" w:space="0" w:color="000000"/>
            </w:tblBorders>
          </w:tblPr>`;

        return match.replace("<w:tbl>", "<w:tbl>" + borderProperties);
      }
      return match;
    });

    return processedXml;
  }

  /**
   * Normalize table cell spacing and padding
   * @param {string} xmlContent - XML content to process
   * @returns {string} XML with normalized table spacing
   */
  normalizeTableSpacing(xmlContent) {
    let processedXml = xmlContent;

    // Normalize cell margins to consistent values
    const cellMarginRegex = /<w:tcMar>(.*?)<\/w:tcMar>/gs;
    processedXml = processedXml.replace(cellMarginRegex, (match, margins) => {
      // Ensure consistent cell margins for better LibreOffice compatibility
      return `<w:tcMar>
        <w:top w:w="108" w:type="dxa"/>
        <w:left w:w="108" w:type="dxa"/>
        <w:bottom w:w="108" w:type="dxa"/>
        <w:right w:w="108" w:type="dxa"/>
      </w:tcMar>`;
    });

    return processedXml;
  }

  /**
   * Fix table width specifications for consistency
   * @param {string} xmlContent - XML content to process
   * @returns {string} XML with fixed table widths
   */
  fixTableWidths(xmlContent) {
    let processedXml = xmlContent;

    // Ensure table widths are properly specified
    const tblWRegex = /<w:tblW[^>]*\/>/g;
    processedXml = processedXml.replace(tblWRegex, (match) => {
      if (!match.includes("w:w=") || match.includes('w:w="0"')) {
        // Set default table width to 100% of container
        return '<w:tblW w:w="5000" w:type="pct"/>';
      }
      return match;
    });

    return processedXml;
  }

  /**
   * Optimize table layout for LibreOffice compatibility
   * @param {string} xmlContent - XML content to process
   * @returns {string} XML with optimized table layout
   */
  optimizeTableLayout(xmlContent) {
    let processedXml = xmlContent;

    // Add table layout properties for better LibreOffice rendering
    const tblPrRegex = /<w:tblPr>(.*?)<\/w:tblPr>/gs;
    processedXml = processedXml.replace(tblPrRegex, (match, properties) => {
      // Add layout property if not present
      if (!properties.includes("<w:tblLayout")) {
        return match.replace(
          "</w:tblPr>",
          '<w:tblLayout w:type="fixed"/></w:tblPr>'
        );
      }
      return match;
    });

    return processedXml;
  }
}

module.exports = TableStyler;
