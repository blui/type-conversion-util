/**
 * Advanced DOCX Pre-processor
 *
 * Normalizes DOCX files for optimal LibreOffice conversion.
 * Implements 7-phase processing pipeline to address format incompatibilities.
 */

const fs = require("fs");
const path = require("path");
const AdmZip = require("adm-zip");

/**
 * DOCX normalization engine with 7-phase processing pipeline.
 */
class AdvancedDocxPreProcessor {
  /**
   * Initialize font mappings and color tables
   */
  constructor() {
    // Font mapping table with visual size compensation factors
    // LibreOffice renders some fonts at different visual sizes than Word
    this.fontMap = this._initializeFontMappings();

    // Theme color to RGB conversion table
    // Word theme colors must be converted to explicit RGB for LibreOffice
    this.themeColorMap = this._initializeColorMappings();
  }

  /**
   * @brief Initialize comprehensive font mapping table
   * @return {Object} Font mapping with size multipliers
   * @private
   */
  _initializeFontMappings() {
    return {
      // Modern Microsoft Office 365 fonts
      Aptos: { target: "Calibri", sizeMultiplier: 0.98 },
      "Aptos Narrow": { target: "Arial Narrow", sizeMultiplier: 0.98 },
      "Aptos Display": { target: "Calibri", sizeMultiplier: 1.02 },
      "Aptos Serif": { target: "Georgia", sizeMultiplier: 1.0 },
      Grandview: { target: "Verdana", sizeMultiplier: 0.98 },
      Seaford: { target: "Georgia", sizeMultiplier: 1.0 },
      Skeena: { target: "Verdana", sizeMultiplier: 1.0 },
      Tenorite: { target: "Tahoma", sizeMultiplier: 1.0 },

      // Microsoft Office theme fonts
      "Calibri Light": { target: "Calibri", sizeMultiplier: 1.0 },
      "Segoe UI Light": { target: "Segoe UI", sizeMultiplier: 1.0 },
      "Helvetica Neue": { target: "Helvetica", sizeMultiplier: 1.0 },

      // Proprietary to open-source equivalents
      "Arial Narrow": { target: "Arial", sizeMultiplier: 0.95 },
      "Times New Roman": { target: "Liberation Serif", sizeMultiplier: 1.0 },
      Arial: { target: "Liberation Sans", sizeMultiplier: 1.0 },
      "Courier New": { target: "Liberation Mono", sizeMultiplier: 1.0 },

      // Font variations
      "Calibri Bold": { target: "Calibri", sizeMultiplier: 1.0 },
      "Arial Bold": { target: "Arial", sizeMultiplier: 1.0 },

      // Safe fonts (no substitution needed)
      Calibri: { target: "Calibri", sizeMultiplier: 1.0 },
      Verdana: { target: "Verdana", sizeMultiplier: 1.0 },
      Georgia: { target: "Georgia", sizeMultiplier: 1.0 },
      Tahoma: { target: "Tahoma", sizeMultiplier: 1.0 },
      "Liberation Sans": { target: "Liberation Sans", sizeMultiplier: 1.0 },
      "Liberation Serif": { target: "Liberation Serif", sizeMultiplier: 1.0 },
      "Liberation Mono": { target: "Liberation Mono", sizeMultiplier: 1.0 },
    };
  }

  /**
   * @brief Initialize Word theme color to RGB mapping
   * @return {Object} Theme color mappings
   * @private
   */
  _initializeColorMappings() {
    return {
      accent1: "4472C4",
      accent2: "ED7D31",
      accent3: "A5A5A5",
      accent4: "FFC000",
      accent5: "5B9BD5",
      accent6: "70AD47",
      dark1: "000000",
      dark2: "44546A",
      light1: "FFFFFF",
      light2: "E7E6E6",
      hyperlink: "0563C1",
      followedHyperlink: "954F72",
      background1: "FFFFFF",
      background2: "E7E6E6",
      text1: "000000",
      text2: "44546A",
    };
  }

