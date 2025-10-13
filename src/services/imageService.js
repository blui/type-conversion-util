/**
 * Image Conversion Service
 *
 * Image file conversions using the Sharp library
 * Supports JPEG, PNG, GIF, BMP, TIFF, SVG, and PSD formats
 */

const sharp = require("sharp");

class ImageService {
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
          sharpInstance = sharpInstance.jpeg({
            quality: 90,
            progressive: true,
          });
          break;
        case "png":
          sharpInstance = sharpInstance.png({
            compressionLevel: 6,
            progressive: true,
          });
          break;
        case "gif":
          // Sharp doesn't support GIF output, convert to PNG instead
          sharpInstance = sharpInstance.png();
          outputPath = outputPath.replace(/\.gif$/, ".png");
          break;
        case "bmp":
          // Sharp doesn't support BMP output, convert to PNG instead
          sharpInstance = sharpInstance.png();
          outputPath = outputPath.replace(/\.bmp$/, ".png");
          break;
        case "tiff":
        case "tif":
          sharpInstance = sharpInstance.tiff({ compression: "lzw" });
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
    try {
      let sharpInstance = sharp(inputPath);

      switch (targetFormat.toLowerCase()) {
        case "jpg":
        case "jpeg":
          sharpInstance = sharpInstance.jpeg({
            quality: 90,
            progressive: true,
          });
          break;
        case "png":
          sharpInstance = sharpInstance.png({
            compressionLevel: 6,
            progressive: true,
          });
          break;
        case "tiff":
        case "tif":
          sharpInstance = sharpInstance.tiff({ compression: "lzw" });
          break;
        default:
          throw new Error(`PSD to ${targetFormat} conversion not supported`);
      }

      await sharpInstance.toFile(outputPath);
      return {
        success: true,
        outputPath,
        filename: require("path").basename(outputPath),
      };
    } catch (error) {
      throw new Error(`PSD conversion failed: ${error.message}`);
    }
  }

  /**
   * Convert SVG files to other formats
   */
  async convertSvg(inputPath, outputPath, targetFormat) {
    try {
      let sharpInstance = sharp(inputPath, { density: 300 });

      switch (targetFormat.toLowerCase()) {
        case "jpg":
        case "jpeg":
          sharpInstance = sharpInstance.jpeg({
            quality: 90,
            progressive: true,
          });
          break;
        case "png":
          sharpInstance = sharpInstance.png({
            compressionLevel: 6,
            progressive: true,
          });
          break;
        case "tiff":
        case "tif":
          sharpInstance = sharpInstance.tiff({ compression: "lzw" });
          break;
        default:
          throw new Error(`SVG to ${targetFormat} conversion not supported`);
      }

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
}

module.exports = new ImageService();
