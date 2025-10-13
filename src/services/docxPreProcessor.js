/**
 * DOCX Pre-Processor
 *
 * Normalizes and fixes DOCX formatting before LibreOffice conversion
 * to improve fidelity and reduce styling issues.
 *
 * Fixes:
 * - Theme colors -> RGB colors
 * - Custom fonts -> standard fonts
 * - Complex styles -> simplified styles
 * - Word-specific features -> LibreOffice-compatible equivalents
 * - Paragraph spacing normalization
 * - Font weight/bold consistency
 */

const fs = require('fs');
const path = require('path');
const AdmZip = require('adm-zip');
const { XMLParser, XMLBuilder } = require('fast-xml-parser');

class DocxPreProcessor {
  constructor() {
    // Font substitution map - replace custom/theme fonts with LibreOffice-friendly fonts
    this.fontMap = {
      // Office theme fonts -> standard fonts
      'Calibri Light': 'Calibri',
      'Segoe UI Light': 'Segoe UI',

      // Custom fonts -> safe alternatives
      'Aptos': 'Calibri',
      'Aptos Narrow': 'Arial Narrow',
      'Arial Narrow': 'Arial',

      // Proprietary fonts -> open equivalents
      'Times New Roman': 'Liberation Serif',
      'Arial': 'Liberation Sans',
      'Courier New': 'Liberation Mono',

      // Keep these safe fonts as-is (LibreOffice handles well)
      'Calibri': 'Calibri',
      'Verdana': 'Verdana',
      'Georgia': 'Georgia',
      'Tahoma': 'Tahoma'
    };

    // Theme color -> RGB mappings (common Office theme colors)
    this.themeColorMap = {
      'accent1': '4472C4',  // Blue
      'accent2': 'ED7D31',  // Orange
      'accent3': 'A5A5A5',  // Gray
      'accent4': 'FFC000',  // Yellow
      'accent5': '5B9BD5',  // Light Blue
      'accent6': '70AD47',  // Green
      'dark1': '000000',    // Black
      'dark2': '44546A',    // Dark Gray
      'light1': 'FFFFFF',   // White
      'light2': 'E7E6E6'    // Light Gray
    };

    this.parserOptions = {
      ignoreAttributes: false,
      attributeNamePrefix: '@_',
      textNodeName: '#text',
      parseAttributeValue: false,
      trimValues: false
    };

    this.builderOptions = {
      ignoreAttributes: false,
      attributeNamePrefix: '@_',
      textNodeName: '#text',
      format: false,  // Don't format XML (preserve structure)
      suppressEmptyNode: false
    };
  }

  /**
   * Pre-process a DOCX file to improve LibreOffice compatibility
   *
   * @param {string} inputPath - Original DOCX file
   * @param {string} outputPath - Pre-processed DOCX file
   * @returns {Promise<Object>} Processing result with applied fixes
   */
  async process(inputPath, outputPath) {
    const fixes = {
      fontsNormalized: 0,
      themeColorsConverted: 0,
      stylesSimplified: 0,
      paragraphsAdjusted: 0,
      boldFixed: 0
    };

    try {
      console.log('Pre-processing DOCX for improved compatibility...');

      // Extract DOCX (it's a ZIP file)
      const zip = new AdmZip(inputPath);
      const zipEntries = zip.getEntries();

      // Parse XML files
      const parser = new XMLParser(this.parserOptions);
      const builder = new XMLBuilder(this.builderOptions);

      // Process document.xml (main content) - using safer regex approach
      const documentEntry = zip.getEntry('word/document.xml');
      if (documentEntry) {
        let documentXml = documentEntry.getData().toString('utf8');
        documentXml = this._processXmlWithRegex(documentXml, fixes);
        zip.updateFile('word/document.xml', Buffer.from(documentXml, 'utf8'));
      }

      // Process styles.xml (document styles) - using safer regex approach
      const stylesEntry = zip.getEntry('word/styles.xml');
      if (stylesEntry) {
        let stylesXml = stylesEntry.getData().toString('utf8');
        stylesXml = this._processXmlWithRegex(stylesXml, fixes);
        zip.updateFile('word/styles.xml', Buffer.from(stylesXml, 'utf8'));
      }

      // Do NOT remove theme - it causes issues
      // Instead, theme colors will be converted to RGB in-place

      // Save processed DOCX
      zip.writeZip(outputPath);

      console.log('Pre-processing complete:');
      console.log(`  • Fonts normalized: ${fixes.fontsNormalized}`);
      console.log(`  • Theme colors converted: ${fixes.themeColorsConverted}`);
      console.log(`  • Styles simplified: ${fixes.stylesSimplified}`);
      console.log(`  • Paragraphs adjusted: ${fixes.paragraphsAdjusted}`);
      console.log(`  • Bold formatting fixed: ${fixes.boldFixed}`);

      return {
        success: true,
        inputPath,
        outputPath,
        fixes
      };

    } catch (error) {
      console.error('DOCX pre-processing error:', error);
      throw new Error(`Pre-processing failed: ${error.message}`);
    }
  }

