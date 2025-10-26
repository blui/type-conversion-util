# Deployment Guide

Complete guide for deploying File Conversion API to Windows Server with IIS.

## Prerequisites

### System Requirements

| Component | Requirement |
|-----------|-------------|
| OS | Windows Server 2016+ or Windows 11 |
| .NET Runtime | .NET 8.0 or later |
| IIS | IIS 8.5+ with ASP.NET Core Module |
| RAM | 4GB minimum, 8GB recommended |
| Disk Space | 2GB (500MB application + LibreOffice bundle) |
| Network | No internet required for operation |

### Required Components

**.NET 8 Runtime:**

```powershell
# Download from: https://dotnet.microsoft.com/download/dotnet/8.0
# Install ASP.NET Core Hosting Bundle
dotnet --version  # Expected: 8.0.x
```

**Visual C++ Redistributable (Bundled):**

The LibreOffice bundle includes Visual C++ runtime DLLs, so no separate installation is required on the server. The required DLLs are bundled automatically when running `bundle-libreoffice.ps1`.

Note: The build machine must have Visual C++ Redistributable (2015-2022) installed to create the bundle. Download from: https://aka.ms/vs/17/release/vc_redist.x64.exe

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
# Step 1: Create LibreOffice bundle (517 MB optimized with VC++ runtime DLLs)
.\bundle-libreoffice.ps1

# Step 2: Create pre-initialized user profile template (5-10 MB)
.\create-libreoffice-profile-template.ps1

# Verify bundle
Test-Path FileConversionApi\LibreOffice\program\soffice.exe

# Verify profile template
Test-Path FileConversionApi\libreoffice-profile-template
```

**What gets bundled:**
- LibreOffice core conversion engines and filters (~500 MB)
- Visual C++ runtime DLLs (bundled in LibreOffice\program, no server installation required)
- Pre-initialized user profile template (eliminates initialization issues under IIS)

**Note:** The build machine must have:
- LibreOffice installed (C:\Program Files\LibreOffice)
- Visual C++ Redistributable 2015-2022 (to copy DLLs from System32)

The deployment servers do NOT need Visual C++ Redistributable installed - all required DLLs are bundled.

## Deployment Steps

### 1. Build Deployment Package

```powershell
cd FileConversionApi
.\deploy.ps1
```

Creates `deploy\release` folder (~550-600 MB) with:
- Compiled .NET application (Release build)
- LibreOffice bundle (500-550 MB optimized)
- Configuration files (appsettings.json, web.config)
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

**Option C: Air-gapped (manual transfer)**

1. Compress `deploy\release` to ZIP
2. Transfer via USB or approved method
3. Extract to `C:\inetpub\FileConversionApi`

## Verify Prerequisites on Server

Before configuring IIS, verify all dependencies are installed:

```powershell
# Check .NET 8 Runtime
dotnet --list-runtimes | findstr "Microsoft.AspNetCore.App 8"

# Check LibreOffice bundle and bundled VC++ DLLs
Test-Path "C:\inetpub\FileConversionApi\LibreOffice\program\soffice.exe"
Test-Path "C:\inetpub\FileConversionApi\LibreOffice\program\msvcp140.dll"
Test-Path "C:\inetpub\FileConversionApi\LibreOffice\program\vcruntime140.dll"
```

If any checks fail, ensure you've deployed using the complete deployment package created by `deploy.ps1`.

## IIS Configuration

### 1. Create Application Pool

In IIS Manager (`inetmgr`):

1. Right-click "Application Pools" → "Add Application Pool"
2. **Name:** `FileConversionApiPool`
3. **.NET CLR version:** **"No Managed Code"** (important for .NET 8)
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

### 3. Configure HTTPS (Recommended)

1. Select site → "Bindings" → Add
2. **Type:** https
3. **Port:** 443
4. **SSL certificate:** Select your certificate (see Self-Signed Certificate section below)

### 4. Set File Permissions

**CRITICAL: This step is required for the application to function.**

Run these commands on the server. Replace the path if your deployment location differs:

```powershell
# Set deployment path variable (adjust if different)
$deployPath = "D:\inetpub\wwwroot\Service\FileConversionApi"

# 1. App_Data - Full Control (Required for temp files, logs, and profile directories)
icacls "$deployPath\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T

# 2. LibreOffice - Read and Execute (Required to run soffice.exe and read bundled DLLs)
icacls "$deployPath\LibreOffice" /grant "IIS_IUSRS:(OI)(CI)RX" /T

