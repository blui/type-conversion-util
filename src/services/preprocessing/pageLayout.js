/**
 * Page Layout Utilities
 *
 * Page layout preservation and pagination control for DOCX preprocessing.
 * Handles page size, margins, orientation, and section breaks.
 */

class PageLayout {
  /**
   * Initialize page layout preservation mappings
   * @returns {Object} Page layout mappings for compatibility optimization
   */
  initializePageLayoutMappings() {
    return {
      // Standard page sizes with precise dimensions
      letter: {
        width: 12240, // 8.5 inches in twips
        height: 15840, // 11 inches in twips
        orientation: "portrait",
      },
      a4: {
        width: 11906, // 210mm in twips
        height: 16838, // 297mm in twips
        orientation: "portrait",
      },
      legal: {
        width: 12240, // 8.5 inches in twips
        height: 20160, // 14 inches in twips
        orientation: "portrait",
      },
      a3: {
        width: 16838, // 297mm in twips
        height: 23811, // 420mm in twips
        orientation: "portrait",
      },

      // Page orientations
      portrait: {
        margins: {
          top: 1440, // 1 inch
          bottom: 1440, // 1 inch
          left: 1440, // 1 inch
          right: 1440, // 1 inch
        },
      },
      landscape: {
        margins: {
          top: 1440, // 1 inch
          bottom: 1440, // 1 inch
          left: 1440, // 1 inch
          right: 1440, // 1 inch
        },
      },
    };
  }

  /**
   * Apply page layout optimizations to XML content
   * @param {string} xmlContent - XML content to process
   * @returns {string} Processed XML with optimized page layout
   */
  applyPageLayoutOptimizations(xmlContent) {
    let processedXml = xmlContent;

    // Ensure proper page size specifications
    processedXml = this.ensurePageSize(processedXml);

    // Normalize page margins
    processedXml = this.normalizePageMargins(processedXml);

    // Preserve section breaks and page breaks
    processedXml = this.preserveSectionBreaks(processedXml);

    // Optimize pagination control
    processedXml = this.optimizePagination(processedXml);

    return processedXml;
  }

  /**
   * Ensure proper page size specifications
   * @param {string} xmlContent - XML content to process
   * @returns {string} XML with ensured page sizes
   */
  ensurePageSize(xmlContent) {
    let processedXml = xmlContent;

    // Check for and fix missing or invalid page size
    const pgSzRegex = /<w:pgSz[^>]*\/>/g;
    processedXml = processedXml.replace(pgSzRegex, (match) => {
      // Default to A4 if page size is missing or invalid
      if (!match.includes("w:w=") || !match.includes("w:h=")) {
        return '<w:pgSz w:w="11906" w:h="16838"/>';
      }
      return match;
    });

    return processedXml;
  }

  /**
   * Normalize page margins for consistency
   * @param {string} xmlContent - XML content to process
   * @returns {string} XML with normalized page margins
   */
  normalizePageMargins(xmlContent) {
    let processedXml = xmlContent;

    // Ensure consistent page margins
    const pgMarRegex = /<w:pgMar[^>]*\/>/g;
    processedXml = processedXml.replace(pgMarRegex, (match) => {
      // Use standard 1-inch margins if not properly specified
      if (
        !match.includes("w:top=") ||
        !match.includes("w:bottom=") ||
        !match.includes("w:left=") ||
        !match.includes("w:right=")
      ) {
        return '<w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440"/>';
      }
      return match;
    });

    return processedXml;
  }

  /**
   * Preserve section breaks and page breaks
   * @param {string} xmlContent - XML content to process
   * @returns {string} XML with preserved section breaks
   */
  preserveSectionBreaks(xmlContent) {
    let processedXml = xmlContent;

    // Ensure section breaks are properly formatted
    const sectPrRegex = /<w:sectPr>(.*?)<\/w:sectPr>/gs;
    processedXml = processedXml.replace(
      sectPrRegex,
      (match, sectionContent) => {
        // Ensure section type is specified
        if (!sectionContent.includes("<w:type")) {
          return match.replace(
            "<w:sectPr>",
            '<w:sectPr><w:type w:val="nextPage"/>'
          );
        }
        return match;
      }
    );

    return processedXml;
  }

  /**
   * Optimize pagination control for better document flow
   * @param {string} xmlContent - XML content to process
   * @returns {string} XML with optimized pagination
   */
  optimizePagination(xmlContent) {
    let processedXml = xmlContent;

    // Ensure proper widow/orphan control
    const widowControlRegex = /<w:widowControl[^>]*\/>/g;
    processedXml = processedXml.replace(widowControlRegex, (match) => {
      if (!match.includes("w:val=")) {
        return '<w:widowControl w:val="1"/>';
      }
      return match;
    });

    // Ensure proper page break control
    const pgBreakBeforeRegex = /<w:pageBreakBefore[^>]*\/>/g;
    processedXml = processedXml.replace(pgBreakBeforeRegex, (match) => {
      if (!match.includes("w:val=")) {
        return '<w:pageBreakBefore w:val="0"/>';
      }
      return match;
    });

    return processedXml;
  }
}

module.exports = PageLayout;
