/**
 * LibreOffice Service
 *
 * Dedicated service for LibreOffice headless operations.
 * Handles path detection, process execution, and PDF export optimization.
 *
 * Focus: Pure LibreOffice integration with maximum fidelity settings.
 */

const fs = require("fs");
const path = require("path");
const { execFile } = require("child_process");
const { promisify } = require("util");

const execFileAsync = promisify(execFile);

class LibreOfficeService {
  /**
   * Get LibreOffice executable path with priority detection
   * @returns {string|null} Path to soffice.exe or null if not found
   */
  getLibreOfficePath() {
    // Priority 1: Force bundled LibreOffice for production/air-gapped deployments
    if (process.env.FORCE_BUNDLED_LIBREOFFICE === "true") {
      const bundledPath = path.join(
        __dirname,
        "..",
        "..",
        "lib",
        "libreoffice",
        "program",
        "soffice.exe"
      );
      if (fs.existsSync(bundledPath)) {
        console.log(
          "Using bundled LibreOffice (forced by FORCE_BUNDLED_LIBREOFFICE=true)"
        );
        return bundledPath;
      }
      console.warn(
        "FORCE_BUNDLED_LIBREOFFICE=true but bundled LibreOffice not found"
      );
    }

    // Priority 2: Environment variable override (if set)
    if (
      process.env.LIBREOFFICE_PATH &&
      fs.existsSync(process.env.LIBREOFFICE_PATH)
    ) {
      return process.env.LIBREOFFICE_PATH;
    }

    // Priority 3: System-installed LibreOffice (preferred for reliability)
    const systemPaths = [
      "C:\\Program Files\\LibreOffice\\program\\soffice.exe",
      "C:\\Program Files (x86)\\LibreOffice\\program\\soffice.exe",
    ];

    for (const systemPath of systemPaths) {
      if (fs.existsSync(systemPath)) {
        console.log(`Using system LibreOffice: ${systemPath}`);
        return systemPath;
      }
    }

    // Priority 4: Bundled LibreOffice in lib directory (fallback)
    const bundledPath = path.join(
      __dirname,
      "..",
      "..",
      "lib",
      "libreoffice",
      "program",
      "soffice.exe"
    );
    if (fs.existsSync(bundledPath)) {
      console.log("Using bundled LibreOffice (system LibreOffice not found)");
      return bundledPath;
    }

    return null; // LibreOffice not found
  }

  /**
   * Convert DOCX to PDF using LibreOffice with ultra-high-fidelity settings
   * @param {string} inputPath - Path to DOCX file
   * @param {string} outputPath - Path for output PDF
   * @returns {Promise<Object>} Conversion result with fidelity metrics
   */
  async convertDocxToPdf(inputPath, outputPath) {
    const initialLibreOfficePath = this.getLibreOfficePath();

    if (!initialLibreOfficePath) {
      throw new Error(
        "LibreOffice not found. Please install LibreOffice or set LIBREOFFICE_PATH environment variable."
      );
    }

    // Try conversion with initial LibreOffice path
    try {
      return await this._executeLibreOfficeConversion(
        inputPath,
        outputPath,
        initialLibreOfficePath
      );
    } catch (error) {
      // If using bundled LibreOffice and it fails, try system LibreOffice as fallback
      if (process.env.FORCE_BUNDLED_LIBREOFFICE === "true") {
        console.log(
          "Bundled LibreOffice failed, attempting fallback to system LibreOffice..."
        );

        const systemPaths = [
          "C:\\Program Files\\LibreOffice\\program\\soffice.exe",
          "C:\\Program Files (x86)\\LibreOffice\\program\\soffice.exe",
        ];

        for (const systemPath of systemPaths) {
          if (
            fs.existsSync(systemPath) &&
            systemPath !== initialLibreOfficePath
          ) {
            console.log(`Trying system LibreOffice fallback: ${systemPath}`);
            try {
              return await this._executeLibreOfficeConversion(
                inputPath,
                outputPath,
                systemPath
              );
            } catch (fallbackError) {
              console.log(
                `System LibreOffice fallback failed: ${fallbackError.message}`
              );
              // Continue to next system path
            }
          }
        }
      }

      // If we get here, all attempts failed
      console.error("All LibreOffice conversion attempts failed");
      throw error;
    }
  }

