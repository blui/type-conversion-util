# Migrating from Deprecated File Formats

## Overview

Starting with version 0.4.0, certain legacy file formats are being deprecated and will be removed in version 0.5.0. This guide helps you migrate your files to modern, widely-supported formats.

## Deprecated Formats

The following formats are deprecated and scheduled for removal:

| Format | Type | Last Used | Replacement |
|--------|------|-----------|-------------|
| **sxw** | StarOffice 1.x Writer | 2005 | ODT, DOCX |
| **sxc** | StarOffice 1.x Calc | 2005 | ODS, XLSX |
| **sxi** | StarOffice 1.x Impress | 2005 | ODP, PPTX |
| **sxd** | StarOffice 1.x Draw | 2005 | ODG, PDF |
| **odg** | OpenDocument Graphics | Active | PDF, SVG |
| **odf** | OpenDocument Formula | Active | PDF, MathML |

## Why Are These Being Deprecated?

**StarOffice 1.x Formats (sxw, sxc, sxi, sxd):**
- Discontinued in 2005 (20 years obsolete)
- Replaced by OpenDocument Format (ODF) in LibreOffice/OpenOffice 2.0+
- Extremely rare in modern enterprise environments
- Only support one-way conversion to PDF (no round-trip capability)
- Maintenance burden without practical benefit

**Specialized ODF Formats (odg, odf):**
- Used for graphics and formulas, not document conversion workflows
- Rarely requested in enterprise document conversion scenarios
- Modern alternatives exist (SVG for graphics, MathML for formulas)
- Only support conversion to PDF

## Deprecation Timeline

### Version 0.4.0 (Current - Soft Deprecation)
- Deprecated formats still work by default
- API responses include deprecation warnings
- Logs track deprecated format usage
- X-Deprecation-Warning header added to responses

### Version 0.5.0 (Planned - Removal)
- Deprecated formats will be removed
- Conversion requests for these formats will return 400 Bad Request
- Configuration option to re-enable will be removed

## Migration Instructions

### Option 1: Using LibreOffice (Recommended)

LibreOffice can open legacy formats and save them to modern formats.

#### Single File Conversion

**Windows:**
```powershell
# Convert SXW to ODT
& "C:\Program Files\LibreOffice\program\soffice.exe" --headless --convert-to odt --outdir "C:\Output" "C:\Input\document.sxw"

# Convert SXW to DOCX
& "C:\Program Files\LibreOffice\program\soffice.exe" --headless --convert-to docx --outdir "C:\Output" "C:\Input\document.sxw"

# Convert SXC to ODS
& "C:\Program Files\LibreOffice\program\soffice.exe" --headless --convert-to ods --outdir "C:\Output" "C:\Input\spreadsheet.sxc"

# Convert SXC to XLSX
& "C:\Program Files\LibreOffice\program\soffice.exe" --headless --convert-to xlsx --outdir "C:\Output" "C:\Input\spreadsheet.sxc"

# Convert SXI to ODP
& "C:\Program Files\LibreOffice\program\soffice.exe" --headless --convert-to odp --outdir "C:\Output" "C:\Input\presentation.sxi"

# Convert SXI to PPTX
& "C:\Program Files\LibreOffice\program\soffice.exe" --headless --convert-to pptx --outdir "C:\Output" "C:\Input\presentation.sxi"

# Convert ODG to PDF
& "C:\Program Files\LibreOffice\program\soffice.exe" --headless --convert-to pdf --outdir "C:\Output" "C:\Input\drawing.odg"
```

**Linux:**
```bash
# Convert SXW to ODT
soffice --headless --convert-to odt --outdir /output /input/document.sxw

# Convert SXW to DOCX
soffice --headless --convert-to docx --outdir /output /input/document.sxw
```

#### Batch Conversion Script (PowerShell)

Save this as `Migrate-LegacyFormats.ps1`:

