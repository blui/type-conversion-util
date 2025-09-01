/**
 * Archive Processing Service
 *
 * Handles archive file operations including ZIP extraction and creation
 * Implements security measures to prevent zip bombs and malicious archives
 */

const fs = require("fs");
const path = require("path");
const archiver = require("archiver");
const extract = require("extract-zip");
const JSZip = require("jszip");

class ArchiveService {
  /**
   * Convert or process archive files
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
   */
  async extractZip(inputPath, outputPath) {
    try {
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

        if (file._data && file._data.uncompressedSize) {
          totalUncompressed += file._data.uncompressedSize;
          if (totalUncompressed > MAX_TOTAL_UNCOMPRESSED) {
            throw new Error("ZIP uncompressed size exceeds limit");
          }
        }
      });

      // Extract the ZIP file
      await extract(inputPath, { dir: path.resolve(extractDir) });

      // Create extraction summary
      const files = await this.listExtractedFiles(extractDir);
      const summaryPath = path.join(extractDir, "extraction_summary.txt");
      const summary = `ZIP Extraction Summary
===================
Source: ${path.basename(inputPath)}
Extracted to: ${extractDir}
Total files: ${files.length}
Date: ${new Date().toISOString()}

Files:
${files.map((f) => `- ${f}`).join("\n")}`;

      fs.writeFileSync(summaryPath, summary);

      return {
        success: true,
        outputPath: summaryPath,
        filename: path.basename(summaryPath),
        extractedFiles: files.length,
        extractDir: extractDir,
      };
    } catch (error) {
      throw new Error(`ZIP extraction failed: ${error.message}`);
    }
  }

  /**
   * List all extracted files recursively
   */
  async listExtractedFiles(dir) {
    const files = [];

    function scanDirectory(currentDir, relativePath = "") {
      const items = fs.readdirSync(currentDir);

      for (const item of items) {
        const fullPath = path.join(currentDir, item);
        const relativeItemPath = path.join(relativePath, item);

        if (fs.statSync(fullPath).isDirectory()) {
          scanDirectory(fullPath, relativeItemPath);
        } else {
          files.push(relativeItemPath);
        }
      }
    }

    scanDirectory(dir);
    return files;
  }

  /**
   * Create ZIP archive from directory
   */
  async createZip(inputPath, outputPath) {
    try {
      return new Promise((resolve, reject) => {
        const output = fs.createWriteStream(outputPath);
        const archive = archiver("zip", { zlib: { level: 9 } });

        output.on("close", () => {
          resolve({
            success: true,
            outputPath,
            filename: path.basename(outputPath),
            size: archive.pointer(),
          });
        });

        archive.on("error", (err) => {
          reject(new Error(`ZIP creation failed: ${err.message}`));
        });

        archive.pipe(output);
        archive.directory(inputPath, false);
        archive.finalize();
      });
    } catch (error) {
      throw new Error(`ZIP creation failed: ${error.message}`);
    }
  }
}

module.exports = new ArchiveService();
