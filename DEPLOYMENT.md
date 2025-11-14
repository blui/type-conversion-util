# Deployment Guide

How to deploy the File Conversion API to Windows Server with IIS.

**Note:** Examples use `C:\inetpub\FileConversionApi` as the deployment folder. Adjust to match your actual location.

## Prerequisites

### What You Need on the Server

| Component | Details |
|-----------|---------|
| OS | Windows Server 2016+ or Windows 11 |
| .NET Runtime | .NET 8.0 or later ([Download](https://dotnet.microsoft.com/download/dotnet/8.0)) |
| IIS | IIS 8.5+ with ASP.NET Core Module |
| RAM | 4GB minimum, 8GB recommended |
| Disk Space | 2GB for the application |
| Network | No internet needed (air-gap friendly) |

### Installing Components

**.NET 8 Runtime:**

```powershell
# Download ASP.NET Core Hosting Bundle from: https://dotnet.microsoft.com/download/dotnet/8.0
# Run the installer, then verify:
dotnet --version  # Should show 8.0.x
```

**Important:** The LibreOffice bundle and Visual C++ runtime are **already included** in the deployment package. You don't need to install anything extra on the server!

**IIS Setup:**

```powershell
# Enable IIS
Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole
Enable-WindowsOptionalFeature -Online -FeatureName IIS-ASPNET45

# Install ASP.NET Core Module (from the hosting bundle installer)
# Then restart IIS
iisreset
```

## Building the Deployment Package

Do this on your **development machine** (where LibreOffice is installed):

**Step 1: Create LibreOffice Bundle**

```powershell
# Packages LibreOffice with all dependencies (~500MB)
.\bundle-libreoffice.ps1

# Verify it worked
Test-Path FileConversionApi\LibreOffice\program\soffice.exe  # Should be True
```

**Step 2: Create Profile Template**

```powershell
# Creates a pre-initialized LibreOffice profile template
.\create-libreoffice-profile-template.ps1

# Verify it worked
Test-Path FileConversionApi\libreoffice-profile-template  # Should be True
```

**Step 3: Build Deployment Package**

```powershell
cd FileConversionApi
.\deploy.ps1

# Creates deployment package at: deploy\release\
# Total size: ~550MB (app + LibreOffice + dependencies)
```

**What gets packaged:**
- .NET application (~50MB)
- LibreOffice bundle with VC++ runtime DLLs (~500MB)
- Pre-initialized profile template (~2KB)
- Configuration files

## Deploying to the Server

### Copy Files

**Option A: Network Copy**

```powershell
Copy-Item .\deploy\release\* -Destination "\\SERVER\C$\inetpub\FileConversionApi" -Recurse -Force
```

**Option B: Robocopy (Better for large files)**

```powershell
robocopy .\deploy\release \\SERVER\C$\inetpub\FileConversionApi /E /MT:8
```

**Option C: Air-Gapped Transfer**

1. Compress `deploy\release` to ZIP
2. Transfer via USB or approved method
3. Extract to `C:\inetpub\FileConversionApi` on server

### Set Up IIS

**1. Create Application Pool**

In IIS Manager (`inetmgr`):

1. Right-click "Application Pools" → "Add Application Pool"
2. **Name:** `FileConversionApiPool`
3. **.NET CLR version:** **"No Managed Code"** (critical for .NET 8!)
4. **Managed pipeline mode:** Integrated
5. Click OK
6. Right-click the pool → "Advanced Settings":
   - **Identity:** `ApplicationPoolIdentity`
   - **Idle Time-out:** `0` (never sleep)
   - **Recycle Interval:** `1740` minutes (29 hours) or `0` to disable

**2. Create Website**

1. Right-click "Sites" → "Add Website"
2. **Site name:** `FileConversionApi`
3. **Application pool:** `FileConversionApiPool`
4. **Physical path:** `C:\inetpub\FileConversionApi`
5. **Binding:**
   - **Type:** http
   - **Port:** `80` (or your preferred port)
   - **Host name:** (blank for intranet, or `fileconversion.company.local`)

**3. Set Permissions (CRITICAL!)**

**The application won't work without these permissions!**

```powershell
$deployPath = "C:\inetpub\FileConversionApi"

# App_Data - Full Control (for temp files, logs, profiles)
icacls "$deployPath\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T

# LibreOffice - Read and Execute (to run soffice.exe)
icacls "$deployPath\LibreOffice" /grant "IIS_IUSRS:(OI)(CI)RX" /T

# Profile Template - Read (to copy template per conversion)
icacls "$deployPath\libreoffice-profile-template" /grant "IIS_IUSRS:(OI)(CI)R" /T

# Verify permissions were set
icacls "$deployPath\App_Data" | Select-String "IIS_IUSRS"
```

**Permission flags explained:**
- `(OI)` - Files inherit the permission
- `(CI)` - Subdirectories inherit the permission
- `F` - Full control
- `RX` - Read and Execute
- `R` - Read only
- `/T` - Apply to all existing files/subdirectories

**4. Start the Service**

```powershell
# Start the application pool and website
iisreset

# Wait a few seconds for it to initialize
Start-Sleep -Seconds 5

# Test it
curl http://localhost/health
```

## Configuration

Edit `C:\inetpub\FileConversionApi\appsettings.json` to customize settings. Restart the application pool after changes:

```powershell
Restart-WebAppPool -Name FileConversionApiPool
```

### Security Settings

**API Key Authentication:**

```json
{
  "Security": {
    "RequireApiKey": true,
    "ApiKeys": [
      "apikey_live_your_secure_key_here"
    ]
  }
}
```

Generate a secure key:
```powershell
$bytes = New-Object byte[] 32
[Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
"apikey_live_" + [Convert]::ToBase64String($bytes) -replace '\+','-' -replace '/','_' -replace '=',''
```

**CORS (for browser access):**

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

**Rate Limiting:**

```json
{
  "IpRateLimiting": {
    "GeneralRules": [
      { "Endpoint": "*", "Period": "1m", "Limit": 30 },
      { "Endpoint": "POST:/api/convert", "Period": "1m", "Limit": 10 }
    ]
  }
}
```

### File Handling

**Using C:\Temp for Temporary Files (Recommended for Production):**

The application auto-detects absolute vs. relative paths - no toggle needed!

**Setup:**

```powershell
# 1. Create directories
New-Item -ItemType Directory -Path "C:\Temp\FileConversionApi\uploads" -Force
New-Item -ItemType Directory -Path "C:\Temp\FileConversionApi\converted" -Force

# 2. Grant permissions
icacls "C:\Temp\FileConversionApi" /grant "IIS APPPOOL\FileConversionApiPool:(OI)(CI)M" /T

# 3. Update appsettings.json
```

```json
{
  "FileHandling": {
    "TempDirectory": "C:\\Temp\\FileConversionApi\\uploads",
    "OutputDirectory": "C:\\Temp\\FileConversionApi\\converted"
  }
}
```

```powershell
# 4. Restart the app
Restart-WebAppPool -Name FileConversionApiPool
```

**Note:** LibreOffice profiles will still use `App_Data\libreoffice-profiles` (hardcoded for IIS permissions).

**File Size Limits:**

```json
{
  "FileHandling": {
    "MaxFileSize": 52428800,  // 50MB in bytes
    "AllowedExtensions": [
      "pdf", "doc", "docx", "xlsx", "pptx",
      "txt", "html", "htm", "xml", "csv"
    ]
  }
}
```

### Performance

**Concurrency Settings:**

```json
{
  "Concurrency": {
    "MaxConcurrentConversions": 2,
    "MaxQueueSize": 10
  }
}
```

Recommended by server size:

| CPU Cores | MaxConcurrentConversions | MaxQueueSize |
|-----------|--------------------------|--------------|
| 2-4 cores | 2 | 6 |
| 4-8 cores | 4 | 12 |
| 8+ cores | 6-8 | 20 |

**Timeouts:**

```json
{
  "LibreOffice": {
    "TimeoutSeconds": 300  // 5 minutes
  }
}
```

### Logging

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "App_Data\\logs\\file-conversion-api-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
    ]
  }
}
```

## Verification

### Quick Health Check

```powershell
$deployPath = "C:\inetpub\FileConversionApi"

