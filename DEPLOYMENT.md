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

```powershell
# Grant IIS_IUSRS read/execute on application
icacls "C:\inetpub\FileConversionApi" /grant "IIS_IUSRS:(OI)(CI)RX" /T

# Grant IIS_IUSRS full control on App_Data for temp files and logs
icacls "C:\inetpub\FileConversionApi\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T
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

**Health Check:**

```powershell
curl http://localhost/health
```

Expected response:

```json
{
  "status": "Healthy",
  "timestamp": "2025-10-25T10:30:00Z",
  "services": {
    "LibreOffice": {
      "status": "Healthy",
      "message": "Available"
    }
  }
}
```

**Detailed Health Check:**

```powershell
curl http://localhost/health/detailed
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