# 3. Profile Template - Read (Required to copy template per conversion)
icacls "$deployPath\libreoffice-profile-template" /grant "IIS_IUSRS:(OI)(CI)R" /T

# Verify permissions were set
Write-Host "`nVerifying permissions..." -ForegroundColor Cyan
icacls "$deployPath\App_Data" | Select-String "IIS_IUSRS"
icacls "$deployPath\LibreOffice" | Select-String "IIS_IUSRS"
icacls "$deployPath\libreoffice-profile-template" | Select-String "IIS_IUSRS"
```

**Permission Flags Explained:**
- `(OI)` - Object Inherit: Files inherit the permission
- `(CI)` - Container Inherit: Subdirectories inherit the permission
- `F` - Full control (Read, Write, Execute, Delete, Modify)
- `RX` - Read and Execute
- `R` - Read only
- `/T` - Apply recursively to all existing files/subdirectories

**Alternative: Grant permissions to specific application pool identity**

If using a custom application pool identity instead of ApplicationPoolIdentity:

```powershell
$deployPath = "D:\inetpub\wwwroot\Service\FileConversionApi"
$poolIdentity = "IIS APPPOOL\FileConversionApiPool"

icacls "$deployPath\App_Data" /grant "${poolIdentity}:(OI)(CI)F" /T
icacls "$deployPath\LibreOffice" /grant "${poolIdentity}:(OI)(CI)RX" /T
icacls "$deployPath\libreoffice-profile-template" /grant "${poolIdentity}:(OI)(CI)R" /T
```

### 5. Start Application

1. Right-click pool → Start
2. Right-click site → Start
3. Wait 5-10 seconds for initialization

## IIS Sub-Application Deployment

If deploying to a virtual directory or sub-application (e.g., `https://server/FileConversionApi`):

**1. Create Application in IIS:**

Instead of creating a new site, create an application under an existing site:

1. Open IIS Manager
2. Expand existing site (e.g., "Default Web Site")
3. Right-click site → "Add Application"
4. **Alias:** `FileConversionApi`
5. **Application Pool:** Select `FileConversionApiPool`
6. **Physical Path:** `C:\inetpub\FileConversionApi`
7. Click OK

**2. Configure PathBase (Optional but Recommended):**

Add to `appsettings.json`:

```json
{
  "PathBase": "/FileConversionApi"
}
```

Or set as environment variable:

```powershell
Set-ItemProperty "IIS:\AppPools\FileConversionApiPool" -Name EnvironmentVariables -Value @{
    "PathBase" = "/FileConversionApi"
}

Restart-WebAppPool -Name FileConversionApiPool
```

**3. Verify Endpoints:**

With sub-application deployment, URLs include the application path:

```powershell
# Health check
curl https://devservice.company.org/FileConversionApi/health

# API information
curl https://devservice.company.org/FileConversionApi/api

# Swagger documentation
curl https://devservice.company.org/FileConversionApi/api-docs

# API endpoint
curl -X POST https://devservice.company.org/FileConversionApi/api/convert `
  -F "file=@document.docx" `
  -F "targetFormat=pdf"
```

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
3. **SSL certificate:** Select certificate by thumbprint

**Distribute to Clients:**

Option 1: Install on workstations (recommended)

```powershell
# On server, export certificate:
$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object {$_.Thumbprint -eq "YOUR_THUMBPRINT"}
$password = ConvertTo-SecureString -String "YourPassword" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath "C:\Temp\FileConversionCert.pfx" -Password $password

# On clients, import to Trusted Root:
$password = ConvertTo-SecureString -String "YourPassword" -Force -AsPlainText
Import-PfxCertificate -FilePath "\\SERVER\Share\FileConversionCert.pfx" `
  -CertStoreLocation "Cert:\LocalMachine\Root" -Password $password