  /**
   * @brief Main entry point - Pre-process DOCX for LibreOffice compatibility
   * @param {string} inputPath - Path to original DOCX file
   * @param {string} outputPath - Path for pre-processed DOCX output
   * @return {Promise<Object>} Processing result with detailed metrics
   * @throws {Error} If processing fails
   */
  async process(inputPath, outputPath) {
    // Initialize optimization tracking metrics
    const fixes = this._initializeMetrics();

    try {
      console.log("Advanced DOCX pre-processing initiated...");
      console.log("Target fidelity: 97-99%");

      // Load DOCX as ZIP archive
      const zip = new AdmZip(inputPath);

      // Execute 7-phase processing pipeline
      this._executePhase1(zip, fixes); // Document content
      this._executePhase2(zip, fixes); // Styles
      this._executePhase3(zip, fixes); // Numbering
      this._executePhase4(zip, fixes); // Settings
      this._executePhase5(zip, fixes); // Headers/footers
      this._executePhase6(zip, fixes); // Footnotes/endnotes
      this._executePhase7(zip, outputPath, fixes); // Finalization

      // Calculate and report results
      return this._generateReport(inputPath, outputPath, fixes);
    } catch (error) {
      console.error("Advanced pre-processing error:", error);
      throw new Error(`Advanced pre-processing failed: ${error.message}`);
    }
  }

  /**
   * @brief Initialize optimization tracking metrics
   * @return {Object} Metrics object
   * @private
   */
  _initializeMetrics() {
    return {
      fontsNormalized: 0,
      fontSizesAdjusted: 0,
      themeColorsConverted: 0,
      stylesFlattened: 0,
      stylesSimplified: 0,
      spacingNormalized: 0,
      tablesOptimized: 0,
      imagesNormalized: 0,
      sectionsNormalized: 0,
      paginationFixed: 0,
      numberingSimplified: 0,
      paragraphsAdjusted: 0,
      boldFixed: 0,
      keepWithNextRemoved: 0,
    };
  }

  /**
   * @brief Phase 1 - Process main document content
   * @param {AdmZip} zip - DOCX archive
   * @param {Object} fixes - Metrics tracker
   * @private
   */
  _executePhase1(zip, fixes) {
    console.log("[1/7] Processing document content...");
    const documentEntry = zip.getEntry("word/document.xml");

    if (documentEntry) {
      let documentXml = documentEntry.getData().toString("utf8");
      documentXml = this._processDocumentXml(documentXml, fixes);
      zip.updateFile("word/document.xml", Buffer.from(documentXml, "utf8"));
    }
  }

  /**
   * @brief Phase 2 - Process and flatten style definitions
   * @param {AdmZip} zip - DOCX archive
   * @param {Object} fixes - Metrics tracker
   * @private
   */
  _executePhase2(zip, fixes) {
    console.log("[2/7] Processing and flattening styles...");
    const stylesEntry = zip.getEntry("word/styles.xml");

    if (stylesEntry) {
      let stylesXml = stylesEntry.getData().toString("utf8");
      stylesXml = this._processStylesXml(stylesXml, fixes);
      zip.updateFile("word/styles.xml", Buffer.from(stylesXml, "utf8"));
    }
  }

  /**
   * @brief Phase 3 - Simplify numbering definitions
   * @param {AdmZip} zip - DOCX archive
   * @param {Object} fixes - Metrics tracker
   * @private
   */
  _executePhase3(zip, fixes) {
    console.log("[3/7] Simplifying numbering definitions...");
    const numberingEntry = zip.getEntry("word/numbering.xml");

    if (numberingEntry) {
      let numberingXml = numberingEntry.getData().toString("utf8");
      numberingXml = this._processNumberingXml(numberingXml, fixes);
      zip.updateFile("word/numbering.xml", Buffer.from(numberingXml, "utf8"));
    }
  }

  /**
   * @brief Phase 4 - Normalize document settings
   * @param {AdmZip} zip - DOCX archive
   * @param {Object} fixes - Metrics tracker
   * @private
   */
  _executePhase4(zip, fixes) {
    console.log("[4/7] Normalizing document settings...");
    const settingsEntry = zip.getEntry("word/settings.xml");

    if (settingsEntry) {
      let settingsXml = settingsEntry.getData().toString("utf8");
      settingsXml = this._processSettingsXml(settingsXml, fixes);
      zip.updateFile("word/settings.xml", Buffer.from(settingsXml, "utf8"));
    }
  }

