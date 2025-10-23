# Deployment Guide

IIS deployment for Windows Server intranet environments. Converts 32 Office document format combinations.

## Prerequisites

### System Requirements

| Component | Requirement |
|-----------|-------------|
| OS | Windows Server 2016+ or Windows 11 |
| .NET Runtime | .NET 8.0 or later |
| IIS | IIS 8.5+ with ASP.NET Core Module |
| RAM | 4GB minimum, 8GB recommended |
| Disk Space | 2GB (500MB application + LibreOffice) |
| Network | No internet required for operation |

### Required Components

**.NET 8 Runtime:**
```powershell
# Download from: https://dotnet.microsoft.com/download/dotnet/8.0
dotnet --version  # Expected: 8.0.x
```

**IIS with ASP.NET Core Module:**
```powershell
# Install IIS
Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole
Enable-WindowsOptionalFeature -Online -FeatureName IIS-ASPNET45

# Download ASP.NET Core Hosting Bundle from dotnet.microsoft.com
# Run installer and restart IIS
iisreset
```

**LibreOffice Bundle:**
```powershell
# Create bundle (517 MB optimized)
.\bundle-libreoffice.ps1

# Verify
Test-Path FileConversionApi\LibreOffice\program\soffice.exe
```

## Deployment Steps

### 1. Build Deployment Package

```powershell
cd FileConversionApi
.\deploy.ps1
```

Creates `deploy\release` folder (~550-600 MB) with:
- Compiled .NET application (Release)
- LibreOffice bundle (500-550 MB optimized)
- Configuration files
- Required directory structure

### 2. Security Scan (If Required)

```powershell
# Scan with organization-approved tools
Scan-MpScan -ScanPath ".\deploy\release" -ScanType FullScan
```

### 3. Copy to Windows Server

**Option A: Network copy**
```powershell
Copy-Item .\deploy\release\* -Destination "\\SERVER\C$\inetpub\FileConversionApi" -Recurse -Force
```

**Option B: Robocopy (more reliable)**
```powershell
robocopy .\deploy\release \\SERVER\C$\inetpub\FileConversionApi /E /MT:8
```

**Option C: Air-gapped (manual)**
1. Compress `deploy\release` to ZIP
2. Transfer via USB or approved method
3. Extract to `C:\inetpub\FileConversionApi`

## IIS Configuration

### 1. Create Application Pool

In IIS Manager (`inetmgr`):

1. Right-click "Application Pools" → "Add Application Pool"
2. **Name:** `FileConversionApiPool`
3. **.NET CLR version:** **"No Managed Code"**
4. **Managed pipeline mode:** Integrated
5. Click OK

6. Right-click pool → "Advanced Settings":
   - **Identity:** `ApplicationPoolIdentity`
   - **Idle Time-out:** `0` (never sleep)
   - **Recycle Interval:** `1740` minutes (29 hours) or `0` to disable

### 2. Create Website

1. Right-click "Sites" → "Add Website"
2. **Site name:** `FileConversionApi`
3. **Application pool:** `FileConversionApiPool`
4. **Physical path:** `C:\inetpub\FileConversionApi`
5. **Binding:**
   - **Type:** http
   - **IP:** All Unassigned
   - **Port:** `80` (or preferred)
   - **Host name:** (blank for intranet, or `fileconversion.company.local`)

### 3. Configure HTTPS

1. Select site → "Bindings" → Add
2. **Type:** https
3. **Port:** 443
4. **SSL certificate:** Select your certificate (see Self-Signed Certificate section below)

### 4. Set File Permissions

```powershell
# Grant IIS_IUSRS read/execute
icacls "C:\inetpub\FileConversionApi" /grant "IIS_IUSRS:(OI)(CI)RX" /T

# Grant IIS_IUSRS full control on App_Data
icacls "C:\inetpub\FileConversionApi\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T
```

### 5. Start Application

1. Right-click pool → Start
2. Right-click site → Start
3. Wait 5-10 seconds for initialization

## Self-Signed Certificate Setup

For intranet environments using self-signed certificates:

**Create Certificate:**
```powershell
$cert = New-SelfSignedCertificate `
  -DnsName "fileconversion.company.local", "SERVER-NAME" `
  -CertStoreLocation "Cert:\LocalMachine\My" `
  -KeyExportPolicy Exportable `
  -NotAfter (Get-Date).AddYears(5) `
  -KeySpec KeyExchange `
  -KeyUsage DigitalSignature, KeyEncipherment

Write-Host "Certificate Thumbprint: $($cert.Thumbprint)" -ForegroundColor Green
```

**Bind to IIS:**
1. IIS Manager → Site → Bindings → Add/Edit
2. **Type:** https, **Port:** 443
3. **SSL certificate:** Select certificate

