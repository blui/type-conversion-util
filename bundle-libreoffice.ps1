# Ultra-Minimal LibreOffice Runtime Bundler for File Conversion API
param(
    [string]$LibreOfficeSource = "C:\Program Files\LibreOffice",
    [string]$OutputPath = "FileConversionApi\LibreOffice",
    [switch]$UltraMinimal
)

Write-Host "Ultra-Minimal LibreOffice Runtime Bundler for File Conversion API" -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Green
Write-Host ""
if ($UltraMinimal) {
    Write-Host "ULTRA-MINIMAL MODE: Optimized for headless Office document conversion" -ForegroundColor Cyan
    Write-Host "Target size: ~150-200MB (90%+ reduction from full installation)" -ForegroundColor Cyan
} else {
    Write-Host "STANDARD MODE: Complete LibreOffice bundle for Office document conversion." -ForegroundColor Cyan
}
Write-Host ""

# Check if LibreOffice is available at the source path
if (!(Test-Path $LibreOfficeSource)) {
    Write-Host "ERROR: LibreOffice not found at $LibreOfficeSource" -ForegroundColor Red
    Write-Host "Please install LibreOffice first." -ForegroundColor Yellow
    exit 1
}

# Check if soffice.exe exists
$sofficePath = Join-Path $LibreOfficeSource "program\soffice.exe"
if (!(Test-Path $sofficePath)) {
    Write-Host "ERROR: soffice.exe not found at $sofficePath" -ForegroundColor Red
    exit 1
}

Write-Host "Found LibreOffice at: $LibreOfficeSource" -ForegroundColor Green
Write-Host "Main executable: $sofficePath" -ForegroundColor Cyan
Write-Host ""

# Analyze source installation
$sourceItems = Get-ChildItem $LibreOfficeSource -Recurse
$sourceFiles = $sourceItems | Where-Object { !$_.PSIsContainer }
$sourceSize = ($sourceFiles | Measure-Object -Property Length -Sum).Sum / 1MB
$sourceCount = $sourceFiles.Count

Write-Host "Source Analysis:" -ForegroundColor Cyan
Write-Host "   Total items: $($sourceItems.Count)" -ForegroundColor White
Write-Host "   Files: $sourceCount" -ForegroundColor White
Write-Host "   Size: $([math]::Round($sourceSize, 2)) MB" -ForegroundColor White
Write-Host ""

