# File Conversion API - Build Release Package
# Creates deploy/release folder for manual security scanning and IIS deployment

param(
    [string]$OutputPath = "deploy\release",
    [switch]$SkipBuild
)

Write-Host "Building File Conversion API release package..." -ForegroundColor Green
Write-Host ""

function Copy-Configuration {
    param([string]$outputPath)

    Write-Host "Copying configuration files..." -ForegroundColor Yellow

    $configFiles = @(
        "appsettings.json",
        "appsettings.Production.json",
        "web.config"
    )

    foreach ($file in $configFiles) {
        if (Test-Path $file) {
            Copy-Item $file -Destination "$outputPath\$file" -Force
            Write-Host "   Copied: $file" -ForegroundColor Green
        } else {
            Write-Host "   WARNING: $file not found" -ForegroundColor Yellow
        }
    }
}

function Publish-Application {
    param([string]$outputPath)

    Write-Host "Publishing .NET application..." -ForegroundColor Yellow

    if (!(Test-Path "FileConversionApi.csproj")) {
        Write-Host "ERROR: FileConversionApi.csproj not found. Run from FileConversionApi directory." -ForegroundColor Red
        exit 1
    }

    if (Test-Path $outputPath) {
        Write-Host "  Cleaning existing deployment directory..." -ForegroundColor Cyan
        Remove-Item "$outputPath\*" -Recurse -Force -ErrorAction SilentlyContinue
    } else {
        New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
    }

    $publishResult = dotnet publish -c Release -r win-x64 --self-contained false -o $outputPath 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to publish application" -ForegroundColor Red
        Write-Host $publishResult -ForegroundColor Red
        exit 1
    }

    Write-Host "Application published to: $outputPath" -ForegroundColor Green
}

function Copy-LibreOfficeBundle {
    param([string]$outputPath)

    Write-Host "Copying LibreOffice bundle..." -ForegroundColor Yellow

    $sourcePath = "LibreOffice"

    if (!(Test-Path $sourcePath)) {
        Write-Host "WARNING: LibreOffice bundle not found at $sourcePath" -ForegroundColor Yellow
        Write-Host "Office document conversions will NOT work." -ForegroundColor Yellow
        Write-Host "Run ..\bundle-libreoffice.ps1 to create the bundle." -ForegroundColor Yellow
        return
    }

    $bundleFiles = Get-ChildItem $sourcePath -Recurse -File
    if ($bundleFiles.Count -lt 10) {
        Write-Host "WARNING: LibreOffice bundle appears incomplete ($($bundleFiles.Count) files)" -ForegroundColor Yellow
        Write-Host "Run ..\bundle-libreoffice.ps1 to create a complete bundle." -ForegroundColor Yellow
        return
    }

    $sofficeExe = Join-Path $sourcePath "program\soffice.exe"
    if (!(Test-Path $sofficeExe)) {
        Write-Host "WARNING: soffice.exe not found in bundle" -ForegroundColor Yellow
        Write-Host "Run ..\bundle-libreoffice.ps1 to create a valid bundle." -ForegroundColor Yellow
        return
    }

    $destPath = Join-Path $outputPath $sourcePath
    if (!(Test-Path $destPath)) {
        New-Item -ItemType Directory -Path $destPath -Force | Out-Null
    }

    Write-Host "  Copying $($bundleFiles.Count) files..." -ForegroundColor Cyan
    Copy-Item "$sourcePath\*" -Destination $destPath -Recurse -Force

    Write-Host "LibreOffice bundle copied successfully" -ForegroundColor Green
}

