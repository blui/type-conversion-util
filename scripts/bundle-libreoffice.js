/**
 * LibreOffice Bundling Script
 *
 * Creates a minimal LibreOffice bundle from system installation for Windows deployment.
 * Copies only essential files needed for headless PDF conversion.
 *
 * Usage: node scripts/bundle-libreoffice.js
 */

const fs = require("fs");
const path = require("path");
const { execSync } = require("child_process");

class LibreOfficeBundler {
  constructor() {
    this.libDir = path.join(__dirname, "..", "lib");
    this.libreOfficeDir = path.join(this.libDir, "libreoffice");

    // System LibreOffice installation paths to check
    this.systemPaths = [
      "C:\\Program Files\\LibreOffice",
      "C:\\Program Files (x86)\\LibreOffice",
    ];
  }

  /**
   * Main bundling process - creates minimal bundle from system LibreOffice
   */
  async bundle() {
    try {
      console.log("Starting LibreOffice bundling from system installation...");

      // Create directories
      console.log("Ensuring directories exist...");
      this.ensureDirectories();
      console.log("Directories check complete");

      // Check if already bundled
      console.log("Checking if already bundled...");
      if (this.isAlreadyBundled()) {
        console.log("LibreOffice already bundled. Skipping setup.");
        return true;
      }
      console.log("Not already bundled, proceeding...");

      // Find system LibreOffice installation
      console.log("About to search for LibreOffice...");
      const systemLibreOfficePath = this.findSystemLibreOffice();
      console.log(`findSystemLibreOffice returned: ${systemLibreOfficePath}`);

      if (!systemLibreOfficePath) {
        console.error("No system LibreOffice installation found.");
        this.showSystemInstallationInstructions();
        return false;
      }

      console.log(`Found system LibreOffice at: ${systemLibreOfficePath}`);

      // Validate system path exists
      console.log(`Checking if path exists: ${systemLibreOfficePath}`);
      if (!fs.existsSync(systemLibreOfficePath)) {
        console.error(
          `System LibreOffice path does not exist: ${systemLibreOfficePath}`
        );
        return false;
      }

      // Create minimal bundle from system installation
      await this.createMinimalBundle(systemLibreOfficePath);

      // Verify bundled installation
      const verified = await this.verifyInstallation();

      if (verified) {
        console.log("LibreOffice bundling completed successfully!");
        console.log(`Minimal bundle created at: ${this.libreOfficeDir}`);
        return true;
      } else {
        console.error("LibreOffice bundling failed - verification failed");
        return false;
      }
    } catch (error) {
      console.error("LibreOffice bundling failed:", error.message);
      return false;
    }
  }

  /**
   * Ensure required directories exist
   */
  ensureDirectories() {
    console.log("Creating required directories...");

    [this.libDir, this.libreOfficeDir].forEach((dir) => {
      if (!fs.existsSync(dir)) {
        fs.mkdirSync(dir, { recursive: true });
        console.log(`Created directory: ${dir}`);
      }
    });
  }

  /**
   * Check if LibreOffice is already bundled
   */
  isAlreadyBundled() {
    const sofficePath = path.join(
      this.libreOfficeDir,
      "program",
      "soffice.exe"
    );

    if (fs.existsSync(sofficePath)) {
      console.log(`Found existing LibreOffice bundle at: ${sofficePath}`);
      return true;
    }

    return false;
  }

  /**
   * Find system LibreOffice installation
   */
  findSystemLibreOffice() {
    console.log("Searching for system LibreOffice installation...");

    for (const loPath of this.systemPaths) {
      console.log(`Checking: ${loPath}`);
      const programPath = path.join(loPath, "program", "soffice.exe");
      console.log(`Looking for: ${programPath}`);
      if (fs.existsSync(programPath)) {
        console.log(`Found LibreOffice at: ${loPath}`);
        return loPath;
      }
    }

    // Check for LibreOffice in PATH
    try {
      console.log("Checking PATH for soffice.exe...");
      const result = execSync("where soffice.exe", { encoding: "utf8" });
      const sofficePath = result.trim().split("\n")[0];
      console.log(`Found in PATH: ${sofficePath}`);
      if (sofficePath && fs.existsSync(sofficePath)) {
        const loDir = path.dirname(path.dirname(sofficePath));
        console.log(`LibreOffice directory: ${loDir}`);
        return loDir;
      }
    } catch (error) {
      console.log("soffice.exe not found in PATH");
    }

    console.log("No LibreOffice installation found");
    return null;
  }

