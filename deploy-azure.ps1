# Azure App Service (Windows) deployment wrapper.
#
# Produces a zip-deployable site bundle at deploy/release.zip by running the
# existing FileConversionApi/deploy.ps1 (the same script the Windows/IIS deploy
# uses) and then compressing its output. The IIS deploy path is unaffected:
# this script does not edit deploy.ps1, web.config, appsettings.json, or any
# application source; it only adds a packaging step around the existing bundle.
#
# Usage:
#
#   .\deploy-azure.ps1
#   .\deploy-azure.ps1 -SkipBuild                              # reuse the prior deploy/release output
#   .\deploy-azure.ps1 -OutputZip deploy\release-staging.zip
#
# Next step after this script succeeds:
#
#   az webapp deploy `
#     --resource-group <rg> `
#     --name <site> `
#     --src-path .\deploy\release.zip `
#     --type zip
#
# See DEPLOYMENT-AZURE.md for the full provisioning + deploy + verification flow.

param(
    [string]$OutputZip = "deploy\release.zip",
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot   = $PSScriptRoot
$apiDir     = Join-Path $repoRoot "FileConversionApi"
$releaseDir = Join-Path $apiDir   "deploy\release"
$zipPath    = Join-Path $repoRoot $OutputZip

Write-Host "Azure App Service deployment package builder" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""

if (!(Test-Path (Join-Path $apiDir "deploy.ps1"))) {
    Write-Host "ERROR: FileConversionApi/deploy.ps1 not found. Run from the repo root." -ForegroundColor Red
    exit 1
}

# Stage 1: run the existing IIS-shaped bundle script. This is the byte-identical
# bundle the IIS deploy uses; the zip wrapper below is the only Azure-specific step.
Write-Host "1. Running FileConversionApi\deploy.ps1..." -ForegroundColor Yellow
Push-Location $apiDir
try {
    if ($SkipBuild) {
        & .\deploy.ps1 -SkipBuild
    } else {
        & .\deploy.ps1
    }
    if ($LASTEXITCODE -ne 0) {
        throw "FileConversionApi/deploy.ps1 failed (exit code $LASTEXITCODE). Inspect its output above; do not ship a partial bundle."
    }
}
finally {
    Pop-Location
}

if (!(Test-Path $releaseDir)) {
    Write-Host "ERROR: Expected release folder not found at $releaseDir after deploy.ps1 ran." -ForegroundColor Red
    exit 1
}

# Stage 2: compress the release tree into a single zip ready for az webapp deploy.
Write-Host ""
Write-Host "2. Compressing release tree..." -ForegroundColor Yellow

$zipParent = Split-Path $zipPath -Parent
if ($zipParent -and !(Test-Path $zipParent)) {
    New-Item -ItemType Directory -Path $zipParent -Force | Out-Null
}

if (Test-Path $zipPath) {
    Write-Host "   Removing previous zip at $zipPath" -ForegroundColor Cyan
    Remove-Item $zipPath -Force
}

# Compress-Archive emits a deterministic layout: each entry below $releaseDir
# becomes a top-level entry inside the zip, which is what az webapp deploy
# expects (it unzips directly into D:\home\site\wwwroot\).
Compress-Archive -Path (Join-Path $releaseDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

$zipSize  = (Get-Item $zipPath).Length / 1MB
$srcSize  = (Get-ChildItem $releaseDir -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB
$srcCount = (Get-ChildItem $releaseDir -Recurse -File).Count

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "Azure deployment package ready" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Zip:    $zipPath" -ForegroundColor White
Write-Host "Size:   $([math]::Round($zipSize, 1)) MB (compressed from $([math]::Round($srcSize, 1)) MB across $srcCount files)" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Confirm Azure CLI is logged in:" -ForegroundColor White
Write-Host "       az account show" -ForegroundColor Gray
Write-Host "  2. Deploy:" -ForegroundColor White
Write-Host "       az webapp deploy --resource-group <rg> --name <site> --src-path $zipPath --type zip" -ForegroundColor Gray
Write-Host "  3. Verify:" -ForegroundColor White
Write-Host "       curl https://<site>.azurewebsites.net/health" -ForegroundColor Gray
Write-Host ""
Write-Host "Full runbook: DEPLOYMENT-AZURE.md" -ForegroundColor Cyan
Write-Host ""
