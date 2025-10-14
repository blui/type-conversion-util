/**
 * Document Conversion Engine
 *
 * Handles document format conversions using LibreOffice as the primary engine.
 * Supports preprocessing for improved conversion quality.
 */

const fs = require("fs");
const path = require("path");

// Import specialized services
const libreOfficeService = require("./libreOfficeService");
const pdfService = require("./pdfService");
const preprocessingService = require("./preprocessingService");

class ConversionEngine {
  /**
   * DOCX to PDF conversion using LibreOffice with ultra-high-fidelity settings
   * Delegates to LibreOffice service for optimized conversion
   *
   * @param {string} inputPath - Path to DOCX file
   * @param {string} outputPath - Path for output PDF
   * @returns {Promise<Object>} Conversion result
   */
  async docxToPdfLibreOffice(inputPath, outputPath) {
    return await libreOfficeService.convertDocxToPdf(inputPath, outputPath);
  }

  /**
   * DOCX to PDF conversion with optional preprocessing
   *
   * Uses LibreOffice for conversion, with optional preprocessing to normalize
   * document formatting for better compatibility.
   *
   * @param {string} inputPath - Path to DOCX file
   * @param {string} outputPath - Path for output PDF
   * @returns {Promise<Object>} Conversion result
   */
  async docxToPdfEnhanced(inputPath, outputPath) {
    const enablePreprocessing = process.env.ENABLE_PREPROCESSING === "true"; // Default false - preprocessing needs rework
    let preprocessedPath = inputPath;
    let preprocessingStats = null;

    // Pre-process DOCX if enabled
    if (enablePreprocessing) {
      console.log("Pre-processing DOCX file...");
      const config = require("../config/config");
      preprocessedPath = path.join(
        config.tempDir,
        `preprocessed_${Date.now()}_${path.basename(inputPath)}`
      );

      try {
        const preprocessResult = await preprocessingService.preprocessDocx(
          inputPath,
          preprocessedPath
        );
        preprocessingStats = {
          enabled: true,
        };
        console.log("Pre-processing completed successfully");
      } catch (preprocessError) {
        console.warn("Pre-processing failed, proceeding with original file");
        console.warn(`   Error: ${preprocessError.message}`);
        // Clean up any partial preprocessed file
        if (fs.existsSync(preprocessedPath)) {
          try {
            fs.unlinkSync(preprocessedPath);
          } catch (cleanupError) {
            console.warn(
              `Failed to cleanup partial file: ${cleanupError.message}`
            );
          }
        }
        preprocessedPath = inputPath;
        preprocessingStats = {
          enabled: false,
          error: preprocessError.message,
        };
      }
    } else {
      preprocessingStats = { enabled: false };
    }

    // Convert using LibreOffice
    console.log("Converting with LibreOffice");
    const result = await this.docxToPdfLibreOffice(
      preprocessedPath,
      outputPath
    );

    // Cleanup preprocessed file
    if (preprocessedPath !== inputPath && fs.existsSync(preprocessedPath)) {
      fs.unlinkSync(preprocessedPath);
    }

    // Add preprocessing stats to result
    result.preprocessing = preprocessingStats;
    return result;
  }

  /**
   * PDF to DOCX conversion
   * Extracts text and structure from PDF to create DOCX document
   *
   * @param {string} inputPath - Path to PDF file
   * @param {string} outputPath - Path for output DOCX
   * @returns {Promise<Object>} Conversion result
   */
  async pdfToDocxEnhanced(inputPath, outputPath) {
    return await pdfService.convertPdfToDocx(inputPath, outputPath);
  }

  /**
   * XLSX to PDF conversion using LibreOffice
   *
   * @param {string} inputPath - Path to XLSX file
   * @param {string} outputPath - Path for output PDF
   * @returns {Promise<Object>} Conversion result
   */
  async xlsxToPdf(inputPath, outputPath) {
    return await libreOfficeService.convertToPdf(inputPath, outputPath, "xlsx");
  }

  /**
   * Get system capabilities and service availability
   * @returns {Promise<Object>} System status information
   */
  async getSystemCapabilities() {
    const libreOffice = await libreOfficeService.getVersion();

    return {
      timestamp: new Date().toISOString(),
      services: {
        libreOffice,
      },
      preprocessing: preprocessingService.getCapabilities(),
    };
  }
}

module.exports = new ConversionEngine();
