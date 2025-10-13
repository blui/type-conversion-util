/**
 * LibreOffice Bundling Script
 *
 * Detects installed LibreOffice and creates minimal bundle for Git LFS commit.
 * Run this ONCE, then commit files to repository.
 * Eliminates runtime download dependency.
 *
 * Usage: node scripts/bundle-libreoffice.js
 *
 * Detection Order:
 *   1. Installed LibreOffice (Program Files)
 *   2. Portable LibreOffice
 *   3. Download from official source (fallback)
 *
 * Post-execution:
 *   1. Install Git LFS: https://git-lfs.github.com/
 *   2. Track LibreOffice files with git lfs track
 *   3. Commit: git add lib/libreoffice .gitattributes && git commit
 */

const https = require('https');
const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

const LIBREOFFICE_VERSION = '7.6.4';
const DOWNLOAD_URL = `https://download.documentfoundation.org/libreoffice/portable/${LIBREOFFICE_VERSION}/LibreOffice_${LIBREOFFICE_VERSION}_Win_x64.zip`;
const LIB_DIR = path.join(__dirname, '..', 'lib');
const DOWNLOAD_PATH = path.join(LIB_DIR, 'libreoffice.zip');
const EXTRACT_PATH = path.join(LIB_DIR, 'libreoffice-full');
const FINAL_PATH = path.join(LIB_DIR, 'libreoffice');

// Common LibreOffice installation locations
const INSTALLATION_PATHS = [
  'C:\\Program Files\\LibreOffice',
  'C:\\Program Files (x86)\\LibreOffice',
  process.env.PROGRAMFILES + '\\LibreOffice',
  process.env['PROGRAMFILES(X86)'] + '\\LibreOffice',
  'C:\\LibreOffice',
  path.join(process.env.LOCALAPPDATA || '', 'LibreOffice'),
  path.join(process.env.USERPROFILE || '', 'LibreOffice')
];

// Minimal build configuration
// Only includes files required for headless DOCX to PDF conversion
const REQUIRED_DIRS = [
  'program',
  'share/config',
  'share/filter',
  'share/registry'
];

// Directories to exclude (reduces size by ~70%)
const EXCLUDE_DIRS = [
  'share/autotext',
  'share/autocorr',
  'share/basic',
  'share/gallery',
  'share/template',
  'share/wordbook',
  'share/extensions',
  'share/Scripts',
  'readmes',
  'help'
];

console.log('╔══════════════════════════════════════════════╗');
console.log('║   LibreOffice Bundling Tool                  ║');
console.log('║   One-Time Setup for Git LFS                 ║');
console.log('╚══════════════════════════════════════════════╝\n');

/**
 * Detect installed LibreOffice
 */
function detectLibreOffice() {
  console.log('[1/4] Detecting LibreOffice installation...\n');

  for (const basePath of INSTALLATION_PATHS) {
    if (!basePath) continue;

    // Check if path exists
    if (!fs.existsSync(basePath)) continue;

    // Look for program directory with soffice.exe
    const programPath = path.join(basePath, 'program', 'soffice.exe');
    if (fs.existsSync(programPath)) {
      console.log(`      Found: ${basePath}`);
      console.log('      Status: Ready for bundling\n');
      return basePath;
    }
  }

  console.log('      Status: Not found on system\n');
  return null;
}

/**
 * Download file (fallback)
 */
function downloadFile(url, dest) {
  return new Promise((resolve, reject) => {
    console.log('[2/4] Downloading LibreOffice (fallback)...');
    console.log(`      Version: ${LIBREOFFICE_VERSION}`);
    console.log(`      Size: ~200MB\n`);

    const file = fs.createWriteStream(dest);
    let downloaded = 0;

    const request = https.get(url, (response) => {
      if (response.statusCode === 302 || response.statusCode === 301) {
        return downloadFile(response.headers.location, dest).then(resolve).catch(reject);
      }

      const total = parseInt(response.headers['content-length'], 10);

      response.on('data', (chunk) => {
        downloaded += chunk.length;
        const percent = ((downloaded / total) * 100).toFixed(1);
        process.stdout.write(`      Progress: ${percent}% (${(downloaded / 1024 / 1024).toFixed(1)}MB)\r`);
      });

      response.pipe(file);

      file.on('finish', () => {
        file.close();
        console.log('\n      Complete\n');
        resolve();
      });
    });

    request.on('error', (err) => {
      fs.unlink(dest, () => {});
      reject(err);
    });

    request.setTimeout(300000, () => {
      request.abort();
      reject(new Error('Download timeout'));
    });
  });
}

