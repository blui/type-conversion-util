const sharp = require("sharp");

class ImageService {
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

  async convertPsd(inputPath, outputPath, targetFormat) {
    try {
      // For PSD files, we'll try to use Sharp's basic support
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

  async convertSvg(inputPath, outputPath, targetFormat) {
    try {
      // Sharp can handle SVG input
      let sharpInstance = sharp(inputPath, { density: 300 }); // High DPI for better quality

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

  // Utility method to get image metadata
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

  // Utility method to resize image during conversion
  async convertWithResize(inputPath, outputPath, targetFormat, width, height) {
    try {
      let sharpInstance = sharp(inputPath);

      if (width || height) {
        sharpInstance = sharpInstance.resize(width, height, {
          fit: "inside",
          withoutEnlargement: true,
        });
      }

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
