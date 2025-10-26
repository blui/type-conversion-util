# Create LibreOffice User Profile Template
# This creates a pre-initialized profile to bundle with deployment

param(
    [string]$LibreOfficePath = "FileConversionApi\LibreOffice\program\soffice.exe",
    [string]$OutputPath = "FileConversionApi\libreoffice-profile-template"
)

$ErrorActionPreference = "Stop"

Write-Host "Creating LibreOffice profile template..." -ForegroundColor Cyan
Write-Host ""

# Verify LibreOffice exists
if (!(Test-Path $LibreOfficePath)) {
    Write-Host "ERROR: LibreOffice not found at $LibreOfficePath" -ForegroundColor Red
    Write-Host "Run bundle-libreoffice.ps1 first" -ForegroundColor Yellow
    exit 1
}

# Clean output directory
if (Test-Path $OutputPath) {
    Write-Host "Removing existing template..." -ForegroundColor Yellow
    Remove-Item $OutputPath -Recurse -Force
}

Write-Host "Creating new profile template..." -ForegroundColor Cyan

# Create temp directory for profile initialization
$tempProfile = Join-Path $env:TEMP "libreoffice-template-init-$(Get-Random)"
Write-Host "  Temporary location: $tempProfile" -ForegroundColor Gray

# Convert to file URI
$profileUri = (New-Object Uri $tempProfile).AbsoluteUri

# Initialize profile by running LibreOffice --version
Write-Host "  Initializing profile (this takes 5-10 seconds)..." -ForegroundColor Cyan

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = (Resolve-Path $LibreOfficePath).Path
$psi.Arguments = "--headless --nofirststartwizard -env:UserInstallation=$profileUri --version"
$psi.UseShellExecute = $false
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true

$process = [System.Diagnostics.Process]::Start($psi)
$output = $process.StandardOutput.ReadToEnd()
$error = $process.StandardError.ReadToEnd()
$process.WaitForExit()

if ($process.ExitCode -ne 0) {
    Write-Host "ERROR: Failed to initialize profile" -ForegroundColor Red
    Write-Host "Exit code: $($process.ExitCode)" -ForegroundColor Red
    Write-Host "Output: $output" -ForegroundColor Gray
    Write-Host "Error: $error" -ForegroundColor Red
    exit 1
}

Write-Host "  Profile initialized successfully" -ForegroundColor Green
Write-Host "  Version: $output" -ForegroundColor Gray

# Wait a moment for files to be written
Start-Sleep -Seconds 2

# Verify profile was created
if (!(Test-Path $tempProfile)) {
    Write-Host "ERROR: Profile directory was not created" -ForegroundColor Red
    exit 1
}

# Get profile size
$profileSize = (Get-ChildItem $tempProfile -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host "  Profile size: $([math]::Round($profileSize, 2)) MB" -ForegroundColor Gray

if ($profileSize -lt 1) {
    Write-Host "WARNING: Profile seems too small (< 1MB)" -ForegroundColor Yellow
    Write-Host "Profile may not have initialized correctly" -ForegroundColor Yellow
}

# Copy to output location
Write-Host "  Copying to: $OutputPath" -ForegroundColor Cyan
Copy-Item $tempProfile -Destination $OutputPath -Recurse -Force

# Clean up temp directory
Remove-Item $tempProfile -Recurse -Force

# Verify final template
$finalSize = (Get-ChildItem $OutputPath -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB
$fileCount = (Get-ChildItem $OutputPath -Recurse -File).Count

Write-Host ""
Write-Host "Template created successfully!" -ForegroundColor Green
Write-Host "  Location: $OutputPath" -ForegroundColor White
Write-Host "  Size: $([math]::Round($finalSize, 2)) MB" -ForegroundColor White
Write-Host "  Files: $fileCount" -ForegroundColor White
Write-Host ""
Write-Host "This template will be bundled with deployment and copied for each conversion." -ForegroundColor Gray
Write-Host "Run deploy.ps1 to include it in the deployment package." -ForegroundColor Gray
