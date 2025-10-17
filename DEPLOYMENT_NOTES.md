# Deployment Notes

## Critical Pre-Deployment Requirements

### 1. LibreOffice Bundle

**IMPORTANT**: The LibreOffice bundle is NOT included in the repository. You MUST create it before deployment.

```powershell
# Install LibreOffice (temporary)
# Download from: https://www.libreoffice.org/download/

# Bundle for deployment
cd <project-root>
.\bundle-libreoffice.ps1 -UltraMinimal
```

**Without the bundle:**

- DOCX/XLSX/PPTX to PDF conversions will fail
- PDF to DOCX conversions will fail
- Other conversions (CSV, images, text) will work

### 2. .NET 8 Runtime

Ensure .NET 8 Runtime is installed on the target server:

```powershell
# Download and install from:
# https://dotnet.microsoft.com/download/dotnet/8.0

# Verify installation
dotnet --version
```

## Windows IIS Deployment

### Automated Deployment (Recommended)

```powershell
# Run from FileConversionApi directory as Administrator
cd FileConversionApi
.\deploy.ps1
```

This script:

1. Creates IIS application pool with proper configuration
2. Publishes .NET 8 application
3. Copies LibreOffice bundle (if present)
4. Generates production appsettings.json
5. Configures permissions
6. Sets up directory structure

### Deployment Verification

```powershell
# Check service health
curl http://localhost/health

# Expected response:
{
  "status": "Healthy",
  "timestamp": "...",
  "services": {
    "LibreOffice": {
      "status": "Healthy",
      "message": "Available"
    }
  }
}

# Check API documentation
Start-Process "http://localhost/api-docs"

# Test conversion
curl -X POST http://localhost/api/convert `
  -F "file=@test.docx" `
  -F "targetFormat=pdf" `
  -o output.pdf
```

### Manual Deployment

If automated deployment fails, follow these steps:

1. **Publish Application**

   ```powershell
   dotnet publish FileConversionApi/FileConversionApi.csproj `
     -c Release `
     -o C:\inetpub\FileConversionApi
   ```

2. **Copy LibreOffice Bundle**

   ```powershell
   Copy-Item FileConversionApi\LibreOffice `
     -Destination C:\inetpub\FileConversionApi\LibreOffice `
     -Recurse
   ```

3. **Create IIS Application Pool**

   - Open IIS Manager
   - Create Application Pool: `FileConversionApiPool`
   - .NET CLR Version: `No Managed Code`
   - Identity: `ApplicationPoolIdentity`

4. **Create IIS Website**

   - Name: `FileConversionApi`
   - Physical Path: `C:\inetpub\FileConversionApi`
   - Application Pool: `FileConversionApiPool`
   - Port: 80 (or desired port)

5. **Set Permissions**
   ```powershell
   icacls "C:\inetpub\FileConversionApi\App_Data" `
     /grant "IIS_IUSRS:(OI)(CI)F" /T
   ```

## Linux Deployment

### Using Systemd Service

```bash
# 1. Publish application
dotnet publish FileConversionApi/FileConversionApi.csproj \
  -c Release \
  -o /opt/file-conversion-api

# 2. Create service file
sudo nano /etc/systemd/system/file-conversion-api.service

[Unit]
Description=File Conversion API
After=network.target

[Service]
Type=simple
User=fileconversion
WorkingDirectory=/opt/file-conversion-api
ExecStart=/usr/bin/dotnet FileConversionApi.dll
Restart=always
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:3000

[Install]
WantedBy=multi-user.target

# 3. Enable and start
sudo systemctl daemon-reload
sudo systemctl enable file-conversion-api
sudo systemctl start file-conversion-api
sudo systemctl status file-conversion-api
```

## Configuration

### Production Settings

Key settings to configure in `appsettings.json`:

```json
{
  "Security": {
    "EnableIPFiltering": true,
    "IPWhitelist": ["192.168.1.0/24", "10.0.0.0/8"]
  },
  "IpRateLimiting": {
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
  }
}
```

### Environment-Specific Overrides

Use environment variables to override configuration:

```powershell
# Windows
$env:Security__EnableIPFiltering = "true"
$env:Concurrency__MaxConcurrentConversions = "4"

# Linux
export Security__EnableIPFiltering=true
export Concurrency__MaxConcurrentConversions=4
```

## Post-Deployment

### Health Monitoring

Monitor the service using built-in endpoints:

```powershell
# Basic health
curl http://localhost/health

# Detailed health with system info
curl http://localhost/health/detailed

# API documentation
curl http://localhost/api-docs
```

### Logging

Logs are written to:

- **Windows**: `C:\inetpub\logs\file-conversion-api-<date>.log`
- **Linux**: `/var/log/file-conversion-api/`
- **Windows Event Log**: Application source `FileConversionApi`

