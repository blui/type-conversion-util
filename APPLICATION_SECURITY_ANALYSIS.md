# Application Security Analysis

Security analysis of the .NET 8 application code and NuGet dependencies for OPSWAT scanning. This covers only the application code, not LibreOffice (see LIBREOFFICE_SECURITY.md).

## Application Information

**Name:** File Conversion API
**Version:** 0.2.0
**Framework:** .NET 8.0 / ASP.NET Core
**Language:** C#
**Target Platform:** Windows Server (win-x64)
**Build Type:** Release (framework-dependent)

## Custom Code

**Location:** `FileConversionApi/`
**Components:** Controllers, Services, Middleware, Models, Utilities
**Total:** ~5,000 lines of C#
**External Libraries:** 20 NuGet packages

## Security Features

**Implemented:**
- Input validation: File type whitelist, size limits (50MB default), MIME verification, path traversal prevention
- IP whitelisting: CIDR-based with bit-level subnet masks, localhost detection, rate limiting per IP
- Rate limiting: AspNetCoreRateLimit (30 req/min default, 10 req/min for conversion)
- Error handling: Sanitized error messages, structured logging with Serilog
- Security headers: CSP, X-Frame-Options, X-XSS-Protection, X-Content-Type-Options, HSTS
- File system security: Temporary file isolation, automatic cleanup, 24-hour retention, GUID-based filenames

**Intentionally Not Implemented (Air-Gapped Environment):**
- Authentication (internal network only)
- Authorization (trusted internal users)
- HTTPS enforcement (optional, configured via IIS)
- SQL injection protection (no database)
- XSS protection (no HTML rendering)

## NuGet Dependencies

All packages from official NuGet.org (Microsoft-operated).

**Microsoft Official (Zero Risk):**
- Microsoft.AspNetCore.OpenApi (8.0.21)
- Microsoft.AspNetCore.Authentication.JwtBearer (8.0.21)
- DocumentFormat.OpenXml (3.0.2)
- Microsoft.Extensions.Configuration.* (8.0.0)

**Industry Standard (Low Risk):**
- Swashbuckle.AspNetCore (6.6.2) - 1.5B+ downloads
- Serilog.* (8.0.1, 5.0.1, 5.0.0, 3.1.0) - 500M+ downloads
- CsvHelper (30.0.1) - 200M+ downloads
- AspNetCore.HealthChecks.* (8.0.0)
- System.Threading.Channels (8.0.0)

**Commercial/Established (Low Risk):**
- iText7 (8.0.2) - AGPL/Commercial, Fortune 500 use
- NPOI (2.7.0) - Apache License, 50M+ downloads
- AspNetCoreRateLimit (5.0.0) - 25M+ downloads

**Community (Medium Risk):**
- PdfSharpCore (1.3.62) - Community fork, 5M+ downloads, MIT license
- SharpZipLib (1.4.2) - 200M+ downloads, MIT license, since 2000

## Expected OPSWAT Detections

**Application DLLs (Low Risk):**
- FileConversionApi.dll (custom code)
- 20+ third-party DLLs from NuGet
- 50+ Microsoft framework DLLs (.NET 8)
- Most signed by Microsoft or well-known publishers
- Community libraries may show "unsigned" or "low reputation"
- All from official NuGet.org

**Configuration Files (Low Risk):**
- appsettings.json, appsettings.Production.json, web.config
- Contains localhost references (127.0.0.1, ::1), file paths (C:\), IP ranges (RFC 1918 private)
- No hardcoded secrets

**Verified Clean:**
- No hardcoded passwords, API keys, connection strings, authentication tokens, certificates, or private keys
- Only `UserSecretsId` GUID (for .NET development secrets management) and `CancellationToken` (async pattern)

**Potential False Positives:**

1. **Process Spawning** (LibreOfficeProcessManager.cs)
   - Spawns LibreOffice processes for conversion
   - Controlled with timeout and automatic cleanup
   - Limited to soffice.exe only
   - Status: Expected behavior - required

2. **File System Access** (Multiple services)
   - Creates/reads/writes files in temp directories
   - Isolated to App_Data\temp only
   - GUID-based filenames (no user control)
   - Automatic cleanup
   - Status: Expected behavior - required

3. **Dynamic Code Execution** (DocxPreProcessor.cs)
   - May flag reflection usage in DocumentFormat.OpenXml
   - Uses GetType(), GetProperty() for document manipulation
   - Read-only reflection, no code generation
   - Status: False positive - standard .NET reflection