/**
 * Extract archive
 */
function extractArchive(zipPath, extractTo) {
  console.log('[2/4] Extracting archive...\n');

  if (!fs.existsSync(extractTo)) {
    fs.mkdirSync(extractTo, { recursive: true });
  }

  try {
    const command = `powershell -command "Expand-Archive -Path '${zipPath}' -DestinationPath '${extractTo}' -Force"`;
    execSync(command, { stdio: 'inherit' });
    console.log('      Complete\n');
  } catch (error) {
    throw new Error(`Extraction failed: ${error.message}`);
  }
}

/**
 * Create minimal build from installed LibreOffice
 */
function createMinimalBuildFromInstalled(sourcePath, targetPath) {
  console.log('[2/4] Creating minimal build from installed LibreOffice...\n');

  // Copy entire directory first
  if (fs.existsSync(targetPath)) {
    deleteDirRecursive(targetPath);
  }

  console.log('      Copying files...');
  fs.cpSync(sourcePath, targetPath, { recursive: true });

  const sizeBefore = getDirectorySize(targetPath);

  // Remove unnecessary components
  console.log('      Removing unnecessary components...');

  EXCLUDE_DIRS.forEach(dir => {
    const fullPath = path.join(targetPath, dir);
    if (fs.existsSync(fullPath)) {
      deleteDirRecursive(fullPath);
    }
  });

  // Remove language packs (keep only English)
  const sharePath = path.join(targetPath, 'share');
  if (fs.existsSync(sharePath)) {
    const langDirs = fs.readdirSync(sharePath)
      .filter(d => /^[a-z]{2}(-[A-Z]{2})?$/.test(d) && d !== 'en' && d !== 'en-US');

    langDirs.forEach(lang => {
      deleteDirRecursive(path.join(sharePath, lang));
    });
  }

  const sizeAfter = getDirectorySize(targetPath);
  const savings = sizeBefore - sizeAfter;
  const percent = ((savings / sizeBefore) * 100).toFixed(1);

  console.log(`      Before: ${sizeBefore.toFixed(1)}MB`);
  console.log(`      After: ${sizeAfter.toFixed(1)}MB`);
  console.log(`      Saved: ${savings.toFixed(1)}MB (${percent}%)\n`);
}

/**
 * Create minimal build from downloaded archive
 */
function createMinimalBuildFromDownload(sourcePath, targetPath) {
  console.log('[3/4] Creating minimal build...\n');

  // Find LibreOffice directory
  const items = fs.readdirSync(sourcePath);
  let loDir = sourcePath;

  if (items.length === 1 && fs.statSync(path.join(sourcePath, items[0])).isDirectory()) {
    loDir = path.join(sourcePath, items[0]);
  }

  // Copy entire directory first
  if (fs.existsSync(targetPath)) {
    deleteDirRecursive(targetPath);
  }

  console.log('      Copying files...');
  fs.cpSync(loDir, targetPath, { recursive: true });

  const sizeBefore = getDirectorySize(targetPath);

  // Remove unnecessary components
  console.log('      Removing unnecessary components...');

  EXCLUDE_DIRS.forEach(dir => {
    const fullPath = path.join(targetPath, dir);
    if (fs.existsSync(fullPath)) {
      deleteDirRecursive(fullPath);
    }
  });

  // Remove language packs (keep only English)
  const sharePath = path.join(targetPath, 'share');
  if (fs.existsSync(sharePath)) {
    const langDirs = fs.readdirSync(sharePath)
      .filter(d => /^[a-z]{2}(-[A-Z]{2})?$/.test(d) && d !== 'en' && d !== 'en-US');

    langDirs.forEach(lang => {
      deleteDirRecursive(path.join(sharePath, lang));
    });
  }

  const sizeAfter = getDirectorySize(targetPath);
  const savings = sizeBefore - sizeAfter;
  const percent = ((savings / sizeBefore) * 100).toFixed(1);

  console.log(`      Before: ${sizeBefore.toFixed(1)}MB`);
  console.log(`      After: ${sizeAfter.toFixed(1)}MB`);
  console.log(`      Saved: ${savings.toFixed(1)}MB (${percent}%)\n`);
}

