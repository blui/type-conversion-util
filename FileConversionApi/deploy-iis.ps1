# Production-Grade IIS Deployment Script for File Conversion API
# Comprehensive deployment with validation, optimization, and hardening
# Requires Administrator privileges

param(
    [Parameter(Mandatory=$false)]
    [string]$IISSiteName = "FileConversionApi",

    [string]$IISAppPoolName = "FileConversionApiPool",
    [string]$IISPhysicalPath = "C:\inetpub\file-conversion-api",
    [int]$Port = 80,
    [switch]$EnableHTTPS,
    [string]$CertificateThumbprint = "",
    [switch]$EnableBackup,
    [switch]$SkipOptimization,
    [switch]$ConfigureFirewall
)

$ErrorActionPreference = "Stop"
$WarningPreference = "Continue"

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  File Conversion API - Production IIS Deployment" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

#region Helper Functions

function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-DiskSpace {
    param([string]$Path, [long]$RequiredMB = 2048)

    $drive = (Get-Item $Path).PSDrive.Name
    $disk = Get-PSDrive $drive
    $availableMB = [math]::Round($disk.Free / 1MB, 2)

    if ($availableMB -lt $RequiredMB) {
        Write-Host "   WARNING: Only $availableMB MB available on drive $drive (need $RequiredMB MB)" -ForegroundColor Yellow
        return $false
    }

    Write-Host "   Disk space: $availableMB MB available" -ForegroundColor Green
    return $true
}

function Backup-Deployment {
    param([string]$SourcePath, [string]$BackupPath)

    if (!(Test-Path $SourcePath)) {
        return $null
    }

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $backupName = "backup-$timestamp"
    $fullBackupPath = Join-Path $BackupPath $backupName

    Write-Host "   Creating backup: $fullBackupPath" -ForegroundColor Gray
    Copy-Item -Path $SourcePath -Destination $fullBackupPath -Recurse -Force

    return $fullBackupPath
}