View logs:

```powershell
# Windows - File logs
Get-Content C:\inetpub\logs\file-conversion-api-*.log -Tail 50 -Wait

# Windows - Event log
Get-EventLog -LogName Application -Source FileConversionApi -Newest 20

# Linux
tail -f /var/log/file-conversion-api/file-conversion-api.log
```

### Performance Tuning

Monitor and adjust based on workload:

1. **Concurrent Conversions**: Adjust based on CPU cores

   - Recommended: 1-2 per CPU core
   - `Concurrency:MaxConcurrentConversions`

2. **Queue Size**: Adjust based on expected load

   - Recommended: 2-3x max concurrent
   - `Concurrency:MaxQueueSize`

3. **Timeouts**: Adjust based on document complexity
   - Document: 60-300 seconds
   - Image: 30-60 seconds

## Troubleshooting

### Common Issues

#### LibreOffice Not Found

**Symptom**: Conversions fail with "LibreOffice executable not found"

**Solution**:

```powershell
# Verify bundle exists
Test-Path C:\inetpub\FileConversionApi\LibreOffice\program\soffice.exe

# If missing, run bundling script and redeploy
.\bundle-libreoffice.ps1 -UltraMinimal
.\deploy.ps1 -SkipPublish
```

#### Permission Denied

**Symptom**: "Access denied" errors in logs

**Solution**:

```powershell
# Grant IIS users full control
icacls "C:\inetpub\FileConversionApi\App_Data" `
  /grant "IIS_IUSRS:(OI)(CI)F" /T

# Restart IIS
iisreset
```

#### Service Won't Start

**Symptom**: 500.30 error or service fails to start

**Solution**:

```powershell
# Check Event Viewer for detailed errors
eventvwr.msc

# Verify .NET 8 installation
dotnet --version

# Check web.config
Get-Content C:\inetpub\FileConversionApi\web.config

# Test application manually
cd C:\inetpub\FileConversionApi
dotnet FileConversionApi.dll
```

## Security Hardening

### Network Security

```powershell
# Windows Firewall
New-NetFirewallRule -DisplayName "File Conversion API" `
  -Direction Inbound `
  -LocalPort 3000 `
  -Protocol TCP `
  -Action Allow `
  -Profile Domain,Private

# Disable public profile access
Remove-NetFirewallRule -DisplayName "File Conversion API" -Profile Public
```

### File System Security

```powershell
# Restrict application directory
icacls "C:\inetpub\FileConversionApi" `
  /inheritance:r `
  /grant "BUILTIN\Administrators:(OI)(CI)F" `
  /grant "IIS_IUSRS:(OI)(CI)RX"

# Full control only on App_Data
icacls "C:\inetpub\FileConversionApi\App_Data" `
  /grant "IIS_IUSRS:(OI)(CI)F"
```

### IP Filtering

Enable IP whitelisting in `appsettings.json`:

```json
{
  "Security": {
    "EnableIPFiltering": true,
    "IPWhitelist": ["127.0.0.1", "::1", "192.168.1.0/24"]
  }
}
```

## Backup and Recovery

### Backup Application

```powershell
# Backup deployment directory
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
Compress-Archive `
  -Path "C:\inetpub\FileConversionApi" `
  -DestinationPath "C:\Backups\FileConversionApi-$timestamp.zip"
```

### Recovery

```powershell
# Stop IIS
iisreset /stop

# Restore from backup
Expand-Archive `
  -Path "C:\Backups\FileConversionApi-<timestamp>.zip" `
  -DestinationPath "C:\inetpub\FileConversionApi" `
  -Force

# Start IIS
iisreset /start

# Verify health
curl http://localhost/health
```

## Maintenance

### Regular Tasks

1. **Log Rotation**: Logs are automatically rotated daily

   - Retention: 30 days (configurable)
   - Location: `C:\inetpub\logs\`

2. **Temp File Cleanup**: Automatic cleanup every 24 hours

   - Retention: 24 hours (configurable)
   - Location: `App_Data\temp\`

3. **Updates**: Check for .NET 8 security updates monthly
   - Download: https://dotnet.microsoft.com/download/dotnet/8.0
   - Test in staging before production

### Performance Monitoring

Monitor key metrics:

- CPU usage (should be < 80% average)
- Memory usage (should be < 2GB per instance)
- Conversion queue depth (should be < 50% of max)
- Error rate (should be < 1%)

Access metrics at `/health/detailed` endpoint.

## Support

For issues and questions:

- Documentation: `README.md`, `ARCHITECTURE.md`
- IIS Deployment: `FileConversionApi/IIS_DEPLOYMENT_README.md`
- API Reference: `/api-docs` when service is running