  /**
   * Process XML using safer regex-based replacements
   * Avoids restructuring XML which can break DOCX compatibility
   *
   * @param {string} xml - XML content
   * @param {Object} fixes - Fix counter
   * @returns {string} Modified XML
   * @private
   */
  _processXmlWithRegex(xml, fixes) {
    let modifiedXml = xml;

    // 1. Convert theme colors to RGB
    // Match: w:color w:themeColor="accent1"
    // Replace with explicit RGB color
    Object.entries(this.themeColorMap).forEach(([themeName, rgbColor]) => {
      const themePattern = new RegExp(`(<w:color[^>]*\\s)w:themeColor="${themeName}"([^>]*w:val=")[^"]*"`, 'g');
      const replacement = `$1w:val="${rgbColor}"`;
      const before = modifiedXml.length;
      modifiedXml = modifiedXml.replace(themePattern, replacement);
      if (modifiedXml.length !== before) {
        fixes.themeColorsConverted++;
      }
    });

    // 2. Normalize fonts - replace problem fonts
    Object.entries(this.fontMap).forEach(([oldFont, newFont]) => {
      if (oldFont !== newFont) {
        // Match font attributes
        const fontPattern = new RegExp(`(w:ascii="|w:hAnsi="|w:cs="|w:eastAsia=")${oldFont}"`, 'g');
        const before = modifiedXml.length;
        modifiedXml = modifiedXml.replace(fontPattern, `$1${newFont}"`);
        if (modifiedXml.length !== before) {
          fixes.fontsNormalized++;
        }
      }
    });

    // 3. Remove theme font references (force explicit fonts)
    const themeFontAttrs = ['w:asciiTheme', 'w:hAnsiTheme', 'w:cstheme', 'w:eastAsiaTheme'];
    themeFontAttrs.forEach(attr => {
      const pattern = new RegExp(`\\s${attr}="[^"]*"`, 'g');
      modifiedXml = modifiedXml.replace(pattern, '');
    });

    // 4. Remove unsupported text effects
    const unsupportedEffects = [
      'w:shadow',
      'w:outline',
      'w:emboss',
      'w:imprint',
      'w14:glow',
      'w14:shadow',
      'w14:reflection',
      'w14:textOutline',
      'w14:textFill'
    ];

    unsupportedEffects.forEach(effect => {
      // Remove entire element: <w:effect ... />
      const selfClosingPattern = new RegExp(`<${effect}[^/]*/>`, 'g');
      const before = modifiedXml.length;
      modifiedXml = modifiedXml.replace(selfClosingPattern, '');
      if (modifiedXml.length !== before) {
        fixes.stylesSimplified++;
      }

      // Remove element with closing tag: <w:effect ...>...</w:effect>
      const pairPattern = new RegExp(`<${effect}[^>]*>.*?</${effect}>`, 'g');
      modifiedXml = modifiedXml.replace(pairPattern, '');
    });

    // 5. Normalize AUTO color to explicit black
    modifiedXml = modifiedXml.replace(/w:val="AUTO"/g, 'w:val="000000"');

    // 6. Fix bold values (ensure explicit)
    modifiedXml = modifiedXml.replace(/<w:b\s*\/>/g, '<w:b w:val="1"/>');
    modifiedXml = modifiedXml.replace(/<w:b\s+w:val="true"\s*\/>/g, '<w:b w:val="1"/>');

    return modifiedXml;
  }

