# Create LibreOffice User Profile Template
# This creates a minimal pre-initialized profile to bundle with deployment
#
# This script creates a minimal profile structure that LibreOffice can use.
# We don't run LibreOffice to initialize it because:
# 1. Full LibreOffice shows dialog boxes even with --headless
# 2. Bundled LibreOffice is missing files needed for initialization
# 3. A minimal structure is sufficient for conversion operations

param(
    [string]$OutputPath = "FileConversionApi\libreoffice-profile-template"
)

$ErrorActionPreference = "Stop"

Write-Host "Creating LibreOffice profile template..." -ForegroundColor Cyan
Write-Host ""

# Clean output directory if it exists
if (Test-Path $OutputPath) {
    Write-Host "Removing existing template..." -ForegroundColor Yellow
    Remove-Item $OutputPath -Recurse -Force
}

Write-Host "Creating minimal profile structure..." -ForegroundColor Cyan

# Create the minimal directory structure needed by LibreOffice
$directories = @(
    "user\extensions",
    "user\uno_packages\cache",
    "user\config",
    "user\psprint",
    "user\temp",
    "config",
    "cache"
)

foreach ($dir in $directories) {
    $fullPath = Join-Path $OutputPath $dir
    New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
    Write-Host "  Created: $dir" -ForegroundColor Gray
}

# Create buildid file (LibreOffice checks for this)
$buildIdPath = Join-Path $OutputPath "user\extensions\buildid"
# Use a generic buildid that works with any LibreOffice version
"LibreOffice_Portable" | Out-File -FilePath $buildIdPath -Encoding ASCII -NoNewline

Write-Host ""
Write-Host "Creating registrymodifications.xcu (LibreOffice configuration)..." -ForegroundColor Cyan

# Create minimal registrymodifications.xcu
# This is the main configuration file LibreOffice uses
$registryXcu = @"
<?xml version="1.0" encoding="UTF-8"?>
<oor:items xmlns:oor="http://openoffice.org/2001/registry" xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <item oor:path="/org.openoffice.Office.Common/Misc"><prop oor:name="FirstRun" oor:op="fuse"><value>false</value></prop></item>
  <item oor:path="/org.openoffice.Office.Common/Help/Registration"><prop oor:name="DonateURL" oor:op="fuse"><value/></prop></item>
  <item oor:path="/org.openoffice.Setup/Office"><prop oor:name="ooSetupInstCompleted" oor:op="fuse"><value>true</value></prop></item>
  <item oor:path="/org.openoffice.Setup/Product"><prop oor:name="ooSetupLastVersion" oor:op="fuse"><value>24.2</value></prop></item>
</oor:items>
"@

$registryPath = Join-Path $OutputPath "user\registrymodifications.xcu"
$registryXcu | Out-File -FilePath $registryPath -Encoding UTF8

Write-Host "  Created: user\registrymodifications.xcu" -ForegroundColor Gray

# Get final size and file count
$finalFiles = Get-ChildItem $OutputPath -Recurse -File
$finalSize = ($finalFiles | Measure-Object -Property Length -Sum).Sum
$fileCount = $finalFiles.Count
$dirCount = (Get-ChildItem $OutputPath -Recurse -Directory).Count

Write-Host ""
Write-Host "Template created successfully!" -ForegroundColor Green
Write-Host "  Location: $OutputPath" -ForegroundColor White
Write-Host "  Size: $([math]::Round($finalSize / 1KB, 2)) KB" -ForegroundColor White
Write-Host "  Directories: $dirCount" -ForegroundColor White
Write-Host "  Files: $fileCount" -ForegroundColor White
Write-Host ""
Write-Host "This minimal template will be copied per-conversion and used by LibreOffice." -ForegroundColor Gray
Write-Host "LibreOffice will expand it with additional files as needed during conversions." -ForegroundColor Gray
Write-Host ""
Write-Host "Next step: Run .\FileConversionApi\deploy.ps1 to include it in deployment package." -ForegroundColor Cyan
