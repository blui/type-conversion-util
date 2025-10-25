# Configuration Deployment Guide

## Overview

This guide explains how to configure the File Conversion API for deployment across different environments (Development, Staging, Production).

## Configuration Architecture

The application uses a single `appsettings.json` file as the base configuration, with environment-specific overrides applied through:
- `appsettings.Development.json` (optional, minimal overrides)
- `appsettings.Production.json` (optional, minimal overrides)
- Environment variables (recommended for sensitive data)
- Command-line arguments (for deployment automation)

## Base Configuration File

Location: `FileConversionApi/appsettings.json`

This file contains all default settings and should work out-of-the-box for most deployments.

### Critical Settings by Environment

#### Development Environment

**Recommended Overrides:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "Security": {
    "EnableIPFiltering": false,
    "EnableRateLimiting": false
  },
  "Development": {
    "EnableSwagger": true,
    "EnableDetailedErrors": true
  }
}
```

#### Production Environment

**Recommended Overrides:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  },
  "Security": {
    "EnableIPFiltering": true,
    "EnableRateLimiting": true,
    "IPWhitelist": ["10.0.0.0/8", "192.168.0.0/16"]
  },
  "LibreOffice": {
    "ForceBundled": true
  },
  "Development": {
    "EnableSwagger": false,
    "EnableDetailedErrors": false
  }
}
```

## Key Configuration Sections

### 1. File Handling (`FileHandling`)

Controls file upload limits and temporary storage.

**Path Configuration:**
- Use relative paths (e.g., `App_Data\temp\uploads`) for portability
- Use absolute paths (e.g., `D:\AppData\FileConversion\Uploads`) for specific locations
- Relative paths resolve from the application's base directory

**Example Absolute Path Configuration:**
```json
{
  "FileHandling": {
    "TempDirectory": "D:\\AppData\\FileConversion\\Uploads",
    "OutputDirectory": "D:\\AppData\\FileConversion\\Converted"
  }
}
```

**Example Network Share Configuration (Windows):**
```json
{
  "FileHandling": {
    "TempDirectory": "\\\\FileServer\\Shared\\FileConversion\\Uploads",
    "OutputDirectory": "\\\\FileServer\\Shared\\FileConversion\\Converted"
  }
}
```

### 2. Security (`Security`)

Controls access restrictions and CORS policies.

**IP Whitelist Format:**
- Single IP: `"127.0.0.1"`
- CIDR notation: `"192.168.1.0/24"`
- Full subnet: `"10.0.0.0/8"`

**Production Security Checklist:**
- [ ] Enable IP filtering if API is not public-facing
- [ ] Configure specific IP whitelist (not broad ranges)
- [ ] Enable rate limiting to prevent abuse
- [ ] Configure AllowedOrigins for CORS if accessed from browsers
- [ ] Set appropriate RequestTimeoutSeconds (default 300)

### 3. LibreOffice (`LibreOffice`)

Controls LibreOffice integration for document conversion.

**Deployment Strategies:**

#### Strategy 1: Bundled LibreOffice (Recommended)
```json
{
  "LibreOffice": {
    "ExecutablePath": "",
    "ForceBundled": true
  }
}
```
Bundle LibreOffice with your application in `AppDirectory/LibreOffice/program/soffice.exe`.

#### Strategy 2: System LibreOffice
```json
{
  "LibreOffice": {
    "ExecutablePath": "",
    "ForceBundled": false
  }
}
```
Application will automatically detect LibreOffice in Program Files directories.

#### Strategy 3: Custom Path
```json
{
  "LibreOffice": {
    "ExecutablePath": "D:\\LibreOffice\\Program\\soffice.exe",
    "ForceBundled": false
  }
}
```
Specify exact path to soffice.exe executable.

**Note:** All C: drive hardcoded paths have been removed. The application now uses `Environment.GetFolderPath` to locate Program Files directories on any drive.

### 4. Logging (`Serilog`)

Controls application logging output.

**Log File Path Configuration:**
- Default: `App_Data\logs\file-conversion-api-.log` (relative path)
- Custom: `D:\Logs\FileConversion\api-.log` (absolute path)
- Network: `\\LogServer\Logs\FileConversion\api-.log` (UNC path)

**Production Logging Configuration:**
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Warning"
    },
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "D:\\Logs\\FileConversion\\api-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      },
      {
        "Name": "EventLog",
        "Args": {
          "source": "FileConversionApi",
          "logName": "Application"
        }
      }
    ]
  }
}
```

### 5. Preprocessing (`Preprocessing`)

Controls DOCX preprocessing for improved conversion fidelity.

**For Best DOC/DOCX to PDF Quality:**
```json
{
  "Preprocessing": {
    "EnableDocxPreprocessing": true,
    "NormalizeFonts": true,
    "ConvertColors": true,
    "FixTextEffects": true
  }
}
```

**For Performance (Skip Preprocessing):**
```json
{
  "Preprocessing": {
    "EnableDocxPreprocessing": false
  }
}
```

### 6. Concurrency (`Concurrency`)

Controls simultaneous conversion operations.

**Small Server (2-4 cores):**
```json
{
  "Concurrency": {
    "MaxConcurrentConversions": 2,
    "MaxQueueSize": 5
  }
}
```

**Large Server (8+ cores):**
```json
{
  "Concurrency": {
    "MaxConcurrentConversions": 4,
    "MaxQueueSize": 20
  }
}
```

## Environment Variables

Override any configuration value using environment variables with double-underscore notation:

```bash
# Windows
set FileHandling__TempDirectory=D:\Temp\FileConversion
set LibreOffice__ExecutablePath=D:\LibreOffice\soffice.exe
set Security__EnableIPFiltering=true

