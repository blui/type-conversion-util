/**
 * Image Conversion Service
 *
 * Handles image file conversions using the Sharp library.
 * Supports various image formats including JPEG, PNG, GIF, BMP, TIFF, SVG, and PSD.
 * Provides high-quality image processing with configurable quality settings.
 */

const sharp = require("sharp");

class ImageService {
  /**
   * Convert image from one format to another
   *
   * @param {string} inputPath - Path to input image file
   * @param {string} outputPath - Path for output image file
   * @param {string} inputFormat - Input image format (extension)
   * @param {string} targetFormat - Target image format (extension)
   * @returns {Promise<Object>} Conversion result with success status and output path
   */
  async convert(inputPath, outputPath, inputFormat, targetFormat) {
    try {
      console.log(`Converting ${inputFormat} to ${targetFormat}`);

      // Handle special cases that require specific processing
      if (inputFormat === "psd") {
        return await this.convertPsd(inputPath, outputPath, targetFormat);
      }

      if (inputFormat === "svg") {
        return await this.convertSvg(inputPath, outputPath, targetFormat);
      }

      // Standard image conversions using Sharp
      let sharpInstance = sharp(inputPath);

      // Apply format-specific options and processing
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
          sharpInstance = sharpInstance.tiff({
            compression: "lzw",
          });
          break;

        default:
          throw new Error(`Unsupported target format: ${targetFormat}`);
      }

      await sharpInstance.toFile(outputPath);
      return { success: true, outputPath };
    } catch (error) {
      console.error("Image conversion error:", error);
      return { success: false, error: error.message };
    }
  }

  /**
   * Convert PSD (Photoshop) files to other formats
   * Note: Limited support for complex PSD files
   *
   * @param {string} inputPath - Path to PSD file
   * @param {string} outputPath - Path for output file
   * @param {string} targetFormat - Target format
   * @returns {Promise<Object>} Conversion result
   */
  async convertPsd(inputPath, outputPath, targetFormat) {
    try {
      // Use Sharp's basic PSD support
      // Note: This may not work for all PSD files, especially complex ones
      let sharpInstance = sharp(inputPath);

      switch (targetFormat.toLowerCase()) {
        case "jpg":
        case "jpeg":
          sharpInstance = sharpInstance.jpeg({ quality: 90 });
          break;
        case "png":
          sharpInstance = sharpInstance.png();
          break;
        default:
          throw new Error(`PSD to ${targetFormat} conversion not supported`);
      }

      await sharpInstance.toFile(outputPath);
      return { success: true, outputPath };
    } catch (error) {
      throw new Error(
        `PSD conversion failed: ${error.message}. Note: Complex PSD files may require Adobe Photoshop or specialized tools.`
      );
    }
  }

  /**
   * Convert SVG files to raster formats
   * Uses high DPI rendering for better quality
   *
   * @param {string} inputPath - Path to SVG file
   * @param {string} outputPath - Path for output file
   * @param {string} targetFormat - Target format
   * @returns {Promise<Object>} Conversion result
   */
  async convertSvg(inputPath, outputPath, targetFormat) {
    try {
      // Use high DPI for better quality SVG rendering
      let sharpInstance = sharp(inputPath, { density: 300 });

      switch (targetFormat.toLowerCase()) {
        case "jpg":
        case "jpeg":
          sharpInstance = sharpInstance.jpeg({ quality: 90 });
          break;
        case "png":
          sharpInstance = sharpInstance.png();
          break;
        default:
          throw new Error(`SVG to ${targetFormat} conversion not supported`);
      }

      await sharpInstance.toFile(outputPath);
      return { success: true, outputPath };
    } catch (error) {
      throw new Error(`SVG conversion failed: ${error.message}`);
    }
  }

  /**
   * Get image metadata and information
   *
   * @param {string} inputPath - Path to image file
   * @returns {Promise<Object>} Image metadata
   */
  async getImageInfo(inputPath) {
    try {
      const metadata = await sharp(inputPath).metadata();
      return {
        width: metadata.width,
        height: metadata.height,
        format: metadata.format,
        size: metadata.size,
        density: metadata.density,
      };
    } catch (error) {
      throw new Error(`Failed to get image info: ${error.message}`);
    }
  }

  /**
   * Convert image with optional resizing
   *
   * @param {string} inputPath - Path to input image
   * @param {string} outputPath - Path for output image
   * @param {string} targetFormat - Target format
   * @param {number} width - Optional target width
   * @param {number} height - Optional target height
   * @returns {Promise<Object>} Conversion result
   */
  async convertWithResize(inputPath, outputPath, targetFormat, width, height) {
    try {
      let sharpInstance = sharp(inputPath);

      // Apply resizing if dimensions provided
      if (width || height) {
        sharpInstance = sharpInstance.resize(width, height, {
          fit: "inside",
          withoutEnlargement: true,
        });
      }

      // Apply format-specific options
      switch (targetFormat.toLowerCase()) {
        case "jpg":
        case "jpeg":
          sharpInstance = sharpInstance.jpeg({ quality: 90 });
          break;
        case "png":
          sharpInstance = sharpInstance.png();
          break;
        case "tiff":
        case "tif":
          sharpInstance = sharpInstance.tiff();
          break;
        default:
          throw new Error(`Unsupported target format: ${targetFormat}`);
      }

      await sharpInstance.toFile(outputPath);
      return { success: true, outputPath };
    } catch (error) {
      return { success: false, error: error.message };
    }
  }
}

module.exports = new ImageService();