```

Option 2: Use corporate internal CA (no distribution needed)

Option 3: Disable SSL validation (development only - not for production)

## Configuration

Edit `C:\inetpub\FileConversionApi\appsettings.json` to adjust settings. Restart IIS application pool after changes.

### Security Configuration

**Rate Limiting:**

```json
{
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": true,
    "GeneralRules": [
      { "Endpoint": "*", "Period": "1m", "Limit": 30 },
      { "Endpoint": "POST:/api/convert", "Period": "1m", "Limit": 10 }
    ]
  }
}
```

**CORS:**

```json
{
  "Security": {
    "AllowedOrigins": [
      "https://intranet.company.local",
      "https://portal.company.local"
    ]
  }
}
```

### File Handling Configuration

**File Size Limits:**

```json
{
  "FileHandling": {
    "MaxFileSize": 52428800,
    "MaxFilesPerRequest": 5,
    "AllowedExtensions": [
      "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx",
      "txt", "rtf", "odt", "ods", "odp", "csv", "xml", "html"
    ]
  }
}
```

**Temporary File Directories:**

Relative paths (default):

```json
{
  "FileHandling": {
    "TempDirectory": "App_Data\\temp\\uploads",
    "OutputDirectory": "App_Data\\temp\\converted"
  }
}
```

Absolute paths:

```json
{
  "FileHandling": {
    "TempDirectory": "D:\\AppData\\FileConversion\\Uploads",
    "OutputDirectory": "D:\\AppData\\FileConversion\\Converted"
  }
}
```

Network share:

```json
{
  "FileHandling": {
    "TempDirectory": "\\\\FileServer\\Shared\\FileConversion\\Uploads",
    "OutputDirectory": "\\\\FileServer\\Shared\\FileConversion\\Converted"
  }
}
```

**Cleanup:**

```json
{
  "FileHandling": {
    "CleanupTempFiles": true,
    "TempFileRetentionHours": 24
  }
}
```

### Performance Configuration

**Concurrency:**

```json
{
  "Concurrency": {
    "MaxConcurrentConversions": 2,
    "MaxQueueSize": 10,
    "ThreadPoolSettings": {
      "MinThreads": 4,
      "MaxThreads": 16
    }
  }
}
```

Recommended settings by server capacity:

| CPU Cores | MaxConcurrentConversions | MaxQueueSize |
|-----------|-------------------------|--------------|
| 2-4 cores | 2 | 6 |
| 4-8 cores | 4 | 12 |
| 8+ cores | 6-8 | 20 |

**Timeouts:**

```json
{
  "LibreOffice": {
    "TimeoutSeconds": 300
  },
  "Network": {
    "RequestTimeout": 300000
  }
}
```

### Logging Configuration

**Serilog:**

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "Microsoft.AspNetCore": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "App_Data\\logs\\file-conversion-api-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  }
}
```

**Apply Configuration Changes:**

```powershell
Restart-WebAppPool -Name FileConversionApiPool
```

## Verification

### Quick Verification Checklist

Run through these steps in order to verify your deployment is working correctly:

#### 1. Verify Files Were Copied

```powershell
$deployPath = "D:\inetpub\wwwroot\Service\FileConversionApi"

# Check main application DLL exists
Test-Path "$deployPath\FileConversionApi.dll"  # Should return True

# Check LibreOffice bundle exists
Test-Path "$deployPath\LibreOffice\program\soffice.exe"  # Should return True

# Check profile template exists
Test-Path "$deployPath\libreoffice-profile-template"  # Should return True
$templateFiles = Get-ChildItem "$deployPath\libreoffice-profile-template" -Recurse -File
Write-Host "Profile template files: $($templateFiles.Count)"  # Should show 2+ files

# Check VC++ DLLs are bundled
Test-Path "$deployPath\LibreOffice\program\msvcp140.dll"  # Should return True
Test-Path "$deployPath\LibreOffice\program\vcruntime140.dll"  # Should return True
Test-Path "$deployPath\LibreOffice\program\msvcp140_atomic_wait.dll"  # Should return True
```

#### 2. Verify Permissions

```powershell
# Check App_Data permissions
icacls "$deployPath\App_Data" | Select-String "IIS_IUSRS.*F"
# Should show: IIS_IUSRS:(OI)(CI)F

# Check LibreOffice permissions
icacls "$deployPath\LibreOffice" | Select-String "IIS_IUSRS.*RX"
# Should show: IIS_IUSRS:(OI)(CI)RX

# Check profile template permissions
icacls "$deployPath\libreoffice-profile-template" | Select-String "IIS_IUSRS.*R"
# Should show: IIS_IUSRS:(OI)(CI)R

# Test write access to App_Data
$testFile = "$deployPath\App_Data\permission-test-$(Get-Random).txt"
"Test" | Out-File $testFile
if (Test-Path $testFile) {
    Write-Host "✓ App_Data is writable" -ForegroundColor Green
    Remove-Item $testFile
} else {
    Write-Host "✗ App_Data is NOT writable - FIX PERMISSIONS!" -ForegroundColor Red
}
```