  /**
   * @brief Phase 5 - Process headers and footers
   * @param {AdmZip} zip - DOCX archive
   * @param {Object} fixes - Metrics tracker
   * @private
   */
  _executePhase5(zip, fixes) {
    console.log("[5/7] Processing headers and footers...");
    const entries = zip.getEntries();

    entries.forEach((entry) => {
      if (entry.entryName.match(/word\/(header|footer)\d*\.xml$/)) {
        let xml = entry.getData().toString("utf8");
        xml = this._processDocumentXml(xml, fixes);
        zip.updateFile(entry.entryName, Buffer.from(xml, "utf8"));
      }
    });
  }

  /**
   * @brief Phase 6 - Process footnotes and endnotes
   * @param {AdmZip} zip - DOCX archive
   * @param {Object} fixes - Metrics tracker
   * @private
   */
  _executePhase6(zip, fixes) {
    console.log("[6/7] Processing footnotes and endnotes...");

    // Process footnotes
    const footnotesEntry = zip.getEntry("word/footnotes.xml");
    if (footnotesEntry) {
      let footnotesXml = footnotesEntry.getData().toString("utf8");
      footnotesXml = this._processDocumentXml(footnotesXml, fixes);
      zip.updateFile("word/footnotes.xml", Buffer.from(footnotesXml, "utf8"));
    }

    // Process endnotes
    const endnotesEntry = zip.getEntry("word/endnotes.xml");
    if (endnotesEntry) {
      let endnotesXml = endnotesEntry.getData().toString("utf8");
      endnotesXml = this._processDocumentXml(endnotesXml, fixes);
      zip.updateFile("word/endnotes.xml", Buffer.from(endnotesXml, "utf8"));
    }
  }

  /**
   * @brief Phase 7 - Finalize and save processed DOCX
   * @param {AdmZip} zip - DOCX archive
   * @param {string} outputPath - Output file path
   * @param {Object} fixes - Metrics tracker
   * @private
   */
  _executePhase7(zip, outputPath, fixes) {
    console.log("[7/7] Finalizing pre-processed document...");
    zip.writeZip(outputPath);
    this._printMetrics(fixes);
  }

  /**
   * @brief Print optimization metrics to console
   * @param {Object} fixes - Metrics tracker
   * @private
   */
  _printMetrics(fixes) {
    console.log("");
    console.log("Advanced pre-processing complete:");
    console.log(`  Fonts normalized: ${fixes.fontsNormalized}`);
    console.log(`  Font sizes adjusted: ${fixes.fontSizesAdjusted}`);
    console.log(`  Theme colors converted: ${fixes.themeColorsConverted}`);
    console.log(`  Styles flattened: ${fixes.stylesFlattened}`);
    console.log(`  Styles simplified: ${fixes.stylesSimplified}`);
    console.log(`  Spacing normalized: ${fixes.spacingNormalized}`);
    console.log(`  Tables optimized: ${fixes.tablesOptimized}`);
    console.log(`  Images normalized: ${fixes.imagesNormalized}`);
    console.log(`  Pagination fixed: ${fixes.paginationFixed}`);
    console.log(`  Keep-with-next removed: ${fixes.keepWithNextRemoved}`);

    const totalFixes = Object.values(fixes).reduce((a, b) => a + b, 0);
    console.log(`  Total optimizations: ${totalFixes}`);
  }

  /**
   * @brief Generate processing result report
   * @param {string} inputPath - Input file path
   * @param {string} outputPath - Output file path
   * @param {Object} fixes - Metrics tracker
   * @return {Object} Processing result
   * @private
   */
  _generateReport(inputPath, outputPath, fixes) {
    const totalFixes = Object.values(fixes).reduce((a, b) => a + b, 0);

    return {
      success: true,
      inputPath,
      outputPath,
      fixes,
      totalOptimizations: totalFixes,
    };
  }

  /**
   * @brief Process document XML with all normalizations
   * @param {string} xml - Document XML content
   * @param {Object} fixes - Metrics tracker
   * @return {string} Normalized XML
   * @private
   */
  _processDocumentXml(xml, fixes) {
    let modified = xml;

    // Apply normalizations in optimized order
    modified = this._normalizeFonts(modified, fixes);
    modified = this._convertThemeColors(modified, fixes);
    modified = this._removeUnsupportedEffects(modified, fixes);
    modified = this._normalizeSpacing(modified, fixes);
    modified = this._optimizeTables(modified, fixes);
    modified = this._normalizeImages(modified, fixes);
    modified = this._fixPaginationControls(modified, fixes);
    modified = this._normalizeFontStyles(modified, fixes);
    modified = this._normalizeColors(modified, fixes);

    return modified;
  }