# 1. Check files exist
Test-Path "$deployPath\FileConversionApi.dll"  # Should be True
Test-Path "$deployPath\LibreOffice\program\soffice.exe"  # Should be True
Test-Path "$deployPath\libreoffice-profile-template"  # Should be True

# 2. Test write permissions
$testFile = "$deployPath\App_Data\test-$(Get-Random).txt"
"Test" | Out-File $testFile
Test-Path $testFile  # Should be True
Remove-Item $testFile

# 3. Check app pool is running
Get-WebAppPoolState -Name "FileConversionApiPool"  # Should show "Started"

# 4. Test health endpoint
curl http://localhost/health  # Should return {"status":"Healthy"}

# 5. Try a conversion
"Hello World" | Out-File C:\temp\test.txt
curl -X POST http://localhost/api/convert -F "file=@C:\temp\test.txt" -F "targetFormat=pdf" -o C:\temp\output.pdf
Test-Path C:\temp\output.pdf  # Should be True
```

## Troubleshooting

### Service Won't Start (500.30 Error)

```powershell
# Check .NET runtime is installed
dotnet --list-runtimes | findstr "Microsoft.AspNetCore.App 8"

# Check Windows Event Log
Get-EventLog -LogName Application -Source "IIS AspNetCore Module" -Newest 20