/**
 * Verify installation
 */
function verifyInstallation(targetPath, stepNumber = 3) {
  console.log(`[${stepNumber}/4] Verifying installation...\n`);

  const soffice = path.join(targetPath, 'program', 'soffice.exe');

  if (!fs.existsSync(soffice)) {
    throw new Error('soffice.exe not found');
  }

  try {
    const version = execSync(`"${soffice}" --version`, {
      stdio: 'pipe',
      encoding: 'utf8'
    }).trim();
    console.log(`      ${version}`);
    console.log('      Verification successful\n');
  } catch (error) {
    throw new Error('LibreOffice executable test failed');
  }
}

/**
 * Cleanup
 */
function cleanup() {
  console.log('Cleaning up temporary files...\n');

  if (fs.existsSync(DOWNLOAD_PATH)) {
    fs.unlinkSync(DOWNLOAD_PATH);
  }

  if (fs.existsSync(EXTRACT_PATH)) {
    deleteDirRecursive(EXTRACT_PATH);
  }
}

/**
 * Utilities
 */
function deleteDirRecursive(dirPath) {
  if (fs.existsSync(dirPath)) {
    fs.readdirSync(dirPath).forEach(file => {
      const curPath = path.join(dirPath, file);
      if (fs.lstatSync(curPath).isDirectory()) {
        deleteDirRecursive(curPath);
      } else {
        fs.unlinkSync(curPath);
      }
    });
    fs.rmdirSync(dirPath);
  }
}

function getDirectorySize(dirPath) {
  let size = 0;

  function calculateSize(p) {
    const stats = fs.statSync(p);
    if (stats.isFile()) {
      size += stats.size;
    } else if (stats.isDirectory()) {
      fs.readdirSync(p).forEach(f => {
        calculateSize(path.join(p, f));
      });
    }
  }

  if (fs.existsSync(dirPath)) {
    calculateSize(dirPath);
  }

  return size / 1024 / 1024; // Convert to MB
}

/**
 * Main execution
 */
async function main() {
  let installedPath = null;

  try {
    // Create lib directory
    if (!fs.existsSync(LIB_DIR)) {
      fs.mkdirSync(LIB_DIR, { recursive: true });
    }

    // Check if already exists
    if (fs.existsSync(FINAL_PATH)) {
      const size = getDirectorySize(FINAL_PATH);
      console.log(`LibreOffice already bundled: ${size.toFixed(1)}MB\n`);
      console.log('To rebundle, delete lib/libreoffice/ and rerun this script.\n');
      return;
    }

    // Detect installed LibreOffice
    installedPath = detectLibreOffice();

    if (installedPath) {
      // Use installed version
      createMinimalBuildFromInstalled(installedPath, FINAL_PATH);
      verifyInstallation(FINAL_PATH, 3);
    } else {
      // Download fallback
      console.log('No installed LibreOffice found. Downloading...\n');
      await downloadFile(DOWNLOAD_URL, DOWNLOAD_PATH);
      extractArchive(DOWNLOAD_PATH, EXTRACT_PATH);
      createMinimalBuildFromDownload(EXTRACT_PATH, FINAL_PATH);
      verifyInstallation(FINAL_PATH, 4);
      cleanup();
    }

    // Final summary
    const finalSize = getDirectorySize(FINAL_PATH);
    console.log('╔══════════════════════════════════════════════╗');
    console.log('║   Bundling Complete                          ║');
    console.log('╚══════════════════════════════════════════════╝\n');
    console.log(`LibreOffice bundled: lib/libreoffice/ (${finalSize.toFixed(1)}MB)\n`);
    console.log('Next steps:\n');
    console.log('1. Install Git LFS:');
    console.log('   https://git-lfs.github.com/\n');
    console.log('2. Track LibreOffice files:');
    console.log('   git lfs track \'lib/libreoffice/**/*\'\n');
    console.log('3. Commit to repository:');
    console.log('   git add lib/libreoffice .gitattributes');
    console.log('   git commit -m "Bundle LibreOffice for offline deployment"\n');
    console.log('4. Push (Git LFS will handle large files):');
    console.log('   git push\n');

  } catch (error) {
    console.error('\nBundling failed:', error.message);
    if (!installedPath) {
      console.error('\nManual download:');
      console.error(DOWNLOAD_URL);
    }
    process.exit(1);
  }
}

main();