#### 3. Verify IIS Configuration

```powershell
# Check application pool exists and is running
Get-WebAppPoolState -Name "FileConversionApiPool"
# Should show: Started

# Check site or application exists
Get-Website | Where-Object { $_.PhysicalPath -like "*FileConversionApi*" }
# OR for sub-application:
# Get-WebApplication | Where-Object { $_.PhysicalPath -like "*FileConversionApi*" }

# Check .NET runtime is installed
dotnet --list-runtimes | Select-String "Microsoft.AspNetCore.App.*8"
# Should show: Microsoft.AspNetCore.App 8.x.x
```

#### 4. Test Health Endpoint

```powershell
# Basic health check
$health = Invoke-RestMethod -Uri "http://localhost/health" -ErrorAction Stop
Write-Host "Status: $($health.status)" -ForegroundColor $(if ($health.status -eq "Healthy") { "Green" } else { "Red" })

# If using sub-application path:
# $health = Invoke-RestMethod -Uri "http://localhost/FileConversionApi/health"
```

Expected response:

```json
{
  "status": "Healthy",
  "timestamp": "2025-10-26T10:30:00Z",
  "services": {
    "LibreOffice": {
      "status": "Healthy",
      "message": "Available"
    }
  }
}
```

#### 5. Test Conversion

Create a simple test file:

```powershell
# Create test document
$testDoc = "$env:TEMP\test-conversion.txt"
"Hello World`nThis is a test document for conversion." | Out-File $testDoc

# Test conversion
$uri = "http://localhost/api/convert"  # Or /FileConversionApi/api/convert for sub-app
$outputFile = "$env:TEMP\test-output.pdf"

curl.exe -X POST $uri -F "file=@$testDoc" -F "targetFormat=pdf" -o $outputFile

# Verify output
if (Test-Path $outputFile) {
    $size = (Get-Item $outputFile).Length
    Write-Host "✓ Conversion successful! PDF created: $size bytes" -ForegroundColor Green
    Write-Host "  Output: $outputFile" -ForegroundColor Gray
} else {
    Write-Host "✗ Conversion FAILED - Check logs" -ForegroundColor Red
}
```

#### 6. Check Logs

```powershell
# View recent logs
$logPath = "$deployPath\App_Data\logs\file-conversion-api-*.log"
Get-ChildItem $logPath | Sort-Object LastWriteTime -Descending | Select-Object -First 1 | ForEach-Object {
    Write-Host "`nRecent log entries from: $($_.Name)" -ForegroundColor Cyan
    Get-Content $_.FullName -Tail 30
}

# Check for errors
Get-Content $logPath -Tail 100 | Select-String -Pattern "Error|Exception|Failed" -Context 2
```

#### 7. Verify Profile Template Usage

After running a conversion, check that the profile template is being used:

```powershell
# Look for profile template usage in logs
Get-Content "$deployPath\App_Data\logs\file-conversion-api-*.log" -Tail 100 | Select-String "profile"

# Expected to see:
# "Copied LibreOffice profile template to: ..."

# If you see this warning, the template is missing:
# "Profile template not found, LibreOffice will create profile"
```

#### 8. Check for Hung Processes

```powershell
# Check for any hung LibreOffice processes
$processes = Get-Process | Where-Object { $_.ProcessName -like "*soffice*" }
if ($processes) {
    Write-Host "WARNING: Found $($processes.Count) LibreOffice process(es)" -ForegroundColor Yellow
    $processes | Format-Table ProcessName, Id, StartTime, CPU, WorkingSet -AutoSize
    Write-Host "If processes are old (>5 minutes), they may be hung. Kill them:" -ForegroundColor Yellow
    Write-Host "  taskkill /F /IM soffice.exe" -ForegroundColor Gray
} else {
    Write-Host "✓ No LibreOffice processes running (normal when idle)" -ForegroundColor Green
}
```

### Complete Verification Script

Run this all-in-one verification script:

```powershell
$deployPath = "D:\inetpub\wwwroot\Service\FileConversionApi"
$baseUrl = "http://localhost"  # Change to /FileConversionApi if sub-application

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "File Conversion API Deployment Verification" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# 1. Files
Write-Host "1. Checking files..." -ForegroundColor Yellow
$checks = @(
    @{ Path = "$deployPath\FileConversionApi.dll"; Name = "Application DLL" },
    @{ Path = "$deployPath\LibreOffice\program\soffice.exe"; Name = "LibreOffice" },
    @{ Path = "$deployPath\libreoffice-profile-template"; Name = "Profile Template" },
    @{ Path = "$deployPath\LibreOffice\program\msvcp140.dll"; Name = "VC++ DLL (msvcp140)" },
    @{ Path = "$deployPath\LibreOffice\program\msvcp140_atomic_wait.dll"; Name = "VC++ DLL (atomic_wait)" }
)
$allFilesOk = $true
foreach ($check in $checks) {
    $exists = Test-Path $check.Path
    $icon = if ($exists) { "✓" } else { "✗"; $allFilesOk = $false }
    $color = if ($exists) { "Green" } else { "Red" }
    Write-Host "  $icon $($check.Name)" -ForegroundColor $color
}