  /**
   * @brief Normalize font families with size compensation
   * @param {string} xml - XML content
   * @param {Object} fixes - Metrics tracker
   * @return {string} Normalized XML
   * @private
   *
   * Maps proprietary Microsoft fonts to LibreOffice-compatible equivalents.
   * Applies size multipliers to compensate for visual rendering differences.
   */
  _normalizeFonts(xml, fixes) {
    let modified = xml;

    // Apply font substitutions
    Object.entries(this.fontMap).forEach(([oldFont, mapping]) => {
      const newFont = mapping.target;
      const sizeMultiplier = mapping.sizeMultiplier;

      if (oldFont !== newFont) {
        const fontPattern = new RegExp(
          `(w:ascii="|w:hAnsi="|w:cs="|w:eastAsia=")${this._escapeRegex(
            oldFont
          )}"`,
          "gi"
        );

        const before = modified;
        modified = modified.replace(fontPattern, `$1${newFont}"`);

        if (modified !== before) {
          fixes.fontsNormalized++;

          if (sizeMultiplier !== 1.0) {
            modified = this._adjustFontSizes(modified, sizeMultiplier, fixes);
          }
        }
      }
    });

    // Remove theme font references
    modified = this._removeThemeFontReferences(modified);

    // Ensure explicit fonts
    modified = this._ensureExplicitFonts(modified);

    return modified;
  }

  /**
   * @brief Adjust font sizes by multiplier
   * @param {string} xml - XML content
   * @param {number} multiplier - Size adjustment factor
   * @param {Object} fixes - Metrics tracker
   * @return {string} Modified XML
   * @private
   */
  _adjustFontSizes(xml, multiplier, fixes) {
    const sizePattern = /<w:sz w:val="(\d+)"\/>/g;

    return xml.replace(sizePattern, (match, size) => {
      const originalSize = parseInt(size);
      const adjustedSize = Math.round(originalSize * multiplier);

      if (adjustedSize !== originalSize) {
        fixes.fontSizesAdjusted++;
        return `<w:sz w:val="${adjustedSize}"/>`;
      }
      return match;
    });
  }

  /**
   * @brief Remove theme font attribute references
   * @param {string} xml - XML content
   * @return {string} Modified XML
   * @private
   */
  _removeThemeFontReferences(xml) {
    let modified = xml;
    const themeFontAttrs = [
      "w:asciiTheme",
      "w:hAnsiTheme",
      "w:cstheme",
      "w:eastAsiaTheme",
    ];

    themeFontAttrs.forEach((attr) => {
      const pattern = new RegExp(`\\s${attr}="[^"]*"`, "g");
      modified = modified.replace(pattern, "");
    });

    return modified;
  }

  /**
   * @brief Ensure all text runs have explicit font specifications
   * @param {string} xml - XML content
   * @return {string} Modified XML
   * @private
   */
  _ensureExplicitFonts(xml) {
    return xml.replace(
      /(<w:rPr>)(?!.*<w:rFonts)/g,
      '$1<w:rFonts w:ascii="Calibri" w:hAnsi="Calibri"/>'
    );
  }

  /**
   * @brief Convert theme colors to explicit RGB values
   * @param {string} xml - XML content
   * @param {Object} fixes - Metrics tracker
   * @return {string} Modified XML
   * @private
   *
   * Word uses theme-based colors that must be converted to explicit RGB
   * for consistent rendering in LibreOffice.
   */
  _convertThemeColors(xml, fixes) {
    let modified = xml;

    Object.entries(this.themeColorMap).forEach(([themeName, rgbColor]) => {
      // Convert text colors
      modified = this._convertTextThemeColor(
        modified,
        themeName,
        rgbColor,
        fixes
      );

      // Convert fill/shading colors
      modified = this._convertFillThemeColor(
        modified,
        themeName,
        rgbColor,
        fixes
      );
    });

    return modified;
  }

