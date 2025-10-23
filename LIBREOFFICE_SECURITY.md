# LibreOffice Bundle - Security Scanning Documentation

## Purpose

This document provides transparency for cybersecurity scanning of the LibreOffice bundle included in the File Conversion API deployment package.

## LibreOffice Information

**Software:** LibreOffice (Open Source Office Suite)
**Purpose:** Headless document conversion engine (DOC/DOCX/XLSX/PPTX to PDF)
**License:** Mozilla Public License v2.0 (MPL-2.0)
**Source:** https://www.libreoffice.org/download/
**Official Repository:** https://github.com/LibreOffice/core
**Vendor:** The Document Foundation (Non-profit organization)

## Why LibreOffice is Included

The File Conversion API requires LibreOffice for Office document conversions in isolated/air-gapped environments where:
- No internet connectivity is available
- No Microsoft Office licenses are deployed
- Headless (non-interactive) conversion is required
- Cross-platform compatibility is needed

**Usage Mode:** Headless only (no GUI, no user interaction, no macro execution)

## Security Profile

**Trust Indicators:**
- Used by governments worldwide (German, French, Italian, UK governments)
- 20+ year open-source history (started as OpenOffice.org in 2000)
- Active development with security patches released regularly
- Mozilla Public License approved for enterprise use
- No telemetry or external network calls in headless mode

**Our Implementation:**
- Runs in isolated process with limited permissions
- No macro execution enabled
- No network access configured
- Automatic process cleanup after conversion
- Timeout controls prevent runaway processes

## Bundle Contents

### Components INCLUDED (Required for Conversion)

Located in `FileConversionApi/LibreOffice/` after running `bundle-libreoffice.ps1`:

**1. Program Directory (~450 MB)**
- `program/soffice.exe` - Main LibreOffice executable
- `program/soffice.bin` - Core conversion engine
- `program/*.dll` - Essential libraries for document processing
- `program/types.rdb` - Type registry for file formats
- `program/services.rdb` - Service registry for conversion filters

**2. Share Directory (~50-100 MB)**
- `share/registry/` - Configuration for document formats
- `share/config/` - Essential configuration files
- `share/filter/` - File format conversion filters (DOC, DOCX, PDF, etc.)
- `share/dtd/` - Document Type Definitions for XML formats
- `share/xslt/` - Transformations for format conversions

**3. Language Support**
- `share/registry/Langpack-en-US.xcd` - English language pack only

### Components REMOVED (Security Optimizations)

The `bundle-libreoffice.ps1` script automatically removes these unnecessary components:

**Removed for Security (bundle-libreoffice.ps1 lines 48-64):**
```powershell
- python-core-*        # Python runtime (macro scripting, not needed)
- wizards              # UI wizards (GUI components, not needed)
- help                 # Help documentation
- readme               # Documentation files
```

**Removed to Reduce Size (bundle-libreoffice.ps1 lines 84-94):**
```powershell
- gallery              # Clip art and images
- template             # Document templates
- Scripts              # User scripts (macro support)
- samples              # Example documents
- autocorr             # Auto-correction files
- autotext             # Auto-text entries
- wordbook             # Dictionary files
- extensions           # Third-party extensions
- uno_packages         # User-installed packages
```

**Removed for Localization (bundle-libreoffice.ps1 lines 96-114):**
```powershell
- Langpack-*.xcd       # All non-English language packs (saves 50-100 MB)
```

**Total Size Reduction:** Approximately 60-70% smaller than full installation

## Expected OPSWAT Scan Results

### Likely Detections (All Safe)

**1. Unsigned Executables**
- `soffice.exe` - LibreOffice main executable
- `soffice.bin` - Conversion engine
- **Reason:** Open-source software, community-signed
- **Mitigation:** Verify SHA256 hash against official LibreOffice distribution

**2. Large Binary Collections**
- Multiple DLL files in `program/` directory
- **Reason:** Required libraries for document format support
- **Mitigation:** All sourced from official LibreOffice distribution

**3. Configuration Files**
- `bootstrap.ini`, `*.xcd` files
- **Reason:** Standard LibreOffice configuration
- **Mitigation:** Contains paths and settings, no executable code