  /**
   * DEPRECATED - DOM-based processing (kept for reference)
   * Process main document content
   *
   * @param {Object} documentData - Parsed document XML
   * @param {Object} fixes - Fix counter object
   * @private
   */
  _processDocument_DEPRECATED(documentData, fixes) {
    if (!documentData['w:document'] || !documentData['w:document']['w:body']) {
      return;
    }

    const body = documentData['w:document']['w:body'];

    // Process paragraphs
    if (body['w:p']) {
      const paragraphs = Array.isArray(body['w:p']) ? body['w:p'] : [body['w:p']];

      paragraphs.forEach(paragraph => {
        this._processParagraph(paragraph, fixes);
      });
    }

    // Process tables
    if (body['w:tbl']) {
      const tables = Array.isArray(body['w:tbl']) ? body['w:tbl'] : [body['w:tbl']];

      tables.forEach(table => {
        this._processTable(table, fixes);
      });
    }
  }

  /**
   * Process a paragraph
   *
   * @param {Object} paragraph - Paragraph object
   * @param {Object} fixes - Fix counter
   * @private
   */
  _processParagraph(paragraph, fixes) {
    // Fix paragraph properties
    if (paragraph['w:pPr']) {
      this._normalizeParagraphProperties(paragraph['w:pPr'], fixes);
    }

    // Process runs (text segments)
    if (paragraph['w:r']) {
      const runs = Array.isArray(paragraph['w:r']) ? paragraph['w:r'] : [paragraph['w:r']];

      runs.forEach(run => {
        this._processRun(run, fixes);
      });
    }
  }

  /**
   * Process a text run
   *
   * @param {Object} run - Run object
   * @param {Object} fixes - Fix counter
   * @private
   */
  _processRun(run, fixes) {
    if (!run['w:rPr']) {
      return;
    }

    const props = run['w:rPr'];

    // Fix font
    if (props['w:rFonts']) {
      this._normalizeFont(props['w:rFonts'], fixes);
    }

    // Fix color (convert theme colors to RGB)
    if (props['w:color']) {
      this._normalizeColor(props['w:color'], fixes);
    }

    // Fix bold (ensure consistent bold representation)
    if (props['w:b']) {
      this._normalizeBold(props['w:b'], fixes);
    }

    // Remove complex text effects that LibreOffice doesn't support
    const unsupportedEffects = ['w:shadow', 'w:outline', 'w:emboss', 'w:imprint', 'w14:glow', 'w14:shadow'];
    unsupportedEffects.forEach(effect => {
      if (props[effect]) {
        delete props[effect];
        fixes.stylesSimplified++;
      }
    });
  }

  /**
   * Normalize font specification
   *
   * @param {Object} fontSpec - Font specification object
   * @param {Object} fixes - Fix counter
   * @private
   */
  _normalizeFont(fontSpec, fixes) {
    const fontAttributes = ['@_w:ascii', '@_w:hAnsi', '@_w:cs', '@_w:eastAsia'];

    fontAttributes.forEach(attr => {
      if (fontSpec[attr]) {
        const originalFont = fontSpec[attr];
        const mappedFont = this.fontMap[originalFont] || originalFont;

        if (mappedFont !== originalFont) {
          fontSpec[attr] = mappedFont;
          fixes.fontsNormalized++;
        }
      }
    });

    // Remove theme font references (force explicit fonts)
    const themeAttributes = ['@_w:asciiTheme', '@_w:hAnsiTheme', '@_w:cstheme', '@_w:eastAsiaTheme'];
    themeAttributes.forEach(attr => {
      if (fontSpec[attr]) {
        delete fontSpec[attr];
      }
    });
  }