function Test-LibreOfficeBundle {
    param([string]$DeployPath)

    $libreOfficePath = Join-Path $DeployPath "LibreOffice\program\soffice.exe"

    if (Test-Path $libreOfficePath) {
        $bundleSize = (Get-ChildItem (Join-Path $DeployPath "LibreOffice") -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB
        Write-Host "   LibreOffice bundle: Found ($([math]::Round($bundleSize, 0)) MB)" -ForegroundColor Green
        return $true
    } else {
        Write-Host "   WARNING: LibreOffice bundle not found" -ForegroundColor Yellow
        Write-Host "   Office document conversions will NOT work" -ForegroundColor Yellow
        Write-Host "   Run bundle-libreoffice.ps1 before deployment" -ForegroundColor Yellow
        return $false
    }
}

#endregion

#region Step 1: Administrator Check

if (!(Test-Administrator)) {
    Write-Host "ERROR: This script must be run as Administrator" -ForegroundColor Red
    Write-Host "Right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    exit 1
}

#endregion

#region Step 2: Prerequisites Validation

Write-Host "1. Validating prerequisites..." -ForegroundColor Yellow

# Check IIS
try {
    Import-Module WebAdministration -ErrorAction Stop
    Write-Host "   IIS: Installed" -ForegroundColor Green
} catch {
    Write-Host "   ERROR: IIS is not installed" -ForegroundColor Red
    Write-Host "   Install: Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole" -ForegroundColor Yellow
    exit 1
}

# Check .NET 8 Runtime
try {
    $dotnetVersion = dotnet --version
    if ($dotnetVersion -match "^8\.") {
        Write-Host "   .NET Runtime: $dotnetVersion" -ForegroundColor Green
    } else {
        Write-Host "   WARNING: .NET 8 not detected (found: $dotnetVersion)" -ForegroundColor Yellow
        Write-Host "   Download: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   WARNING: .NET Runtime not found" -ForegroundColor Yellow
    Write-Host "   Download: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
}

# Check ASP.NET Core Hosting Bundle
$hostingBundle = Get-ItemProperty -Path "HKLM:\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost" -ErrorAction SilentlyContinue
if ($hostingBundle) {
    Write-Host "   ASP.NET Core Module: Installed" -ForegroundColor Green
} else {
    Write-Host "   WARNING: ASP.NET Core Hosting Bundle may not be installed" -ForegroundColor Yellow
    Write-Host "   Download: https://dotnet.microsoft.com/download/dotnet/8.0 (Hosting Bundle)" -ForegroundColor Yellow
}

# Check disk space
Test-DiskSpace -Path (Split-Path $IISPhysicalPath -Parent) -RequiredMB 2048 | Out-Null

#endregion

#region Step 3: Build Deployment Package

Write-Host ""
Write-Host "2. Building deployment package..." -ForegroundColor Yellow

if (!(Test-Path "deploy.ps1")) {
    Write-Host "   ERROR: deploy.ps1 not found" -ForegroundColor Red
    Write-Host "   Run this script from FileConversionApi directory" -ForegroundColor Yellow
    exit 1
}

.\deploy.ps1 -ErrorAction Stop

if (!(Test-Path "deploy\release")) {
    Write-Host "   ERROR: Deployment package not created" -ForegroundColor Red
    exit 1
}

Write-Host "   Deployment package ready" -ForegroundColor Green

# Verify LibreOffice bundle
Test-LibreOfficeBundle -DeployPath "deploy\release" | Out-Null

# Verify web.config
$webConfigPath = "deploy\release\web.config"
if (Test-Path $webConfigPath) {
    Write-Host "   web.config: Found" -ForegroundColor Green
} else {
    Write-Host "   WARNING: web.config not found" -ForegroundColor Yellow
}

#endregion

#region Step 4: Backup Existing Deployment

if ($EnableBackup -and (Test-Path $IISPhysicalPath)) {
    Write-Host ""
    Write-Host "3. Backing up existing deployment..." -ForegroundColor Yellow

    $backupRoot = Join-Path (Split-Path $IISPhysicalPath -Parent) "backups"
    if (!(Test-Path $backupRoot)) {
        New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
    }

    $backupPath = Backup-Deployment -SourcePath $IISPhysicalPath -BackupPath $backupRoot
    if ($backupPath) {
        Write-Host "   Backup created: $backupPath" -ForegroundColor Green

        # Keep only last 5 backups
        $backups = Get-ChildItem $backupRoot -Directory | Sort-Object CreationTime -Descending
        if ($backups.Count -gt 5) {
            $backups | Select-Object -Skip 5 | Remove-Item -Recurse -Force
            Write-Host "   Cleaned old backups (kept last 5)" -ForegroundColor Gray
        }
    }
}

#endregion

#region Step 5: Stop Existing IIS Resources

Write-Host ""
Write-Host "4. Stopping existing IIS resources..." -ForegroundColor Yellow

if (Test-Path "IIS:\Sites\$IISSiteName") {
    Write-Host "   Stopping website: $IISSiteName" -ForegroundColor Gray
    Stop-WebSite -Name $IISSiteName -ErrorAction SilentlyContinue
}

if (Test-Path "IIS:\AppPools\$IISAppPoolName") {
    Write-Host "   Stopping app pool: $IISAppPoolName" -ForegroundColor Gray
    Stop-WebAppPool -Name $IISAppPoolName -ErrorAction SilentlyContinue

    # Wait for app pool to fully stop
    $maxWait = 30
    $waited = 0
    while ((Get-WebAppPoolState -Name $IISAppPoolName).Value -ne "Stopped" -and $waited -lt $maxWait) {
        Start-Sleep -Seconds 1
        $waited++
    }

    if ($waited -ge $maxWait) {
        Write-Host "   WARNING: App pool did not stop gracefully, forcing..." -ForegroundColor Yellow
    }
}

#endregion

#region Step 6: Deploy Application Files

Write-Host ""
Write-Host "5. Deploying application files..." -ForegroundColor Yellow

if (!(Test-Path $IISPhysicalPath)) {
    New-Item -ItemType Directory -Path $IISPhysicalPath -Force | Out-Null
    Write-Host "   Created directory: $IISPhysicalPath" -ForegroundColor Gray
}

Write-Host "   Copying files to $IISPhysicalPath..." -ForegroundColor Gray
Copy-Item "deploy\release\*" -Destination $IISPhysicalPath -Recurse -Force

$fileCount = (Get-ChildItem $IISPhysicalPath -Recurse -File).Count
$deploySize = [math]::Round((Get-ChildItem $IISPhysicalPath -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
Write-Host "   Deployed $fileCount files ($deploySize MB)" -ForegroundColor Green

#endregion

#region Step 7: Configure Application Pool

Write-Host ""
Write-Host "6. Configuring IIS Application Pool..." -ForegroundColor Yellow

if (!(Test-Path "IIS:\AppPools\$IISAppPoolName")) {
    Write-Host "   Creating app pool: $IISAppPoolName" -ForegroundColor Gray
    New-WebAppPool -Name $IISAppPoolName
}

# Basic configuration
Set-ItemProperty "IIS:\AppPools\$IISAppPoolName" -Name managedRuntimeVersion -Value ""
Set-ItemProperty "IIS:\AppPools\$IISAppPoolName" -Name processModel.identityType -Value "ApplicationPoolIdentity"
Set-ItemProperty "IIS:\AppPools\$IISAppPoolName" -Name enable32BitAppOnWin64 -Value $false

if (!$SkipOptimization) {
    Write-Host "   Applying production optimizations..." -ForegroundColor Gray

    # Process Model Optimization
    Set-ItemProperty "IIS:\AppPools\$IISAppPoolName" -Name processModel.idleTimeout -Value "00:00:00"  # Never idle timeout
    Set-ItemProperty "IIS:\AppPools\$IISAppPoolName" -Name startMode -Value "AlwaysRunning"  # Always running

    # Recycling Configuration
    Set-ItemProperty "IIS:\AppPools\$IISAppPoolName" -Name recycling.periodicRestart.time -Value "1.05:00:00"  # Recycle daily at 1:00 AM
    Set-ItemProperty "IIS:\AppPools\$IISAppPoolName" -Name recycling.periodicRestart.memory -Value 2097152  # Recycle at 2GB

    # Queue Length
    Set-ItemProperty "IIS:\AppPools\$IISAppPoolName" -Name queueLength -Value 5000

    # Rapid-Fail Protection
    Set-ItemProperty "IIS:\AppPools\$IISAppPoolName" -Name failure.rapidFailProtection -Value $true
    Set-ItemProperty "IIS:\AppPools\$IISAppPoolName" -Name failure.rapidFailProtectionInterval -Value "00:05:00"
    Set-ItemProperty "IIS:\AppPools\$IISAppPoolName" -Name failure.rapidFailProtectionMaxCrashes -Value 5

    Write-Host "   Production optimizations applied" -ForegroundColor Green
}

Write-Host "   App pool configured (No Managed Code, ApplicationPoolIdentity)" -ForegroundColor Green

#endregion

#region Step 8: Configure Website

Write-Host ""
Write-Host "7. Configuring IIS Website..." -ForegroundColor Yellow

if (!(Test-Path "IIS:\Sites\$IISSiteName")) {
    Write-Host "   Creating website: $IISSiteName" -ForegroundColor Gray
    New-WebSite -Name $IISSiteName -PhysicalPath $IISPhysicalPath -ApplicationPool $IISAppPoolName -Port $Port
} else {
    Write-Host "   Updating existing website: $IISSiteName" -ForegroundColor Gray
    Set-ItemProperty "IIS:\Sites\$IISSiteName" -Name physicalPath -Value $IISPhysicalPath
    Set-ItemProperty "IIS:\Sites\$IISSiteName" -Name applicationPool -Value $IISAppPoolName
}

# Configure preload
if (!$SkipOptimization) {
    Set-ItemProperty "IIS:\Sites\$IISSiteName" -Name applicationDefaults.preloadEnabled -Value $true
}

Write-Host "   Website configured on port $Port" -ForegroundColor Green

#endregion

#region Step 9: Configure HTTPS

if ($EnableHTTPS) {
    Write-Host ""
    Write-Host "8. Configuring HTTPS..." -ForegroundColor Yellow

    if ([string]::IsNullOrEmpty($CertificateThumbprint)) {
        Write-Host "   ERROR: Certificate thumbprint required for HTTPS" -ForegroundColor Red
        Write-Host "   Use: Get-ChildItem Cert:\LocalMachine\My to list certificates" -ForegroundColor Yellow
    } else {
        $binding = Get-WebBinding -Name $IISSiteName -Protocol "https" -Port 443 -ErrorAction SilentlyContinue
        if ($null -eq $binding) {
            New-WebBinding -Name $IISSiteName -Protocol https -Port 443
        }

        $cert = Get-ChildItem -Path "Cert:\LocalMachine\My" | Where-Object { $_.Thumbprint -eq $CertificateThumbprint }
        if ($null -ne $cert) {
            $binding = Get-WebBinding -Name $IISSiteName -Protocol "https" -Port 443
            $binding.AddSslCertificate($CertificateThumbprint, "my")
            Write-Host "   HTTPS binding configured (Port 443)" -ForegroundColor Green
            Write-Host "   Certificate: $($cert.Subject)" -ForegroundColor Gray
        } else {
            Write-Host "   ERROR: Certificate not found with thumbprint $CertificateThumbprint" -ForegroundColor Red
        }
    }
}

#endregion

#region Step 10: Configure Permissions

Write-Host ""
Write-Host "9. Setting file permissions..." -ForegroundColor Yellow

# App_Data permissions
$appDataPath = Join-Path $IISPhysicalPath "App_Data"
if (Test-Path $appDataPath) {
    icacls "$appDataPath" /grant "IIS_IUSRS:(OI)(CI)F" /T /Q
    Write-Host "   Granted IIS_IUSRS full control to App_Data" -ForegroundColor Green
} else {
    Write-Host "   WARNING: App_Data directory not found" -ForegroundColor Yellow
}

# Application directory read permissions
icacls "$IISPhysicalPath" /grant "IIS_IUSRS:(OI)(CI)RX" /T /Q
Write-Host "   Granted IIS_IUSRS read/execute to application directory" -ForegroundColor Green

#endregion

#region Step 11: Configure Windows Firewall

if ($ConfigureFirewall) {
    Write-Host ""
    Write-Host "10. Configuring Windows Firewall..." -ForegroundColor Yellow

    $httpRule = Get-NetFirewallRule -DisplayName "File Conversion API - HTTP" -ErrorAction SilentlyContinue
    if (!$httpRule) {
        New-NetFirewallRule -DisplayName "File Conversion API - HTTP" `
                            -Direction Inbound `
                            -Protocol TCP `
                            -LocalPort $Port `
                            -Action Allow `
                            -Profile Domain,Private | Out-Null
        Write-Host "   Created firewall rule for HTTP port $Port" -ForegroundColor Green
    } else {
        Write-Host "   HTTP firewall rule already exists" -ForegroundColor Gray
    }

    if ($EnableHTTPS) {
        $httpsRule = Get-NetFirewallRule -DisplayName "File Conversion API - HTTPS" -ErrorAction SilentlyContinue
        if (!$httpsRule) {
            New-NetFirewallRule -DisplayName "File Conversion API - HTTPS" `
                                -Direction Inbound `
                                -Protocol TCP `
                                -LocalPort 443 `
                                -Action Allow `
                                -Profile Domain,Private | Out-Null
            Write-Host "   Created firewall rule for HTTPS port 443" -ForegroundColor Green
        } else {
            Write-Host "   HTTPS firewall rule already exists" -ForegroundColor Gray
        }
    }
}

#endregion

#region Step 12: Configure Logging

Write-Host ""
Write-Host "11. Configuring logging..." -ForegroundColor Yellow

# Configure stdout logging in web.config
$webConfigPath = Join-Path $IISPhysicalPath "web.config"
if (Test-Path $webConfigPath) {
    [xml]$webConfig = Get-Content $webConfigPath
    $aspNetCore = $webConfig.configuration.location.'system.webServer'.aspNetCore
    if ($aspNetCore) {
        $aspNetCore.stdoutLogEnabled = "true"
        $aspNetCore.stdoutLogFile = ".\logs\stdout"
        $webConfig.Save($webConfigPath)
        Write-Host "   Enabled stdout logging" -ForegroundColor Green
    }
}

# Create logs directory
$logsPath = Join-Path $IISPhysicalPath "logs"
if (!(Test-Path $logsPath)) {
    New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
}
icacls "$logsPath" /grant "IIS_IUSRS:(OI)(CI)F" /T /Q

Write-Host "   Logs directory configured" -ForegroundColor Green

#endregion

#region Step 13: Start Services

Write-Host ""
Write-Host "12. Starting IIS services..." -ForegroundColor Yellow

Start-WebAppPool -Name $IISAppPoolName
Write-Host "   App pool started: $IISAppPoolName" -ForegroundColor Gray

Start-WebSite -Name $IISSiteName
Write-Host "   Website started: $IISSiteName" -ForegroundColor Gray

# Wait for application to initialize
Write-Host "   Waiting for application to initialize..." -ForegroundColor Gray
Start-Sleep -Seconds 8

#endregion

#region Step 14: Verify Deployment

Write-Host ""
Write-Host "13. Verifying deployment..." -ForegroundColor Yellow

$healthUrl = "http://localhost:$Port/health"
$healthCheckPassed = $false

try {
    $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 15
    if ($response.StatusCode -eq 200) {
        Write-Host "   Health endpoint: PASSED ($($response.StatusCode))" -ForegroundColor Green

        $content = $response.Content | ConvertFrom-Json
        if ($content.status -eq "Healthy") {
            Write-Host "   Application status: Healthy" -ForegroundColor Green
            $healthCheckPassed = $true

            # Check LibreOffice availability
            if ($content.libreOfficeAvailable -eq $true) {
                Write-Host "   LibreOffice: Available" -ForegroundColor Green
            } else {
                Write-Host "   WARNING: LibreOffice not available" -ForegroundColor Yellow
            }
        }
    }
} catch {
    Write-Host "   ERROR: Health check failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   Check:" -ForegroundColor Yellow
    Write-Host "   - IIS Manager > Sites > $IISSiteName (should be Started)" -ForegroundColor Gray
    Write-Host "   - IIS Manager > Application Pools > $IISAppPoolName (should be Started)" -ForegroundColor Gray
    Write-Host "   - Event Viewer > Application Log" -ForegroundColor Gray
    Write-Host "   - $IISPhysicalPath\logs\stdout*.log" -ForegroundColor Gray
}

# Test API info endpoint
if ($healthCheckPassed) {
    try {
        $apiUrl = "http://localhost:$Port/api/supported-formats"
        $apiResponse = Invoke-WebRequest -Uri $apiUrl -UseBasicParsing -TimeoutSec 10
        if ($apiResponse.StatusCode -eq 200) {
            Write-Host "   API endpoint: PASSED" -ForegroundColor Green
        }
    } catch {
        Write-Host "   WARNING: API endpoint test failed" -ForegroundColor Yellow
    }
}

#endregion

#region Step 15: Display Summary

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Deployment Summary" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "IIS Configuration:" -ForegroundColor Yellow
Write-Host "  Site Name:        $IISSiteName" -ForegroundColor White
Write-Host "  App Pool:         $IISAppPoolName" -ForegroundColor White
Write-Host "  Physical Path:    $IISPhysicalPath" -ForegroundColor White
Write-Host "  HTTP Port:        $Port" -ForegroundColor White
if ($EnableHTTPS) {
    Write-Host "  HTTPS Port:       443" -ForegroundColor White
}
Write-Host ""
Write-Host "Endpoints:" -ForegroundColor Yellow
Write-Host "  Health:           http://localhost:$Port/health" -ForegroundColor Cyan
Write-Host "  API Info:         http://localhost:$Port/api" -ForegroundColor Cyan
Write-Host "  Supported Formats: http://localhost:$Port/api/supported-formats" -ForegroundColor Cyan
Write-Host "  API Docs:         http://localhost:$Port/api-docs" -ForegroundColor Cyan
Write-Host ""
Write-Host "Monitoring:" -ForegroundColor Yellow
Write-Host "  Application Logs: $IISPhysicalPath\App_Data\logs" -ForegroundColor White
Write-Host "  Stdout Logs:      $IISPhysicalPath\logs" -ForegroundColor White
Write-Host "  IIS Logs:         C:\inetpub\logs\LogFiles\W3SVC*" -ForegroundColor White
Write-Host "  Event Viewer:     Application Log > FileConversionApi" -ForegroundColor White
Write-Host ""
Write-Host "Management Commands:" -ForegroundColor Yellow
Write-Host "  IIS Manager:      inetmgr" -ForegroundColor Cyan
Write-Host "  Restart Site:     Restart-WebAppPool '$IISAppPoolName'" -ForegroundColor Cyan
Write-Host "  Stop Site:        Stop-WebSite '$IISSiteName'" -ForegroundColor Cyan
Write-Host "  Start Site:       Start-WebSite '$IISSiteName'" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Review and update appsettings.Production.json:" -ForegroundColor White
Write-Host "     - Security:AllowedOrigins (CORS configuration)" -ForegroundColor Gray
Write-Host "     - Security:IPWhitelist (allowed IP ranges)" -ForegroundColor Gray
Write-Host "     - LibreOffice:MaxConcurrentConversions (based on server capacity)" -ForegroundColor Gray
Write-Host ""
Write-Host "  2. Test file conversion:" -ForegroundColor White
Write-Host "     curl -X POST http://localhost:$Port/api/convert \" -ForegroundColor Gray
Write-Host "          -F file=@test.docx \" -ForegroundColor Gray
Write-Host "          -F targetFormat=pdf \" -ForegroundColor Gray
Write-Host "          -o output.pdf" -ForegroundColor Gray
Write-Host ""
Write-Host "  3. Monitor initial operations in:" -ForegroundColor White
Write-Host "     - IIS Manager > Sites > $IISSiteName > Logging" -ForegroundColor Gray
Write-Host "     - Event Viewer > Windows Logs > Application" -ForegroundColor Gray
Write-Host ""

if ($healthCheckPassed) {
    Write-Host "Deployment completed successfully!" -ForegroundColor Green
} else {
    Write-Host "Deployment completed with warnings - review health check results" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

#endregion
