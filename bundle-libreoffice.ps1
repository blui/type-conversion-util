# Minimal LibreOffice Runtime Bundler for Headless Document Conversion
# This script creates an optimized bundle (~150-200MB) for server-side Office conversion
param(
    [string]$LibreOfficeSource = "C:\Program Files\LibreOffice",
    [string]$OutputPath = "FileConversionApi\LibreOffice",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "LibreOffice Minimal Bundle Creator" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Verify LibreOffice installation
if (!(Test-Path $LibreOfficeSource)) {
    Write-Host "[ERROR] LibreOffice not found at: $LibreOfficeSource" -ForegroundColor Red
    Write-Host "Please install LibreOffice from: https://www.libreoffice.org/download/`n" -ForegroundColor Yellow
    exit 1
}

$sofficePath = Join-Path $LibreOfficeSource "program\soffice.exe"
if (!(Test-Path $sofficePath)) {
    Write-Host "[ERROR] soffice.exe not found at: $sofficePath" -ForegroundColor Red
    exit 1
}

Write-Host "[INFO] Source: $LibreOfficeSource" -ForegroundColor White
Write-Host "[INFO] Output: $OutputPath" -ForegroundColor White

# Analyze source
$sourceFiles = Get-ChildItem $LibreOfficeSource -Recurse -File -ErrorAction SilentlyContinue
$sourceSizeMB = [math]::Round(($sourceFiles | Measure-Object -Property Length -Sum).Sum / 1MB, 2)
Write-Host "[INFO] Source size: $sourceSizeMB MB ($($sourceFiles.Count) files)`n" -ForegroundColor White

# Check if output exists
if ((Test-Path $OutputPath) -and !$Force) {
    Write-Host "[WARNING] Bundle already exists at: $OutputPath" -ForegroundColor Yellow
    $response = Read-Host "Delete and recreate? (y/N)"
    if ($response -ne 'y') {
        Write-Host "[INFO] Cancelled by user`n" -ForegroundColor Yellow
        exit 0
    }
}

# Clean output directory
if (Test-Path $OutputPath) {
    Write-Host "[INFO] Removing existing bundle..." -ForegroundColor Yellow
    Remove-Item $OutputPath -Recurse -Force
}

New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# Copy essential program directory
Write-Host "`n[STEP 1/4] Copying program directory..." -ForegroundColor Cyan
$programSrc = Join-Path $LibreOfficeSource "program"
$programDest = Join-Path $OutputPath "program"
Copy-Item $programSrc -Destination $programDest -Recurse -Force

# Remove unnecessary components from program directory
Write-Host "[STEP 2/4] Removing unnecessary components..." -ForegroundColor Cyan

$removeProgramDirs = @(
    "python-core-*",           # Python runtime (not needed for headless)
    "wizards",                 # UI wizards
    "help",                    # Help documentation
    "readme"                   # Readme files
)

$removedSize = 0
foreach ($pattern in $removeProgramDirs) {
    $items = Get-ChildItem $programDest -Directory -Filter $pattern -ErrorAction SilentlyContinue
    foreach ($item in $items) {
        $size = (Get-ChildItem $item.FullName -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB
        Remove-Item $item.FullName -Recurse -Force -ErrorAction SilentlyContinue
        $removedSize += $size
        Write-Host "  Removed: $($item.Name) ($([math]::Round($size, 1)) MB)" -ForegroundColor Gray
    }
}

# Copy minimal share components
Write-Host "[STEP 3/4] Copying essential share components..." -ForegroundColor Cyan
$shareSrc = Join-Path $LibreOfficeSource "share"
$shareDest = Join-Path $OutputPath "share"
New-Item -ItemType Directory -Path $shareDest -Force | Out-Null

# Essential share directories for conversion
$essentialShareDirs = @(
    "registry",        # Configuration registry (REQUIRED)
    "config",          # LibreOffice config (REQUIRED)
    "filter",          # Import/export filters (REQUIRED)
    "dtd",             # XML DTDs for filters
    "xslt"             # XSLT transformations for filters
)

foreach ($shareDir in $essentialShareDirs) {
    $srcDir = Join-Path $shareSrc $shareDir
    if (Test-Path $srcDir) {
        $destDir = Join-Path $shareDest $shareDir
        Copy-Item $srcDir -Destination $destDir -Recurse -Force
        Write-Host "  Copied: $shareDir" -ForegroundColor Gray
    }
}

# Remove large unnecessary share directories
$removeShareDirs = @(
    "gallery",         # Clip art gallery
    "template",        # Document templates  
    "wizards",         # Document wizards
    "Scripts",         # Script macros
    "samples",         # Sample documents
    "autocorr",        # Auto-correct files
    "autotext",        # Auto-text entries
    "wordbook",        # Dictionaries (if not needed)
    "extensions",      # Extensions
    "uno_packages"     # UNO packages
)

foreach ($dir in $removeShareDirs) {
    $srcDir = Join-Path $shareSrc $dir
    if (Test-Path $srcDir) {
        $size = (Get-ChildItem $srcDir -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB
        Write-Host "  Skipped: $dir ($([math]::Round($size, 1)) MB)" -ForegroundColor DarkGray
    }
}

# Skip help directory entirely
if (Test-Path (Join-Path $LibreOfficeSource "help")) {
    $helpSize = (Get-ChildItem (Join-Path $LibreOfficeSource "help") -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB
    Write-Host "  Skipped: help ($([math]::Round($helpSize, 1)) MB)" -ForegroundColor DarkGray
}

# Verify soffice.exe was copied
Write-Host "`n[STEP 4/4] Verifying bundle..." -ForegroundColor Cyan
$sofficeBundle = Join-Path $programDest "soffice.exe"
if (!(Test-Path $sofficeBundle)) {
    Write-Host "[ERROR] soffice.exe not found in bundle!" -ForegroundColor Red
    exit 1
}

# Calculate final size
$finalFiles = Get-ChildItem $OutputPath -Recurse -File -ErrorAction SilentlyContinue
$finalSizeMB = [math]::Round(($finalFiles | Measure-Object -Property Length -Sum).Sum / 1MB, 2)
$reductionPercent = [math]::Round((1 - ($finalSizeMB / $sourceSizeMB)) * 100, 1)

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "Bundle Created Successfully" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "Location: $OutputPath" -ForegroundColor White
Write-Host "Size: $finalSizeMB MB ($($finalFiles.Count) files)" -ForegroundColor White
Write-Host "Original: $sourceSizeMB MB" -ForegroundColor Gray
Write-Host "Reduction: $reductionPercent%`n" -ForegroundColor Cyan

Write-Host "This bundle includes:" -ForegroundColor Yellow
Write-Host "  - Core conversion engines (DOC, DOCX, PDF, XLS, PPT)" -ForegroundColor White
Write-Host "  - Import/export filters" -ForegroundColor White
Write-Host "  - Configuration and registry" -ForegroundColor White
Write-Host "`nRemoved (not needed for headless conversion):" -ForegroundColor Yellow
Write-Host "  - Python runtime, UI wizards, help docs" -ForegroundColor Gray
Write-Host "  - Templates, galleries, samples" -ForegroundColor Gray
Write-Host "  - Extensions and extra packages`n" -ForegroundColor Gray

Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Run: dotnet build FileConversionApi/FileConversionApi.csproj" -ForegroundColor White
Write-Host "2. Test conversions with the API`n" -ForegroundColor White

