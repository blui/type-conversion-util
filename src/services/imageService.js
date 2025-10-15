/**
 * Image Conversion Service
 *
 * Image file conversions using the Sharp library
 * Supports JPEG, PNG, GIF, BMP, TIFF, SVG, and PSD formats
 */

const sharp = require("sharp");

class ImageService {
  // Common image quality settings
  static QUALITY_SETTINGS = {
    jpeg: { quality: 90, progressive: true },
    png: { compressionLevel: 6, progressive: true },
    tiff: { compression: "lzw" },
  };
  /**
   * Convert image from one format to another
   */
  async convert(inputPath, outputPath, inputFormat, targetFormat) {
    try {
      console.log(`Converting ${inputFormat} to ${targetFormat}`);

      // Handle special cases
      if (inputFormat === "psd") {
        return await this.convertPsd(inputPath, outputPath, targetFormat);
      }

      if (inputFormat === "svg") {
        return await this.convertSvg(inputPath, outputPath, targetFormat);
      }

      // Standard image conversions using Sharp
      let sharpInstance = sharp(inputPath);

      // Apply format-specific options
      switch (targetFormat.toLowerCase()) {
        case "jpg":
        case "jpeg":
          sharpInstance = sharpInstance.jpeg(
            ImageService.QUALITY_SETTINGS.jpeg
          );
          break;
        case "png":
          sharpInstance = sharpInstance.png(ImageService.QUALITY_SETTINGS.png);
          break;
        case "gif":
          // Sharp doesn't support GIF output, convert to PNG instead
          sharpInstance = sharpInstance.png(ImageService.QUALITY_SETTINGS.png);
          outputPath = outputPath.replace(/\.gif$/, ".png");
          break;
        case "bmp":
          // Sharp doesn't support BMP output, convert to PNG instead
          sharpInstance = sharpInstance.png(ImageService.QUALITY_SETTINGS.png);
          outputPath = outputPath.replace(/\.bmp$/, ".png");
          break;
        case "tiff":
        case "tif":
          sharpInstance = sharpInstance.tiff(
            ImageService.QUALITY_SETTINGS.tiff
          );
          break;
        default:
          throw new Error(`Unsupported target format: ${targetFormat}`);
      }

      await sharpInstance.toFile(outputPath);
      return {
        success: true,
        outputPath,
        filename: require("path").basename(outputPath),
      };
    } catch (error) {
      console.error("Image conversion error:", error);
      return { success: false, error: error.message };
    }
  }

  /**
   * Convert PSD files to other formats
   */
  async convertPsd(inputPath, outputPath, targetFormat) {
    return this._convertWithSharp(inputPath, outputPath, targetFormat, "PSD");
  }

  /**
   * Convert SVG files to other formats
   */
  async convertSvg(inputPath, outputPath, targetFormat) {
    try {
      let sharpInstance = sharp(inputPath, { density: 300 });
      const settings = this._getFormatSettings(targetFormat, "SVG");

      sharpInstance = sharpInstance[settings.format](settings.options);
      await sharpInstance.toFile(outputPath);

      return {
        success: true,
        outputPath,
        filename: require("path").basename(outputPath),
      };
    } catch (error) {
      throw new Error(`SVG conversion failed: ${error.message}`);
    }
  }

  /**
   * Helper method for PSD conversion using shared settings
   */
  async _convertWithSharp(inputPath, outputPath, targetFormat, sourceType) {
    try {
      let sharpInstance = sharp(inputPath);
      const settings = this._getFormatSettings(targetFormat, sourceType);

      sharpInstance = sharpInstance[settings.format](settings.options);
      await sharpInstance.toFile(outputPath);

      return {
        success: true,
        outputPath,
        filename: require("path").basename(outputPath),
      };
    } catch (error) {
      throw new Error(`${sourceType} conversion failed: ${error.message}`);
    }
  }

  /**
   * Get format settings for conversion
   */
  _getFormatSettings(targetFormat, sourceType) {
    switch (targetFormat.toLowerCase()) {
      case "jpg":
      case "jpeg":
        return { format: "jpeg", options: ImageService.QUALITY_SETTINGS.jpeg };
      case "png":
        return { format: "png", options: ImageService.QUALITY_SETTINGS.png };
      case "tiff":
      case "tif":
        return { format: "tiff", options: ImageService.QUALITY_SETTINGS.tiff };
      default:
        throw new Error(
          `${sourceType} to ${targetFormat} conversion not supported`
        );
    }
  }
}

module.exports = new ImageService();