  /**
   * Create minimal LibreOffice bundle from system installation
   */
  async createMinimalBundle(systemLibreOfficePath) {
    console.log("Creating minimal LibreOffice bundle...");
    console.log(`Source path: ${systemLibreOfficePath}`);
    console.log(`Destination path: ${this.libreOfficeDir}`);

    if (!systemLibreOfficePath || typeof systemLibreOfficePath !== "string") {
      throw new Error(
        `Invalid system LibreOffice path: ${systemLibreOfficePath}`
      );
    }

    // Note: System LibreOffice functionality test is skipped for now
    // since the version command may fail but actual conversion works
    console.log("Note: Skipping system LibreOffice functionality test");
    console.log(
      "Proceeding with bundle creation assuming system LibreOffice is functional"
    );

    // Copy the entire program directory first, then remove unnecessary files
    console.log("Copying entire program directory...");
    const programSource = path.join(systemLibreOfficePath, "program");
    const programDest = path.join(this.libreOfficeDir, "program");

    if (fs.existsSync(programSource)) {
      await this.copyPath(programSource, programDest);
      console.log("Program directory copied");
    }

    // Copy essential share directories
    const shareDirs = ["registry", "config"];
    for (const shareDir of shareDirs) {
      const shareSource = path.join(systemLibreOfficePath, "share", shareDir);
      const shareDest = path.join(this.libreOfficeDir, "share", shareDir);

      if (fs.existsSync(shareSource)) {
        // For config directory, only copy essential subdirectories
        if (shareDir === "config") {
          await this.copyEssentialConfig(shareSource, shareDest);
        } else {
          await this.copyPath(shareSource, shareDest);
        }
        console.log(`Share directory ${shareDir} copied`);
      }
    }

    // Remove unnecessary files to reduce bundle size
    console.log("Removing unnecessary files to reduce bundle size...");
    await this.cleanBundle();

    const stats = { copiedCount: 0, totalSize: 0 };

    // Count final files
    const countFiles = (dirPath) => {
      if (!fs.existsSync(dirPath)) return;
      const items = fs.readdirSync(dirPath);
      for (const item of items) {
        const itemPath = path.join(dirPath, item);
        const stat = fs.statSync(itemPath);
        if (stat.isDirectory()) {
          countFiles(itemPath);
        } else {
          stats.copiedCount++;
          stats.totalSize += stat.size;
        }
      }
    };

    countFiles(this.libreOfficeDir);

    console.log(
      `\nMinimal bundle created with ${stats.copiedCount} items (${(
        stats.totalSize /
        1024 /
        1024
      ).toFixed(1)} MB)`
    );
  }