4. **Network Listeners** (ASP.NET Core Kestrel)
   - Opens HTTP/HTTPS ports
   - Configured via IIS (reverse proxy)
   - IP filtering via middleware
   - Status: Expected behavior - web API

## No Malware Patterns

Application does NOT:
- Self-modify or inject code
- Manipulate registry or create services
- Attempt privilege escalation
- Use anti-debugging or obfuscation
- Log keystrokes or capture screens
- Harvest credentials
- Communicate with C&C servers
- Exfiltrate data (air-gapped)
- Mine cryptocurrency
- Have backdoors or remote access

Application DOES:
- Accept HTTP requests on configured port
- Validate and read uploaded files
- Spawn LibreOffice for conversion
- Write converted files to temp directory
- Return converted file to requester
- Clean up temporary files
- Log operations

Pattern: Standard web API file processing service

## Build Artifacts

**Release Build Output:** `deploy\release`
```
FileConversionApi.dll          - Compiled code (~200 KB)
FileConversionApi.pdb          - Debug symbols (optional)
web.config                     - IIS configuration
appsettings.json              - Runtime configuration
*.dll (20+ files)             - NuGet dependencies (~10-20 MB)
runtimes/                     - Native libraries
LibreOffice/                  - Conversion engine (~500 MB)
```

**Total Application Size (excluding LibreOffice):** ~50 MB

**Build Reproducibility:**
```powershell
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained false
Get-FileHash bin\Release\net8.0\publish\FileConversionApi.dll
```
NuGet packages restored from NuGet.org with package hash verification.

## Security Scan Recommendations

**Pre-Scan:**
1. Scan .NET application separately from LibreOffice
2. Verify NuGet packages: `dotnet nuget verify FileConversionApi.csproj`
3. Review custom code in FileConversionApi/ (no obfuscation)

**Expected Results:**

| Component | Expected | Risk | Action |
|-----------|----------|------|--------|
| Custom .NET Code | Pass | Low | Approve |
| Microsoft Packages | Pass | Low | Approve |
| Popular Libraries | Pass | Low | Approve |
| iText7, NPOI | Pass (may flag license) | Low | Review license |
| PdfSharpCore | May flag as fork | Medium | Verify GitHub |
| SharpZipLib | May flag compression | Medium | Review - legitimate |
| Configuration Files | Pass | Low | Approve |
| LibreOffice Bundle | See LIBREOFFICE_SECURITY.md | Medium | See doc |

**Approval Criteria:**
- No actual malware signatures
- All NuGet packages from NuGet.org
- No hardcoded secrets
- Process spawning limited to documented LibreOffice use
- File access limited to documented temp directories
- Network access matches web API pattern

**Escalate if:**
- Unexpected malware signatures (beyond heuristics)
- Modified NuGet packages (hash mismatch)
- Unrecognized executables beyond LibreOffice
- Suspicious network activity beyond HTTP listener
- Evidence of obfuscation or packing

## License Compliance

| Package | License | Commercial Use | Notes |
|---------|---------|----------------|-------|
| Microsoft.* | MIT | Allowed | Official Microsoft |
| Swashbuckle | MIT | Allowed | Open source |
| Serilog | Apache 2.0 | Allowed | Open source |
| iText7 | AGPL 3.0 | Restrictions | Using free version, AGPL compliance required |
| NPOI | Apache 2.0 | Allowed | Open source |
| PdfSharpCore | MIT | Allowed | Open source fork |
| CsvHelper | MS-PL/Apache | Allowed | Dual license |
| SharpZipLib | MIT | Allowed | Open source |
| AspNetCoreRateLimit | MIT | Allowed | Open source |

**Note:** iText7 is AGPL - requires source code disclosure if distributed. Internal-only deployment, but verify with legal if distributing externally.

## Conclusion

The .NET application code is clean, uses industry-standard libraries from official sources, and implements proper security controls for air-gapped intranet deployment.

**Expected OPSWAT Outcome:** Pass with possible minor flags on community libraries (PdfSharpCore, SharpZipLib) which are legitimate and widely-used.

**Recommendation:** Approve deployment pending verification that NuGet packages are from official NuGet.org repository.

---

**Version:** 1.0
**Updated:** 2025-10-23