# Try running manually
cd C:\inetpub\FileConversionApi
dotnet FileConversionApi.dll

# Restart IIS
iisreset
```

### Conversions Fail or Hang

**Check profile template:**

```powershell
# Verify template exists
Test-Path "C:\inetpub\FileConversionApi\libreoffice-profile-template"

# Check logs for template usage
Get-Content "C:\inetpub\FileConversionApi\App_Data\logs\*.log" -Tail 100 | Select-String "profile"

# Should see: "Copied LibreOffice profile template to..."
# If you see: "Profile template not found" - template is missing!
```

**Kill hung processes:**

```powershell
taskkill /F /IM soffice.exe
```

### DLL Not Found (Exit Code -1073741515)

**Verify Visual C++ runtime DLLs are bundled:**

```powershell
Test-Path "C:\inetpub\FileConversionApi\LibreOffice\program\msvcp140.dll"
Test-Path "C:\inetpub\FileConversionApi\LibreOffice\program\vcruntime140.dll"
Test-Path "C:\inetpub\FileConversionApi\LibreOffice\program\msvcp140_atomic_wait.dll"

# If any are missing, recreate the bundle on build machine
.\bundle-libreoffice.ps1 -Force
```

### Permission Errors

```powershell
# Grant full access to App_Data
icacls "C:\inetpub\FileConversionApi\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T

# Restart IIS
iisreset
```

### Swagger/API Docs Shows 404

```powershell
# Verify XML documentation file exists
Test-Path "C:\inetpub\FileConversionApi\FileConversionApi.xml"

# If missing, redeploy using deploy.ps1
# The XML file is automatically included

# Check IIS can access it
icacls "C:\inetpub\FileConversionApi\FileConversionApi.xml"

# Restart app pool
Restart-WebAppPool -Name FileConversionApiPool
```

### Conversions Are Slow

```powershell
# Reduce concurrent conversions
# Edit appsettings.json:
{
  "Concurrency": {
    "MaxConcurrentConversions": 1,
    "MaxQueueSize": 5
  }
}

# Restart app pool
Restart-WebAppPool -Name FileConversionApiPool
```

### Check Logs

```powershell
# View recent logs
Get-Content "C:\inetpub\FileConversionApi\App_Data\logs\*.log" -Tail 50

# Search for errors
Get-Content "C:\inetpub\FileConversionApi\App_Data\logs\*.log" -Tail 500 | Select-String "Error|Exception|Failed"

# Check for LibreOffice issues
Get-Content "C:\inetpub\FileConversionApi\App_Data\logs\*.log" -Tail 500 | Select-String "LibreOffice|soffice"
```

## Advanced Scenarios

### Sub-Application Deployment

Deploy under an existing IIS site (e.g., `https://server/FileConversionApi`):

**1. Create Application (not website):**

1. IIS Manager → Expand existing site (e.g., "Default Web Site")
2. Right-click site → "Add Application"
3. **Alias:** `FileConversionApi`
4. **Application Pool:** `FileConversionApiPool`
5. **Physical Path:** `C:\inetpub\FileConversionApi`

