# LibreOffice Bundle - Security Documentation

Security documentation for the LibreOffice bundle included in the deployment package.

## LibreOffice Details

**Software:** LibreOffice
**Purpose:** Headless document conversion (DOC/DOCX/XLSX/PPTX to PDF)
**License:** Mozilla Public License v2.0
**Source:** https://www.libreoffice.org/download/
**Repository:** https://github.com/LibreOffice/core
**Vendor:** The Document Foundation (non-profit)

## Why It's Included

Required for Office document conversion in air-gapped environments where:
- No internet connectivity available
- No Microsoft Office licenses deployed
- Headless (non-interactive) conversion needed

**Usage:** Headless mode only (no GUI, no user interaction, no macro execution)

## Trust Profile

**Industry Use:**
- Used by governments worldwide (German, French, Italian, UK)
- 20+ year open-source history (started as OpenOffice.org in 2000)
- Active security patch releases
- No telemetry or network calls in headless mode

**Our Implementation:**
- Runs in isolated process with limited permissions
- No macro execution enabled
- No network access configured
- Automatic process cleanup with timeout controls

## Bundle Contents

**Included (Required):**

Located in `FileConversionApi/LibreOffice/` after running `bundle-libreoffice.ps1`:

- `program/soffice.exe` - Main executable
- `program/soffice.bin` - Core conversion engine
- `program/*.dll` - Essential libraries
- `program/*.rdb` - Type and service registries
- `share/registry/` - Format configuration
- `share/config/` - Essential configuration
- `share/filter/` - Conversion filters (DOC, DOCX, PDF, etc.)
- `share/dtd/` and `share/xslt/` - XML format support
- `share/registry/Langpack-en-US.xcd` - English language only

**Removed (Security Optimizations):**

See `bundle-libreoffice.ps1` script for details:

- Python runtime (macro scripting)
- UI wizards and help
- Gallery, templates, samples
- Auto-correction and dictionaries
- Extensions and user packages
- All non-English language packs (saves 50-100 MB)

**Size Reduction:** 60-70% smaller than full installation

## Expected Scan Results

**Will Detect (All Safe):**

1. **Unsigned Executables** - `soffice.exe`, `soffice.bin`
   - Reason: Open-source software, community-signed
   - Mitigation: Verify SHA256 against official distribution

2. **Large Binary Collections** - Multiple DLLs in `program/`
   - Reason: Required libraries for document formats
   - Mitigation: All from official LibreOffice distribution

3. **Configuration Files** - `bootstrap.ini`, `*.xcd` files
   - Reason: Standard LibreOffice configuration
   - Mitigation: Contains paths and settings, no executable code

4. **Heuristic Flags** - Process spawning, file system access
   - Reason: Normal operation for document conversion
   - Mitigation: Runs with limited IIS AppPool permissions

**False Positives:**
Some engines flag LibreOffice as "potentially unwanted" due to generic office suite behavior patterns. These are false positives - LibreOffice is legitimate software.

## Verification

**Check Source Authenticity:**
```powershell
Get-FileHash FileConversionApi\LibreOffice\program\soffice.exe -Algorithm SHA256
```
Compare against official LibreOffice download

**Review Bundle Script:**
Check `bundle-libreoffice.ps1` to see what's included/excluded

**Verify No Network Activity:**
```powershell
netstat -ano | findstr soffice
```
Should show no network connections after conversion

**Check Process Isolation:**
```powershell
Get-Process soffice* | Select-Object Id, ProcessName, Path
```
Verify runs under IIS AppPool identity

## Deployment Security

**File Permissions:**
- LibreOffice directory: Read/Execute only for IIS_IUSRS
- Temp directories: Full control for IIS_IUSRS only

**Process Isolation:**
- Runs under ApplicationPoolIdentity (low privilege)
- Automatic timeout (5 minutes, configurable)
- Process cleanup after each conversion

**Network Isolation:**
- No outbound network configuration
- Headless mode (no user interaction)
- No macro execution enabled

**Input Validation:**
- File type validation before conversion
- File size limits (50 MB default)
- MIME type verification
- Extension whitelist

## Compliance

**Industry Usage:**
- German Government, French Gendarmerie (72K+ workstations), Italian Defense, UK Government, United Nations

**Security:**
- CVE tracking: https://www.cvedetails.com/vendor/10028/Libreoffice.html
- Regular security updates from The Document Foundation
- Full source code available for audit
- Reproducible builds

## Air-Gap Considerations

Bundle is self-contained for isolated environments:
1. Run `bundle-libreoffice.ps1` on development machine
2. Security scan the `FileConversionApi/LibreOffice/` directory
3. Package as part of deployment
4. Transfer to isolated Windows Server
5. No additional downloads required

## Updates

**Update Process:**
1. Download new LibreOffice from official site
2. Run `bundle-libreoffice.ps1 -Force` to recreate bundle
3. Security scan updated bundle
4. Deploy through standard change control

**Official Support:**
- Website: https://www.libreoffice.org/
- Security Advisories: https://www.libreoffice.org/about-us/security/

## Summary

LibreOffice is trusted open-source software used globally by governments and enterprises. The bundle has been optimized for security:

1. Removed unnecessary components (Python, wizards, scripts, samples)
2. Removed non-English language packs
3. Runs in headless mode only
4. Operates under limited permissions
5. No network access configured
6. No macro execution enabled

Bundle contains only essential conversion engines and is appropriate for secure, isolated environments.

---

**Updated:** 2025-10-23
**Version:** 1.0