  /**
   * Normalize color (convert theme colors to RGB)
   *
   * @param {Object} colorSpec - Color specification
   * @param {Object} fixes - Fix counter
   * @private
   */
  _normalizeColor(colorSpec, fixes) {
    // If color uses theme reference, convert to explicit RGB
    if (colorSpec['@_w:themeColor']) {
      const themeColor = colorSpec['@_w:themeColor'];
      const rgbColor = this.themeColorMap[themeColor];

      if (rgbColor) {
        colorSpec['@_w:val'] = rgbColor;
        delete colorSpec['@_w:themeColor'];
        delete colorSpec['@_w:themeShade'];
        delete colorSpec['@_w:themeTint'];
        fixes.themeColorsConverted++;
      }
    }

    // Ensure 6-digit hex format
    if (colorSpec['@_w:val']) {
      let color = colorSpec['@_w:val'].toUpperCase();
      if (color === 'AUTO') {
        colorSpec['@_w:val'] = '000000'; // Black
      } else if (color.length === 3) {
        // Expand 3-digit hex to 6-digit
        colorSpec['@_w:val'] = color[0] + color[0] + color[1] + color[1] + color[2] + color[2];
      }
    }
  }

  /**
   * Normalize bold formatting
   *
   * @param {Object} boldSpec - Bold specification
   * @param {Object} fixes - Fix counter
   * @private
   */
  _normalizeBold(boldSpec, fixes) {
    // Ensure bold is explicitly enabled (not ambiguous)
    if (!boldSpec['@_w:val'] || boldSpec['@_w:val'] === 'true' || boldSpec['@_w:val'] === '1') {
      boldSpec['@_w:val'] = '1';
      fixes.boldFixed++;
    }
  }

  /**
   * Normalize paragraph properties
   *
   * @param {Object} pPr - Paragraph properties
   * @param {Object} fixes - Fix counter
   * @private
   */
  _normalizeParagraphProperties(pPr, fixes) {
    // Normalize spacing (convert relative to absolute)
    if (pPr['w:spacing']) {
      const spacing = pPr['w:spacing'];

      // Ensure numeric values
      if (spacing['@_w:before']) {
        spacing['@_w:before'] = String(parseInt(spacing['@_w:before']) || 0);
      }
      if (spacing['@_w:after']) {
        spacing['@_w:after'] = String(parseInt(spacing['@_w:after']) || 0);
      }
      if (spacing['@_w:line']) {
        spacing['@_w:line'] = String(parseInt(spacing['@_w:line']) || 240);
      }

      fixes.paragraphsAdjusted++;
    }

    // Remove widow/orphan control (LibreOffice handles differently)
    if (pPr['w:widowControl']) {
      delete pPr['w:widowControl'];
    }
  }

  /**
   * Process table
   *
   * @param {Object} table - Table object
   * @param {Object} fixes - Fix counter
   * @private
   */
  _processTable(table, fixes) {
    // Process table rows
    if (table['w:tr']) {
      const rows = Array.isArray(table['w:tr']) ? table['w:tr'] : [table['w:tr']];

      rows.forEach(row => {
        // Process cells
        if (row['w:tc']) {
          const cells = Array.isArray(row['w:tc']) ? row['w:tc'] : [row['w:tc']];

          cells.forEach(cell => {
            // Process cell paragraphs
            if (cell['w:p']) {
              const paragraphs = Array.isArray(cell['w:p']) ? cell['w:p'] : [cell['w:p']];
              paragraphs.forEach(p => this._processParagraph(p, fixes));
            }
          });
        }
      });
    }
  }

  /**
   * Process document styles
   *
   * @param {Object} stylesData - Parsed styles XML
   * @param {Object} fixes - Fix counter
   * @private
   */
  _processStyles(stylesData, fixes) {
    if (!stylesData['w:styles'] || !stylesData['w:styles']['w:style']) {
      return;
    }

    const styles = Array.isArray(stylesData['w:styles']['w:style'])
      ? stylesData['w:styles']['w:style']
      : [stylesData['w:styles']['w:style']];

    styles.forEach(style => {
      // Process style run properties
      if (style['w:rPr']) {
        if (style['w:rPr']['w:rFonts']) {
          this._normalizeFont(style['w:rPr']['w:rFonts'], fixes);
        }
        if (style['w:rPr']['w:color']) {
          this._normalizeColor(style['w:rPr']['w:color'], fixes);
        }
        if (style['w:rPr']['w:b']) {
          this._normalizeBold(style['w:rPr']['w:b'], fixes);
        }
      }

      // Process style paragraph properties
      if (style['w:pPr']) {
        this._normalizeParagraphProperties(style['w:pPr'], fixes);
      }

      fixes.stylesSimplified++;
    });
  }
}

module.exports = new DocxPreProcessor();
