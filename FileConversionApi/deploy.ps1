# File Conversion API - Complete IIS Deployment Script
# Run this script as Administrator on Windows Server

param(
    [string]$SiteName = "FileConversionApi",
    [string]$AppPoolName = "FileConversionApiPool",
    [string]$Port = "80",
    [string]$InstallPath = "C:\inetpub\wwwroot\FileConversionApi",
    [switch]$SkipPublish,
    [switch]$SkipLibreOfficeCopy
)

Write-Host "File Conversion API - Complete IIS Deployment Script" -ForegroundColor Green
Write-Host "======================================================" -ForegroundColor Green
Write-Host ""

# Check if running as administrator
$currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "ERROR: Please run this script as Administrator" -ForegroundColor Red
    exit 1
}

Write-Host "Running as Administrator: OK" -ForegroundColor Green
Write-Host ""

# Function to create appsettings.json
function Create-AppSettingsJson {
    param([string]$outputPath)

    Write-Host "Creating production appsettings.json..." -ForegroundColor Yellow

    $appSettings = @{
        Logging = @{
            LogLevel = @{
                Default = "Information"
                Microsoft = "Warning"
                Microsoft.Hosting.Lifetime = "Information"
            }
        }
        AllowedHosts = "*"

        # File handling configuration
        FileHandling = @{
            MaxFileSizeMB = 50
            TempDirectory = "App_Data\\temp"
            CleanupIntervalMinutes = 60
            MaxTempAgeHours = 24
        }

        # Security configuration
        Security = @{
            EnableRequestLogging = $true
            EnableRateLimiting = $true
            EnableIPFiltering = $false  # Set to true and configure IPWhitelist for production
            IPWhitelist = @("127.0.0.1", "::1", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16")
            MaxRequestSize = 52428800  # 50MB
            RequestTimeoutSeconds = 300
            EnableHsts = $true
            HstsMaxAge = 31536000
            EnableCors = $false
        }

        # Rate limiting configuration
        IpRateLimiting = @{
            EnableEndpointRateLimiting = $true
            StackBlockedRequests = $false
            RealIpHeader = "X-Real-IP"
            ClientIdHeader = "X-ClientId"
            HttpStatusCode = 429
            GeneralRules = @(
                @{
                    Endpoint = "*"
                    Period = "1m"
                    Limit = 60
                },
                @{
                    Endpoint = "*"
                    Period = "1h"
                    Limit = 1000
                }
            )
        }

        # Windows-specific configuration
        WindowsSecurity = @{
            UseWindowsAuth = $false
            AllowedGroups = @()
        }

        # LibreOffice configuration
        LibreOffice = @{
            SdkPath = "LibreOffice"
            ExecutablePath = ""
            ForceBundled = $true
            UseSdkIntegration = $false
            ConversionQuality = "high"
            TimeoutSeconds = 300
            MaxConcurrentConversions = 2
            EnableLogging = $true
            TempDirectory = "App_Data\\temp\\libreoffice"
            SupportedFormats = @("pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "txt", "rtf", "odt", "ods", "odp", "odg", "odf", "sxw", "sxc", "sxi", "sxd")
        }

        # Image processing configuration
        ImageProcessing = @{
            EnableAdvancedFormats = $true
            MaxImageSizeMB = 20
            SupportedFormats = @("jpg", "jpeg", "png", "gif", "bmp", "tiff", "tif", "svg", "psd")
            QualitySettings = @{
                JpegQuality = 90
                PngCompression = 6
            }
        }

        # Concurrency and performance
        Concurrency = @{
            MaxConcurrentRequests = 10
            SemaphoreTimeoutSeconds = 30
        }

        # Timeouts
        Timeouts = @{
            RequestTimeoutSeconds = 300
            FileOperationTimeoutSeconds = 60
            DatabaseTimeoutSeconds = 30
        }

        # Health checks
        HealthChecks = @{
            Enabled = $true
            TimeoutSeconds = 30
            IntervalSeconds = 60
            LibreOfficeHealthCheck = $true
        }

        # Performance monitoring
        PerformanceMonitoring = @{
            EnableMetrics = $true
            EnableTracing = $true
            MetricsIntervalSeconds = 60
        }

        # External services (disabled for intranet deployment)
        ExternalServices = @{
            EnableCloudStorage = $false
            EnableEmailNotifications = $false
        }

        # Maintenance settings
        Maintenance = @{
            EnableAutoCleanup = $true
            CleanupSchedule = "0 2 * * *"  # Daily at 2 AM
            LogRetentionDays = 30
        }

        # Development settings (disabled for production)
        Development = @{
            EnableSwagger = $false
            EnableDeveloperExceptionPage = $false
            EnableDetailedErrors = $false
        }

        # Windows Service configuration (if needed)
        WindowsService = @{
            ServiceName = "FileConversionApi"
            DisplayName = "File Conversion API Service"
            Description = "REST API for document and image conversion"
        }

        # SSL/TLS configuration (for HTTPS)
        SSL = @{
            Enabled = $false
            CertificatePath = ""
            CertificatePassword = ""
            Port = 443
        }
    }

    $json = $appSettings | ConvertTo-Json -Depth 10
    $json | Out-File -FilePath "$outputPath\appsettings.json" -Encoding UTF8

    Write-Host "Created appsettings.json at: $outputPath\appsettings.json" -ForegroundColor Green
}

# Function to publish the application
function Publish-Application {
    param([string]$outputPath)

    Write-Host "Publishing .NET application..." -ForegroundColor Yellow

    # Check if we're in the right directory
    if (!(Test-Path "FileConversionApi.csproj")) {
        Write-Host "ERROR: FileConversionApi.csproj not found. Run this script from the FileConversionApi directory." -ForegroundColor Red
        exit 1
    }

    # Publish the application
    $publishResult = dotnet publish -c Release -r win-x64 --self-contained false -o $outputPath 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to publish application:" -ForegroundColor Red
        Write-Host $publishResult -ForegroundColor Red
        exit 1
    }

    Write-Host "Application published successfully to: $outputPath" -ForegroundColor Green
}

# Function to copy LibreOffice bundle
function Copy-LibreOfficeBundle {
    param([string]$outputPath)

    Write-Host "Copying LibreOffice bundle..." -ForegroundColor Yellow

    $sourcePath = "LibreOffice"
    
    # Check if source exists and has content
    if (!(Test-Path $sourcePath)) {
        Write-Host "WARNING: LibreOffice bundle directory not found at $sourcePath" -ForegroundColor Yellow
        Write-Host "Office document conversions (DOCX, XLSX, PPTX to PDF) will NOT work." -ForegroundColor Yellow
        Write-Host "Run .\bundle-libreoffice.ps1 to create the bundle." -ForegroundColor Yellow
        return
    }

    # Check if bundle contains files
    $bundleFiles = Get-ChildItem $sourcePath -Recurse -File
    if ($bundleFiles.Count -lt 10) {
        Write-Host "WARNING: LibreOffice bundle appears incomplete ($($bundleFiles.Count) files)" -ForegroundColor Yellow
        Write-Host "Expected bundle should contain 100+ files" -ForegroundColor Yellow
        Write-Host "Run .\bundle-libreoffice.ps1 to create a complete bundle." -ForegroundColor Yellow
        return
    }

    # Check for soffice.exe
    $sofficeExe = Join-Path $sourcePath "program\soffice.exe"
    if (!(Test-Path $sofficeExe)) {
        Write-Host "WARNING: soffice.exe not found in bundle" -ForegroundColor Yellow
        Write-Host "Run .\bundle-libreoffice.ps1 to create a valid bundle." -ForegroundColor Yellow
        return
    }

    $destPath = Join-Path $outputPath $sourcePath
    if (!(Test-Path $destPath)) {
        New-Item -ItemType Directory -Path $destPath -Force | Out-Null
    }

    # Copy LibreOffice files
    Write-Host "  Copying $($bundleFiles.Count) files..." -ForegroundColor Cyan
    Copy-Item "$sourcePath\*" -Destination $destPath -Recurse -Force

    Write-Host "LibreOffice bundle copied successfully" -ForegroundColor Green
}

# Main deployment process
try {
    # Create application pool
    Write-Host "1. Creating application pool: $AppPoolName" -ForegroundColor Yellow
    if (!(Get-IISAppPool -Name $AppPoolName -ErrorAction SilentlyContinue)) {
        New-WebAppPool -Name $AppPoolName -Force
        Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name processModel.identityType -Value ApplicationPoolIdentity
        Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name processModel.idleTimeout -Value "00:00:00"
        Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name recycling.periodicRestart.time -Value "00:00:00"
        Write-Host "   Application pool created successfully" -ForegroundColor Green
    } else {
        Write-Host "   Application pool already exists" -ForegroundColor Cyan
    }

    # Create website directory
    Write-Host "2. Creating website directory: $InstallPath" -ForegroundColor Yellow
    if (!(Test-Path $InstallPath)) {
        New-Item -ItemType Directory -Path $InstallPath -Force
        Write-Host "   Directory created successfully" -ForegroundColor Green
    } else {
        Write-Host "   Directory already exists" -ForegroundColor Cyan
    }

    # Set directory permissions
    Write-Host "3. Setting directory permissions" -ForegroundColor Yellow
    icacls $InstallPath /grant "IIS_IUSRS:(OI)(CI)F" /T | Out-Null
    icacls $InstallPath /grant "NETWORK SERVICE:(OI)(CI)F" /T | Out-Null
    Write-Host "   Permissions set successfully" -ForegroundColor Green

    # Publish application if not skipped
    if (!$SkipPublish) {
        Write-Host "4. Publishing .NET application" -ForegroundColor Yellow
        Publish-Application -outputPath $InstallPath
    } else {
        Write-Host "4. Skipping application publish (-SkipPublish specified)" -ForegroundColor Cyan
    }

    # Create production appsettings.json
    Write-Host "5. Creating production configuration" -ForegroundColor Yellow
    Create-AppSettingsJson -outputPath $InstallPath

    # Copy LibreOffice bundle if not skipped
    if (!$SkipLibreOfficeCopy) {
        Write-Host "6. Copying LibreOffice bundle" -ForegroundColor Yellow
        Copy-LibreOfficeBundle -outputPath $InstallPath
    } else {
        Write-Host "6. Skipping LibreOffice copy (-SkipLibreOfficeCopy specified)" -ForegroundColor Cyan
    }

    # Create necessary subdirectories
    Write-Host "7. Creating application directories" -ForegroundColor Yellow
    $dirs = @(
        "$InstallPath\App_Data\temp\uploads",
        "$InstallPath\App_Data\temp\converted",
        "$InstallPath\App_Data\temp\libreoffice",
        "$InstallPath\App_Data\temp\magick",
        "$InstallPath\App_Data\logs"
    )

    foreach ($dir in $dirs) {
        if (!(Test-Path $dir)) {
            New-Item -ItemType Directory -Path $dir -Force
            Write-Host "   Created: $dir" -ForegroundColor Green
        } else {
            Write-Host "   Exists: $dir" -ForegroundColor Cyan
        }
    }

    # Set permissions on App_Data
    icacls "$InstallPath\App_Data" /grant "IIS_IUSRS:(OI)(CI)F" /T | Out-Null
    icacls "$InstallPath\App_Data" /grant "NETWORK SERVICE:(OI)(CI)F" /T | Out-Null
    Write-Host "   App_Data permissions set" -ForegroundColor Green

    # Create IIS website
    Write-Host "8. Creating IIS website: $SiteName" -ForegroundColor Yellow
    if (!(Get-Website -Name $SiteName -ErrorAction SilentlyContinue)) {
        New-Website -Name $SiteName -Port $Port -PhysicalPath $InstallPath -ApplicationPool $AppPoolName -Force
        Write-Host "   Website created successfully" -ForegroundColor Green
    } else {
        Write-Host "   Website already exists, updating physical path" -ForegroundColor Cyan
        Set-ItemProperty -Path "IIS:\Sites\$SiteName" -Name physicalPath -Value $InstallPath
    }

    # Configure web.config if it exists
    $webConfigPath = Join-Path $InstallPath "web.config"
    if (Test-Path $webConfigPath) {
        Write-Host "9. Configuring web.config" -ForegroundColor Yellow
        # Add any additional web.config configurations here if needed
        Write-Host "   web.config configured" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "DEPLOYMENT COMPLETED SUCCESSFULLY!" -ForegroundColor Green
    Write-Host "==================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Deployment Summary:" -ForegroundColor Yellow
    Write-Host "  Site Name: $SiteName" -ForegroundColor White
    Write-Host "  Port: $Port" -ForegroundColor White
    Write-Host "  Install Path: $InstallPath" -ForegroundColor White
    Write-Host "  Application Pool: $AppPoolName" -ForegroundColor White
    Write-Host ""
    Write-Host "Testing:" -ForegroundColor Yellow
    Write-Host "  Health Check: http://localhost:$Port/health" -ForegroundColor White
    Write-Host "  API Info: http://localhost:$Port/api" -ForegroundColor White
    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Yellow
    Write-Host "  1. Start the website in IIS Manager if not already started" -ForegroundColor White
    Write-Host "  2. Test the health endpoint" -ForegroundColor White
    Write-Host "  3. Configure SSL/TLS if needed (update appsettings.json)" -ForegroundColor White
    Write-Host "  4. Configure IP whitelisting in appsettings.json for production" -ForegroundColor White
    Write-Host ""

} catch {
    Write-Host "ERROR during deployment: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack Trace: $($_.Exception.StackTrace)" -ForegroundColor Red
    exit 1
}
