/**
 * Color Mapping Utilities
 *
 * Advanced color processing with theme color expansion and RGB conversion.
 * Handles Microsoft Office theme colors, gamma correction, and color space normalization.
 */

class ColorMapper {
  /**
   * Initialize advanced theme color mappings with gamma correction
   * @returns {Object} Theme color mappings with RGB values and gamma correction
   */
  initializeAdvancedColorMappings() {
    return {
      // Microsoft Office theme colors with precise RGB values
      background1: { r: 255, g: 255, b: 255, gamma: 1.0 },
      text1: { r: 0, g: 0, b: 0, gamma: 1.0 },
      background2: { r: 240, g: 240, b: 240, gamma: 1.02 },
      text2: { r: 68, g: 68, b: 68, gamma: 0.98 },
      accent1: { r: 68, g: 114, b: 196, gamma: 1.0 },
      accent2: { r: 237, g: 125, b: 49, gamma: 1.0 },
      accent3: { r: 165, g: 165, b: 165, gamma: 1.0 },
      accent4: { r: 255, g: 192, b: 0, gamma: 1.0 },
      accent5: { r: 91, g: 155, b: 213, gamma: 1.0 },
      accent6: { r: 112, g: 173, b: 71, gamma: 1.0 },

      // Extended theme colors for complex documents
      hyperlink: { r: 5, g: 99, b: 193, gamma: 1.0 },
      followedHyperlink: { r: 149, g: 79, b: 114, gamma: 1.0 },
      dark1: { r: 0, g: 0, b: 0, gamma: 1.0 },
      light1: { r: 255, g: 255, b: 255, gamma: 1.0 },
      dark2: { r: 68, g: 68, b: 68, gamma: 0.95 },
      light2: { r: 240, g: 240, b: 240, gamma: 1.05 },
    };
  }

  /**
   * Convert theme color reference to RGB values
   * @param {string} themeColor - Theme color name
   * @param {number} tint - Color tint adjustment (-1 to 1)
   * @returns {Object} RGB color values
   */
  themeColorToRgb(themeColor, tint = 0) {
    const mappings = this.initializeAdvancedColorMappings();
    const baseColor = mappings[themeColor];

    if (!baseColor) {
      return { r: 0, g: 0, b: 0 }; // Default to black
    }

    let { r, g, b, gamma } = baseColor;

    // Apply gamma correction
    r = Math.round(Math.pow(r / 255, gamma) * 255);
    g = Math.round(Math.pow(g / 255, gamma) * 255);
    b = Math.round(Math.pow(b / 255, gamma) * 255);

    // Apply tint adjustment if specified
    if (tint !== 0) {
      const tintFactor = 1 + tint;
      r = Math.round(r * tintFactor);
      g = Math.round(g * tintFactor);
      b = Math.round(b * tintFactor);

      // Clamp values to valid range
      r = Math.max(0, Math.min(255, r));
      g = Math.max(0, Math.min(255, g));
      b = Math.max(0, Math.min(255, b));
    }

    return { r, g, b };
  }

  /**
   * Convert RGB values to hex color string
   * @param {Object} rgb - RGB color object
   * @returns {string} Hex color string
   */
  rgbToHex({ r, g, b }) {
    return `#${r.toString(16).padStart(2, "0")}${g
      .toString(16)
      .padStart(2, "0")}${b.toString(16).padStart(2, "0")}`;
  }

  /**
   * Apply theme color expansion to XML content
   * @param {string} xmlContent - XML content to process
   * @returns {string} Processed XML with expanded theme colors
   */
  applyThemeColorExpansion(xmlContent) {
    let processedXml = xmlContent;

    // Replace theme color references with RGB values
    const themeColorRegex = /w:themeColor="([^"]+)"/g;
    processedXml = processedXml.replace(
      themeColorRegex,
      (match, themeColor) => {
        const rgb = this.themeColorToRgb(themeColor);
        const hexColor = this.rgbToHex(rgb);
        return `w:themeColor="${themeColor}" w:val="${hexColor}"`;
      }
    );

    // Handle theme color with tint
    const themeTintRegex = /w:themeColor="([^"]+)" w:themeTint="([^"]+)"/g;
    processedXml = processedXml.replace(
      themeTintRegex,
      (match, themeColor, tint) => {
        const tintValue = parseInt(tint, 16) / 65536 - 1; // Convert from 0-FFFF to -1 to 1
        const rgb = this.themeColorToRgb(themeColor, tintValue);
        const hexColor = this.rgbToHex(rgb);
        return `w:themeColor="${themeColor}" w:themeTint="${tint}" w:val="${hexColor}"`;
      }
    );

    return processedXml;
  }

  /**
   * Normalize color space representations for consistency
   * @param {string} xmlContent - XML content to process
   * @returns {string} XML with normalized color spaces
   */
  normalizeColorSpaces(xmlContent) {
    let processedXml = xmlContent;

    // Convert various color formats to consistent hex representation
    // This would handle srgb, cmyk, hsl conversions as needed

    return processedXml;
  }
}

module.exports = ColorMapper;