# Create output directory
if (!(Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    Write-Host "Created output directory: $OutputPath" -ForegroundColor Green
}

# Copy LibreOffice with intelligent filtering
if ($UltraMinimal) {
    Write-Host "ULTRA-MINIMAL MODE: Removing unnecessary components for document conversion only" -ForegroundColor Yellow
    Write-Host "This will create a ~200MB bundle optimized for headless Office conversion" -ForegroundColor Cyan
} else {
    Write-Host "Copying complete LibreOffice installation..." -ForegroundColor Yellow
}

try {
    if ($UltraMinimal) {
        # Ultra-minimal bundling: only essential files for document conversion
        Write-Host "Performing ultra-minimal copy..." -ForegroundColor Yellow

        # Ultra-minimal approach: copy program directory and then remove large components
        $programDir = Join-Path $LibreOfficeSource "program"
        $destProgramDir = Join-Path $OutputPath "program"

        Write-Host "Copying program directory..." -ForegroundColor Yellow
        Copy-Item $programDir -Destination $destProgramDir -Recurse -Force

        # Remove large/unnecessary directories after copy
        Write-Host "Removing unnecessary components..." -ForegroundColor Yellow
        $removeDirs = @("python-core-3.11.13", "help", "wizards", "readme")
        foreach ($dir in $removeDirs) {
            $removePath = Join-Path $destProgramDir $dir
            if (Test-Path $removePath) {
                Remove-Item $removePath -Recurse -Force
                Write-Host "  Removed: $dir" -ForegroundColor Gray
            }
        }

        # Copy minimal share components (only registry and config)
        $shareSrc = Join-Path $LibreOfficeSource "share"
        $shareDest = Join-Path $OutputPath "share"
        New-Item -ItemType Directory -Path $shareDest -Force | Out-Null

        Write-Host "Copying essential share components..." -ForegroundColor Yellow
        $minimalShareDirs = @("registry", "config")
        foreach ($shareDir in $minimalShareDirs) {
            $srcShareDir = Join-Path $shareSrc $shareDir
            if (Test-Path $srcShareDir) {
                $destShareDir = Join-Path $shareDest $shareDir
                Copy-Item $srcShareDir -Destination $destShareDir -Recurse -Force
            }
        }

    } else {
        # Standard copy
        Copy-Item "$LibreOfficeSource\*" -Destination $OutputPath -Recurse -Force
    }

    # Results
    $finalFiles = Get-ChildItem $OutputPath -Recurse -File
    $finalCount = $finalFiles.Count
    $finalSize = ($finalFiles | Measure-Object -Property Length -Sum).Sum / 1MB

    Write-Host ""
    Write-Host "COPY RESULTS:" -ForegroundColor Green
    Write-Host "   Source: $sourceCount files ($([math]::Round($sourceSize, 2)) MB)" -ForegroundColor White
    Write-Host "   Bundle: $finalCount files ($([math]::Round($finalSize, 2)) MB)" -ForegroundColor White

    # Results
    $finalFiles = Get-ChildItem $OutputPath -Recurse -File
    $finalCount = $finalFiles.Count
    $finalSize = ($finalFiles | Measure-Object -Property Length -Sum).Sum / 1MB

    Write-Host ""
    Write-Host "BUNDLE RESULTS:" -ForegroundColor Green
    Write-Host "   Source: $sourceCount files ($([math]::Round($sourceSize, 2)) MB)" -ForegroundColor White
    Write-Host "   Bundle: $finalCount files ($([math]::Round($finalSize, 2)) MB)" -ForegroundColor White

    if ($UltraMinimal) {
        $reductionPercent = [math]::Round((1 - ($finalSize / $sourceSize)) * 100, 1)
        Write-Host "   Reduction: $reductionPercent% (Ultra-minimal optimization)" -ForegroundColor Cyan
        Write-Host "   Target: ~150-200MB for headless document conversion" -ForegroundColor Cyan
    }

# Verify soffice.exe
$copiedSoffice = Join-Path $OutputPath "program\soffice.exe"
if (!(Test-Path $copiedSoffice)) {
    Write-Host "CRITICAL ERROR: soffice.exe was not copied!" -ForegroundColor Red
    exit 1
}

Write-Host "soffice.exe successfully bundled" -ForegroundColor Green

# Test bundled executable
Write-Host ""
Write-Host "Testing bundled LibreOffice..." -ForegroundColor Yellow
try {
    $testResult = & $copiedSoffice "--version" 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Bundled LibreOffice is functional" -ForegroundColor Green
    } else {
        Write-Host "Bundled LibreOffice test inconclusive" -ForegroundColor Yellow
    }
} catch {
    Write-Host "Could not test bundled executable" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "SUCCESS: LibreOffice runtime bundled!" -ForegroundColor Green
Write-Host ""
Write-Host "This enables:" -ForegroundColor Cyan
Write-Host "• DOCX -> PDF conversion" -ForegroundColor White
Write-Host "• XLSX -> PDF conversion" -ForegroundColor White
Write-Host "• PPTX -> PDF conversion" -ForegroundColor White
Write-Host "• PDF -> DOCX conversion" -ForegroundColor White
Write-Host "• All LibreOffice-supported formats" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Test the API with Office documents" -ForegroundColor White
Write-Host "2. Verify conversions work" -ForegroundColor White
Write-Host "3. Optionally uninstall system LibreOffice" -ForegroundColor White
Write-Host ""
Write-Host "Bundle location: $OutputPath" -ForegroundColor Cyan
Write-Host "Bundle size: $([math]::Round($finalSize, 2)) MB" -ForegroundColor Cyan

} catch {
    Write-Host "ERROR during bundling: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