function Copy-ProfileTemplate {
    param([string]$outputPath)

    Write-Host "Copying LibreOffice profile template..." -ForegroundColor Yellow

    $sourcePath = "libreoffice-profile-template"

    if (!(Test-Path $sourcePath)) {
        Write-Host "WARNING: LibreOffice profile template not found at $sourcePath" -ForegroundColor Yellow
        Write-Host "Profile template eliminates initialization issues under IIS." -ForegroundColor Yellow
        Write-Host "Run ..\create-libreoffice-profile-template.ps1 to create it." -ForegroundColor Yellow
        return
    }

    $templateFiles = Get-ChildItem $sourcePath -Recurse -File
    if ($templateFiles.Count -lt 1) {
        Write-Host "WARNING: Profile template appears empty" -ForegroundColor Yellow
        Write-Host "Run ..\create-libreoffice-profile-template.ps1 to create it." -ForegroundColor Yellow
        return
    }

    $destPath = Join-Path $outputPath $sourcePath
    if (!(Test-Path $destPath)) {
        New-Item -ItemType Directory -Path $destPath -Force | Out-Null
    }

    Write-Host "  Copying $($templateFiles.Count) files..." -ForegroundColor Cyan
    Copy-Item "$sourcePath\*" -Destination $destPath -Recurse -Force

    $templateSize = ($templateFiles | Measure-Object -Property Length -Sum).Sum / 1KB
    Write-Host "Profile template copied successfully ($([math]::Round($templateSize, 2)) KB)" -ForegroundColor Green
}

try {
    $fullOutputPath = Join-Path (Get-Location) $OutputPath

    # Publish application
    if (!$SkipBuild) {
        Write-Host "1. Publishing .NET application" -ForegroundColor Yellow
        Publish-Application -outputPath $fullOutputPath
    } else {
        Write-Host "1. Skipping application build (-SkipBuild specified)" -ForegroundColor Cyan
    }

    # Copy configuration
    Write-Host "2. Copying configuration files" -ForegroundColor Yellow
    if (!(Test-Path $fullOutputPath)) {
        New-Item -ItemType Directory -Path $fullOutputPath -Force | Out-Null
    }
    Copy-Configuration -outputPath $fullOutputPath

    # Copy LibreOffice bundle
    Write-Host "3. Copying LibreOffice bundle" -ForegroundColor Yellow
    Copy-LibreOfficeBundle -outputPath $fullOutputPath

    # Copy profile template
    Write-Host "4. Copying LibreOffice profile template" -ForegroundColor Yellow
    Copy-ProfileTemplate -outputPath $fullOutputPath

    # Create application directories
    Write-Host "5. Creating application directories" -ForegroundColor Yellow
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

    # Package summary
    $packageSize = (Get-ChildItem $fullOutputPath -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB
    $fileCount = (Get-ChildItem $fullOutputPath -Recurse -File).Count

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "Release package created successfully" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Location: $fullOutputPath" -ForegroundColor White
    Write-Host "Size:     $([math]::Round($packageSize, 1)) MB ($fileCount files)" -ForegroundColor White
    Write-Host ""
    Write-Host "Deployment Steps:" -ForegroundColor Yellow
    Write-Host "  1. Security scan the '$OutputPath' folder" -ForegroundColor White
    Write-Host "  2. Copy entire folder to server (e.g., D:\inetpub\wwwroot\Service\FileConversionApi)" -ForegroundColor White
    Write-Host "  3. Set permissions (run on server):" -ForegroundColor White
    Write-Host "       icacls ""D:\inetpub\wwwroot\Service\FileConversionApi\App_Data"" /grant ""IIS_IUSRS:(OI)(CI)F"" /T" -ForegroundColor Gray
    Write-Host "       icacls ""D:\inetpub\wwwroot\Service\FileConversionApi\LibreOffice"" /grant ""IIS_IUSRS:(OI)(CI)RX"" /T" -ForegroundColor Gray
    Write-Host "       icacls ""D:\inetpub\wwwroot\Service\FileConversionApi\libreoffice-profile-template"" /grant ""IIS_IUSRS:(OI)(CI)R"" /T" -ForegroundColor Gray
    Write-Host "  4. Configure IIS application pool and site" -ForegroundColor White
    Write-Host "  5. Restart IIS: iisreset" -ForegroundColor White
    Write-Host "  6. Test endpoints:" -ForegroundColor White
    Write-Host "       - Health:   http://localhost/health" -ForegroundColor Cyan
    Write-Host "       - API Docs: http://localhost/api-docs" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Documentation: ..\DEPLOYMENT.md" -ForegroundColor Cyan
    Write-Host ""

} catch {
    Write-Host "ERROR during package creation: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