  /**
   * @brief Convert theme color in text elements
   * @param {string} xml - XML content
   * @param {string} themeName - Theme color name
   * @param {string} rgbColor - RGB hex value
   * @param {Object} fixes - Metrics tracker
   * @return {string} Modified XML
   * @private
   */
  _convertTextThemeColor(xml, themeName, rgbColor, fixes) {
    const themePattern = new RegExp(
      `(<w:color[^>]*?)w:themeColor="${themeName}"([^>]*?)(/?>)`,
      "g"
    );

    return xml.replace(themePattern, (match, before, after, close) => {
      fixes.themeColorsConverted++;

      // Remove existing color value and theme attributes
      let cleanBefore = before.replace(/\s*w:val="[^"]*"/g, "");
      let cleanAfter = after.replace(/w:theme(Shade|Tint)="[^"]*"/g, "");

      return `${cleanBefore}w:val="${rgbColor}"${cleanAfter}${close}`;
    });
  }

  /**
   * @brief Convert theme color in fill/shading elements
   * @param {string} xml - XML content
   * @param {string} themeName - Theme color name
   * @param {string} rgbColor - RGB hex value
   * @param {Object} fixes - Metrics tracker
   * @return {string} Modified XML
   * @private
   */
  _convertFillThemeColor(xml, themeName, rgbColor, fixes) {
    const shadingPattern = new RegExp(
      `(<w:shd[^>]*?)w:themeFill="${themeName}"([^>]*?)(/?>)`,
      "g"
    );

    return xml.replace(shadingPattern, (match, before, after, close) => {
      fixes.themeColorsConverted++;

      // Remove existing fill value and theme attributes
      let cleanBefore = before.replace(/\s*w:fill="[^"]*"/g, "");
      let cleanAfter = after.replace(/w:theme(Shade|Tint)="[^"]*"/g, "");

      return `${cleanBefore}w:fill="${rgbColor}"${cleanAfter}${close}`;
    });
  }

  /**
   * @brief Remove Word-specific effects unsupported by LibreOffice
   * @param {string} xml - XML content
   * @param {Object} fixes - Metrics tracker
   * @return {string} Modified XML
   * @private
   */
  _removeUnsupportedEffects(xml, fixes) {
    let modified = xml;

    const unsupportedEffects = [
      "w:shadow",
      "w:outline",
      "w:emboss",
      "w:imprint",
      "w14:glow",
      "w14:shadow",
      "w14:reflection",
      "w14:textOutline",
      "w14:textFill",
      "w14:ligatures",
      "w14:numForm",
      "w14:numSpacing",
      "w14:stylisticSets",
      "w14:cntxtAlts",
      "w14:props3d",
      "w:effect",
      "w:bdr",
      'w:shd[^>]*w:fill="auto"',
    ];

    unsupportedEffects.forEach((effect) => {
      // Remove self-closing tags
      const selfClosingPattern = new RegExp(`<${effect}[^/]*/>`, "g");
      const before = modified.length;
      modified = modified.replace(selfClosingPattern, "");
      if (modified.length !== before) fixes.stylesSimplified++;

      // Remove tags with content
      const pairPattern = new RegExp(
        `<${effect}[^>]*>.*?</${effect.split("[")[0]}>`,
        "gs"
      );
      modified = modified.replace(pairPattern, "");
    });

    return modified;
  }

  /**
   * @brief Normalize spacing for pagination fidelity
   * @param {string} xml - XML content
   * @param {Object} fixes - Metrics tracker
   * @return {string} Modified XML
   * @private
   *
   * Word and LibreOffice interpret line spacing differently.
   * This normalization is critical for accurate page count matching.
   */
  _normalizeSpacing(xml, fixes) {
    let modified = xml;

    // Normalize line spacing
    modified = this._normalizeLineSpacing(modified, fixes);

    // Normalize paragraph spacing
    modified = this._normalizeParagraphSpacing(modified, fixes);

    // Normalize indentation
    modified = this._normalizeIndentation(modified, fixes);

    return modified;
  }

  /**
   * @brief Normalize line spacing rules
   * @param {string} xml - XML content
   * @param {Object} fixes - Metrics tracker
   * @return {string} Modified XML
   * @private
   */
  _normalizeLineSpacing(xml, fixes) {
    // Convert auto line spacing to explicit values
    return xml.replace(
      /<w:spacing([^>]*)w:lineRule="auto"([^>]*)\/>/g,
      (match, before, after) => {
        fixes.spacingNormalized++;

        // Remove existing line values to avoid duplicates
        let cleanBefore = before.replace(/\s*w:line="[^"]*"/g, "");
        let cleanAfter = after.replace(/\s*w:line="[^"]*"/g, "");

        return `<w:spacing${cleanBefore}w:lineRule="exact" w:line="240"${cleanAfter}/>`;
      }
    );
  }