# Linux
export FileHandling__TempDirectory=/var/tmp/fileconversion
export LibreOffice__ExecutablePath=/opt/libreoffice/program/soffice
export Security__EnableIPFiltering=true
```

## IIS Deployment

### web.config Example

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <handlers>
      <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
    </handlers>
    <aspNetCore processPath="dotnet"
                arguments=".\FileConversionApi.dll"
                stdoutLogEnabled="true"
                stdoutLogFile=".\logs\stdout"
                hostingModel="inprocess">
      <environmentVariables>
        <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        <environmentVariable name="FileHandling__TempDirectory" value="D:\inetpub\FileConversion\Temp" />
        <environmentVariable name="FileHandling__OutputDirectory" value="D:\inetpub\FileConversion\Output" />
        <environmentVariable name="Serilog__WriteTo__0__Args__path" value="D:\inetpub\logs\file-conversion-api-.log" />
      </environmentVariables>
    </aspNetCore>
  </system.webServer>
</configuration>
```

### IIS Application Pool Settings

1. **.NET CLR Version:** No Managed Code
2. **Managed Pipeline Mode:** Integrated
3. **Identity:** ApplicationPoolIdentity (or custom service account with file system permissions)
4. **Enable 32-Bit Applications:** False

### File System Permissions

Grant the IIS application pool identity:
- **Read/Write** access to temp directories
- **Read/Write** access to output directories
- **Read** access to LibreOffice installation directory
- **Write** access to log directories

## Docker Deployment

### Example Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY publish/ .

# Install LibreOffice
RUN apt-get update && apt-get install -y \
    libreoffice \
    libreoffice-writer \
    libreoffice-calc \
    && apt-get clean

# Create directories
RUN mkdir -p /app/App_Data/temp/uploads \
             /app/App_Data/temp/converted \
             /app/App_Data/temp/libreoffice \
             /app/App_Data/logs

ENV ASPNETCORE_ENVIRONMENT=Production
ENV FileHandling__TempDirectory=/app/App_Data/temp/uploads
ENV FileHandling__OutputDirectory=/app/App_Data/temp/converted
ENV LibreOffice__ExecutablePath=/usr/bin/soffice

EXPOSE 80
ENTRYPOINT ["dotnet", "FileConversionApi.dll"]
```

## Configuration Validation

The application validates critical configuration on startup. Check the logs for:

```
[INF] LibreOffice executable found: <path>
[INF] Configuration validation successful
```

If configuration issues are detected:

```
[ERR] LibreOffice executable not found
[ERR] Temp directory not accessible: <path>
```

## Troubleshooting

### Issue: "LibreOffice executable not found"

**Solution:**
1. Verify LibreOffice is installed or bundled
2. Check `LibreOffice:ExecutablePath` in appsettings.json
3. Ensure file system permissions allow reading the executable

### Issue: "Access denied to temp directory"

**Solution:**
1. Verify the application pool identity has write permissions
2. Check the configured paths exist and are accessible
3. Use absolute paths if relative paths cause issues

### Issue: "Conversion timeout"

**Solution:**
1. Increase `LibreOffice:TimeoutSeconds` (default 300)
2. Check server resources (CPU, memory)
3. Reduce `Concurrency:MaxConcurrentConversions` if system is overloaded

## Migration from Old Configuration

If you have existing `deployment/appsettings.json` or environment-specific files:

1. Copy environment-specific settings to `appsettings.Production.json`
2. Remove hardcoded C: drive paths
3. Use relative paths or environment variables
4. Delete redundant `deployment/appsettings.json` file

## Best Practices

1. **Use environment variables** for deployment-specific paths and secrets
2. **Use relative paths** in base appsettings.json for portability
3. **Enable IP filtering** in production environments
4. **Bundle LibreOffice** with the application for consistent behavior
5. **Configure appropriate concurrency** limits based on server capacity
6. **Enable preprocessing** for DOC/DOCX conversions for best quality
7. **Use structured logging** (Serilog) for troubleshooting
8. **Monitor temp directories** for orphaned files

## Support

For issues or questions about configuration, refer to:
- Application logs in configured log directory
- Windows Event Viewer (if EventLog sink is enabled)
- Health check endpoint: `GET /health`