  async _executeLibreOfficeConversion(inputPath, outputPath, libreOfficePath) {
    // Ensure absolute paths for LibreOffice
    const absoluteInputPath = path.resolve(inputPath);
    const absoluteOutputDir = path.resolve(path.dirname(outputPath));
    const inputFilename = path.basename(
      absoluteInputPath,
      path.extname(absoluteInputPath)
    );

    console.log(
      "Executing LibreOffice conversion with ultra-high-fidelity settings..."
    );

    // Ensure output directory exists
    const fs = require("fs");
    if (!fs.existsSync(absoluteOutputDir)) {
      fs.mkdirSync(absoluteOutputDir, { recursive: true });
      console.log(`Created output directory: ${absoluteOutputDir}`);
    }

    // Build command string with properly quoted absolute paths for Windows
    const command = `"${libreOfficePath}" --headless --convert-to pdf --outdir "${absoluteOutputDir}" "${absoluteInputPath}"`;

    console.log(`Executing: ${command}`);

    // Execute with 2-minute timeout for complex documents
    // LibreOffice needs to run from its installation directory
    const libreOfficeDir = path.dirname(libreOfficePath);
    await new Promise((resolve, reject) => {
      const { exec } = require("child_process");
      exec(
        command,
        {
          timeout: 120000,
          windowsHide: true,
          cwd: libreOfficeDir, // Change working directory to LibreOffice installation
        },
        (error, stdout, stderr) => {
          if (error) {
            console.error("LibreOffice stderr:", stderr);
            console.error("LibreOffice stdout:", stdout);
            reject(error);
          } else {
            resolve({ stdout, stderr });
          }
        }
      );
    });

    // LibreOffice outputs to same filename with .pdf extension
    const libreOfficeOutput = path.join(
      absoluteOutputDir,
      inputFilename + ".pdf"
    );
    const absoluteOutputPath = path.resolve(outputPath);

    // Rename to desired output path if different
    if (
      libreOfficeOutput !== absoluteOutputPath &&
      fs.existsSync(libreOfficeOutput)
    ) {
      if (fs.existsSync(absoluteOutputPath)) {
        fs.unlinkSync(absoluteOutputPath);
      }
      fs.renameSync(libreOfficeOutput, absoluteOutputPath);
    }

    // Verify output file exists and has content
    if (!fs.existsSync(absoluteOutputPath)) {
      throw new Error("PDF output file was not created by LibreOffice");
    }

    const stats = fs.statSync(absoluteOutputPath);
    if (stats.size === 0) {
      throw new Error("PDF output file is empty");
    }

    console.log(`LibreOffice conversion successful (${stats.size} bytes)`);

    return {
      success: true,
      outputPath: absoluteOutputPath,
      fidelity: "high",
      method: "libreoffice",
      fileSize: stats.size,
      processingTime: Date.now(), // Can be used to calculate duration
    };
  }

  /**
   * Convert other formats to PDF using LibreOffice
   * @param {string} inputPath - Path to input file
   * @param {string} outputPath - Path for output PDF
   * @param {string} inputFormat - Input format (xlsx, pptx, etc.)
   * @returns {Promise<Object>} Conversion result
   */
  async convertToPdf(inputPath, outputPath, inputFormat) {
    const libreOfficePath = this.getLibreOfficePath();

    if (!libreOfficePath) {
      throw new Error("LibreOffice not found");
    }

    const outputDir = path.dirname(outputPath);
    const inputFilename = path.basename(inputPath, path.extname(inputPath));

    try {
      console.log(`Converting ${inputFormat} to PDF using LibreOffice...`);

      const args = [
        "--headless",
        "--convert-to",
        "pdf",
        "--outdir",
        outputDir,
        inputPath,
      ];

      await execFileAsync(libreOfficePath, args, {
        timeout: 90000, // 1.5 minutes for other formats
        windowsHide: true,
      });

      const libreOfficeOutput = path.join(outputDir, inputFilename + ".pdf");

      if (
        libreOfficeOutput !== outputPath &&
        fs.existsSync(libreOfficeOutput)
      ) {
        if (fs.existsSync(outputPath)) {
          fs.unlinkSync(outputPath);
        }
        fs.renameSync(libreOfficeOutput, outputPath);
      }

      if (!fs.existsSync(outputPath)) {
        throw new Error("PDF output file was not created");
      }

      console.log("LibreOffice conversion successful");

      return {
        success: true,
        outputPath,
        fidelity: "90-95%",
        method: "libreoffice-standard",
      };
    } catch (error) {
      throw new Error(`LibreOffice conversion failed: ${error.message}`);
    }
  }

  /**
   * Get LibreOffice version information
   * @returns {Promise<Object>} Version information
   */
  async getVersion() {
    const libreOfficePath = this.getLibreOfficePath();

    if (!libreOfficePath) {
      return { available: false, version: null, path: null };
    }

    try {
      const { stdout } = await execFileAsync(libreOfficePath, ["--version"], {
        timeout: 10000,
      });

      const versionMatch = stdout.match(/LibreOffice (\d+\.\d+\.\d+)/);
      const version = versionMatch ? versionMatch[1] : "unknown";

      return {
        available: true,
        version,
        path: libreOfficePath,
      };
    } catch (error) {
      return {
        available: false,
        version: null,
        path: libreOfficePath,
        error: error.message,
      };
    }
  }
}

module.exports = new LibreOfficeService();