**Distribute to Clients:**

Option 1: Install on workstations (recommended)
```powershell
# On server, export:
$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object {$_.Thumbprint -eq "YOUR_THUMBPRINT"}
$password = ConvertTo-SecureString -String "YourPassword" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath "C:\Temp\FileConversionCert.pfx" -Password $password

# On clients:
$password = ConvertTo-SecureString -String "YourPassword" -Force -AsPlainText
Import-PfxCertificate -FilePath "\\SERVER\Share\FileConversionCert.pfx" `
  -CertStoreLocation "Cert:\LocalMachine\Root" -Password $password
```

Option 2: Use corporate internal CA (no distribution needed)

Option 3: Disable SSL validation (development only - not for production)

## Post-Deployment Configuration

Edit `C:\inetpub\FileConversionApi\appsettings.json` to adjust settings. Restart IIS application pool after changes.

**IP Whitelisting:**
```json
"Security": {
  "EnableIPFiltering": true,
  "IPWhitelist": [
    "192.168.1.0/24",
    "10.0.0.0/8",
    "172.16.0.0/12"
  ]
}
```

**File Size Limits:**
```json
"FileHandling": {
  "MaxFileSize": 52428800,  // 50 MB
  "TempFileRetentionHours": 24
}
```

**Conversion Performance:**
```json
"Concurrency": {
  "MaxConcurrentConversions": 2,  // Adjust based on CPU cores
  "MaxQueueSize": 10
}
```

Recommended by server capacity:
- 2-4 cores: 2 concurrent, 6 queue
- 4-8 cores: 4 concurrent, 12 queue
- 8+ cores: 6-8 concurrent, 20 queue

**Timeouts:**
```json
"LibreOffice": {
  "TimeoutSeconds": 300  // 5 minutes (adjust for large docs)
}
```

**Apply Changes:**
```powershell
Restart-WebAppPool -Name FileConversionApiPool
```

## Verification

**Health Check:**
```powershell
curl http://localhost/health
```

Expected:
```json
{
  "status": "Healthy",
  "timestamp": "2025-10-23T10:30:00Z",
  "services": {"LibreOffice": {"status": "Healthy"}}
}
```

**API Documentation:**
```
http://fileconversion.company.local/api-docs
```

**Test Conversion:**
```powershell
curl -X POST http://fileconversion.company.local/api/convert `
  -F "file=@sample.docx" -F "targetFormat=pdf" -o output.pdf
```

**View Logs:**
```powershell
Get-Content "C:\inetpub\FileConversionApi\App_Data\logs\file-conversion-api-*.log" -Tail 50
```

## Troubleshooting

**500.30 Error (Service Won't Start):**
```powershell
# Check runtime
dotnet --list-runtimes | findstr "Microsoft.AspNetCore.App 8"

# Check Event Viewer: Windows Logs → Application

# Test manually
cd C:\inetpub\FileConversionApi
dotnet FileConversionApi.dll

# Restart pool
Restart-WebAppPool -Name FileConversionApiPool
```

**LibreOffice Not Found:**
```powershell
# Verify bundle exists
Test-Path C:\inetpub\FileConversionApi\LibreOffice\program\soffice.exe

# If false, redeploy:
# 1. .\bundle-libreoffice.ps1
# 2. .\deploy.ps1
# 3. Copy deploy\release to server
```

**Permission Denied:**
```powershell
icacls "C:\inetpub\FileConversionApi\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T
iisreset
```

**SSL Certificate Errors:**
See Self-Signed Certificate section. Export from server and import to "Trusted Root Certification Authorities" on client workstations, or deploy via Group Policy.

**Conversion Timeouts:**
Edit `appsettings.json` to increase `TimeoutSeconds`, then restart pool.

**High Memory Usage:**
Reduce `MaxConcurrentConversions` in `appsettings.json`.

**Diagnostic Commands:**
```powershell
# Check IIS status
Get-WebAppPoolState -Name FileConversionApiPool
Get-Website -Name FileConversionApi

# View logs
Get-Content "C:\inetpub\FileConversionApi\App_Data\logs\*.log" -Tail 50

# Check Event Log
Get-EventLog -LogName Application -Source "IIS AspNetCore Module" -Newest 20

# Check LibreOffice processes
Get-Process | Where-Object {$_.ProcessName -like "*soffice*"}
```

## Documentation

- **README.md** - API reference
- **SUPPORTED_CONVERSIONS.md** - Conversion matrix
- **ARCHITECTURE.md** - Architecture overview
- **LIBREOFFICE_SECURITY.md** - LibreOffice security documentation
- **APPLICATION_SECURITY_ANALYSIS.md** - Application security analysis

**Endpoints:**
- Health: `http://your-server/health`
- API docs: `http://your-server/api-docs`

---
