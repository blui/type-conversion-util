# LibreOffice Bundle Creator for File Conversion API
# Creates minimal bundle with all necessary components for Office document conversion
param(
    [string]$LibreOfficeSource = "C:\Program Files\LibreOffice",
    [string]$OutputPath = "FileConversionApi\LibreOffice",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host "Creating LibreOffice bundle for File Conversion API..." -ForegroundColor Cyan
Write-Host ""

# Check LibreOffice installation
$sofficePath = Join-Path $LibreOfficeSource "program\soffice.exe"
if (!(Test-Path $sofficePath)) {
    Write-Host "ERROR: LibreOffice not found at $LibreOfficeSource" -ForegroundColor Red
    Write-Host "Install LibreOffice from https://www.libreoffice.org/download/" -ForegroundColor Yellow
    exit 1
}

$sourceSize = (Get-ChildItem $LibreOfficeSource -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host "Source: $LibreOfficeSource ($([math]::Round($sourceSize, 1)) MB)" -ForegroundColor White
Write-Host "Output: $OutputPath" -ForegroundColor White
Write-Host ""

# Check existing bundle
if ((Test-Path $OutputPath) -and !$Force) {
    Write-Host "Bundle already exists. Use -Force to recreate." -ForegroundColor Yellow
    exit 0
}

# Clean output
if (Test-Path $OutputPath) {
    Remove-Item $OutputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# Copy program directory
Write-Host "1. Copying program files..." -ForegroundColor Cyan
$programSrc = Join-Path $LibreOfficeSource "program"
$programDest = Join-Path $OutputPath "program"
Copy-Item $programSrc -Destination $programDest -Recurse -Force

# Remove unnecessary components
Write-Host "2. Removing unnecessary files..." -ForegroundColor Cyan

$removeProgramDirs = @(
    "python-core-*",  # Python runtime
    "wizards",        # UI wizards
    "help",           # Help docs
    "readme"          # Readme files
)

$removedSize = 0
foreach ($pattern in $removeProgramDirs) {
    $items = Get-ChildItem $programDest -Directory -Filter $pattern -ErrorAction SilentlyContinue
    foreach ($item in $items) {
        $size = (Get-ChildItem $item.FullName -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB
        Remove-Item $item.FullName -Recurse -Force -ErrorAction SilentlyContinue
        $removedSize += $size
    }
}
Write-Host "  Removed $([math]::Round($removedSize, 1)) MB of unnecessary files" -ForegroundColor Gray

# Copy essential share components
Write-Host "3. Copying essential share files..." -ForegroundColor Cyan
$shareSrc = Join-Path $LibreOfficeSource "share"
$shareDest = Join-Path $OutputPath "share"
New-Item -ItemType Directory -Path $shareDest -Force | Out-Null

# Essential directories for conversion
$essentialShareDirs = @("registry", "config", "filter", "dtd", "xslt")

foreach ($shareDir in $essentialShareDirs) {
    $srcDir = Join-Path $shareSrc $shareDir
    if (Test-Path $srcDir) {
        $destDir = Join-Path $shareDest $shareDir
        Copy-Item $srcDir -Destination $destDir -Recurse -Force
    }
}

# Large directories we skip (templates, samples, help, etc.)
$skippedDirs = @("gallery", "template", "wizards", "Scripts", "samples", "autocorr", "autotext", "wordbook", "extensions", "uno_packages", "help")
$skippedSize = 0

foreach ($dir in $skippedDirs) {
    $srcDir = Join-Path $shareSrc $dir
    if (Test-Path $srcDir) {
        $size = (Get-ChildItem $srcDir -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB
        $skippedSize += $size
    }
}
Write-Host "  Skipped $([math]::Round($skippedSize, 1)) MB of optional files" -ForegroundColor DarkGray

# Remove non-English language packs (saves 50-100 MB)
Write-Host "4. Removing non-English language packs..." -ForegroundColor Cyan
$registryPath = Join-Path $shareDest "registry"
if (Test-Path $registryPath) {
    $langPacksRemoved = 0
    $langPacksSize = 0
    $langPacks = Get-ChildItem $registryPath -Filter "Langpack-*.xcd" -ErrorAction SilentlyContinue

    foreach ($langPack in $langPacks) {
        # Keep English language pack, remove all others
        if ($langPack.Name -notlike "*en-US*" -and $langPack.Name -notlike "*en_US*") {
            $langPacksSize += $langPack.Length / 1MB
            Remove-Item $langPack.FullName -Force -ErrorAction SilentlyContinue
            $langPacksRemoved++
        }
    }

    Write-Host "  Removed $langPacksRemoved language packs ($([math]::Round($langPacksSize, 1)) MB)" -ForegroundColor Gray
}

# Copy Visual C++ Runtime DLLs
Write-Host "5. Bundling Visual C++ Runtime DLLs..." -ForegroundColor Cyan

# Required Visual C++ Redistributable DLLs for LibreOffice
# Include all VC++ 2015-2022 runtime DLLs to ensure complete compatibility
$vcRuntimeDlls = @(
    "msvcp140.dll",                 # C++ Standard Library
    "vcruntime140.dll",             # C Runtime
    "vcruntime140_1.dll",           # Additional C Runtime (exception handling)
    "msvcp140_1.dll",               # C++ Standard Library (additional)
    "msvcp140_2.dll",               # C++ Standard Library (additional)
    "msvcp140_atomic_wait.dll",     # C++ atomic wait operations (C++20)
    "msvcp140_codecvt_ids.dll",     # C++ code conversion
    "concrt140.dll",                # Concurrency Runtime
    "vccorlib140.dll"               # Windows Runtime C++ Library
)

$vcDllsFound = 0
$vcDllsMissing = @()

foreach ($dll in $vcRuntimeDlls) {
    $systemDll = Join-Path $env:SystemRoot "System32\$dll"

    if (Test-Path $systemDll) {
        $destDll = Join-Path $programDest $dll
        Copy-Item $systemDll -Destination $destDll -Force
        $vcDllsFound++
        Write-Host "  Copied $dll" -ForegroundColor Gray
    } else {
        $vcDllsMissing += $dll
    }
}

if ($vcDllsMissing.Count -gt 0) {
    Write-Host "  WARNING: Missing $($vcDllsMissing.Count) DLLs on build machine: $($vcDllsMissing -join ', ')" -ForegroundColor Yellow

    # Check if critical DLLs are missing
    $criticalDlls = @("msvcp140.dll", "vcruntime140.dll", "vcruntime140_1.dll", "msvcp140_atomic_wait.dll")
    $missingCritical = $vcDllsMissing | Where-Object { $criticalDlls -contains $_ }

    if ($missingCritical.Count -gt 0) {
        Write-Host "  ERROR: Critical DLLs missing: $($missingCritical -join ', ')" -ForegroundColor Red
        Write-Host "  LibreOffice will NOT work without these!" -ForegroundColor Red
        Write-Host "  Install Visual C++ Redistributable (2015-2022) on build machine" -ForegroundColor Yellow
        Write-Host "  Download: https://aka.ms/vs/17/release/vc_redist.x64.exe" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "  Continuing anyway, but deployment will require manual DLL installation..." -ForegroundColor Yellow
    } else {
        Write-Host "  Non-critical DLLs missing, bundle should still work" -ForegroundColor Gray
    }
} else {
    Write-Host "  All $vcDllsFound Visual C++ runtime DLLs bundled successfully" -ForegroundColor Green
}

# Verify bundle
Write-Host "6. Verifying bundle..." -ForegroundColor Cyan
$sofficeBundle = Join-Path $programDest "soffice.exe"
if (!(Test-Path $sofficeBundle)) {
    Write-Host "ERROR: soffice.exe not found in bundle!" -ForegroundColor Red
    exit 1
}

# Calculate final size
$finalFiles = Get-ChildItem $OutputPath -Recurse -File
$finalSizeMB = [math]::Round(($finalFiles | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
$reductionPercent = [math]::Round((1 - ($finalSizeMB / $sourceSize)) * 100, 1)

Write-Host ""
Write-Host "Bundle created successfully!" -ForegroundColor Green
Write-Host "Location: $OutputPath" -ForegroundColor White
Write-Host "Size: $finalSizeMB MB ($($finalFiles.Count) files)" -ForegroundColor White
Write-Host "Reduced by: $reductionPercent%" -ForegroundColor Cyan
Write-Host ""
Write-Host "Includes: Core conversion engines, filters, configuration, and Visual C++ runtime" -ForegroundColor White
Write-Host "Removed: UI components, help docs, templates, samples" -ForegroundColor Gray
Write-Host ""
Write-Host "Ready for deployment! (No Visual C++ Redistributable installation required)" -ForegroundColor Green


