# DOC File Conversion Test Suite
# Dr. Alistair Finch
# 
# Comprehensive testing of DOC file conversion capabilities
# Tests all supported DOC conversions with proper error handling

param(
    [string]$ApiUrl = "http://localhost:3000",
    [string]$TestFile = "sample.doc"
)

Write-Host "DOC File Conversion Test Suite" -ForegroundColor Green
Write-Host "=============================" -ForegroundColor Green
Write-Host ""

# Validate test file exists
if (!(Test-Path $TestFile)) {
    Write-Host "ERROR: Test file not found: $TestFile" -ForegroundColor Red
    Write-Host "Please provide a sample .doc file to test conversions" -ForegroundColor Yellow
    exit 1
}

Write-Host "Test Configuration:" -ForegroundColor Cyan
Write-Host "  API URL: $ApiUrl" -ForegroundColor White
Write-Host "  Test File: $TestFile" -ForegroundColor White
Write-Host "  File Size: $([math]::Round((Get-Item $TestFile).Length / 1KB, 2)) KB" -ForegroundColor White
Write-Host ""

# Test health endpoint first
Write-Host "1. Testing API Health..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$ApiUrl/health" -Method Get -ErrorAction Stop
    if ($health.status -eq "Healthy") {
        Write-Host "   ✓ API is healthy" -ForegroundColor Green
    } else {
        Write-Host "   ✗ API health check failed: $($health.status)" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "   ✗ Cannot connect to API: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   Make sure the API is running at $ApiUrl" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Check supported formats
Write-Host "2. Verifying DOC Conversion Support..." -ForegroundColor Yellow
try {
    $formats = Invoke-RestMethod -Uri "$ApiUrl/api/supported-formats" -Method Get -ErrorAction Stop
    $docFormats = $formats.documents.conversions.doc
    
    Write-Host "   Supported DOC conversions:" -ForegroundColor Cyan
    foreach ($format in $docFormats) {
        Write-Host "     - DOC → $($format.ToUpper())" -ForegroundColor White
    }
    
    if ($docFormats.Count -ge 6) {
        Write-Host "   ✓ All expected conversions supported" -ForegroundColor Green
    } else {
        Write-Host "   ⚠ Only $($docFormats.Count) conversions found (expected 6+)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   ✗ Failed to retrieve supported formats" -ForegroundColor Red
}

Write-Host ""

# Test conversions
$conversions = @(
    @{ Format = "pdf";  Description = "Portable Document Format (distribution)" },
    @{ Format = "docx"; Description = "Modern Word format (modernization)" },
    @{ Format = "txt";  Description = "Plain text (text extraction)" },
    @{ Format = "rtf";  Description = "Rich Text Format (cross-platform)" },
    @{ Format = "odt";  Description = "OpenDocument Text (open standard)" },
    @{ Format = "html"; Description = "HTML (web publishing)" }
)

$successCount = 0
$failCount = 0

Write-Host "3. Testing DOC Conversions..." -ForegroundColor Yellow
Write-Host ""

foreach ($conversion in $conversions) {
    $format = $conversion.Format
    $description = $conversion.Description
    $outputFile = "output_$(Get-Date -Format 'yyyyMMdd_HHmmss').$format"
    
    Write-Host "   Testing DOC → $($format.ToUpper())" -ForegroundColor Cyan
    Write-Host "   Purpose: $description" -ForegroundColor Gray
    
    try {
        $form = @{
            file = Get-Item $TestFile
            targetFormat = $format
        }
        
        $startTime = Get-Date
        
        Invoke-RestMethod -Uri "$ApiUrl/api/convert" `
            -Method Post `
            -Form $form `
            -OutFile $outputFile `
            -ErrorAction Stop
        
        $endTime = Get-Date
        $duration = ($endTime - $startTime).TotalSeconds
        
        if (Test-Path $outputFile) {
            $outputSize = (Get-Item $outputFile).Length
            
            if ($outputSize -gt 0) {
                Write-Host "   ✓ Success" -ForegroundColor Green
                Write-Host "     Output: $outputFile ($([math]::Round($outputSize / 1KB, 2)) KB)" -ForegroundColor Gray
                Write-Host "     Duration: $([math]::Round($duration, 2))s" -ForegroundColor Gray
                $successCount++
            } else {
                Write-Host "   ✗ Failed - Output file is empty" -ForegroundColor Red
                $failCount++
            }
        } else {
            Write-Host "   ✗ Failed - Output file not created" -ForegroundColor Red
            $failCount++
        }
    } catch {
        Write-Host "   ✗ Failed - $($_.Exception.Message)" -ForegroundColor Red
        $failCount++
        
        # Try to extract error details from response
        if ($_.ErrorDetails) {
            try {
                $errorInfo = $_.ErrorDetails.Message | ConvertFrom-Json
                Write-Host "     Error: $($errorInfo.error)" -ForegroundColor Red
            } catch {
                Write-Host "     Error: $($_.ErrorDetails.Message)" -ForegroundColor Red
            }
        }
    }
    
    Write-Host ""
}

# Summary
Write-Host "Test Summary" -ForegroundColor Green
Write-Host "============" -ForegroundColor Green
Write-Host "Total Tests: $($conversions.Count)" -ForegroundColor White
Write-Host "Passed: $successCount" -ForegroundColor Green
Write-Host "Failed: $failCount" -ForegroundColor $(if ($failCount -gt 0) { "Red" } else { "Green" })
Write-Host ""

if ($failCount -eq 0) {
    Write-Host "✓ All DOC conversions working correctly" -ForegroundColor Green
    Write-Host ""
    Write-Host "Output files created:" -ForegroundColor Cyan
    Get-ChildItem output_*.* | ForEach-Object {
        Write-Host "  - $($_.Name) ($([math]::Round($_.Length / 1KB, 2)) KB)" -ForegroundColor White
    }
    exit 0
} else {
    Write-Host "✗ Some conversions failed" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "1. Ensure LibreOffice bundle is present and complete" -ForegroundColor White
    Write-Host "2. Check API logs for detailed error messages" -ForegroundColor White
    Write-Host "3. Verify the test file is a valid DOC file" -ForegroundColor White
    Write-Host "4. Check /health/detailed for system status" -ForegroundColor White
    exit 1
}

