# Security Scan Preparation - File Conversion API

## Overview

This document provides guidance for cybersecurity teams performing OPSWAT or similar security scans on the File Conversion API deployment package.

## Deployment Package Contents

### What You'll Be Scanning

**Location:** `FileConversionApi/deploy/release/` (after running deploy.ps1)

**Total Size:** ~550-600 MB

**Contents:**
1. .NET 8 Application (~50 MB)
   - Compiled DLLs and assemblies
   - ASP.NET Core runtime dependencies
   - Third-party NuGet packages

2. LibreOffice Bundle (~500 MB)
   - Document conversion engine
   - See LIBREOFFICE_SECURITY.md for detailed breakdown

3. Configuration Files (~1 MB)
   - appsettings.json
   - web.config
   - Environment templates

## Pre-Scan Checklist

### Before Submitting for Scan

- [ ] Run `bundle-libreoffice.ps1` to create LibreOffice bundle
- [ ] Run `deploy.ps1` to create deployment package
- [ ] Verify `deploy/release` directory contains all files
- [ ] Review LIBREOFFICE_SECURITY.md
- [ ] Document LibreOffice source (https://www.libreoffice.org/)
- [ ] Note deployment is for isolated/air-gapped environment

### Information to Provide to Security Team

**1. Application Details**
```
Name: File Conversion API
Version: 0.2.0
Platform: .NET 8.0 / ASP.NET Core
Runtime: Windows Server 2016+ / IIS 8.5+
Purpose: Office document format conversion (DOC/DOCX/XLSX/PPTX to PDF)
Environment: Isolated Windows Server (no internet access)
```

**2. Third-Party Components**
```
LibreOffice:
  - Version: [Determined from installed version]
  - Source: https://www.libreoffice.org/download/
  - License: Mozilla Public License v2.0
  - Purpose: Headless document conversion
  - Security: See LIBREOFFICE_SECURITY.md

.NET Dependencies (NuGet):
  - All sourced from nuget.org (official Microsoft package repository)
  - Included in deployment package
  - Full list in FileConversionApi.csproj
```

**3. Network Requirements**
```
Inbound:  HTTP/HTTPS (ports 80/443) - from internal network only
Outbound: None - air-gapped deployment
```

## Expected OPSWAT Findings

### High Confidence - Safe Detections

These will be flagged but are expected and safe:

**1. LibreOffice Executables**
```
File: FileConversionApi/LibreOffice/program/soffice.exe
Detection: Unsigned executable
Reason: Open-source software from The Document Foundation
Action: Verify against official LibreOffice SHA256 hash
Status: SAFE - Required for conversion
```

```
File: FileConversionApi/LibreOffice/program/soffice.bin
Detection: Unsigned executable / Heuristic detection
Reason: Core conversion engine
Action: Cross-reference with official LibreOffice distribution
Status: SAFE - Required for conversion
```

**2. DLL Files (100+ files)**
```
Files: FileConversionApi/LibreOffice/program/*.dll
Detection: Multiple unsigned DLLs
Reason: LibreOffice support libraries
Action: Verify bundle was created from official source
Status: SAFE - Required libraries
```

**3. Configuration Files**
```
Files: *.ini, *.xcd, *.rdb
Detection: Configuration files with system paths
Reason: Standard LibreOffice configuration
Status: SAFE - No executable code
```

### Medium Confidence - Review Needed

**1. Process Spawning Capabilities**
```
Detection: Application can spawn child processes
Reason: LibreOffice runs as separate process for each conversion
Mitigation:
  - Runs under IIS AppPool (low privilege)
  - Automatic timeout (5 minutes default)
  - Process cleanup after conversion
Status: EXPECTED BEHAVIOR - Required for conversion
```

**2. File System Access**
```
Detection: Read/write access to temp directories
Reason: Conversion requires reading input files and writing output
Mitigation:
  - Limited to App_Data directory only
  - Automatic cleanup after conversion
  - 24-hour retention maximum
Status: EXPECTED BEHAVIOR - Required for conversion
```

**3. Large Binary Collections**
```
Detection: Many executables and libraries in single package
Reason: Self-contained deployment for air-gapped environment
Mitigation:
  - All components from verified sources
  - No external dependencies
  - Offline operation only
Status: REQUIRED FOR AIR-GAP DEPLOYMENT
```

### Low Confidence - Potential False Positives

**1. Generic "Office Suite" Heuristics**
```
Detection: Office suite behavior patterns
Reason: LibreOffice is an office suite (headless mode)
Mitigation: Macros disabled, GUI removed, headless only
Status: FALSE POSITIVE - Legitimate software
```

**2. "Potentially Unwanted Application" (PUA)**
```
Detection: Some AV engines flag LibreOffice as PUA
Reason: Generic heuristics for bundled office software
Mitigation: Official open-source software, used globally
Status: FALSE POSITIVE - Trusted software
```

## OPSWAT-Specific Guidance

### MetaDefender Settings

**Recommended Scan Profile:**
```
Scan Type: Deep scan with all engines
Workflow: Custom (exclude known false positives)
File Reputation: Cross-reference with OPSWAT File Reputation
Hash Check: Verify known LibreOffice hashes
```

**Expected Engines to Flag LibreOffice:**
- Generic heuristics engines
- Engines without LibreOffice whitelist
- Behavioral analysis engines (process spawning)

**Expected Engines to PASS:**
- Signature-based engines (no known malware signatures)
- Reputation engines (LibreOffice is trusted globally)
- Hash-based engines (matches official distribution)

### Recommended Workflow Actions

**1. Initial Scan**
- Run full deep scan with all engines
- Note all detections
- Separate LibreOffice detections from application detections

**2. LibreOffice Verification**
- Compare detected files against LIBREOFFICE_SECURITY.md
- Verify SHA256 hashes against official distribution
- Check LibreOffice version and source
- Review bundle-libreoffice.ps1 for removed components

**3. Application Code Review**
- Scan .NET DLLs separately
- Verify NuGet package sources
- Review appsettings.json for misconfigurations
- Check for hardcoded credentials (none should exist)

**4. Risk Assessment**
- LibreOffice: Approved open-source (low risk)
- .NET Application: Custom code (review based on policy)
- Configuration: No secrets, all configurable (low risk)
- Network: No outbound (isolated environment, low risk)

## Approval Criteria

### Recommended for Approval IF:

- [ ] All LibreOffice detections match expected list above
- [ ] No actual malware signatures detected
- [ ] LibreOffice source verified from official site
- [ ] .NET application code passes review
- [ ] No hardcoded credentials found
- [ ] Configuration files reviewed and acceptable
- [ ] Deployment environment is isolated/air-gapped
- [ ] Security team reviews LIBREOFFICE_SECURITY.md

### Escalation Needed IF:

- [ ] Unknown malware signatures detected (not LibreOffice heuristics)
- [ ] Suspicious network behavior beyond documented scope
- [ ] Hardcoded credentials or API keys found
- [ ] Unverified executables beyond LibreOffice bundle
- [ ] Configuration allows unsafe operations

## Evidence for Security Review

### Provided Documentation

1. **LIBREOFFICE_SECURITY.md** - Detailed LibreOffice component breakdown
2. **SECURITY_SCAN_PREPARATION.md** - This file
3. **bundle-libreoffice.ps1** - Script showing exactly what's included/excluded
4. **deploy.ps1** - Deployment package creation script
5. **DEPLOYMENT_NOTES.md** - IIS deployment procedures
6. **README.md** - Application overview and usage

### Verification Steps

**Verify LibreOffice Authenticity:**
```powershell
# On development machine where bundle was created:
Get-ChildItem "C:\Program Files\LibreOffice" -Recurse |
  Where-Object {$_.Name -eq "soffice.exe"} |
  Get-FileHash -Algorithm SHA256

# Compare against hash in deployment package:
Get-FileHash FileConversionApi\deploy\release\LibreOffice\program\soffice.exe
```

**Verify No Network Configuration:**
```powershell
# Search for network-related settings:
Get-ChildItem FileConversionApi\deploy\release\ -Recurse -Include *.json,*.config |
  Select-String -Pattern "http://|https://|ftp://" -CaseSensitive
# Should only find local URLs and documentation
```

**Verify No Hardcoded Secrets:**
```powershell
# Search for potential secrets:
Get-ChildItem FileConversionApi\deploy\release\ -Recurse -Include *.json,*.config,*.cs,*.dll |
  Select-String -Pattern "password|secret|apikey|connectionstring" -CaseSensitive
# Should only find configuration keys, not actual secrets
```

## Support During Scan

### Questions to Expect from Security Team

**Q: Why is LibreOffice needed?**
A: Office document conversion in air-gapped environment. No Microsoft Office licenses available. LibreOffice provides open-source conversion capability.

**Q: Why so many executables and DLLs?**
A: LibreOffice is a complete office suite. We've removed 60-70% of unnecessary components. Remaining files are required for document conversion.

**Q: Can we use a cloud-based conversion service instead?**
A: No - deployment target is isolated Windows Server with no internet access. Self-contained solution required.

**Q: Why not use Microsoft Office Interop?**
A: Office Interop requires Office licenses and is not designed for server-side automation. LibreOffice is built for headless server use.

**Q: Can we remove more LibreOffice components?**
A: Bundle is already optimized. Removing additional components will break conversion functionality. See bundle-libreoffice.ps1 for details.

**Q: Are macros enabled?**
A: No - LibreOffice runs in headless mode with no macro execution. Python runtime (macro engine) already removed from bundle.

### Contact Information

**Development Team:** [Your Team]
**Security Team:** [Your Security Team]
**Deployment Lead:** [Deployment Contact]
**Technical Questions:** [Technical Contact]

## Timeline Expectations

**Typical OPSWAT Review Process:**
1. Initial automated scan: 30-60 minutes
2. Security team review: 1-3 business days
3. False positive investigation: 1-2 business days
4. Management approval: 1-2 business days

**Total Time:** 3-7 business days typical

**Expedited Review:**
- Provide all documentation upfront
- Pre-identify expected detections
- Verify LibreOffice source before submission
- Schedule meeting with security team

## Final Checklist

Before submitting for OPSWAT scan:

- [ ] Read LIBREOFFICE_SECURITY.md
- [ ] Read SECURITY_SCAN_PREPARATION.md (this file)
- [ ] Verify deployment package created (deploy.ps1 completed)
- [ ] Confirm LibreOffice bundle source documented
- [ ] Review expected OPSWAT detections list
- [ ] Prepare responses to common questions
- [ ] Identify security team contacts
- [ ] Schedule follow-up meeting if needed

---

**Document Version:** 1.0
**Last Updated:** 2025-10-23
**Prepared For:** Cybersecurity OPSWAT Scanning
