# Deployment Guide

How to deploy the File Conversion API to Windows Server with IIS.

**Note:** Examples use `..\inetpub\FileConversionApi` as the deployment folder. Adjust accordingly.

## Prerequisites

| Component    | Details                                                                          |
| ------------ | -------------------------------------------------------------------------------- |
| OS           | Windows Server 2016+ or Windows 11                                               |
| .NET Runtime | .NET 8.0 or later ([Download](https://dotnet.microsoft.com/download/dotnet/8.0)) |
| IIS          | IIS 8.5+ with ASP.NET Core Module                                                |
| RAM          | 4GB minimum, 8GB recommended                                                     |
| Disk Space   | 2GB for the application                                                          |

### Installing Components

**.NET 8 Runtime:**

```powershell
# Download ASP.NET Core Hosting Bundle from: https://dotnet.microsoft.com/download/dotnet/8.0
# Run the installer, then verify:
dotnet --version  # Should show 8.0.x
```

**Important:** Our LibreOffice bundle **already includes** Visual C++ runtime dependencies.

**IIS Setup:**

```powershell
# Enable IIS
Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole
Enable-WindowsOptionalFeature -Online -FeatureName IIS-ASPNET45

# Install ASP.NET Core Module
# Then restart IIS
iisreset
```

## Building the Deployment Package

Do this on the **development machine** (assumes LibreOffice is installed):

**Step 1: Create LibreOffice Bundle**

```powershell
# Packages LibreOffice with all dependencies (~500MB)
.\bundle-libreoffice.ps1

# Verify it worked
Test-Path FileConversionApi\LibreOffice\program\soffice.exe  # Should return True
```

**Step 2: Create Profile Template**

```powershell
# Creates a pre-initialized LibreOffice profile template
.\create-libreoffice-profile-template.ps1

# Verify it worked
Test-Path FileConversionApi\libreoffice-profile-template  # Should return True
```

**Step 3: Build Deployment Package**

```powershell
cd FileConversionApi
.\deploy.ps1

# Creates deployment package at: deploy\release\
```

**What gets packaged:**

- .NET application (~50MB)
- LibreOffice bundle with VC++ runtime DLLs (~500MB)
- Pre-initialized profile template (~2KB)
- Configuration files

## Deploying to the Server

### Copy Files

```powershell
Copy-Item .\deploy\release\* -Destination "\\SERVER\..\inetpub\FileConversionApi" -Recurse -Force
```

### Set Up IIS

**1. Create Application Pool**

In IIS Manager (`inetmgr`):

1. Right-click "Application Pools" → "Add Application Pool"
2. **Name:** `FileConversionApiPool`
3. **.NET CLR version:** **"No Managed Code"**
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
4. **Physical path:** `..\inetpub\FileConversionApi`
5. **Binding:**
   - **Type:** http
   - **Port:** `80` (or your preferred port)
   - **Host name:** (blank for intranet, or `fileconversion.company.local`)

**3. Set Permissions**

**The application will not work without these permissions.**

```powershell
$deployPath = "..\inetpub\FileConversionApi"

# App_Data - Full Control (for temp files, logs, profiles)
icacls "$deployPath\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T

# LibreOffice - Read and Execute (to run soffice.exe)
icacls "$deployPath\LibreOffice" /grant "IIS_IUSRS:(OI)(CI)RX" /T

# Profile Template - Read (to copy template per conversion)
icacls "$deployPath\libreoffice-profile-template" /grant "IIS_IUSRS:(OI)(CI)R" /T

# Verify permissions were set
icacls "$deployPath\App_Data" | Select-String "IIS_IUSRS"
```

**Permission flags:**

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

Edit `..\inetpub\FileConversionApi\appsettings.json` to customize settings. Restart the application pool after changes.

### API Key Authentication

```json
{
  "Security": {
    "RequireApiKey": true,
    "ApiKeys": ["apikey_live_your_key_here"]
  }
}
```

### File Handling

**Recommended to use C:\Temp for Temporary Files.**

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
# 1. Test health endpoint
curl http://localhost/health  # Should return {"status":"Healthy"}

# 2. Try a conversion
"Hello World" | Out-File C:\temp\test.txt
curl -X POST http://localhost/api/convert -F "file=@C:\temp\test.txt" -F "targetFormat=pdf" -o C:\temp\output.pdf
Test-Path C:\temp\output.pdf  # Should be True
```

## Troubleshooting

### Service Won't Start (500.30 Error)

```powershell
# Check .NET runtime is installed
dotnet --list-runtimes | findstr "Microsoft.AspNetCore.App 8"
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
icacls "..\inetpub\FileConversionApi\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T
```

### Check Logs

```powershell
# View recent logs
Get-Content "..\inetpub\FileConversionApi\App_Data\logs\*.log" -Tail 50

# Search for errors
Get-Content "..\inetpub\FileConversionApi\App_Data\logs\*.log" -Tail 500 | Select-String "Error|Exception|Failed"

# Check for LibreOffice issues
Get-Content "..\inetpub\FileConversionApi\App_Data\logs\*.log" -Tail 500 | Select-String "LibreOffice|soffice"
```
