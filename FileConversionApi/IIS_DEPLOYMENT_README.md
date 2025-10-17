# IIS Deployment Guide for File Conversion API

This guide provides comprehensive instructions for deploying the File Conversion API to Windows Server IIS.

## Prerequisites

### Windows Server Requirements

- Windows Server 2016 or later
- IIS 8.5 or later with ASP.NET Core Module
- .NET 8.0 Runtime installed
- PowerShell 5.1 or later

### IIS Components Required

- Web Server (IIS)
- ASP.NET Core Module v2
- .NET Core Hosting Bundle

## Automated Deployment (Recommended)

The easiest way to deploy is using the automated PowerShell script included in the repository.

### Quick Deployment

1. **Install IIS and .NET Runtime:**

   Run PowerShell as Administrator:

   ```powershell
   # Install IIS
   Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole, IIS-WebServer, IIS-CommonHttpFeatures, IIS-HttpErrors, IIS-HttpLogging, IIS-RequestFiltering, IIS-StaticContent, IIS-CertProvider, IIS-DefaultDocument, IIS-DirectoryBrowsing, IIS-WebSockets, IIS-ApplicationInit, IIS-ASPNET, IIS-NetFxExtensibility, IIS-ASP, IIS-ISAPIExtensions, IIS-ISAPIFilter, IIS-ServerSideIncludes, IIS-HealthAndDiagnostics, IIS-HttpTracing, IIS-Security, IIS-RequestFiltering, IIS-Performance, IIS-WebServerManagementTools, IIS-ManagementConsole

   # Install ASP.NET Core Hosting Bundle (download from Microsoft)
   # Visit: https://dotnet.microsoft.com/download/dotnet/8.0
   ```

2. **Run the automated deployment script:**

   ```powershell
   # Navigate to the project directory
   cd "C:\path\to\FileConversionApi"

   # Run deployment script as Administrator
   .\deploy.ps1
   ```

   The script will:
   - Create the IIS application pool
   - Set up the website directory
   - Publish the .NET application
   - Create production-ready `appsettings.json`
   - Copy the LibreOffice bundle from git
   - Configure IIS website
   - Set proper permissions

### Custom Deployment Options

```powershell
# Deploy with custom settings
.\deploy.ps1 -SiteName "MyApi" -Port "8080" -InstallPath "D:\MyApp"

# Skip publishing (if already published)
.\deploy.ps1 -SkipPublish

# Skip LibreOffice copy (if not needed)
.\deploy.ps1 -SkipLibreOfficeCopy
```

## Manual Deployment Steps

If you prefer manual deployment, follow these steps:

### 1. Install IIS and ASP.NET Core Module

Run PowerShell as Administrator:

```powershell
# Install IIS
Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole, IIS-WebServer, IIS-CommonHttpFeatures, IIS-HttpErrors, IIS-HttpLogging, IIS-RequestFiltering, IIS-StaticContent, IIS-CertProvider, IIS-DefaultDocument, IIS-DirectoryBrowsing, IIS-WebSockets, IIS-ApplicationInit, IIS-ASPNET, IIS-NetFxExtensibility, IIS-ASP, IIS-ISAPIExtensions, IIS-ISAPIFilter, IIS-ServerSideIncludes, IIS-HealthAndDiagnostics, IIS-HttpTracing, IIS-Security, IIS-RequestFiltering, IIS-Performance, IIS-WebServerManagementTools, IIS-ManagementConsole

# Install ASP.NET Core Hosting Bundle (download from Microsoft)
# Visit: https://dotnet.microsoft.com/download/dotnet/8.0
```

### 2. Create Application Pool

1. Open IIS Manager
2. Right-click "Application Pools" → "Add Application Pool"
3. Name: `FileConversionApiPool`
4. .NET CLR Version: `No Managed Code`
5. Process Model → Identity: `ApplicationPoolIdentity`
6. Advanced Settings → Process Model → Idle Time-out: `0`
7. Recycling → Regular Time Interval: `0`
8. Click OK

### 3. Create Website Directory

```powershell
# Create website directory
New-Item -ItemType Directory -Path "C:\inetpub\wwwroot\FileConversionApi" -Force

# Set permissions for IIS_IUSRS
icacls "C:\inetpub\wwwroot\FileConversionApi" /grant "IIS_IUSRS:(OI)(CI)F" /T
```

### 4. Publish Application

From the project directory:

```bash
# Publish for IIS
dotnet publish FileConversionApi\FileConversionApi.csproj -c Release -o "C:\inetpub\wwwroot\FileConversionApi"
```

### 5. Configure IIS Website

