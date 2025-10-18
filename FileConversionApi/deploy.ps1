# File Conversion API - Build Deployment Package
# Creates a deployment-ready package that can be manually copied to IIS

param(
    [string]$OutputPath = "deployment",
    [switch]$SkipBuild
)

Write-Host "File Conversion API - Build Deployment Package" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Green
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

    # Clean output directory if it exists
    if (Test-Path $outputPath) {
        Write-Host "  Cleaning existing deployment directory..." -ForegroundColor Cyan
        Remove-Item "$outputPath\*" -Recurse -Force -ErrorAction SilentlyContinue
    } else {
        New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
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

# Main build process
try {
    $fullOutputPath = Join-Path (Get-Location) $OutputPath

    # Publish application if not skipped
    if (!$SkipBuild) {
        Write-Host "1. Publishing .NET application" -ForegroundColor Yellow
        Publish-Application -outputPath $fullOutputPath
    } else {
        Write-Host "1. Skipping application build (-SkipBuild specified)" -ForegroundColor Cyan
    }

    # Create production appsettings.json
    Write-Host "2. Creating production configuration" -ForegroundColor Yellow
    Create-AppSettingsJson -outputPath $fullOutputPath

    # Copy LibreOffice bundle
    Write-Host "3. Copying LibreOffice bundle" -ForegroundColor Yellow
    Copy-LibreOfficeBundle -outputPath $fullOutputPath

    # Create necessary subdirectories
    Write-Host "4. Creating application directories" -ForegroundColor Yellow
    $dirs = @(
        "$fullOutputPath\App_Data\temp\uploads",
        "$fullOutputPath\App_Data\temp\converted",
        "$fullOutputPath\App_Data\temp\libreoffice",
        "$fullOutputPath\App_Data\logs"
    )

    foreach ($dir in $dirs) {
        if (!(Test-Path $dir)) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            Write-Host "   Created: $($dir.Replace($fullOutputPath, '.'))" -ForegroundColor Green
        }
    }

    # Get deployment package size
    $packageSize = (Get-ChildItem $fullOutputPath -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB
    $fileCount = (Get-ChildItem $fullOutputPath -Recurse -File).Count

    Write-Host ""
    Write-Host "DEPLOYMENT PACKAGE CREATED SUCCESSFULLY!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Package Details:" -ForegroundColor Yellow
    Write-Host "  Location: $fullOutputPath" -ForegroundColor White
    Write-Host "  Size: $([math]::Round($packageSize, 2)) MB" -ForegroundColor White
    Write-Host "  Files: $fileCount" -ForegroundColor White
    Write-Host ""
    Write-Host "Manual Deployment Instructions:" -ForegroundColor Yellow
    Write-Host "  1. Copy contents of '$OutputPath' folder to your IIS directory:" -ForegroundColor White
    Write-Host "     Example: C:\inetpub\wwwroot\FileConversionApi\" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  2. Set directory permissions (run as Administrator):" -ForegroundColor White
    Write-Host "     icacls C:\inetpub\wwwroot\FileConversionApi /grant `"IIS_IUSRS:(OI)(CI)F`" /T" -ForegroundColor Gray
    Write-Host "     icacls C:\inetpub\wwwroot\FileConversionApi /grant `"NETWORK SERVICE:(OI)(CI)F`" /T" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  3. Create IIS Application Pool:" -ForegroundColor White
    Write-Host "     - Name: FileConversionApiPool" -ForegroundColor Gray
    Write-Host "     - .NET CLR Version: No Managed Code" -ForegroundColor Gray
    Write-Host "     - Managed Pipeline Mode: Integrated" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  4. Create IIS Website:" -ForegroundColor White
    Write-Host "     - Site Name: FileConversionApi" -ForegroundColor Gray
    Write-Host "     - Physical Path: C:\inetpub\wwwroot\FileConversionApi" -ForegroundColor Gray
    Write-Host "     - Application Pool: FileConversionApiPool" -ForegroundColor Gray
    Write-Host "     - Binding: http://*:80" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  5. Test deployment:" -ForegroundColor White
    Write-Host "     http://localhost/health" -ForegroundColor Gray
    Write-Host ""
    Write-Host "For detailed deployment instructions, see DEPLOYMENT_NOTES.md" -ForegroundColor Cyan
    Write-Host ""

} catch {
    Write-Host "ERROR during package creation: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack Trace: $($_.Exception.StackTrace)" -ForegroundColor Red
    exit 1
}