**4. Potential Heuristic Flags**
- Process spawning capabilities (soffice.exe launches child processes)
- File system access (required for reading/writing documents)
- **Reason:** Normal operation for document conversion
- **Mitigation:** Runs with limited IIS AppPool permissions

### False Positives to Expect

Some antivirus engines may flag LibreOffice components as "potentially unwanted" due to:
- Generic heuristics for "office suite" behavior
- Ability to execute macros (disabled in our headless configuration)
- File format parsing engines (required for conversion)

**These are false positives** - LibreOffice is legitimate software.

## Verification Steps for Security Team

### 1. Verify Source Authenticity

Download official LibreOffice from:
```
https://www.libreoffice.org/download/
```

Compare SHA256 hashes of key executables:
```powershell
Get-FileHash FileConversionApi\LibreOffice\program\soffice.exe -Algorithm SHA256
```

### 2. Review Bundle Script

Examine `bundle-libreoffice.ps1` to see exactly what is included/excluded:
```powershell
# Lines 48-64: Removed components
# Lines 84-94: Skipped directories
# Lines 96-114: Language pack removal
```

### 3. Verify No Network Activity

LibreOffice in headless mode makes no network calls:
```powershell
# After deployment, monitor with:
netstat -ano | findstr soffice
# Should show no network connections
```

### 4. Check Process Isolation

Verify LibreOffice runs under IIS AppPool identity with limited permissions:
```powershell
# Check running process:
Get-Process soffice* | Select-Object Id, ProcessName, Path, UserName
```

## Deployment Security Measures

**1. File System Permissions**
- LibreOffice directory: Read/Execute only for IIS_IUSRS
- Temp directories: Full control for IIS_IUSRS only
- No write access to program files

**2. Process Isolation**
- Runs under ApplicationPoolIdentity (low privilege)
- Automatic timeout after 5 minutes (configurable)
- Process cleanup after each conversion

**3. Network Isolation**
- No outbound network configuration
- Headless mode (no user interaction)
- No macro execution enabled

**4. Input Validation**
- File type validation before conversion
- File size limits enforced (50 MB default)
- MIME type verification
- Extension whitelist

## Compliance and Trust

**Industry Usage:**
- German Government: Full LibreOffice deployment
- French Gendarmerie: 72,000+ workstations
- Italian Defense: Official office suite
- UK Government: Approved for use
- United Nations: Standardized deployment

**Security Certifications:**
- CVE tracking: https://www.cvedetails.com/vendor/10028/Libreoffice.html
- Regular security updates from The Document Foundation
- Active security team monitoring vulnerabilities

**Open Source Transparency:**
- Full source code available for audit
- Community review of all changes
- No proprietary/hidden code
- Reproducible builds

## Air-Gap Deployment Considerations

**Why Self-Contained Bundle:**
- Target environment has no internet access
- No package manager (apt, yum, chocolatey) access
- No external dependencies allowed
- Must be fully functional offline

**Bundle Verification:**
1. Run `bundle-libreoffice.ps1` on development machine
2. Security scan the resulting `FileConversionApi/LibreOffice/` directory
3. Package as part of deployment
4. Transfer to isolated Windows Server
5. No additional downloads required

## Support and Updates

**Official Support:**
- Website: https://www.libreoffice.org/
- Security Advisories: https://www.libreoffice.org/about-us/security/
- Bug Tracker: https://bugs.documentfoundation.org/

**Update Process:**
1. Download new LibreOffice version from official site
2. Run `bundle-libreoffice.ps1 -Force` to recreate bundle
3. Security scan updated bundle
4. Deploy new version through standard change control

## Contact Information

**Project Maintainer:** [Your Team Name]
**Security Questions:** [Your Security Team Contact]
**Deployment Date:** [To be filled during deployment]
**LibreOffice Version:** [Determined by bundle-libreoffice.ps1 from installed version]

## Conclusion

LibreOffice is a trusted, open-source solution used by governments and enterprises worldwide. The bundled version has been optimized for security by:

1. Removing unnecessary components (Python, wizards, scripts, samples)
2. Removing non-English language packs
3. Running in headless mode only
4. Operating under limited permissions
5. No network access configured
6. No macro execution enabled

The bundle contains only essential conversion engines and is appropriate for deployment in secure, isolated environments.

---

**Last Updated:** 2025-10-23
**Document Version:** 1.0
