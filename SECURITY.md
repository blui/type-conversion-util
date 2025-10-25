# Security Documentation

Comprehensive security analysis of the File Conversion API for security scanning and deployment approval.

## Application Overview

**Name:** File Conversion API
**Version:** 0.2.0
**Framework:** .NET 8.0 / ASP.NET Core
**Platform:** Windows Server (win-x64)
**Deployment:** Air-gapped intranet environments

## Security Architecture

### Defense in Depth

**Network Layer:**
- Rate limiting per IP address (AspNetCoreRateLimit)
- Configurable CORS for controlling API access
- Security headers (CSP, X-Frame-Options, HSTS, X-Content-Type-Options)

**Application Layer:**
- File type whitelist validation
- File size limits (50MB default, configurable)
- MIME type verification
- Path traversal prevention
- Request timeout enforcement (5 minutes default)
- Sanitized error messages (no information disclosure)

**Processing Layer:**
- Isolated LibreOffice process execution with timeout controls
- Temporary file isolation with GUID-based filenames
- Automatic cleanup with configurable retention
- Concurrency controls via semaphore

### Security Features

**Implemented:**
- Input validation and sanitization
- Rate limiting (30 req/min general, 10 req/min conversion)
- CORS configuration for access control
- Security headers on all responses
- Structured logging (Serilog) with operation IDs
- File system security with automatic cleanup
- Process isolation for conversion tasks

**Intentionally Not Implemented (Internal Service Design):**
- Authentication (deploy behind corporate VPN/firewall or add if needed)
- IP whitelisting (use network firewall/reverse proxy for this)
- HTTPS enforcement (configure via IIS/reverse proxy)
- Database (no persistence = no SQL injection risk)
- HTML rendering (no XSS risk)

## NuGet Dependencies

All packages from official NuGet.org repository.

### Microsoft Official (Zero Risk)
- Microsoft.AspNetCore.OpenApi (8.0.21)
- Microsoft.AspNetCore.Authentication.JwtBearer (8.0.21)
- DocumentFormat.OpenXml (3.0.2)
- Microsoft.Extensions.Configuration.* (8.0.0)

### Industry Standard (Low Risk)
- Swashbuckle.AspNetCore (6.6.2) - 1.5B+ downloads
- Serilog.* (8.0.1, 5.0.1, 5.0.0, 3.1.0) - 500M+ downloads
- CsvHelper (30.0.1) - 200M+ downloads
- AspNetCore.HealthChecks.* (8.0.0)
- System.Threading.Channels (8.0.0)

### Commercial/Established (Low Risk)
- iText7 (8.0.2) - AGPL/Commercial, Fortune 500 adoption
- NPOI (2.7.0) - Apache License, 50M+ downloads
- AspNetCoreRateLimit (5.0.0) - 25M+ downloads

### Community (Medium Risk - Verified Safe)
- PdfSharpCore (1.3.62) - Community fork, 5M+ downloads, MIT license
- SharpZipLib (1.4.2) - 200M+ downloads, MIT license, established since 2000

**Verification:**
```powershell
dotnet nuget verify FileConversionApi.csproj
```

## LibreOffice Bundle

### Details

**Software:** LibreOffice
**License:** Mozilla Public License v2.0
**Source:** https://www.libreoffice.org/download/
**Vendor:** The Document Foundation (non-profit)

### Purpose

Headless document conversion in air-gapped environments where:
- No internet connectivity available
- No Microsoft Office licenses deployed
- Non-interactive conversion required

**Usage:** Headless mode only (no GUI, no user interaction, no macro execution)

### Trust Profile

**Industry Adoption:**
- Government use worldwide (German, French, Italian, UK administrations)
- 20+ year open-source history (OpenOffice.org since 2000)
- Active security patch releases
- No telemetry or network calls in headless mode

**Implementation Security:**
- Isolated process execution with limited permissions
- Macro execution disabled
- Network access not configured
- Automatic process cleanup with timeout controls
- Runs under IIS AppPool identity (restricted)

### Bundle Contents

**Included (Required):**
- `program/soffice.exe` - Main executable
- `program/soffice.bin` - Conversion engine
- `program/*.dll` - Essential libraries
- `share/registry/` - Format configuration
- `share/filter/` - Conversion filters (DOC, DOCX, PDF, etc.)
- English language pack only