1. Open IIS Manager
2. Right-click "Sites" → "Add Website"
3. Site name: `FileConversionApi`
4. Application pool: `FileConversionApiPool`
5. Physical path: `C:\inetpub\wwwroot\FileConversionApi`
6. Port: `80` (or your preferred port)
7. Click OK

### 6. Configure Application Directory Permissions

```powershell
# Create necessary directories
New-Item -ItemType Directory -Path "C:\inetpub\wwwroot\FileConversionApi\App_Data\temp\uploads" -Force
New-Item -ItemType Directory -Path "C:\inetpub\wwwroot\FileConversionApi\App_Data\temp\converted" -Force
New-Item -ItemType Directory -Path "C:\inetpub\wwwroot\FileConversionApi\App_Data\temp\libreoffice" -Force
New-Item -ItemType Directory -Path "C:\inetpub\wwwroot\FileConversionApi\App_Data\temp\magick" -Force
New-Item -ItemType Directory -Path "C:\inetpub\wwwroot\FileConversionApi\App_Data\logs" -Force

# Set permissions
icacls "C:\inetpub\wwwroot\FileConversionApi\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T
```

### 7. Configure LibreOffice

**Note:** The automated deployment script automatically copies the LibreOffice bundle from the git repository, so this step is handled automatically.

If deploying manually and LibreOffice is not installed system-wide, copy LibreOffice binaries to the application directory:

```powershell
# Copy LibreOffice binaries (manual deployment only)
Copy-Item "LibreOffice\*" -Destination "C:\inetpub\wwwroot\FileConversionApi\LibreOffice" -Recurse -Force
```

### 8. Test Deployment

1. Start the website in IIS Manager (or use the automated script)
2. Open browser to `http://localhost/health`
3. Verify response: `{"status":"Healthy","timestamp":"...","services":{"LibreOffice":{"status":"Healthy","message":"Available"}}}`
4. Test API: `http://localhost/api/supported-formats`

## Configuration

### Production appsettings.json

The automated deployment script creates a comprehensive production-ready `appsettings.json` with optimized settings for Windows Server deployment.

#### Key Production Features:

- **Security**: IP whitelisting, rate limiting (60 req/min, 1000 req/hour), request size limits (50MB)
- **LibreOffice**: Configured for bundled runtime from git repository
- **Performance**: Optimized timeouts, concurrent request handling
- **Monitoring**: Health checks, performance metrics, structured logging
- **IIS Integration**: Proper permissions for IIS application pool identity

#### Manual Configuration

If you need to modify the production configuration after deployment, edit `appsettings.json` in the installation directory:

```json
{
  "LibreOffice": {
    "SdkPath": "LibreOffice",
    "ForceBundled": true,
    "UseSdkIntegration": false,
    "TimeoutSeconds": 300
  },
  "Security": {
    "EnableIPFiltering": false,
    "IPWhitelist": ["127.0.0.1", "::1", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16"]
  }
}
```

### SSL Configuration

For HTTPS deployment:

1. Obtain SSL certificate (self-signed for testing, CA-signed for production)
2. Configure IIS binding for HTTPS (port 443)
3. Update firewall rules for port 443
4. Optionally update `appsettings.json` SSL section if using custom certificates

## Troubleshooting

### Common Issues

1. **500.30 ASP.NET Core app failed to start**

   - Check Event Viewer for detailed error messages
   - Verify .NET 8.0 runtime is installed
   - Check file permissions on App_Data directory

2. **LibreOffice not found**

   - Ensure LibreOffice binaries are in the `LibreOffice` directory
   - Or install LibreOffice system-wide
   - Check `appsettings.json` LibreOffice configuration

3. **File access denied**
   - Verify IIS_IUSRS has write permissions on App_Data directories
   - Check NTFS permissions on temp directories

### Logs

- Application logs: `C:\inetpub\logs\FileConversionApi\*.log`
- IIS logs: `C:\inetpub\logs\LogFiles\`
- Event Viewer: Windows Logs → Application

## Security Considerations

1. **IP Filtering**: Configure IP whitelist in `appsettings.json`
2. **Rate Limiting**: Adjust rate limits based on load requirements
3. **File Upload Limits**: Configure maximum file sizes appropriately
4. **HTTPS**: Always use HTTPS in production
5. **Firewall**: Configure Windows Firewall for internal network access only

## Monitoring

- Health check endpoint: `/health`
- Detailed health check: `/health/detailed`
- Application logs via Serilog
- Windows Event Log integration

## Performance Tuning

- Adjust `Concurrency.MaxConcurrentConversions` based on server capacity
- Configure `Timeouts` appropriately for your use case
- Monitor memory usage with large file conversions
- Consider load balancing for high-traffic scenarios