  /**
   * Clean bundle by removing unnecessary files to reduce size
   */
  async cleanBundle() {
    const programDir = path.join(this.libreOfficeDir, "program");

    if (!fs.existsSync(programDir)) return;

    // Files to remove (not needed for headless PDF conversion)
    const filesToRemove = [
      // Development and debug files
      "msdia140.dll", // Microsoft debug DLL
      "ucrtbased.dll", // Debug CRT
      "vcruntime140d.dll", // Debug runtime

      // GUI-related files (not needed for headless)
      "sbase.exe",
      "scalc.exe",
      "sdraw.exe",
      "simpress.exe",
      "smath.exe",
      "swriter.exe",

      // Optional components
      "python.exe", // We keep python-core but remove standalone exe
      "gengal.exe",
      "regmerge.exe",
      "regview.exe",
      "unopkg.exe",
      "uno.exe",

      // Additional unnecessary executables
      "senddoc.exe",
      "ui-previewer.exe",
      "xpdfimport.exe",
      "svgfilter.exe",
      "xsltfilter.exe",

      // Help and documentation files
      "gid_Background.bmp",
      "gid_Background.png",
      "gid_Module_Background.bmp",
      "gid_Module_Background.png",
    ];

    // Directories to remove (not needed for headless PDF conversion)
    const dirsToRemove = [
      "wizards", // Wizard components
      "opencl", // OpenCL components
      "opengl", // OpenGL components
      "intl", // Internationalization data (keep minimal)
      "shell", // Shell extensions
      "shlxthdl", // Shell extensions
      "__pycache__", // Python cache
      "resource", // GUI resources
      "logs", // Log directory (not needed)
      "python-core-3.11.13", // Python core (we can try without it)
    ];

    // Remove specific files
    for (const file of filesToRemove) {
      const filePath = path.join(programDir, file);
      if (fs.existsSync(filePath)) {
        try {
          fs.unlinkSync(filePath);
          console.log(`Removed unnecessary file: ${file}`);
        } catch (error) {
          console.warn(`Could not remove ${file}:`, error.message);
        }
      }
    }

    // Remove specific directories
    for (const dir of dirsToRemove) {
      const dirPath = path.join(programDir, dir);
      if (fs.existsSync(dirPath)) {
        try {
          fs.rmSync(dirPath, { recursive: true, force: true });
          console.log(`Removed unnecessary directory: ${dir}`);
        } catch (error) {
          console.warn(`Could not remove directory ${dir}:`, error.message);
        }
      }
    }

    // Remove large language packs and help files (keep English only)
    const items = fs.readdirSync(programDir);
    for (const item of items) {
      const itemPath = path.join(programDir, item);
      const stat = fs.statSync(itemPath);

      if (stat.isDirectory()) {
        // Remove language directories except English variants
        if (
          item.match(/^lang-(?!en)/) ||
          item.match(/help-/) ||
          item.match(/readme-/i)
        ) {
          try {
            fs.rmSync(itemPath, { recursive: true, force: true });
            console.log(`Removed language/help directory: ${item}`);
          } catch (error) {
            console.warn(`Could not remove ${item}:`, error.message);
          }
        }

        // Remove more unnecessary directories
        if (
          item.match(
            /gallery|palette|templates?|presets?|autotext|autocorr|linguistic|wordbook/
          )
        ) {
          try {
            fs.rmSync(itemPath, { recursive: true, force: true });
            console.log(`Removed unnecessary directory: ${item}`);
          } catch (error) {
            console.warn(`Could not remove ${item}:`, error.message);
          }
        }
      } else if (stat.isFile()) {
        // Remove more unnecessary files
        if (
          item.match(/\.(bmp|png|jpg|jpeg|gif|svg)$/i) &&
          item.includes("gid_")
        ) {
          try {
            fs.unlinkSync(itemPath);
            console.log(`Removed GUI image: ${item}`);
          } catch (error) {
            console.warn(`Could not remove ${item}:`, error.message);
          }
        }
      }
    }
  }

  /**
   * Copy only essential config files to reduce bundle size
   */
  async copyEssentialConfig(sourceConfigDir, destConfigDir) {
    // Only copy essential config files needed for headless PDF conversion
    const essentialConfigItems = ["soffice.cfg"];

    // Create destination directory
    if (!fs.existsSync(destConfigDir)) {
      fs.mkdirSync(destConfigDir, { recursive: true });
    }

    for (const item of essentialConfigItems) {
      const sourcePath = path.join(sourceConfigDir, item);
      const destPath = path.join(destConfigDir, item);

      if (fs.existsSync(sourcePath)) {
        await this.copyPath(sourcePath, destPath);
      }
    }
  }

  /**
   * Test if LibreOffice installation is functional
   */
  async testLibreOfficeFunctionality(libreOfficePath) {
    const sofficePath = path.join(libreOfficePath, "program", "soffice.exe");

    if (!fs.existsSync(sofficePath)) {
      console.error(`soffice.exe not found at: ${sofficePath}`);
      return false;
    }

    try {
      const { execFile } = require("child_process");
      const { promisify } = require("util");
      const execFileAsync = promisify(execFile);

      console.log("Testing LibreOffice version command...");
      const { stdout } = await execFileAsync(sofficePath, ["--version"], {
        timeout: 10000,
        cwd: path.dirname(sofficePath),
      });

      if (stdout.includes("LibreOffice")) {
        console.log("✓ System LibreOffice is functional");
        return true;
      } else {
        console.error("Unexpected version output:", stdout);
        return false;
      }
    } catch (error) {
      console.error("System LibreOffice test failed:", error.message);
      return false;
    }
  }

