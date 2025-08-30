const fs = require("fs");
const path = require("path");
const archiver = require("archiver");
const extract = require("extract-zip");
const JSZip = require("jszip");

class ArchiveService {
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

      // Extract the ZIP file
      await extract(inputPath, { dir: path.resolve(extractDir) });

      // Create a summary file
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
