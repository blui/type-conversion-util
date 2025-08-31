/**
 * Archive Processing Service
 *
 * Handles archive file operations including ZIP extraction and creation.
 * Implements security measures to prevent zip bombs and malicious archives.
 * Provides comprehensive archive information and safe extraction capabilities.
 */

const fs = require("fs");
const path = require("path");
const archiver = require("archiver");
const extract = require("extract-zip");
const JSZip = require("jszip");

class ArchiveService {
  /**
   * Convert or process archive files
   * Currently supports ZIP extraction operations
   *
   * @param {string} inputPath - Path to input archive file
   * @param {string} outputPath - Path for output file or directory
   * @param {string} inputFormat - Input archive format (extension)
   * @param {string} targetFormat - Target operation (e.g., "extract")
   * @returns {Promise<Object>} Processing result with success status and output path
   */
  async convert(inputPath, outputPath, inputFormat, targetFormat) {
    try {
      console.log(`Processing ${inputFormat} archive`);

      switch (`${inputFormat}-${targetFormat}`) {
        case "zip-extract":
          return await this.extractZip(inputPath, outputPath);

        default:
          throw new Error(
            `Archive operation ${inputFormat} to ${targetFormat} is not supported`
          );
      }
    } catch (error) {
      console.error("Archive processing error:", error);
      return { success: false, error: error.message };
    }
  }

  /**
   * Safely extract ZIP files with security validation
   * Implements zip bomb protection and entry limits
   *
   * @param {string} inputPath - Path to ZIP file
   * @param {string} outputPath - Path for extraction summary
   * @returns {Promise<Object>} Extraction result with file list and summary
   */
  async extractZip(inputPath, outputPath) {
    try {
      // Create extraction directory
      const extractDir = outputPath.replace(
        path.extname(outputPath),
        "_extracted"
      );

      if (!fs.existsSync(extractDir)) {
        fs.mkdirSync(extractDir, { recursive: true });
      }

      // Security limits to prevent zip bombs
      const MAX_ENTRIES = 10000;
      const MAX_TOTAL_UNCOMPRESSED = 1024 * 1024 * 1024; // 1 GB

      // Pre-validate archive using JSZip before extraction
      const data = fs.readFileSync(inputPath);
      const zip = await JSZip.loadAsync(data);

      let entryCount = 0;
      let totalUncompressed = 0;

      zip.forEach((relativePath, file) => {
        entryCount += 1;
        if (entryCount > MAX_ENTRIES) {
          throw new Error("ZIP has too many entries");
        }

        // Check uncompressed size if available
        if (file._data && file._data.uncompressedSize) {
          totalUncompressed += file._data.uncompressedSize;
          if (totalUncompressed > MAX_TOTAL_UNCOMPRESSED) {
            throw new Error("ZIP uncompressed size exceeds limit");
          }
        }
      });

      // Extract the ZIP file (extract-zip prevents Zip Slip by default)
      await extract(inputPath, { dir: path.resolve(extractDir) });

      // Create extraction summary
      const files = await this.listExtractedFiles(extractDir);
      const summaryPath = path.join(extractDir, "extraction_summary.txt");
      const summary = `ZIP Extraction Summary
===================
Source: ${path.basename(inputPath)}
Extracted to: ${extractDir}
Total files: ${files.length}

Files extracted:
${files.map((file) => `- ${file}`).join("\n")}
`;

      fs.writeFileSync(summaryPath, summary);

      return {
        success: true,
        outputPath: summaryPath,
        extractedDir: extractDir,
        extractedFiles: files,
      };
    } catch (error) {
      throw new Error(`ZIP extraction failed: ${error.message}`);
    }
  }

  /**
   * Recursively list all files in extracted directory
   *
   * @param {string} directory - Directory to scan
   * @param {string} relativePath - Current relative path (for recursion)
   * @returns {Promise<Array>} Array of file paths relative to base directory
   */
  async listExtractedFiles(directory, relativePath = "") {
    const files = [];
    const items = fs.readdirSync(directory);

    for (const item of items) {
      const fullPath = path.join(directory, item);
      const itemRelativePath = path.join(relativePath, item);

      if (fs.statSync(fullPath).isDirectory()) {
        const subFiles = await this.listExtractedFiles(
          fullPath,
          itemRelativePath
        );
        files.push(...subFiles);
      } else {
        files.push(itemRelativePath);
      }
    }

    return files;
  }

  /**
   * Create ZIP archive from directory
   *
   * @param {string} sourceDir - Source directory to archive
   * @param {string} outputPath - Path for output ZIP file
   * @returns {Promise<Object>} Creation result with file size
   */
  async createZip(sourceDir, outputPath) {
    return new Promise((resolve, reject) => {
      const output = fs.createWriteStream(outputPath);
      const archive = archiver("zip", {
        zlib: { level: 9 }, // Maximum compression
      });

      output.on("close", () => {
        console.log(`ZIP created: ${archive.pointer()} total bytes`);
        resolve({ success: true, outputPath, size: archive.pointer() });
      });

      archive.on("error", (err) => {
        reject(new Error(`ZIP creation failed: ${err.message}`));
      });

      archive.pipe(output);
      archive.directory(sourceDir, false);
      archive.finalize();
    });
  }

  /**
   * Get detailed information about ZIP file contents
   *
   * @param {string} zipPath - Path to ZIP file
   * @returns {Promise<Object>} ZIP file information
   */
  async getZipInfo(zipPath) {
    try {
      const data = fs.readFileSync(zipPath);
      const zip = await JSZip.loadAsync(data);

      const files = [];
      zip.forEach((relativePath, file) => {
        files.push({
          name: relativePath,
          size: file._data ? file._data.uncompressedSize : 0,
          compressed: file._data ? file._data.compressedSize : 0,
          isDirectory: file.dir,
        });
      });

      return {
        totalFiles: files.filter((f) => !f.isDirectory).length,
        totalDirectories: files.filter((f) => f.isDirectory).length,
        files: files,
      };
    } catch (error) {
      throw new Error(`Failed to get ZIP info: ${error.message}`);
    }
  }
}

module.exports = new ArchiveService();