  /**
   * @brief Normalize paragraph spacing (before/after)
   * @param {string} xml - XML content
   * @param {Object} fixes - Metrics tracker
   * @return {string} Modified XML
   * @private
   */
  _normalizeParagraphSpacing(xml, fixes) {
    return xml.replace(/<w:spacing([^>]*)\/>/g, (match, attrs) => {
      let normalized = attrs;

      // Ensure explicit before/after values
      if (!attrs.includes("w:before=")) {
        normalized += ' w:before="0"';
        fixes.spacingNormalized++;
      }
      if (!attrs.includes("w:after=")) {
        normalized += ' w:after="0"';
        fixes.spacingNormalized++;
      }

      // Normalize line spacing values
      if (attrs.includes("w:line=")) {
        const lineMatch = attrs.match(/w:line="(\d+)"/);
        if (lineMatch) {
          const lineValue = parseInt(lineMatch[1]);
          if (lineValue < 100 || lineValue > 600) {
            normalized = normalized.replace(/w:line="\d+"/, 'w:line="240"');
            fixes.spacingNormalized++;
          }
        }
      }

      return `<w:spacing${normalized}/>`;
    });
  }

  /**
   * @brief Normalize indentation values
   * @param {string} xml - XML content
   * @param {Object} fixes - Metrics tracker
   * @return {string} Modified XML
   * @private
   */
  _normalizeIndentation(xml, fixes) {
    return xml.replace(/<w:ind([^>]*)\/>/g, (match, attrs) => {
      // Convert decimal values to integers
      let normalized = attrs.replace(
        /w:(left|right|firstLine|hanging)="([^"]+)"/g,
        (m, type, value) => {
          const intValue = Math.round(parseFloat(value) || 0);
          return `w:${type}="${intValue}"`;
        }
      );

      if (normalized !== attrs) {
        fixes.spacingNormalized++;
      }

      return `<w:ind${normalized}/>`;
    });
  }

  /**
   * @brief Optimize table structures for LibreOffice
   * @param {string} xml - XML content
   * @param {Object} fixes - Metrics tracker
   * @return {string} Modified XML
   * @private
   *
   * Tables are the primary source of fidelity issues between Word and
   * LibreOffice. This optimization converts percentage/auto widths to
   * absolute values and simplifies complex table properties.
   */
  _optimizeTables(xml, fixes) {
    let modified = xml;

    // Normalize table widths
    modified = this._normalizeTableWidths(modified, fixes);

    // Normalize cell widths
    modified = this._normalizeCellWidths(modified, fixes);

    // Remove problematic properties
    modified = this._removeProblematicTableProperties(modified);

    // Simplify borders
    modified = this._simplifyTableBorders(modified);

    return modified;
  }

  /**
   * @brief Normalize table widths to absolute values
   * @param {string} xml - XML content
   * @param {Object} fixes - Metrics tracker
   * @return {string} Modified XML
   * @private
   */
  _normalizeTableWidths(xml, fixes) {
    return xml.replace(
      /<w:tblW w:w="(\d+)" w:type="(auto|pct|dxa)"\/>/g,
      (match, width, type) => {
        fixes.tablesOptimized++;

        // Standard page width: 8.5" - 2" margins = 6.5" = 9360 DXA
        // (DXA = twentieths of a point)
        if (type === "pct") {
          const percentage = parseInt(width) / 50; // Word uses 50 = 100%
          const absoluteWidth = Math.round(9360 * (percentage / 100));
          return `<w:tblW w:w="${absoluteWidth}" w:type="dxa"/>`;
        } else if (type === "auto") {
          return `<w:tblW w:w="9360" w:type="dxa"/>`;
        }

        return match;
      }
    );
  }

  /**
   * @brief Normalize table cell widths
   * @param {string} xml - XML content
   * @param {Object} fixes - Metrics tracker
   * @return {string} Modified XML
   * @private
   */
  _normalizeCellWidths(xml, fixes) {
    return xml.replace(
      /<w:tcW w:w="(\d+)" w:type="(auto|pct|dxa)"\/>/g,
      (match, width, type) => {
        if (type === "auto" || type === "pct") {
          fixes.tablesOptimized++;
          return `<w:tcW w:w="${width}" w:type="dxa"/>`;
        }
        return match;
      }
    );
  }

  /**
   * @brief Remove problematic table properties
   * @param {string} xml - XML content
   * @return {string} Modified XML
   * @private
   */
  _removeProblematicTableProperties(xml) {
    let modified = xml;

    // Remove table positioning (causes layout issues)
    modified = modified.replace(/<w:tblpPr[^>]*\/>/g, "");

    // Remove table overlap settings
    modified = modified.replace(/<w:tblOverlap[^>]*\/>/g, "");

    return modified;
  }

  /**
   * @brief Simplify table border specifications
   * @param {string} xml - XML content
   * @return {string} Modified XML
   * @private
   */
  _simplifyTableBorders(xml) {
    return xml.replace(
      /<w:tcBorders>(.*?)<\/w:tcBorders>/gs,
      (match, borders) => {
        let simplified = borders.replace(
          /w:color="[^"]*"/g,
          'w:color="000000"'
        );
        simplified = simplified.replace(/w:sz="[^"]*"/g, 'w:sz="4"');
        return `<w:tcBorders>${simplified}</w:tcBorders>`;
      }
    );
  }

  /**
   * @brief Normalize image positioning and sizing
   * @param {string} xml - XML content
   * @param {Object} fixes - Metrics tracker
   * @return {string} Modified XML
   * @private
   */
  _normalizeImages(xml, fixes) {
    let modified = xml;

    // Normalize inline images
    modified = this._normalizeInlineImages(modified, fixes);

    // Normalize anchored images
    modified = this._normalizeAnchoredImages(modified, fixes);

    return modified;
  }

  /**
   * @brief Normalize inline image positioning
   * @param {string} xml - XML content
   * @param {Object} fixes - Metrics tracker
   * @return {string} Modified XML
   * @private
   */
  _normalizeInlineImages(xml, fixes) {
    return xml.replace(/<wp:inline([^>]*)>/g, (match) => {
      fixes.imagesNormalized++;
      // Remove distance from text (causes positioning issues)
      return match.replace(/dist[TBLR]="[^"]*"/g, "");
    });
  }

  /**
   * @brief Normalize anchored image positioning
   * @param {string} xml - XML content
   * @param {Object} fixes - Metrics tracker
   * @return {string} Modified XML
   * @private
   */
  _normalizeAnchoredImages(xml, fixes) {
    return xml.replace(/<wp:anchor([^>]*)>/g, (match) => {
      fixes.imagesNormalized++;
      let normalized = match;

      // Remove problematic positioning attributes
      normalized = normalized.replace(
        /layoutInCell="[^"]*"/,
        'layoutInCell="0"'
      );
      normalized = normalized.replace(/behindDoc="[^"]*"/, 'behindDoc="0"');

      return normalized;
    });
  }

  /**
   * @brief Fix pagination controls
   * @param {string} xml - XML content
   * @param {Object} fixes - Metrics tracker
   * @return {string} Modified XML
   * @private
   *
   * Addresses page count discrepancies by removing pagination controls
   * that Word and LibreOffice interpret differently.
   */
  _fixPaginationControls(xml, fixes) {
    let modified = xml;

    // Remove keep-with-next
    const keepWithNextPattern = /<w:keepNext[^>]*\/>/g;
    const keepMatches = modified.match(keepWithNextPattern);
    modified = modified.replace(keepWithNextPattern, "");
    if (keepMatches) {
      fixes.keepWithNextRemoved += keepMatches.length;
      fixes.paginationFixed += keepMatches.length;
    }

    // Remove widow/orphan control
    modified = modified.replace(/<w:widowControl[^>]*\/>/g, "");
    fixes.paginationFixed++;

    // Normalize page breaks
    modified = modified.replace(
      /<w:pageBreakBefore w:val="(true|1|on)"\/>/g,
      '<w:pageBreakBefore w:val="1"/>'
    );

    // Simplify section breaks
    modified = modified.replace(
      /<w:type w:val="(nextColumn|evenPage|oddPage)"\/>/g,
      (match, type) => {
        fixes.paginationFixed++;
        return '<w:type w:val="nextPage"/>';
      }
    );

    return modified;
  }

  /**
   * @brief Normalize bold, italic, underline formatting
   * @param {string} xml - XML content
   * @param {Object} fixes - Metrics tracker
   * @return {string} Modified XML
   * @private
   */
  _normalizeFontStyles(xml, fixes) {
    let modified = xml;

    // Normalize bold values
    modified = modified.replace(/<w:b\s*\/>/g, '<w:b w:val="1"/>');
    modified = modified.replace(
      /<w:b\s+w:val="true"\s*\/>/g,
      '<w:b w:val="1"/>'
    );
    modified = modified.replace(/<w:b\s+w:val="on"\s*\/>/g, '<w:b w:val="1"/>');

    // Normalize italic values
    modified = modified.replace(/<w:i\s*\/>/g, '<w:i w:val="1"/>');
    modified = modified.replace(
      /<w:i\s+w:val="true"\s*\/>/g,
      '<w:i w:val="1"/>'
    );

    return modified;
  }

  /**
   * @brief Normalize color values
   * @param {string} xml - XML content
   * @param {Object} fixes - Metrics tracker
   * @return {string} Modified XML
   * @private
   */
  _normalizeColors(xml, fixes) {
    let modified = xml;

    // Convert AUTO to explicit values
    modified = modified.replace(/w:val="AUTO"/gi, 'w:val="000000"');
    modified = modified.replace(/w:color="AUTO"/gi, 'w:color="000000"');
    modified = modified.replace(/w:fill="AUTO"/gi, 'w:fill="FFFFFF"');

    // Expand 3-digit hex to 6-digit format
    modified = modified.replace(
      /w:(val|color|fill)="([0-9A-Fa-f]{3})"/g,
      (match, attr, color) => {
        const expanded =
          color[0] + color[0] + color[1] + color[1] + color[2] + color[2];
        return `w:${attr}="${expanded.toUpperCase()}"`;
      }
    );

    return modified;
  }

  /**
   * @brief Process styles.xml with style flattening
   * @param {string} xml - Styles XML content
   * @param {Object} fixes - Metrics tracker
   * @return {string} Processed XML
   * @private
   */
  _processStylesXml(xml, fixes) {
    let modified = xml;

    // Apply document normalizations
    modified = this._normalizeFonts(modified, fixes);
    modified = this._convertThemeColors(modified, fixes);
    modified = this._removeUnsupportedEffects(modified, fixes);
    modified = this._normalizeSpacing(modified, fixes);

    // Flatten style inheritance
    modified = modified.replace(/<w:basedOn w:val="[^"]*"\/>/g, "");
    fixes.stylesFlattened++;

    // Remove next style references
    modified = modified.replace(/<w:next w:val="[^"]*"\/>/g, "");

    return modified;
  }

  /**
   * @brief Process numbering.xml to simplify list definitions
   * @param {string} xml - Numbering XML content
   * @param {Object} fixes - Metrics tracker
   * @return {string} Processed XML
   * @private
   */
  _processNumberingXml(xml, fixes) {
    const supported = [
      "decimal",
      "bullet",
      "lowerLetter",
      "upperLetter",
      "lowerRoman",
      "upperRoman",
    ];

    return xml.replace(/w:numFmt="[^"]*"/g, (match) => {
      fixes.numberingSimplified++;
      const format = match.match(/w:numFmt="([^"]*)"/)[1];

      if (!supported.includes(format)) {
        return 'w:numFmt="decimal"';
      }
      return match;
    });
  }

  /**
   * @brief Process settings.xml for optimal LibreOffice compatibility
   * @param {string} xml - Settings XML content
   * @param {Object} fixes - Metrics tracker
   * @return {string} Processed XML
   * @private
   */
  _processSettingsXml(xml, fixes) {
    let modified = xml;

    // Disable compatibility mode
    modified = modified.replace(/<w:compat>.*?<\/w:compat>/gs, "<w:compat/>");

    // Set optimal view settings
    if (!modified.includes("<w:view ")) {
      modified = modified.replace(
        "</w:settings>",
        '<w:view w:val="print"/></w:settings>'
      );
    }

    return modified;
  }

  /**
   * @brief Escape special regex characters in string
   * @param {string} string - Input string
   * @return {string} Escaped string
   * @private
   */
  _escapeRegex(string) {
    return string.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  }
}

module.exports = new AdvancedDocxPreProcessor();