```powershell
<#
.SYNOPSIS
    Converts legacy file formats to modern alternatives using LibreOffice.

.DESCRIPTION
    Batch converts StarOffice 1.x formats (sxw, sxc, sxi, sxd) and specialized
    ODF formats (odg, odf) to modern formats using LibreOffice headless mode.

.PARAMETER InputDirectory
    Directory containing files to convert

.PARAMETER OutputDirectory
    Directory where converted files will be saved

.PARAMETER TargetFormat
    Target format: odt, docx, ods, xlsx, odp, pptx, pdf (default: auto-detect)

.EXAMPLE
    .\Migrate-LegacyFormats.ps1 -InputDirectory "C:\LegacyDocs" -OutputDirectory "C:\ConvertedDocs"

.EXAMPLE
    .\Migrate-LegacyFormats.ps1 -InputDirectory "C:\LegacyDocs" -OutputDirectory "C:\ConvertedDocs" -TargetFormat pdf
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$InputDirectory,

    [Parameter(Mandatory=$true)]
    [string]$OutputDirectory,

    [Parameter(Mandatory=$false)]
    [ValidateSet("odt", "docx", "ods", "xlsx", "odp", "pptx", "pdf", "auto")]
    [string]$TargetFormat = "auto"
)

# Find LibreOffice installation
$libreOfficePaths = @(
    "C:\Program Files\LibreOffice\program\soffice.exe",
    "C:\Program Files (x86)\LibreOffice\program\soffice.exe",
    "$env:ProgramFiles\LibreOffice\program\soffice.exe"
)

$soffice = $libreOfficePaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $soffice) {
    Write-Error "LibreOffice not found. Install from https://www.libreoffice.org/download/"
    exit 1
}

Write-Host "Using LibreOffice: $soffice" -ForegroundColor Green

# Create output directory
if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}

# Define format mappings
$formatMappings = @{
    "sxw" = @{ "default" = "odt"; "alternatives" = @("docx", "pdf") }
    "sxc" = @{ "default" = "ods"; "alternatives" = @("xlsx", "pdf") }
    "sxi" = @{ "default" = "odp"; "alternatives" = @("pptx", "pdf") }
    "sxd" = @{ "default" = "pdf"; "alternatives" = @("odg") }
    "odg" = @{ "default" = "pdf"; "alternatives" = @() }
    "odf" = @{ "default" = "pdf"; "alternatives" = @() }
}

# Get all legacy format files
$legacyFiles = Get-ChildItem -Path $InputDirectory -Recurse -Include *.sxw,*.sxc,*.sxi,*.sxd,*.odg,*.odf

Write-Host "`nFound $($legacyFiles.Count) legacy format files" -ForegroundColor Cyan
Write-Host "Converting files...`n" -ForegroundColor Cyan

$converted = 0
$failed = 0

