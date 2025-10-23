# Deployment Guide

Simple IIS deployment for Windows Server intranet environments.

**Office Document Conversions - 32 Supported Format Combinations**

## Table of Contents

- [Prerequisites](#prerequisites)
- [Deployment Steps](#deployment-steps)
- [Manual IIS Configuration](#manual-iis-configuration)
- [Self-Signed Certificate Setup](#self-signed-certificate-setup)
- [Post-Deployment Configuration](#post-deployment-configuration)
- [Verification](#verification)
- [Troubleshooting](#troubleshooting)

## Prerequisites

### System Requirements

| Component    | Requirement                               |
| ------------ | ----------------------------------------- |
| OS           | Windows Server 2016+ or Windows 11        |
| .NET Runtime | .NET 8.0 or later                         |
| IIS          | IIS 8.5 or later with ASP.NET Core Module |
| RAM          | 4GB minimum, 8GB recommended              |
| Disk Space   | 2GB (500MB application + LibreOffice)     |
| Network      | No internet required for operation        |

### Required Components

**.NET 8 Runtime:**

```powershell
# Download from: https://dotnet.microsoft.com/download/dotnet/8.0

# Verify installation
dotnet --version
# Expected: 8.0.x
```

**IIS with ASP.NET Core Module:**

```powershell
# Install IIS (if not already installed)
Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole
Enable-WindowsOptionalFeature -Online -FeatureName IIS-ASPNET45

# Download ASP.NET Core Hosting Bundle
# https://dotnet.microsoft.com/download/dotnet/8.0
# Run the installer and restart IIS
iisreset
```

**LibreOffice Bundle:**

```powershell
# Create bundle (required for Office document conversions - 517 MB optimized)
.\bundle-libreoffice.ps1

# Verify bundle
Test-Path FileConversionApi\LibreOffice\program\soffice.exe
```

**What works without LibreOffice:**

- Image conversions
- XLSX/CSV conversions
- PDF text extraction
- Text to PDF creation

**What requires LibreOffice:**

- DOC/DOCX conversions
- Office to PDF conversions
- PDF to Office conversions
- OpenDocument formats

## Deployment Steps

### Step 1: Build Deployment Package

From your development machine where you've cloned the repository:

```powershell
# Navigate to the API directory
cd FileConversionApi

# Create deployment package
.\deploy.ps1
```

This creates a `deploy\release` folder containing:
- Compiled .NET application (Release configuration)
- LibreOffice bundle (500-550 MB optimized)
- Configuration files (appsettings.json, web.config)
- Required directory structure (App_Data, logs, etc.)
- All runtime dependencies

**Package location:** `FileConversionApi\deploy\release`

**Package contents:**
- Approximately 550-600 MB total
- 500+ files including LibreOffice bundle
- Ready for direct copy to IIS server

### Step 2: Security Scan (If Required)

If your organization requires security scanning before deployment:

```powershell
# The deploy\release folder is now ready for scanning
# Scan with your organization's approved tools
# Example: Windows Defender
Scan-MpScan -ScanPath ".\deploy\release" -ScanType FullScan
```

### Step 3: Copy to Windows Server

Transfer the deployment package to your Windows Server. You can use:

**Option A: File copy (if you have network access):**

```powershell
# From your development machine
Copy-Item .\deploy\release\* `
  -Destination "\\SERVER\C$\inetpub\FileConversionApi" `
  -Recurse -Force
```

**Option B: Robocopy (more reliable for large transfers):**

```powershell
robocopy .\deploy\release \\SERVER\C$\inetpub\FileConversionApi /E /MT:8
```

**Option C: Manual copy (air-gapped environments):**
1. Compress the `deploy\release` folder to ZIP
2. Transfer via USB drive or approved method
3. Extract on the Windows Server to `C:\inetpub\FileConversionApi`

---

## Manual IIS Configuration

Once files are copied to the Windows Server, configure IIS:

### 1. Create Application Pool

Open IIS Manager (`inetmgr`) on the Windows Server:

1. Expand server node → Right-click "Application Pools" → "Add Application Pool"
2. **Name:** `FileConversionApiPool`
3. **.NET CLR version:** Select **"No Managed Code"**
4. **Managed pipeline mode:** Integrated
5. Click **OK**

6. Right-click the new pool `FileConversionApiPool` → "Advanced Settings":
   - **Process Model → Identity:** `ApplicationPoolIdentity` (default)
   - **Process Model → Idle Time-out (minutes):** `0` (never sleep)
   - **Recycling → Regular Time Interval (minutes):** `1740` (29 hours, or disable with `0`)

7. Click **OK**

### 2. Create Website

1. In IIS Manager, right-click "Sites" → "Add Website"
2. **Site name:** `FileConversionApi`
3. **Application pool:** `FileConversionApiPool`
4. **Physical path:** `C:\inetpub\FileConversionApi`
5. **Binding:**
   - **Type:** http
   - **IP address:** All Unassigned
   - **Port:** `80` (or your preferred port)
   - **Host name:** (leave blank for intranet, or use `fileconversion.company.local`)
6. Click **OK**

### 3. Configure HTTPS (Required for Production)

**Important:** For intranet deployments using self-signed certificates, see the [Self-Signed Certificate Setup](#self-signed-certificate-setup) section below.

1. In IIS Manager, select your site `FileConversionApi`
2. Click "Bindings..." in the Actions pane
3. Click **Add**
4. **Type:** https
5. **Port:** 443
6. **SSL certificate:** Select your certificate (see next section)
7. Click **OK**

### 4. Set File Permissions

Run these commands on the Windows Server in PowerShell (as Administrator):

```powershell
# Grant IIS_IUSRS read/execute on application directory
icacls "C:\inetpub\FileConversionApi" /grant "IIS_IUSRS:(OI)(CI)RX" /T

# Grant IIS_IUSRS full control on App_Data (for temp files and logs)
icacls "C:\inetpub\FileConversionApi\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T
```

### 5. Start the Application

1. In IIS Manager, right-click `FileConversionApiPool` → **Start**
2. Right-click `FileConversionApi` site → **Start**
3. Wait 5-10 seconds for application initialization

---

## Self-Signed Certificate Setup

For isolated intranet environments, you'll typically use a self-signed certificate for HTTPS.

### Create Self-Signed Certificate on Windows Server

Run on the Windows Server (PowerShell as Administrator):

```powershell
# Create self-signed certificate
$cert = New-SelfSignedCertificate `
  -DnsName "fileconversion.company.local", "SERVER-NAME" `
  -CertStoreLocation "Cert:\LocalMachine\My" `
  -KeyExportPolicy Exportable `
  -NotAfter (Get-Date).AddYears(5) `
  -KeySpec KeyExchange `
  -KeyUsage DigitalSignature, KeyEncipherment

# Display certificate thumbprint (you'll need this for IIS binding)
Write-Host "Certificate Thumbprint: $($cert.Thumbprint)" -ForegroundColor Green
```

**Note:** Replace `fileconversion.company.local` and `SERVER-NAME` with your actual DNS name and server hostname.

### Bind Certificate to IIS

1. Open IIS Manager
2. Select your site → Bindings → Add (or Edit existing HTTPS binding)
3. **Type:** https
4. **Port:** 443
5. **SSL certificate:** Select the certificate you just created
6. Click **OK**

### Configure Client Workstations to Trust the Certificate

For clients calling the API from within the intranet to avoid SSL errors, you have three options:

#### Option 1: Install Certificate on All Client Workstations (Recommended)

Export the certificate from the server and install it on all client machines:

**On the Windows Server:**

```powershell
# Export certificate (will prompt for password)
$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object {$_.Thumbprint -eq "YOUR_THUMBPRINT"}
$password = ConvertTo-SecureString -String "YourPasswordHere" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath "C:\Temp\FileConversionCert.pfx" -Password $password
```

**On each client workstation (PowerShell as Administrator):**

```powershell
# Import certificate to Trusted Root Certification Authorities
$password = ConvertTo-SecureString -String "YourPasswordHere" -Force -AsPlainText
Import-PfxCertificate -FilePath "\\SERVER\Share\FileConversionCert.pfx" `
  -CertStoreLocation "Cert:\LocalMachine\Root" `
  -Password $password
```

**Or via Group Policy (for domain environments):**
1. Copy .pfx file to domain controller
2. Open Group Policy Management
3. Create or edit GPO → Computer Configuration → Windows Settings → Security Settings → Public Key Policies → Trusted Root Certification Authorities
4. Right-click → Import → Select your .pfx file
5. Link GPO to appropriate OU
6. Run `gpupdate /force` on client machines

#### Option 2: Use Corporate Internal CA

If your organization has an internal Certificate Authority:

```powershell
# Request certificate from internal CA
certreq -new request.inf request.req
certreq -submit -config "CA-SERVER\CA-NAME" request.req certificate.cer
certreq -accept certificate.cer
```

This eliminates the need to distribute certificates to clients, as they already trust your internal CA.

#### Option 3: Disable SSL Validation in Client Code (Development Only)

**WARNING: Only use in isolated development/testing environments!**

For .NET clients calling the API:

```csharp
// Add this ONLY in development/testing code
ServicePointManager.ServerCertificateValidationCallback +=
    (sender, cert, chain, sslPolicyErrors) => true;

// Your API calls
var client = new HttpClient();
var response = await client.GetAsync("https://fileconversion.company.local/health");
```

For PowerShell clients:

```powershell
# Skip certificate validation (development only)
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

Invoke-RestMethod -Uri "https://fileconversion.company.local/health"
```

**Production Recommendation:** Always use Option 1 (distribute certificate) or Option 2 (internal CA) for production environments.

---

## Post-Deployment Configuration

After deployment, you can adjust settings by editing `appsettings.json` directly on the server without recompiling.

**Location:** `C:\inetpub\FileConversionApi\appsettings.json`

### Common Configuration Changes

#### 1. IP Whitelisting (Security)

Edit to match your intranet network ranges:

```json
"Security": {
  "EnableIPFiltering": true,
  "IPWhitelist": [
    "192.168.1.0/24",     // Your office network
    "10.0.0.0/8",         // Corporate VPN range
    "172.16.0.0/12"       // Data center range
  ],
  "AllowedOrigins": [
    "http://intranet.company.local",
    "http://app.company.local"
  ]
}
```

**To disable IP filtering (not recommended for production):**
```json
"EnableIPFiltering": false
```

#### 2. File Size Limits

Adjust based on your document sizes:

```json
"FileHandling": {
  "MaxFileSize": 52428800,          // 50 MB in bytes
  "TempFileRetentionHours": 24
}
```

Common values:
- `52428800` = 50 MB (default)
- `104857600` = 100 MB
- `209715200` = 200 MB

#### 3. Conversion Performance

Tune based on server CPU cores:

```json
"Concurrency": {
  "MaxConcurrentConversions": 2,    // Number of simultaneous conversions
  "MaxQueueSize": 10                // Number of queued requests
}
```

**Recommended by server capacity:**

| Server CPUs | MaxConcurrentConversions | MaxQueueSize |
|-------------|-------------------------|--------------|
| 2-4 cores   | 2                       | 6            |
| 4-8 cores   | 4                       | 12           |
| 8+ cores    | 6-8                     | 20           |

#### 4. Timeouts

Increase for large documents:

```json
"LibreOffice": {
  "TimeoutSeconds": 300             // 5 minutes default
}
```

Common values:
- `300` = 5 minutes (default)
- `600` = 10 minutes (large documents)
- `900` = 15 minutes (very large/complex documents)

#### 5. Rate Limiting

Adjust API call limits per client:

```json
"IpRateLimiting": {
  "GeneralRules": [
    { "Endpoint": "*", "Period": "1m", "Limit": 30 },
    { "Endpoint": "POST:/api/convert", "Period": "1m", "Limit": 10 }
  ]
}
```

#### 6. Logging Levels

Change log verbosity:

```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information"        // Options: Debug, Information, Warning, Error
  }
}
```

### Applying Configuration Changes

After editing `appsettings.json`:

```powershell
# Restart the IIS application pool
Restart-WebAppPool -Name FileConversionApiPool

# OR restart IIS entirely
iisreset
```

Changes take effect immediately after restart.

## Verification

After deployment, verify the API is working correctly.

### 1. Health Check

```powershell
# Basic health check (from Windows Server)
curl http://localhost/health

# From another workstation on the intranet
curl http://fileconversion.company.local/health
```

**Expected response:**
```json
{
  "status": "Healthy",
  "timestamp": "2025-10-23T10:30:00Z",
  "services": {
    "LibreOffice": {
      "status": "Healthy",
      "message": "Available"
    }
  }
}
```

### 2. API Documentation

Open Swagger UI in a browser:
```
http://fileconversion.company.local/api-docs
```

### 3. Test Conversion

```powershell
# Test document conversion
curl -X POST http://fileconversion.company.local/api/convert `
  -F "file=@sample.docx" `
  -F "targetFormat=pdf" `
  -o output.pdf

# Verify output file was created
Test-Path output.pdf
```

### 4. View Logs

Check application logs to verify startup and operations:

```powershell
# View recent logs
Get-Content "C:\inetpub\FileConversionApi\App_Data\logs\file-conversion-api-*.log" -Tail 50
```

## Troubleshooting

### 1. Service Won't Start (500.30 Error)

**Symptoms:**
- Browser shows "500.30 - ASP.NET Core app failed to start"
- Site doesn't respond

**Solutions:**

```powershell
# Check .NET 8 Runtime is installed
dotnet --list-runtimes | findstr "Microsoft.AspNetCore.App 8"

# Check Event Viewer for errors
eventvwr.msc
# Navigate to: Windows Logs → Application
# Look for errors from "IIS AspNetCore Module"

# Test the application manually
cd C:\inetpub\FileConversionApi
dotnet FileConversionApi.dll
# If this works, it's a permissions or IIS configuration issue

# Restart application pool
Restart-WebAppPool -Name FileConversionApiPool
```

### 2. LibreOffice Not Found

**Symptoms:**
- Conversion requests return "LibreOffice not available"
- `/health` endpoint shows LibreOffice as unhealthy

**Solution:**

```powershell
# Verify LibreOffice bundle exists
Test-Path C:\inetpub\FileConversionApi\LibreOffice\program\soffice.exe

# If false, you need to redeploy with LibreOffice bundle:
# 1. On development machine: .\bundle-libreoffice.ps1
# 2. On development machine: .\deploy.ps1
# 3. Copy deploy\release to server again
```

### 3. Permission Denied Errors

**Symptoms:**
- "Access denied" errors in logs
- Cannot create temp files or write logs

**Solution:**

```powershell
# Grant IIS_IUSRS full control on App_Data
icacls "C:\inetpub\FileConversionApi\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T

# Restart IIS
iisreset
```

### 4. SSL/Certificate Errors from Clients

**Symptoms:**
- Clients get "certificate not trusted" errors
- HTTPS connections fail from workstations

**Solution:**

See [Self-Signed Certificate Setup](#self-signed-certificate-setup) section above. You need to:
1. Export the certificate from the server
2. Import it into "Trusted Root Certification Authorities" on each client workstation
3. Or deploy via Group Policy (recommended for domain environments)

### 5. Conversion Timeouts

**Symptoms:**
- Large documents fail with timeout errors
- Conversions exceed configured time limits

**Solution:**

Edit `C:\inetpub\FileConversionApi\appsettings.json`:

```json
{
  "LibreOffice": {
    "TimeoutSeconds": 600        // Increase from 300 to 600 (10 minutes)
  }
}
```

Then restart: `Restart-WebAppPool -Name FileConversionApiPool`

### 6. High Memory Usage

**Symptoms:**
- Server running out of memory
- Application pool recycling frequently

**Solution:**

Reduce concurrent conversions in `appsettings.json`:

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

# View recent application logs
Get-Content "C:\inetpub\FileConversionApi\App_Data\logs\file-conversion-api-*.log" -Tail 50

# View Windows Event Log
Get-EventLog -LogName Application -Source "IIS AspNetCore Module" -Newest 20

# Check for running LibreOffice processes
Get-Process | Where-Object {$_.ProcessName -like "*soffice*"}

# Test manually
cd C:\inetpub\FileConversionApi
dotnet FileConversionApi.dll
```

---

## Additional Resources

**Project Documentation:**
- API Reference: [README.md](README.md)
- Conversion Matrix: [SUPPORTED_CONVERSIONS.md](SUPPORTED_CONVERSIONS.md)
- Architecture Overview: [ARCHITECTURE.md](ARCHITECTURE.md)

**Endpoints:**
- Health check: `http://your-server/health`
- API documentation: `http://your-server/api-docs`

**Scripts:**
- `bundle-libreoffice.ps1` - Create LibreOffice bundle (run once during setup)
- `deploy.ps1` - Build deployment package
- `test-conversion.ps1` - Test API functionality

---
