# Deployment Guide

Complete deployment procedures for Windows Server and Windows 11 with IIS.

**Office Document Conversions Only - 32 Conversion Paths**

## Table of Contents

- [Prerequisites](#prerequisites)
- [Windows IIS Deployment](#windows-iis-deployment)
- [Configuration](#configuration)
- [Verification](#verification)
- [Security Hardening](#security-hardening)
- [Troubleshooting](#troubleshooting)
- [Maintenance](#maintenance)
- [High Availability](#high-availability)

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

## Windows IIS Deployment

### Automated Production Deployment (Recommended)

The `deploy-iis.ps1` script provides **production-grade automated deployment** with comprehensive validation, optimization, and hardening.

#### Quick Start

```powershell
# Navigate to API directory
cd FileConversionApi

# Basic deployment (uses defaults)
.\deploy-iis.ps1

# Production deployment with all features
.\deploy-iis.ps1 -EnableBackup -ConfigureFirewall

# Custom configuration
.\deploy-iis.ps1 -IISSiteName "MyConversionAPI" `
                 -IISPhysicalPath "D:\Apps\FileConversion" `
                 -Port 8080 `
                 -EnableBackup `
                 -ConfigureFirewall
```

#### Script Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `IISSiteName` | "FileConversionApi" | Name of IIS website |
| `IISAppPoolName` | "FileConversionApiPool" | Name of IIS application pool |
| `IISPhysicalPath` | "C:\inetpub\file-conversion-api" | Installation directory |
| `Port` | 80 | HTTP port |
| `EnableHTTPS` | (switch) | Enable HTTPS on port 443 |
| `CertificateThumbprint` | "" | SSL certificate thumbprint for HTTPS |
| `EnableBackup` | (switch) | Backup existing deployment before update |
| `SkipOptimization` | (switch) | Skip production optimizations |
| `ConfigureFirewall` | (switch) | Automatically configure Windows Firewall |

#### What the Script Does (15 Steps)

1. **Administrator Check** - Verifies elevation
2. **Prerequisites Validation**:
   - IIS installation and WebAdministration module
   - .NET 8 Runtime detection
   - ASP.NET Core Hosting Bundle verification
   - Disk space validation (2GB minimum)
3. **Build Deployment Package**:
   - Runs `deploy.ps1` to create release package
   - Verifies LibreOffice bundle presence (warns if missing)
   - Validates web.config existence
4. **Backup Existing Deployment** (if `-EnableBackup`):
   - Creates timestamped backup
   - Keeps last 5 backups automatically
5. **Stop IIS Resources Gracefully**:
   - Stops website and app pool
   - Waits up to 30 seconds for graceful shutdown
6. **Deploy Application Files**:
   - Copies all files to IIS directory
   - Reports file count and deployment size
7. **Configure Application Pool** (Production Optimized):
   - No Managed Code (.NET CLR: None)
   - ApplicationPoolIdentity
   - AlwaysRunning start mode
   - No idle timeout (never stops)
   - Daily recycling at 1:00 AM
   - Memory limit: 2GB
   - Queue length: 5000
   - Rapid-fail protection: 5 crashes per 5 minutes
8. **Configure Website**:
   - Creates or updates IIS site
   - Enables preload (application warmup)
9. **Configure HTTPS** (if `-EnableHTTPS`):
   - Creates HTTPS binding on port 443
   - Binds SSL certificate from thumbprint
10. **Set File Permissions**:
    - IIS_IUSRS: Read/Execute on app directory
    - IIS_IUSRS: Full Control on App_Data
11. **Configure Windows Firewall** (if `-ConfigureFirewall`):
    - Creates inbound rule for HTTP port
    - Creates inbound rule for HTTPS port 443 (if enabled)
12. **Configure Logging**:
    - Enables stdout logging in web.config
    - Creates logs directory with proper permissions
13. **Start Services**:
    - Starts application pool
    - Starts website
    - Waits 8 seconds for initialization
14. **Verify Deployment**:
    - Tests `/health` endpoint
    - Validates application status
    - Checks LibreOffice availability
    - Tests `/api/supported-formats` endpoint
15. **Display Summary**:
    - IIS configuration details
    - Endpoint URLs
    - Monitoring locations
    - Management commands
    - Next steps

#### Production Optimizations Applied

The script applies these IIS optimizations (unless `-SkipOptimization` is used):

**App Pool Settings:**
- **AlwaysRunning**: Application never stops (instant first request)
- **No Idle Timeout**: Prevents app from sleeping during inactivity
- **Scheduled Recycling**: Daily at 1:00 AM (minimal impact time)
- **Memory Limit**: Recycles at 2GB to prevent memory leaks
- **Queue Length**: 5000 requests (handles traffic spikes)
- **Rapid-Fail Protection**: Restarts after 5 crashes in 5 minutes

**Website Settings:**
- **Preload Enabled**: Application starts immediately when app pool starts
- **Stdout Logging**: Captures startup errors and diagnostics

#### HTTPS Configuration

To enable HTTPS with a certificate:

```powershell
# List available certificates
Get-ChildItem Cert:\LocalMachine\My

# Deploy with HTTPS
.\deploy-iis.ps1 -EnableHTTPS -CertificateThumbprint "YOUR_CERT_THUMBPRINT_HERE"
```

**For Self-Signed Certificate (Development/Testing):**

```powershell
# Create self-signed certificate
$cert = New-SelfSignedCertificate -DnsName "fileconversion.company.local" `
                                   -CertStoreLocation "Cert:\LocalMachine\My" `
                                   -KeyExportPolicy Exportable `
                                   -NotAfter (Get-Date).AddYears(5)

# Deploy with the certificate
.\deploy-iis.ps1 -EnableHTTPS -CertificateThumbprint $cert.Thumbprint
```

---

### Manual Deployment (Alternative)

If you prefer manual deployment or need custom setup:

#### Build Deployment Package

```powershell
# Navigate to API directory
cd FileConversionApi

# Build deployment package
.\deploy.ps1

# The script creates a local 'deploy\release' folder with:
# - Compiled application (Release configuration)
# - LibreOffice bundle (~400-450 MB optimized)
# - Production appsettings.json
# - Required directory structure
# - All dependencies
```

**Manual Script Parameters:**

```powershell
# Custom output path
.\deploy.ps1 -OutputPath "dist"

# Skip build (only copy configs and LibreOffice)
.\deploy.ps1 -SkipBuild
```

### Deploy to IIS

**1. Copy Deployment Package:**

After running `.\deploy.ps1` in the FileConversionApi directory:

```powershell
# Copy the entire deployment folder to IIS directory
Copy-Item FileConversionApi\deployment\* `
  -Destination C:\inetpub\wwwroot\FileConversionApi `
  -Recurse -Force

# Alternative: Use robocopy for better performance
robocopy FileConversionApi\deployment C:\inetpub\wwwroot\FileConversionApi /E /MT:8
```

The deployment package includes:

- Compiled application (all DLLs and executables)
- LibreOffice bundle (517 MB optimized)
- Production `appsettings.json`
- Required directory structure
- Configuration files (web.config, etc.)

**2. Create IIS Application Pool:**

| Setting      | Value                   |
| ------------ | ----------------------- |
| Name         | FileConversionApiPool   |
| .NET CLR     | No Managed Code         |
| Identity     | ApplicationPoolIdentity |
| Idle Timeout | 0 (disable)             |
| Recycling    | 0 (disable periodic)    |

**Using IIS Manager:**

1. Open IIS Manager (`inetmgr`)
2. Right-click "Application Pools" → "Add Application Pool"
3. Name: `FileConversionApiPool`
4. .NET CLR Version: `No Managed Code`
5. Click OK
6. Right-click the new pool → "Advanced Settings"
7. Process Model → Identity: `ApplicationPoolIdentity`
8. Process Model → Idle Time-out: `0`
9. Recycling → Regular Time Interval: `0`

**3. Create IIS Website:**

| Setting          | Value                        |
| ---------------- | ---------------------------- |
| Name             | FileConversionApi            |
| Physical Path    | C:\inetpub\FileConversionApi |
| Application Pool | FileConversionApiPool        |
| Port             | 80 (or desired port)         |

**Using IIS Manager:**

1. Right-click "Sites" → "Add Website"
2. Site name: `FileConversionApi`
3. Application pool: `FileConversionApiPool`
4. Physical path: `C:\inetpub\FileConversionApi`
5. Port: `80` (or your preferred port)
6. Click OK

**4. Set Permissions:**

```powershell
# Create required directories
New-Item -ItemType Directory -Path "C:\inetpub\FileConversionApi\App_Data\temp" -Force
New-Item -ItemType Directory -Path "C:\inetpub\FileConversionApi\App_Data\logs" -Force

# Grant IIS permissions
icacls "C:\inetpub\FileConversionApi" /grant "IIS_IUSRS:(OI)(CI)RX" /T
icacls "C:\inetpub\FileConversionApi\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T
```

**6. Verify Deployment:**

```powershell
# Check health
curl http://localhost/health

# Test conversion
curl -X POST http://localhost/api/convert `
  -F "file=@test.docx" `
  -F "targetFormat=pdf" `
  -o output.pdf
```

## Configuration

### Production Configuration

**appsettings.Production.json:**

```json
{
  "Security": {
    "EnableIPFiltering": true,
    "IPWhitelist": ["192.168.1.0/24", "10.0.0.0/8"]
  },
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": true,
    "GeneralRules": [
      {
        "Endpoint": "*",
        "Period": "1m",
        "Limit": 30
      }
    ]
  },
  "LibreOffice": {
    "SdkPath": "LibreOffice",
    "ForceBundled": true,
    "TimeoutSeconds": 300,
    "MaxConcurrentConversions": 2
  },
  "Concurrency": {
    "MaxConcurrentConversions": 2,
    "MaxQueueSize": 10
  },
  "FileHandling": {
    "MaxFileSize": 52428800,
    "TempFileRetentionHours": 24
  }
}
```

### Environment Variables

Override configuration without modifying files:

```powershell
$env:Security__EnableIPFiltering = "true"
$env:Security__IPWhitelist__0 = "192.168.1.0/24"
$env:Concurrency__MaxConcurrentConversions = "4"
```

### Performance Tuning

| Setting                   | Default | Recommended By CPU Cores |           |          |
| ------------------------- | ------- | ------------------------ | --------- | -------- |
|                           |         | 2-4 cores                | 4-8 cores | 8+ cores |
| MaxConcurrentConversions  | 2       | 2                        | 4         | 6-8      |
| MaxQueueSize              | 10      | 6                        | 12        | 20       |
| DocumentConversionTimeout | 60000ms | 60000ms                  | 90000ms   | 120000ms |

## Verification

### Health Checks

```powershell
# Basic health check
curl http://localhost/health

# Expected response:
{
  "status": "Healthy",
  "timestamp": "2025-10-17T10:30:00Z",
  "services": {
    "LibreOffice": {
      "status": "Healthy",
      "message": "Available"
    }
  }
}

# Detailed diagnostics
curl http://localhost/health/detailed
```

### Test Conversion

```powershell
# Test document conversion
curl -X POST http://localhost/api/convert `
  -F "file=@sample.docx" `
  -F "targetFormat=pdf" `
  -o output.pdf

# Verify output file
Test-Path output.pdf
```

### Verify API Documentation

```powershell
# Open Swagger UI
Start-Process "http://localhost/api-docs"
```

## Security Hardening

### Network Security

**Windows Firewall:**

```powershell
# Allow API port
New-NetFirewallRule `
  -DisplayName "File Conversion API" `
  -Direction Inbound `
  -LocalPort 3000 `
  -Protocol TCP `
  -Action Allow `
  -Profile Domain,Private

# Block public access
Remove-NetFirewallRule -DisplayName "File Conversion API" -Profile Public
```

### File System Permissions

**Principle of Least Privilege:**

```powershell
# Restrict application directory (read-only)
icacls "C:\inetpub\FileConversionApi" `
  /inheritance:r `
  /grant "BUILTIN\Administrators:(OI)(CI)F" `
  /grant "IIS_IUSRS:(OI)(CI)RX"

# Allow write to App_Data only
icacls "C:\inetpub\FileConversionApi\App_Data" `
  /grant "IIS_IUSRS:(OI)(CI)F"
```

### IP Whitelisting

Enable in production:

```json
{
  "Security": {
    "EnableIPFiltering": true,
    "IPWhitelist": ["127.0.0.1", "::1", "192.168.1.0/24", "10.0.0.0/8"]
  }
}
```

### HTTPS Configuration

**Bind SSL Certificate:**

1. Open IIS Manager
2. Select your site
3. Bindings → Add
4. Type: https
5. Port: 443
6. SSL certificate: Select your certificate

**Force HTTPS (web.config):**

```xml
<system.webServer>
  <rewrite>
    <rules>
      <rule name="HTTP to HTTPS redirect" stopProcessing="true">
        <match url="(.*)" />
        <conditions>
          <add input="{HTTPS}" pattern="off" />
        </conditions>
        <action type="Redirect" url="https://{HTTP_HOST}/{R:1}" redirectType="Permanent" />
      </rule>
    </rules>
  </rewrite>
</system.webServer>
```

## Troubleshooting

### Common Issues

#### LibreOffice Not Found

**Symptom:** Conversions fail with "LibreOffice executable not found"

**Solution:**

```powershell
# Verify bundle exists
Test-Path C:\inetpub\FileConversionApi\LibreOffice\program\soffice.exe

# If missing, rebundle and rebuild package
.\bundle-libreoffice.ps1
.\deploy.ps1
```

#### Permission Denied

**Symptom:** "Access denied" errors in logs

**Solution:**

```powershell
# Grant IIS permissions
icacls "C:\inetpub\FileConversionApi\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T

# Restart IIS
iisreset
```

#### Service Won't Start

**Symptom:** 500.30 error or service fails to start

**Solutions:**

```powershell
# 1. Check .NET installation
dotnet --version

# 2. View Event Viewer
eventvwr.msc
# Navigate to: Windows Logs > Application

# 3. Test manually
cd C:\inetpub\FileConversionApi
dotnet FileConversionApi.dll

# 4. Check application pool
Get-WebAppPoolState -Name FileConversionApiPool

# 5. Restart application pool
Restart-WebAppPool -Name FileConversionApiPool
```

#### Conversion Timeout

**Symptom:** Large documents fail with timeout error

**Solution:**

```json
{
  "LibreOffice": {
    "TimeoutSeconds": 600
  },
  "Timeouts": {
    "DocumentConversion": 120000
  }
}
```

#### High Memory Usage

**Symptom:** Memory usage exceeds 2GB

**Solution:**

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
# View application logs
Get-Content C:\inetpub\logs\file-conversion-api-*.log -Tail 50 -Wait

# View Event Log
Get-EventLog -LogName Application -Source FileConversionApi -Newest 20

# Check IIS status
Get-WebAppPoolState -Name FileConversionApiPool
Get-WebSite -Name FileConversionApi

# Check running processes
Get-Process | Where-Object {$_.ProcessName -like "*soffice*"}

# Monitor performance
Get-Counter "\Process(FileConversionApi*)\% Processor Time"
Get-Counter "\Process(FileConversionApi*)\Working Set - Private"
```

## Maintenance

### Automated Maintenance

**Automatic processes:**

| Task               | Frequency | Retention | Location          |
| ------------------ | --------- | --------- | ----------------- |
| Log rotation       | Daily     | 30 days   | `logs/` directory |
| Temp file cleanup  | Hourly    | 24 hours  | `App_Data/temp/`  |
| Failed conversions | Immediate | N/A       | Automatic cleanup |

### Manual Maintenance

**Monthly tasks:**

1. **Check for .NET updates:**

```powershell
# Download from: https://dotnet.microsoft.com/download/dotnet/8.0
# Test in staging before production
```

2. **Review logs for errors:**

```powershell
# Check error rate
Get-Content logs\*.log | Select-String "ERROR" | Measure-Object
```

3. **Monitor disk space:**

```powershell
Get-PSDrive C | Select-Object Used,Free
```

### Performance Monitoring

**Key metrics to monitor:**

| Metric           | Threshold          | Action                        |
| ---------------- | ------------------ | ----------------------------- |
| CPU usage        | < 80% average      | Add concurrent conversions    |
| Memory usage     | < 2GB per instance | Reduce concurrent conversions |
| Conversion queue | < 50% of max       | Increase queue size           |
| Error rate       | < 1%               | Investigate logs              |
| Disk space       | > 20% free         | Clean up or expand storage    |

**Access metrics:**

```powershell
curl http://localhost/health/detailed
```

**Windows Performance Counters:**

```powershell
# Monitor CPU
Get-Counter "\Processor(_Total)\% Processor Time" -Continuous

# Monitor Memory
Get-Counter "\Memory\Available MBytes" -Continuous

# Monitor Disk
Get-Counter "\PhysicalDisk(_Total)\% Disk Time" -Continuous
```

### Backup and Recovery

**Backup:**

```powershell
# Create timestamped backup
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
Compress-Archive `
  -Path "C:\inetpub\FileConversionApi" `
  -DestinationPath "C:\Backups\FileConversionApi-$timestamp.zip"
```

**Recovery:**

```powershell
# Stop service
iisreset /stop

# Restore from backup
Expand-Archive `
  -Path "C:\Backups\FileConversionApi-20251017-143000.zip" `
  -DestinationPath "C:\inetpub\FileConversionApi" `
  -Force

# Start service
iisreset /start

# Verify
curl http://localhost/health
```

### Update Procedure

**Single-server update:**

```powershell
# 1. Stop IIS
iisreset /stop

# 2. Backup current deployment
.\backup.ps1

# 3. Deploy new version
.\deploy.ps1

# 4. Verify health
curl http://localhost/health

# 5. Start IIS
iisreset /start
```

## High Availability

### Load Balancer Configuration

**Windows Network Load Balancing (NLB):**

1. Open Server Manager
2. Add Roles and Features
3. Select Network Load Balancing
4. Configure cluster with multiple servers

**Health check settings:**

| Setting             | Value         |
| ------------------- | ------------- |
| Endpoint            | `/health`     |
| Interval            | 10 seconds    |
| Timeout             | 5 seconds     |
| Healthy threshold   | 2 consecutive |
| Unhealthy threshold | 3 consecutive |

**Hardware Load Balancer Configuration:**

```
# Example F5 BIG-IP configuration
pool file_conversion_pool {
    monitor http /health
    members {
        192.168.1.10:80
        192.168.1.11:80
    }
    load-balancing-mode round-robin
}
```

### Multi-Server Deployment

**Architecture:**

```
Windows Load Balancer (NLB/Hardware)
     │
     ├── Server 1 (Windows Server 2019)
     │    ├── IIS + API Instance
     │    ├── LibreOffice Bundle
     │    └── Local Storage (C:\inetpub\FileConversionApi)
     │
     └── Server 2 (Windows Server 2019)
          ├── IIS + API Instance
          ├── LibreOffice Bundle
          └── Local Storage (C:\inetpub\FileConversionApi)
```

**Considerations:**

- Each server operates independently (stateless)
- No shared storage required
- LibreOffice bundle on each server
- Identical configuration on all servers
- Use environment variables for server-specific settings

**Zero-downtime update:**

1. Deploy to Server 2
2. Verify health checks pass
3. Remove Server 1 from load balancer
4. Deploy to Server 1
5. Verify health checks pass
6. Add Server 1 back to load balancer

## Windows-Specific Optimizations

### IIS Configuration

**Application Pool Advanced Settings:**

```
Process Model:
  - Maximum Worker Processes: 1
  - Idle Time-out: 0 (disable)

Recycling:
  - Regular Time Interval: 0 (disable)
  - Request Limit: 0 (disable)
  - Specific Times: Clear all

Rapid-Fail Protection:
  - Enabled: True
  - Failure Interval: 5 minutes
  - Maximum Failures: 5
```

### Windows Event Log Integration

**View logs:**

```powershell
# Get latest entries
Get-EventLog -LogName Application -Source FileConversionApi -Newest 20

# Filter by level
Get-EventLog -LogName Application -Source FileConversionApi -EntryType Error -Newest 10

# Export logs
Get-EventLog -LogName Application -Source FileConversionApi | Export-Csv events.csv
```

### Performance Monitor

**Create custom performance counter set:**

```powershell
# Create data collector set
$dataCollectorSet = New-Object -COM Pla.DataCollectorSet
$dataCollectorSet.DisplayName = "File Conversion API"
$dataCollectorSet.Duration = 3600
$dataCollectorSet.SubdirectoryFormat = 1
$dataCollectorSet.SubdirectoryFormatPattern = "yyyyMMdd"

# Add counters
$dataCollectorSet.Counters = @(
    "\Processor(_Total)\% Processor Time",
    "\Memory\Available MBytes",
    "\PhysicalDisk(_Total)\% Disk Time",
    "\Process(FileConversionApi*)\Working Set"
)

# Start collection
$dataCollectorSet.Start()
```

## Support

**Documentation:**

- Architecture: [ARCHITECTURE.md](ARCHITECTURE.md)
- API Reference: [README.md](README.md)
- Conversion Matrix: [SUPPORTED_CONVERSIONS.md](SUPPORTED_CONVERSIONS.md)

**Operational:**

- Health endpoint: `/health/detailed`
- API documentation: `/api-docs`
- Log files: Check `logs/` directory
- Windows Event Log: Application source `FileConversionApi`

**Common Scripts:**

- `test-conversion.ps1` - Operational verification
- `bundle-libreoffice.ps1` - Create optimized LibreOffice bundle (517 MB)
- `deploy.ps1` - Build deployment package for IIS

---