foreach ($file in $legacyFiles) {
    $extension = $file.Extension.TrimStart('.').ToLower()

    # Determine target format
    if ($TargetFormat -eq "auto") {
        $targetExt = $formatMappings[$extension]["default"]
    } else {
        $targetExt = $TargetFormat
    }

    Write-Host "Converting: $($file.Name) -> $targetExt" -NoNewline

    try {
        # Create relative directory structure in output
        $relativePath = $file.DirectoryName.Replace($InputDirectory, "")
        $outputDir = Join-Path $OutputDirectory $relativePath

        if (-not (Test-Path $outputDir)) {
            New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
        }

        # Convert file
        $process = Start-Process -FilePath $soffice `
            -ArgumentList "--headless", "--convert-to", $targetExt, "--outdir", $outputDir, $file.FullName `
            -Wait -PassThru -NoNewWindow -RedirectStandardError "$env:TEMP\soffice_error.log"

        if ($process.ExitCode -eq 0) {
            Write-Host " [OK]" -ForegroundColor Green
            $converted++
        } else {
            $errorMsg = Get-Content "$env:TEMP\soffice_error.log" -Raw -ErrorAction SilentlyContinue
            Write-Host " [FAILED]" -ForegroundColor Red
            Write-Host "  Error: $errorMsg" -ForegroundColor Red
            $failed++
        }
    }
    catch {
        Write-Host " [FAILED]" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
        $failed++
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Conversion Complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Successful: $converted" -ForegroundColor Green
Write-Host "Failed: $failed" -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Green" })
Write-Host "Output Directory: $OutputDirectory" -ForegroundColor Cyan
```

**Usage:**
```powershell
# Convert all legacy files to default modern formats
.\Migrate-LegacyFormats.ps1 -InputDirectory "C:\Documents\Legacy" -OutputDirectory "C:\Documents\Modern"

# Convert all legacy files to PDF
.\Migrate-LegacyFormats.ps1 -InputDirectory "C:\Documents\Legacy" -OutputDirectory "C:\Documents\Modern" -TargetFormat pdf
```

### Option 2: Using This API Before Deprecation

You can use the conversion API to migrate your files while deprecated formats are still supported.

**PowerShell Script:**
```powershell
param(
    [string]$ApiUrl = "http://localhost:3000",
    [string]$InputDirectory,
    [string]$OutputDirectory
)

$files = Get-ChildItem -Path $InputDirectory -Recurse -Include *.sxw,*.sxc,*.sxi,*.sxd,*.odg,*.odf

foreach ($file in $files) {
    $targetFormat = switch ($file.Extension) {
        ".sxw" { "odt" }
        ".sxc" { "ods" }
        ".sxi" { "odp" }
        ".sxd" { "pdf" }
        ".odg" { "pdf" }
        ".odf" { "pdf" }
    }

    $outputFile = Join-Path $OutputDirectory "$($file.BaseName).$targetFormat"

    Write-Host "Converting $($file.Name) to $targetFormat..."

    $form = @{
        file = Get-Item -Path $file.FullName
        targetFormat = $targetFormat
    }

    Invoke-RestMethod -Uri "$ApiUrl/api/convert" -Method Post -Form $form -OutFile $outputFile
}
```

### Option 3: GUI Conversion (LibreOffice)

For individual files or small batches:

1. Open LibreOffice
2. File → Open → Select your legacy format file
3. File → Save As
4. Choose format:
   - **For documents (sxw):** ODT or DOCX
   - **For spreadsheets (sxc):** ODS or XLSX
   - **For presentations (sxi):** ODP or PPTX
   - **For drawings (sxd, odg):** PDF
5. Click Save

## Recommended Modern Formats

### For Maximum Compatibility
- **Documents:** DOCX (Microsoft Office 2007+)
- **Spreadsheets:** XLSX (Microsoft Office 2007+)
- **Presentations:** PPTX (Microsoft Office 2007+)

### For Open Standards
- **Documents:** ODT (OpenDocument Text)
- **Spreadsheets:** ODS (OpenDocument Spreadsheet)
- **Presentations:** ODP (OpenDocument Presentation)

### For Archival/Distribution
- **All types:** PDF (Portable Document Format)

## Format Comparison

| Feature | Legacy (SX*) | Modern (OD*) | Modern (Office) |
|---------|-------------|--------------|-----------------|
| File Size | Larger | Smaller | Smaller |
| Compatibility | Poor | Good | Excellent |
| Feature Support | Limited | Full | Full |
| Active Development | No | Yes | Yes |
| Industry Standard | No | Yes (ISO 26300) | Yes (ISO 29500) |

## Configuration Options

### Temporarily Re-enable Deprecated Formats

If you need more time to migrate, you can temporarily keep deprecated formats enabled in `appsettings.json`:

```json
{
  "FileHandling": {
    "EnableDeprecatedFormats": true,
    "WarnOnDeprecatedFormats": true
  }
}
```

### Disable Deprecation Warnings

To suppress warnings while you migrate:

```json
{
  "FileHandling": {
    "EnableDeprecatedFormats": true,
    "WarnOnDeprecatedFormats": false
  }
}
```

**Note:** These options will be removed in version 0.5.0.

## Monitoring Deprecated Format Usage

Check your logs for deprecation warnings:

```powershell
# View deprecation usage in logs
Get-Content "App_Data\logs\*.log" | Select-String "DEPRECATION"

# Count usage by format
Get-Content "App_Data\logs\*.log" | Select-String "DEPRECATION: Processing deprecated format" |
    ForEach-Object { ($_ -split "'")[1] } | Group-Object | Sort-Object Count -Descending
```

## Need Help?

If you encounter issues during migration:

1. Check the logs: `App_Data\logs\file-conversion-api-*.log`
2. Review the error messages for specific file conversion failures
3. Try converting problematic files manually in LibreOffice GUI
4. For corrupted files, you may need to recover content manually

## FAQ

**Q: What happens if I try to use a deprecated format after version 0.5.0?**
A: The API will return a 400 Bad Request error with a message directing you to this migration guide.

**Q: Can I continue using deprecated formats indefinitely?**
A: No. The configuration option to enable deprecated formats will be removed in version 0.5.0.

**Q: Will my existing converted files be affected?**
A: No. Only the input formats are being deprecated. If you've already converted files to PDF, DOCX, ODT, etc., those files are unaffected.

**Q: What if I have thousands of legacy files?**
A: Use the batch conversion PowerShell script provided above. It processes files recursively and preserves directory structure.

**Q: Is there a performance difference between formats?**
A: Modern formats (DOCX, XLSX, PPTX, ODT, ODS, ODP) are ZIP-based and more efficient. Legacy formats are XML-based and larger.

**Q: Which format should I choose for long-term archival?**
A: PDF for guaranteed visual fidelity, or ODT/ODS/ODP for editability (ISO standard, widely supported).

## Additional Resources

- LibreOffice Download: https://www.libreoffice.org/download/
- OpenDocument Format Specification: https://www.oasis-open.org/committees/tc_home.php?wg_abbrev=office
- Microsoft Office Formats: https://www.microsoft.com/en-us/microsoft-365

## Timeline Summary

- **Now (v0.4.0):** Deprecation warnings active, formats still work
- **Version 0.5.0:** Deprecated formats removed
- **Recommended Action:** Migrate files within the next release cycle
