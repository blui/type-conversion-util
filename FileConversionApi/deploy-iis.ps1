# Automated IIS Deployment Script for File Conversion API
# Requires Administrator privileges

param(
    [Parameter(Mandatory=$false)]
    [string]$IISSiteName = "FileConversionApi",

    [string]$IISAppPoolName = "FileConversionApiPool",
    [string]$IISPhysicalPath = "C:\inetpub\file-conversion-api",
    [int]$Port = 80,
    [switch]$EnableHTTPS,
    [string]$CertificateThumbprint = ""
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  File Conversion API - IIS Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (!$isAdmin) {
    Write-Host "ERROR: This script must be run as Administrator" -ForegroundColor Red
    Write-Host "Right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    exit 1
}

# Check if IIS is installed
Write-Host "1. Checking prerequisites..." -ForegroundColor Yellow
try {
    Import-Module WebAdministration -ErrorAction Stop
    Write-Host "   IIS module loaded successfully" -ForegroundColor Green
} catch {
    Write-Host "   ERROR: IIS is not installed or WebAdministration module is not available" -ForegroundColor Red
    Write-Host "   Install IIS using: Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole" -ForegroundColor Yellow
    exit 1
}

# Build deployment package
Write-Host ""
Write-Host "2. Building deployment package..." -ForegroundColor Yellow
if (Test-Path "deploy.ps1") {
    .\deploy.ps1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "   ERROR: Deployment package build failed" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "   ERROR: deploy.ps1 not found. Run from FileConversionApi directory" -ForegroundColor Red
    exit 1
}

if (!(Test-Path "deploy\release")) {
    Write-Host "   ERROR: Deployment package not found at deploy\release" -ForegroundColor Red
    exit 1
}

Write-Host "   Deployment package ready" -ForegroundColor Green

# Stop existing site and pool if they exist
Write-Host ""
Write-Host "3. Stopping existing IIS resources..." -ForegroundColor Yellow
if (Test-Path "IIS:\Sites\$IISSiteName") {
    Write-Host "   Stopping existing website: $IISSiteName" -ForegroundColor Gray
    Stop-WebSite -Name $IISSiteName -ErrorAction SilentlyContinue
}

if (Test-Path "IIS:\AppPools\$IISAppPoolName") {
    Write-Host "   Stopping existing app pool: $IISAppPoolName" -ForegroundColor Gray
    Stop-WebAppPool -Name $IISAppPoolName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

# Deploy files
Write-Host ""
Write-Host "4. Deploying application files..." -ForegroundColor Yellow
if (!(Test-Path $IISPhysicalPath)) {
    New-Item -ItemType Directory -Path $IISPhysicalPath -Force | Out-Null
    Write-Host "   Created directory: $IISPhysicalPath" -ForegroundColor Gray
}

Write-Host "   Copying files to $IISPhysicalPath..." -ForegroundColor Gray
Copy-Item "deploy\release\*" -Destination $IISPhysicalPath -Recurse -Force

$fileCount = (Get-ChildItem $IISPhysicalPath -Recurse -File).Count
Write-Host "   Deployed $fileCount files" -ForegroundColor Green

# Configure App Pool
Write-Host ""
Write-Host "5. Configuring IIS Application Pool..." -ForegroundColor Yellow
if (!(Test-Path "IIS:\AppPools\$IISAppPoolName")) {
    Write-Host "   Creating new app pool: $IISAppPoolName" -ForegroundColor Gray
    New-WebAppPool -Name $IISAppPoolName
}

Set-ItemProperty "IIS:\AppPools\$IISAppPoolName" -Name managedRuntimeVersion -Value ""
Set-ItemProperty "IIS:\AppPools\$IISAppPoolName" -Name processModel.identityType -Value "ApplicationPoolIdentity"
Set-ItemProperty "IIS:\AppPools\$IISAppPoolName" -Name enable32BitAppOnWin64 -Value $false
Write-Host "   App pool configured (No Managed Code, ApplicationPoolIdentity)" -ForegroundColor Green

# Configure Website
Write-Host ""
Write-Host "6. Configuring IIS Website..." -ForegroundColor Yellow
if (!(Test-Path "IIS:\Sites\$IISSiteName")) {
    Write-Host "   Creating new website: $IISSiteName" -ForegroundColor Gray
    New-WebSite -Name $IISSiteName -PhysicalPath $IISPhysicalPath -ApplicationPool $IISAppPoolName -Port $Port
} else {
    Write-Host "   Updating existing website: $IISSiteName" -ForegroundColor Gray
    Set-ItemProperty "IIS:\Sites\$IISSiteName" -Name physicalPath -Value $IISPhysicalPath
    Set-ItemProperty "IIS:\Sites\$IISSiteName" -Name applicationPool -Value $IISAppPoolName
}

# Configure HTTPS if requested
if ($EnableHTTPS -and ![string]::IsNullOrEmpty($CertificateThumbprint)) {
    Write-Host "   Configuring HTTPS binding with certificate $CertificateThumbprint" -ForegroundColor Gray
    $binding = Get-WebBinding -Name $IISSiteName -Protocol "https" -Port 443
    if ($null -eq $binding) {
        New-WebBinding -Name $IISSiteName -Protocol https -Port 443
    }

    $cert = Get-ChildItem -Path "Cert:\LocalMachine\My" | Where-Object { $_.Thumbprint -eq $CertificateThumbprint }
    if ($null -ne $cert) {
        $binding = Get-WebBinding -Name $IISSiteName -Protocol "https" -Port 443
        $binding.AddSslCertificate($CertificateThumbprint, "my")
        Write-Host "   HTTPS binding configured" -ForegroundColor Green
    } else {
        Write-Host "   WARNING: Certificate not found with thumbprint $CertificateThumbprint" -ForegroundColor Yellow
    }
}

Write-Host "   Website configured on port $Port" -ForegroundColor Green

# Set permissions
Write-Host ""
Write-Host "7. Setting file permissions..." -ForegroundColor Yellow
$appDataPath = Join-Path $IISPhysicalPath "App_Data"
if (Test-Path $appDataPath) {
    icacls "$appDataPath" /grant "IIS_IUSRS:(OI)(CI)F" /T /Q
    Write-Host "   Granted IIS_IUSRS full control to App_Data" -ForegroundColor Green
} else {
    Write-Host "   WARNING: App_Data directory not found" -ForegroundColor Yellow
}

# Start services
Write-Host ""
Write-Host "8. Starting IIS services..." -ForegroundColor Yellow
Start-WebAppPool -Name $IISAppPoolName
Write-Host "   App pool started: $IISAppPoolName" -ForegroundColor Gray

Start-WebSite -Name $IISSiteName
Write-Host "   Website started: $IISSiteName" -ForegroundColor Gray

# Wait for startup
Write-Host "   Waiting for application to start..." -ForegroundColor Gray
Start-Sleep -Seconds 5

# Verify deployment
Write-Host ""
Write-Host "9. Verifying deployment..." -ForegroundColor Yellow
$healthUrl = "http://localhost:$Port/health"
try {
    $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 10
    if ($response.StatusCode -eq 200) {
        Write-Host "   Health check PASSED ($($response.StatusCode))" -ForegroundColor Green

        $content = $response.Content | ConvertFrom-Json
        if ($content.status -eq "Healthy") {
            Write-Host "   Application status: Healthy" -ForegroundColor Green
        }
    }
} catch {
    Write-Host "   Health check FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   Check IIS logs and Windows Event Viewer for details" -ForegroundColor Yellow
}

# Display deployment summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Deployment Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Site Name:       $IISSiteName" -ForegroundColor White
Write-Host "App Pool:        $IISAppPoolName" -ForegroundColor White
Write-Host "Physical Path:   $IISPhysicalPath" -ForegroundColor White
Write-Host "HTTP Port:       $Port" -ForegroundColor White
if ($EnableHTTPS) {
    Write-Host "HTTPS Port:      443" -ForegroundColor White
}
Write-Host ""
Write-Host "Endpoints:" -ForegroundColor Yellow
Write-Host "  Health:   http://localhost:$Port/health" -ForegroundColor Cyan
Write-Host "  API Docs: http://localhost:$Port/api-docs" -ForegroundColor Cyan
Write-Host "  API Info: http://localhost:$Port/api" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Update Security:AllowedOrigins in appsettings.Production.json" -ForegroundColor White
Write-Host "  2. Configure IP whitelist in appsettings.Production.json" -ForegroundColor White
Write-Host "  3. Test conversion: curl -X POST http://localhost:$Port/api/convert -F file=@test.docx -F targetFormat=pdf -o output.pdf" -ForegroundColor White
Write-Host "  4. Monitor logs in $IISPhysicalPath\App_Data\logs" -ForegroundColor White
Write-Host ""
Write-Host "Deployment completed successfully!" -ForegroundColor Green
Write-Host ""