**Removed (Security Optimizations):**
- Python runtime (macro scripting)
- UI components (wizards, help)
- Gallery, templates, samples
- All non-English language packs
- Extensions and user packages

**Size Reduction:** 60-70% smaller than full installation (~500MB vs ~2GB)

**Bundle Script:** `bundle-libreoffice.ps1` (review for transparency)

### Verification

```powershell
# Verify executable hash
Get-FileHash FileConversionApi\LibreOffice\program\soffice.exe -Algorithm SHA256

# Compare against official LibreOffice download
# Hash available at: https://www.libreoffice.org/download/
```

## Expected Security Scan Results

### .NET Application

**Will Detect (Low Risk - Expected):**

1. **Custom DLL** - FileConversionApi.dll
   - Reason: Compiled custom code
   - Mitigation: Source code available in repository

2. **Third-Party DLLs** - 20+ NuGet packages
   - Reason: Dependencies from NuGet.org
   - Mitigation: All from official Microsoft repository with hash verification

3. **Framework DLLs** - 50+ Microsoft .NET 8 assemblies
   - Reason: .NET runtime dependencies
   - Mitigation: Official Microsoft signed assemblies

4. **Configuration Files** - appsettings.json, web.config
   - Reason: Contains localhost IPs (127.0.0.1, ::1), file paths, RFC 1918 private ranges
   - Mitigation: No hardcoded secrets, only configuration

### LibreOffice Bundle

**Will Detect (Medium Risk - False Positives):**

1. **Unsigned Executables** - soffice.exe, soffice.bin
   - Reason: Open-source software, community-signed
   - Mitigation: Verify SHA256 against official distribution

2. **Large Binary Collection** - Multiple DLLs in program/
   - Reason: Required libraries for document formats
   - Mitigation: All from official LibreOffice distribution

3. **Heuristic Flags** - Process spawning, file system access
   - Reason: Normal operation for document conversion
   - Mitigation: Limited to documented conversion use, runs with restricted permissions

4. **"Potentially Unwanted"** - Generic office suite behavior
   - Reason: Some engines flag office software generically
   - Mitigation: False positive - LibreOffice is legitimate, widely-used software

## Security Scan False Positives

**Expected Behavioral Detections:**

1. **Process Spawning** (LibreOfficeProcessManager.cs:line 45)
   - Behavior: Spawns LibreOffice processes for conversion
   - Control: Timeout limits, automatic cleanup, limited to soffice.exe only
   - Status: Required for core functionality

2. **File System Access** (Multiple services)
   - Behavior: Creates/reads/writes files in App_Data\temp
   - Control: Isolated to temp directories, GUID filenames, automatic cleanup
   - Status: Required for core functionality

3. **Dynamic Code** (DocxPreProcessor.cs)
   - Behavior: .NET reflection (GetType(), GetProperty())
   - Control: Read-only reflection for DOCX manipulation, no code generation
   - Status: Standard .NET pattern - false positive

4. **Network Listener** (ASP.NET Core Kestrel)
   - Behavior: Opens HTTP/HTTPS ports
   - Control: IIS reverse proxy, IP filtering middleware
   - Status: Required for web API

## No Malware Patterns

**Application Does NOT:**
- Self-modify or inject code
- Manipulate registry or create services
- Attempt privilege escalation
- Use anti-debugging or obfuscation
- Log keystrokes or capture screens
- Harvest credentials
- Communicate with C&C servers
- Exfiltrate data
- Mine cryptocurrency
- Contain backdoors or remote access

**Application DOES:**
- Accept HTTP requests on configured port
- Validate and process uploaded files
- Spawn LibreOffice for conversion
- Write converted files to temp directory
- Return converted file to requester
- Clean up temporary files
- Log operations to file and console

**Pattern:** Standard enterprise web API file processing service

## Build Artifacts

**Release Build Output:** `deploy\release`
- FileConversionApi.dll (~200 KB compiled code)
- appsettings.json (configuration)
- web.config (IIS configuration)
- 20+ dependency DLLs (~10-20 MB)
- LibreOffice bundle (~500 MB)

**Total Size:** ~550 MB (500 MB LibreOffice + 50 MB application)