# 2. Permissions
Write-Host "`n2. Checking permissions..." -ForegroundColor Yellow
$permOk = $true
try {
    $testFile = "$deployPath\App_Data\test-$(Get-Random).txt"
    "test" | Out-File $testFile -ErrorAction Stop
    Remove-Item $testFile -ErrorAction Stop
    Write-Host "  ✓ App_Data writable" -ForegroundColor Green
} catch {
    Write-Host "  ✗ App_Data NOT writable" -ForegroundColor Red
    $permOk = $false
}

# 3. IIS
Write-Host "`n3. Checking IIS..." -ForegroundColor Yellow
try {
    $poolState = Get-WebAppPoolState -Name "FileConversionApiPool" -ErrorAction Stop
    Write-Host "  ✓ Application pool: $($poolState.Value)" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Application pool not found or not running" -ForegroundColor Red
}

# 4. Health Check
Write-Host "`n4. Testing health endpoint..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$baseUrl/health" -ErrorAction Stop
    $healthOk = $health.status -eq "Healthy"
    $icon = if ($healthOk) { "✓" } else { "✗" }
    $color = if ($healthOk) { "Green" } else { "Red" }
    Write-Host "  $icon Status: $($health.status)" -ForegroundColor $color
} catch {
    Write-Host "  ✗ Health check failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Summary
Write-Host "`n========================================" -ForegroundColor Cyan
if ($allFilesOk -and $permOk) {
    Write-Host "Deployment verification PASSED" -ForegroundColor Green
    Write-Host "Ready for production use" -ForegroundColor Green
} else {
    Write-Host "Deployment verification FAILED" -ForegroundColor Red
    Write-Host "Fix issues above before using" -ForegroundColor Red
}
Write-Host "========================================`n" -ForegroundColor Cyan
```

### API Documentation

Access Swagger UI for interactive API testing:

```
http://fileconversion.company.local/api-docs
```

### View Logs

```powershell
Get-Content "D:\inetpub\wwwroot\Service\FileConversionApi\App_Data\logs\file-conversion-api-*.log" -Tail 50
```

## Troubleshooting

### 500.30 Error (Service Won't Start)

```powershell
# Check .NET runtime is installed
dotnet --list-runtimes | findstr "Microsoft.AspNetCore.App 8"

# Check Event Viewer
# Windows Logs → Application

# Test manually
cd C:\inetpub\FileConversionApi
dotnet FileConversionApi.dll

# Restart pool
Restart-WebAppPool -Name FileConversionApiPool
```

### LibreOffice Not Found

```powershell
# Verify bundle exists
Test-Path C:\inetpub\FileConversionApi\LibreOffice\program\soffice.exe

# If false, redeploy:
# 1. Run .\bundle-libreoffice.ps1 on build machine
# 2. Run .\deploy.ps1 to create package
# 3. Copy deploy\release to server
```

### LibreOffice Conversion Fails (Exit Code -1073741515)

**Symptoms:**
- API returns "Conversion failed"
- Log shows: `LibreOffice process completed. ExitCode: -1073741515`
- Error: "Expected output file was not created"

**Cause:** Missing Visual C++ runtime DLLs. This should not occur if using a bundle created with the latest `bundle-libreoffice.ps1` script (which includes the DLLs automatically).

**Solution:**

```powershell
# 1. Verify Visual C++ DLLs are present in LibreOffice bundle
Test-Path "C:\inetpub\FileConversionApi\LibreOffice\program\msvcp140.dll"
Test-Path "C:\inetpub\FileConversionApi\LibreOffice\program\vcruntime140.dll"
Test-Path "C:\inetpub\FileConversionApi\LibreOffice\program\vcruntime140_1.dll"

# 2. If DLLs are missing, recreate the bundle on the build machine
# On build machine:
.\bundle-libreoffice.ps1 -Force
.\deploy.ps1

# 3. If you need a quick fix, copy DLLs manually from System32
Copy-Item "C:\Windows\System32\msvcp140.dll" -Destination "C:\inetpub\FileConversionApi\LibreOffice\program\"
Copy-Item "C:\Windows\System32\vcruntime140.dll" -Destination "C:\inetpub\FileConversionApi\LibreOffice\program\"
Copy-Item "C:\Windows\System32\vcruntime140_1.dll" -Destination "C:\inetpub\FileConversionApi\LibreOffice\program\"

# 4. Restart IIS
iisreset

# 5. Test conversion
curl -X POST http://localhost/FileConversionApi/api/convert `
  -F "file=@test.docx" `
  -F "targetFormat=pdf" `
  -o output.pdf
```

**Note:** Bundles created with the updated `bundle-libreoffice.ps1` script include these DLLs automatically, eliminating the need for Visual C++ Redistributable installation on servers.

### LibreOffice Profile Initialization Issues (Exit Code 1, Hung Processes)

**Symptoms:**
- Conversion requests hang indefinitely (timeout after 180+ seconds)
- Logs show: `LibreOffice process completed. ExitCode: 1`
- `tasklist | findstr soffice` shows hung soffice.exe processes
- Profile directory only contains 369 bytes (should be several MB)
- Only `user/extensions/buildid` created, missing `config/`, `cache/` directories

**Cause:** LibreOffice cannot complete profile initialization under IIS identity due to:
- Inability to read LibreOffice/share configuration files
- Permission conflicts when profile created by different user (e.g., manual testing as admin)
- Incomplete initialization process

**Solution:** Use pre-created profile template (recommended approach)

The profile template is created once on the build machine (where all permissions work) and bundled with deployment. On each conversion, the template is copied to a unique directory instead of letting LibreOffice initialize from scratch.

**Verify profile template exists in deployment:**
```powershell
# Should exist and be 5-10 MB
Test-Path "C:\inetpub\FileConversionApi\libreoffice-profile-template"
dir "C:\inetpub\FileConversionApi\libreoffice-profile-template" -Recurse

# Should contain these directories:
# - config/
# - cache/
# - user/extensions/
# - user/uno_packages/
```

**If profile template is missing:**
```powershell
# On build machine, recreate it:
.\create-libreoffice-profile-template.ps1

# Verify it was created:
dir FileConversionApi\libreoffice-profile-template /S

# Redeploy application
.\deploy.ps1

# Copy to server
```

**Check logs for profile template usage:**
```powershell
# Should see this message:
Get-Content "C:\inetpub\FileConversionApi\App_Data\logs\*.log" -Tail 100 | findstr "profile"

# Expected: "Copied LibreOffice profile template to: ..."
# Warning: "Profile template not found, LibreOffice will create profile" (template missing!)
```

**If still experiencing issues after using profile template:**
```powershell
# 1. Clean up any existing profile directories created during testing
Remove-Item "C:\inetpub\FileConversionApi\App_Data\libreoffice-profiles" -Recurse -Force

# 2. Kill any hung LibreOffice processes
taskkill /F /IM soffice.exe /IM soffice.bin

# 3. Ensure correct permissions
icacls "C:\inetpub\FileConversionApi\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T

# 4. Restart IIS
iisreset

# 5. Test conversion
curl -X POST http://localhost/api/convert -F "file=@test.txt" -F "targetFormat=pdf" -o output.pdf
```

### Permission Denied Errors

```powershell
# Grant IIS_IUSRS full access to App_Data
icacls "C:\inetpub\FileConversionApi\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T

# Restart IIS
iisreset
```

### SSL Certificate Errors

Export from server and import to "Trusted Root Certification Authorities" on client workstations, or deploy via Group Policy.

### Conversion Timeouts

Edit `appsettings.json`:

```json
{
  "LibreOffice": {
    "TimeoutSeconds": 600
  }
}
```

Then restart application pool:

```powershell
Restart-WebAppPool -Name FileConversionApiPool
```

### High Memory Usage

Reduce concurrent conversions:

```json
{
  "Concurrency": {
    "MaxConcurrentConversions": 1,
    "MaxQueueSize": 5
  }
}
```

### Diagnostic Commands

```powershell
# Check IIS status
Get-WebAppPoolState -Name FileConversionApiPool
Get-Website -Name FileConversionApi

# View recent logs
Get-Content "C:\inetpub\FileConversionApi\App_Data\logs\*.log" -Tail 50

# Check Event Log
Get-EventLog -LogName Application -Source "IIS AspNetCore Module" -Newest 20

# Check LibreOffice processes
Get-Process | Where-Object {$_.ProcessName -like "*soffice*"}

# Test endpoint
curl http://localhost/health
curl http://localhost/api
```

### Common Issues

**Issue: 404 Not Found**

- Verify IIS site is started
- Check that bindings match your URL
- Verify physical path points to correct directory

**Issue: Swagger documentation returns 404 (/api-docs or /swagger/v1/swagger.json)**

The XML documentation file must be present in the deployment directory for Swagger to work.

```powershell
# Verify XML file exists
Test-Path "C:\inetpub\FileConversionApi\FileConversionApi.xml"

# If missing, redeploy using deploy.ps1 script
# The file should be automatically included in the deployment package

# Check IIS can access the file
icacls "C:\inetpub\FileConversionApi\FileConversionApi.xml"

# Restart application pool
Restart-WebAppPool -Name FileConversionApiPool

# Verify Swagger endpoint is accessible
curl http://localhost/swagger/v1/swagger.json
curl http://localhost/api-docs
```

If the XML file is present but Swagger still doesn't work:
- Check Event Viewer for ASP.NET Core errors
- Verify .NET 8 runtime is properly installed
- Check that the application pool is running
- Review application logs in App_Data\logs

**Issue: Rate limiting blocks all requests**

- Review IpRateLimiting configuration rules
- Verify rate limit thresholds are appropriate
- Temporarily disable rate limiting for testing

**Issue: Files not cleaning up**

- Verify App_Data permissions (IIS_IUSRS must have full control)
- Check CleanupTempFiles setting is true
- Review TempFileRetentionHours setting

**Issue: Slow conversions**

- Check server CPU and memory usage
- Reduce MaxConcurrentConversions
- Increase TimeoutSeconds for large documents
- Consider vertical scaling (more CPU/RAM)

## Environment Variables

Configuration can be overridden with environment variables:

```powershell
# Set environment variable for application pool
Set-ItemProperty "IIS:\AppPools\FileConversionApiPool" -Name EnvironmentVariables -Value @{
    "FileHandling__MaxFileSize" = "104857600"
    "Concurrency__MaxConcurrentConversions" = "4"
}

# Restart pool
Restart-WebAppPool -Name FileConversionApiPool
```

## High Availability Deployment

For load-balanced deployments:

1. Deploy to multiple servers using same steps
2. Configure load balancer with health check: `/health`
3. Use network share for temporary directories (optional)
4. No session affinity needed - service is stateless

**Load Balancer Configuration:**

- Health check endpoint: `http://server/health`
- Health check interval: 30 seconds
- Unhealthy threshold: 2 consecutive failures
- Timeout: 5 seconds

## Maintenance

**Update LibreOffice Bundle:**

```powershell
# On build machine
.\bundle-libreoffice.ps1
.\deploy.ps1

# Copy deploy\release to server
# Restart application pool
Restart-WebAppPool -Name FileConversionApiPool
```

**Update Application:**

```powershell
# Build new version
dotnet publish -c Release

# Stop application pool
Stop-WebAppPool -Name FileConversionApiPool

# Copy new files to server
Copy-Item .\bin\Release\net8.0\publish\* -Destination "C:\inetpub\FileConversionApi" -Force

# Start application pool
Start-WebAppPool -Name FileConversionApiPool
```

**Log Rotation:**

Logs automatically rotate daily with 30-day retention (configurable in Serilog settings).

**Monitor Disk Space:**

```powershell
# Check App_Data size
Get-ChildItem "C:\inetpub\FileConversionApi\App_Data" -Recurse | Measure-Object -Property Length -Sum

# Manual cleanup if needed
Remove-Item "C:\inetpub\FileConversionApi\App_Data\temp\*" -Recurse -Force
```