  /**
   * Copy file or directory recursively
   */
  async copyPath(source, destination) {
    const stat = fs.statSync(source);

    if (stat.isDirectory()) {
      if (!fs.existsSync(destination)) {
        fs.mkdirSync(destination, { recursive: true });
      }
      const items = fs.readdirSync(source);
      for (const item of items) {
        const sourcePath = path.join(source, item);
        const destPath = path.join(destination, item);
        await this.copyPath(sourcePath, destPath);
      }
    } else {
      // Ensure destination directory exists
      const destDir = path.dirname(destination);
      if (!fs.existsSync(destDir)) {
        fs.mkdirSync(destDir, { recursive: true });
      }
      fs.copyFileSync(source, destination);
    }
  }

  /**
   * Show instructions for system LibreOffice installation
   */
  showSystemInstallationInstructions() {
    console.log("\nLibreOffice not found on system.");
    console.log("Please install LibreOffice and run this script again, or:");
    console.log(
      "1. Download LibreOffice from: https://www.libreoffice.org/download/download/"
    );
    console.log(
      "2. Install it to the default location (C:\\Program Files\\LibreOffice)"
    );
    console.log("3. Run this bundling script again");
    console.log(
      "\nAlternative: Set LIBREOFFICE_PATH environment variable to your LibreOffice installation directory."
    );
  }

  /**
   * Verify LibreOffice installation
   */
  async verifyInstallation() {
    console.log("Verifying LibreOffice bundle...");

    const sofficePath = path.join(
      this.libreOfficeDir,
      "program",
      "soffice.exe"
    );

    if (!fs.existsSync(sofficePath)) {
      console.error(`soffice.exe not found at: ${sofficePath}`);
      return false;
    }

    try {
      // Skip version test for now as it may not work in bundled environment
      console.log("Skipping version test for bundled LibreOffice");

      // Test actual PDF conversion with a simple HTML file
      console.log("Testing bundled LibreOffice PDF conversion...");

      // Create a simple HTML test file
      const testHtmlPath = path.join(this.libDir, "test.html");
      const testPdfPath = path.join(this.libDir, "test.pdf");

      fs.writeFileSync(testHtmlPath, "<html><body><h1>Test</h1></body></html>");

      try {
        const { execFile } = require("child_process");
        const { promisify } = require("util");
        const execFileAsync = promisify(execFile);

        await execFileAsync(
          sofficePath,
          [
            "--headless",
            "--convert-to",
            "pdf",
            "--outdir",
            this.libDir,
            testHtmlPath,
          ],
          {
            timeout: 30000,
            cwd: path.dirname(sofficePath),
          }
        );

        if (fs.existsSync(testPdfPath)) {
          console.log("✓ LibreOffice PDF conversion test passed");
          // Cleanup test files
          fs.unlinkSync(testHtmlPath);
          fs.unlinkSync(testPdfPath);
          console.log("LibreOffice bundle verification successful!");
          return true;
        } else {
          console.error("PDF conversion test failed - output file not created");
          return false;
        }
      } catch (convertError) {
        console.error("PDF conversion test failed:", convertError.message);
        // Cleanup test file
        if (fs.existsSync(testHtmlPath)) fs.unlinkSync(testHtmlPath);
        return false;
      }
    } catch (error) {
      console.error("LibreOffice bundle verification failed:", error.message);
      return false;
    }
  }
}

// Main execution
if (require.main === module) {
  const bundler = new LibreOfficeBundler();

  bundler
    .bundle()
    .then((success) => {
      process.exit(success ? 0 : 1);
    })
    .catch((error) => {
      console.error("Bundling failed:", error);
      process.exit(1);
    });
}

module.exports = LibreOfficeBundler;