**Reproducibility:**
```powershell
dotnet build -c Release FileConversionApi/FileConversionApi.csproj
dotnet publish -c Release -r win-x64 --self-contained false
Get-FileHash deploy\release\FileConversionApi.dll
```

## Hardened Configuration

**No Hardcoded Secrets:**
- No passwords, API keys, connection strings
- No authentication tokens or certificates
- No private keys
- Only configuration GUIDs (UserSecretsId for dev, CancellationToken for async)

**Configuration Security:**
- All secrets via environment variables or secure configuration
- CORS configured for allowed origins
- Rate limits prevent abuse
- Timeout controls prevent resource exhaustion

## OPSWAT Scan Recommendations

### Pre-Scan Preparation

1. Scan .NET application separately from LibreOffice bundle
2. Verify NuGet package hashes match NuGet.org
3. Review custom code (no obfuscation, fully readable)

### Expected Results

| Component | Expected Result | Risk Level | Action |
|-----------|----------------|------------|--------|
| Custom .NET Code | Pass | Low | Approve |
| Microsoft Packages | Pass | Low | Approve |
| Popular Libraries | Pass | Low | Approve |
| iText7, NPOI | Pass (may flag AGPL license) | Low | Review license |
| PdfSharpCore | May flag as fork | Medium | Verify GitHub source |
| SharpZipLib | May flag compression libs | Medium | Review - legitimate |
| Configuration Files | Pass | Low | Approve |
| LibreOffice (soffice.exe) | May flag unsigned/heuristic | Medium | Verify hash, approve |
| LibreOffice DLLs | May flag large binary set | Medium | Standard for office suite |

### Approval Criteria

**Approve if:**
- No actual malware signatures (heuristic flags acceptable)
- All NuGet packages from official NuGet.org
- No hardcoded secrets
- Process spawning limited to documented LibreOffice use
- File access limited to temp directories
- Network activity matches web API pattern
- LibreOffice hash matches official distribution

**Escalate if:**
- Unexpected malware signatures beyond heuristics
- Modified NuGet packages (hash mismatch)
- Unrecognized executables beyond LibreOffice
- Suspicious network activity beyond HTTP listener
- Evidence of obfuscation, packing, or code injection

## License Compliance

| Package | License | Commercial Use | Notes |
|---------|---------|----------------|-------|
| Microsoft.* | MIT | Allowed | Official Microsoft |
| Swashbuckle | MIT | Allowed | Open source |
| Serilog | Apache 2.0 | Allowed | Open source |
| **iText7** | **AGPL 3.0** | **Restrictions** | **Requires source disclosure if distributed** |
| NPOI | Apache 2.0 | Allowed | Open source |
| PdfSharpCore | MIT | Allowed | Open source fork |
| CsvHelper | MS-PL/Apache | Allowed | Dual license |
| SharpZipLib | MIT | Allowed | Open source |
| LibreOffice | MPL 2.0 | Allowed | Open source |

**AGPL Notice:** iText7 requires source code disclosure if software is distributed externally. For internal-only deployment, this is typically acceptable. Verify with legal if distributing beyond your organization.

## Deployment Security

### Production Hardening

**In appsettings.json:**
```json
{
  "AllowedOrigins": ["https://yourapp.company.com"],   // Restrict CORS
  "ConversionTimeout": 180,                            // Reduce for tighter control
  "RateLimiting": {
    "GeneralLimit": 20,                                // Tighter rate limits
    "ConversionLimit": 5
  }
}
```

**IIS Configuration:**
- Run under dedicated AppPool with restricted identity
- Grant minimum required file system permissions
- Configure HTTPS with valid certificate
- Enable request filtering
- Set up Windows Firewall rules

**Monitoring:**
- Enable Serilog logging to file and Windows Event Log
- Monitor conversion success/failure rates
- Track failed authentication attempts (if enabled)
- Alert on unusual file sizes or processing times

## Conclusion

The File Conversion API is designed for secure, air-gapped enterprise deployment with:

- Clean, auditable source code
- Industry-standard libraries from official sources
- Multi-layer security controls
- No malware patterns or malicious behavior
- Transparent build and deployment process

**Expected OPSWAT Outcome:** Pass with minor heuristic flags on LibreOffice (false positives)

**Recommendation:** Approve for deployment in isolated enterprise environments

---

**Version:** 1.0
**Last Updated:** 2025-10-24
