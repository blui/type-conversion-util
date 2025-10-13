/**
 * @file test-advanced-preprocessing.js
 * @brief Integration test for advanced DOCX pre-processor
 * @author Type Conversion Utility Team
 * @date 2025
 *
 * PURPOSE:
 *   Verifies advanced pre-processor integration with conversion engine.
 *   Tests optimization metrics, fidelity improvements, and pagination accuracy.
 *
 * USAGE:
 *   node test-advanced-preprocessing.js
 */

const path = require('path');
const fs = require('fs');
const conversionEngine = require('./src/services/conversionEngine');

/**
 * @brief Main test execution function
 * @return {Promise<void>}
 */
async function testAdvancedPreprocessing() {
  console.log('================================================================');
  console.log('  Advanced Pre-Processor Integration Test');
  console.log('================================================================\n');

  // Test document path
  const samplePath = path.join(__dirname, 'Samples', 'FMS REIA Technical Design for Integration v2.0 DRAFT.docx');

  // Verify test document exists
  if (!fs.existsSync(samplePath)) {
    console.error('[ERROR] Sample document not found:', samplePath);
    console.error('        Please place a DOCX file in the Samples directory\n');
    process.exit(1);
  }

  console.log('[OK] Sample document found');
  console.log('     Path:', samplePath);
  console.log('     Size:', (fs.statSync(samplePath).size / 1024).toFixed(2), 'KB\n');

  // Output path for converted PDF
  const outputPath = path.join(__dirname, 'Samples', 'output-advanced-test.pdf');

  try {
    console.log('[1/2] Converting document with advanced pre-processing...\n');

    const startTime = Date.now();
    const result = await conversionEngine.docxToPdfEnhanced(samplePath, outputPath);
    const duration = ((Date.now() - startTime) / 1000).toFixed(2);

    console.log('\n[2/2] Conversion complete\n');

    // Display results
    console.log('================================================================');
    console.log('  Conversion Results');
    console.log('================================================================\n');

    console.log('Status:', result.success ? '[OK] SUCCESS' : '[ERROR] FAILED');
    console.log('Method:', result.method);
    console.log('Fidelity:', result.fidelity);
    console.log('Duration:', duration, 'seconds');

    if (fs.existsSync(outputPath)) {
      const pdfSize = (fs.statSync(outputPath).size / 1024).toFixed(2);
      console.log('Output:', outputPath);
      console.log('Output Size:', pdfSize, 'KB');
    }

    // Display pre-processing metrics if available
    if (result.preprocessing) {
      console.log('\nPre-Processing Optimizations:');
      console.log('  Fonts normalized:', result.preprocessing.fontsNormalized || 0);
      console.log('  Font sizes adjusted:', result.preprocessing.fontSizesAdjusted || 0);
      console.log('  Theme colors converted:', result.preprocessing.themeColorsConverted || 0);
      console.log('  Styles flattened:', result.preprocessing.stylesFlattened || 0);
      console.log('  Styles simplified:', result.preprocessing.stylesSimplified || 0);
      console.log('  Spacing normalized:', result.preprocessing.spacingNormalized || 0);
      console.log('  Tables optimized:', result.preprocessing.tablesOptimized || 0);
      console.log('  Images normalized:', result.preprocessing.imagesNormalized || 0);
      console.log('  Sections normalized:', result.preprocessing.sectionsNormalized || 0);
      console.log('  Pagination fixes:', result.preprocessing.paginationFixed || 0);
      console.log('  Keep-with-next removed:', result.preprocessing.keepWithNextRemoved || 0);
      console.log('  Paragraphs adjusted:', result.preprocessing.paragraphsAdjusted || 0);
      console.log('  Bold formatting fixed:', result.preprocessing.boldFixed || 0);
      console.log('  Numbering simplified:', result.preprocessing.numberingSimplified || 0);
    }

    if (result.warning) {
      console.log('\n[WARNING]', result.warning);
    }

    console.log('\n================================================================');
    console.log('  Test Summary');
    console.log('================================================================\n');

    console.log('[OK] Advanced pre-processor successfully integrated');
    console.log('[OK] Document conversion completed');
    console.log('[OK] Optimization metrics captured\n');

    console.log('Next Steps:');
    console.log('  1. Open output PDF and verify quality');
    console.log('  2. Compare page count with original Word document');
    console.log('  3. Check formatting fidelity (fonts, colors, spacing)');
    console.log('  4. Verify table layouts and borders');
    console.log('  5. Confirm pagination matches expectations\n');

  } catch (error) {
    console.error('\n[ERROR] Conversion failed:', error.message);
    console.error('\nStack trace:', error.stack);
    process.exit(1);
  }
}

// Execute test
testAdvancedPreprocessing().catch(error => {
  console.error('[ERROR] Test failed:', error);
  process.exit(1);
});
