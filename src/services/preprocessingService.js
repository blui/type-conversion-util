/**
 * Pre-processing Service
 *
 * Dedicated service for document pre-processing operations.
 * Manages ultra-high-fidelity DOCX optimization and other pre-conversion tasks.
 *
 * Focus: Document optimization for maximum conversion fidelity.
 */

const docxPreProcessor = require("./docxPreProcessorFidelity");

class PreprocessingService {
  /**
   * Pre-process DOCX file for ultra-high-fidelity conversion
   * Applies 12-phase optimization pipeline for 98-99% fidelity
   *
   * @param {string} inputPath - Path to original DOCX file
   * @param {string} outputPath - Path for optimized DOCX output
   * @returns {Promise<Object>} Pre-processing result with metrics
   */
  async preprocessDocx(inputPath, outputPath) {
    try {
      console.log("Initiating ultra-high-fidelity DOCX pre-processing...");

      const preprocessor = new docxPreProcessor();
      const result = await preprocessor.process(inputPath, outputPath);

      if (result.success) {
        console.log("Pre-processing completed successfully");
        console.log(
          `   Optimizations applied: ${result.optimizations.totalOptimizations}`
        );
        console.log(`   Target fidelity: ${result.fidelity}`);
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
      name: "Ultra-High-Fidelity DOCX Pre-processor",
      version: "2.0.0",
      targetFidelity: "98-99%",
      supportedFormats: ["docx"],
      optimizationPhases: 12,
      features: [
        "Advanced font mapping with visual compensation",
        "Theme color expansion with gamma correction",
        "Complex table structure optimization",
        "Header/footer positioning preservation",
        "Image embedding and positioning accuracy",
        "Page layout and pagination control",
        "Footnote/endnote formatting retention",
        "Document settings compatibility optimization",
        "XML structure integrity validation",
        "Optimized compression and packaging",
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
