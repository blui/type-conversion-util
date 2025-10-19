/**
 * Pre-processing Service
 *
 * Handles document pre-processing to improve conversion quality.
 * Normalizes formatting for better compatibility with conversion engines.
 */

const docxPreProcessor = require("./docxPreProcessor");

class PreprocessingService {
  /**
   * Pre-process DOCX file to normalize formatting
   * Improves compatibility with conversion engines by handling common issues
   *
   * @param {string} inputPath - Path to original DOCX file
   * @param {string} outputPath - Path for processed DOCX output
   * @returns {Promise<Object>} Pre-processing result
   */
  async preprocessDocx(inputPath, outputPath) {
    try {
      console.log("Pre-processing DOCX file...");

      const result = await docxPreProcessor.process(inputPath, outputPath);

      if (result.success) {
        console.log("Pre-processing completed successfully");
      }

      return result;
    } catch (error) {
      console.warn("Pre-processing failed, proceeding with original file");
      console.warn(`   Error: ${error.message}`);

      // Return fallback result - allows conversion to continue with original file
      return {
        success: false,
        error: error.message,
        preprocessing: { enabled: false, error: error.message },
        warning: "Pre-processing failed, using original file",
      };
    }
  }

  /**
   * Get pre-processing capabilities and status
   * @returns {Object} Pre-processor status and capabilities
   */
  getCapabilities() {
    return {
      available: true,
      name: "DOCX Pre-processor",
      version: "1.0.0",
      supportedFormats: ["docx"],
      features: [
        "Font normalization",
        "Theme color expansion",
        "Style simplification",
      ],
    };
  }

  /**
   * Validate if pre-processing is beneficial for a given file
   * @param {string} filePath - Path to file to analyze
   * @returns {Promise<Object>} Analysis result with recommendation
   */
  async analyzeFile(filePath) {
    try {
      // Basic file analysis to determine if pre-processing is beneficial
      const stats = require("fs").statSync(filePath);
      const fileSizeMB = stats.size / (1024 * 1024);

      // Pre-processing is most beneficial for:
      // - Files larger than 1MB (complex documents)
      // - Files with complex formatting
      const shouldPreprocess = fileSizeMB > 1;

      return {
        shouldPreprocess,
        fileSizeMB: fileSizeMB.toFixed(2),
        reason: shouldPreprocess
          ? "File size indicates complex document - pre-processing recommended"
          : "Small file - pre-processing may not be necessary",
      };
    } catch (error) {
      // Default to pre-processing on error
      return {
        shouldPreprocess: true,
        error: error.message,
        reason: "Unable to analyze file - defaulting to pre-processing",
      };
    }
  }
}

module.exports = new PreprocessingService();