**2. Optional - Configure PathBase:**

Add to `appsettings.json`:

```json
{
  "PathBase": "/FileConversionApi"
}
```

**3. Test endpoints:**

```powershell
curl https://server/FileConversionApi/health
curl https://server/FileConversionApi/api
curl https://server/FileConversionApi/api-docs
```

### HTTPS Setup

**Option 1: Self-Signed Certificate (Intranet)**

```powershell
# Create certificate
$cert = New-SelfSignedCertificate `
  -DnsName "fileconversion.company.local", "SERVER-NAME" `
  -CertStoreLocation "Cert:\LocalMachine\My" `
  -KeyExportPolicy Exportable `
  -NotAfter (Get-Date).AddYears(5)

Write-Host "Certificate Thumbprint: $($cert.Thumbprint)" -ForegroundColor Green

# Bind to IIS site
# IIS Manager → Site → Bindings → Add
# Type: https, Port: 443, Select certificate by thumbprint
```

**Option 2: Corporate CA Certificate (Recommended)**

Get certificate from your organization's certificate authority and bind in IIS.

### Load Balanced Deployment

For high availability:

1. Deploy to multiple servers (same steps)
2. Configure load balancer:
   - **Health check:** `http://server/health`
   - **Interval:** 30 seconds
   - **Unhealthy threshold:** 2 consecutive failures
   - **Timeout:** 5 seconds
   - **No session affinity needed** (stateless service)

### Environment Variables

Override configuration without editing `appsettings.json`:

```powershell
# Set environment variables for app pool
Set-ItemProperty "IIS:\AppPools\FileConversionApiPool" -Name EnvironmentVariables -Value @{
    "FileHandling__MaxFileSize" = "104857600"  # 100MB
    "Concurrency__MaxConcurrentConversions" = "4"
}

# Restart pool
Restart-WebAppPool -Name FileConversionApiPool
```

## Maintenance

### Update Application

```powershell
# On build machine: rebuild deployment package
.\deploy.ps1

# On server: stop app pool
Stop-WebAppPool -Name FileConversionApiPool

# Copy new files
Copy-Item .\deploy\release\* -Destination "C:\inetpub\FileConversionApi" -Recurse -Force

# Start app pool
Start-WebAppPool -Name FileConversionApiPool

# Verify
curl http://localhost/health
```

### Update LibreOffice Bundle

```powershell
# On build machine:
.\bundle-libreoffice.ps1 -Force
.\create-libreoffice-profile-template.ps1
.\deploy.ps1

# Deploy to server (same as application update above)
```

### Monitor Disk Space

```powershell
# Check App_Data size
$size = (Get-ChildItem "C:\inetpub\FileConversionApi\App_Data" -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host "App_Data size: $([math]::Round($size, 2)) MB"

# Manual cleanup if needed (service does this automatically)
Remove-Item "C:\inetpub\FileConversionApi\App_Data\temp\*" -Recurse -Force
```

### View Logs

```powershell
# Latest log file
$latestLog = Get-ChildItem "C:\inetpub\FileConversionApi\App_Data\logs\*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

# View last 50 lines
Get-Content $latestLog.FullName -Tail 50

# Follow in real-time
Get-Content $latestLog.FullName -Wait -Tail 20
```

## Complete Deployment Checklist

Use this to verify your deployment:

```
☐ .NET 8 Runtime installed on server
☐ IIS installed with ASP.NET Core Module
☐ LibreOffice bundle created (bundle-libreoffice.ps1)
☐ Profile template created (create-libreoffice-profile-template.ps1)
☐ Deployment package built (deploy.ps1)
☐ Files copied to server
☐ Application pool created (No Managed Code)
☐ Website/application created in IIS
☐ Permissions set on App_Data (Full Control)
☐ Permissions set on LibreOffice (Read & Execute)
☐ Permissions set on libreoffice-profile-template (Read)
☐ IIS restarted
☐ Health endpoint returns 200 OK
☐ Test conversion succeeds
☐ Logs are being written
☐ API documentation accessible
```

---

Need help? Check the logs first - they usually tell you what's wrong. The most common issues are missing permissions and missing dependencies.
