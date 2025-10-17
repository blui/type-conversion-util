# PowerShell script for testing file conversion API
param(
    [string]$FilePath = "FMS REIA Integration Roles and Responsibilities.docx",
    [string]$TargetFormat = "pdf",
    [string]$OutputFile = "test.pdf",
    [string]$ApiUrl = "http://localhost:3000"
)

Write-Host "Testing File Conversion API" -ForegroundColor Green
Write-Host "File: $FilePath" -ForegroundColor Cyan
Write-Host "Target Format: $TargetFormat" -ForegroundColor Cyan
Write-Host "Output: $OutputFile" -ForegroundColor Cyan
Write-Host "API URL: $ApiUrl" -ForegroundColor Cyan
Write-Host ""

# Check if file exists
if (!(Test-Path $FilePath)) {
    Write-Host "Error: File '$FilePath' not found!" -ForegroundColor Red
    exit 1
}

# Create multipart form data
Add-Type -AssemblyName System.Net.Http

$client = New-Object System.Net.Http.HttpClient
$content = New-Object System.Net.Http.MultipartFormDataContent

# Add file
$fileStream = [System.IO.File]::OpenRead($FilePath)
$fileContent = New-Object System.Net.Http.StreamContent($fileStream)
$content.Add($fileContent, "file", [System.IO.Path]::GetFileName($FilePath))

# Add target format
$targetFormatContent = New-Object System.Net.Http.StringContent($TargetFormat)
$content.Add($targetFormatContent, "targetFormat")

try {
    Write-Host "Sending conversion request..." -ForegroundColor Yellow
    $response = $client.PostAsync("$ApiUrl/api/convert", $content).Result

    if ($response.IsSuccessStatusCode) {
        Write-Host "✓ Conversion successful!" -ForegroundColor Green

        # Save the response to file
        $responseStream = $response.Content.ReadAsStreamAsync().Result
        $outputStream = [System.IO.File]::Create($OutputFile)
        $responseStream.CopyTo($outputStream)
        $outputStream.Close()

        Write-Host "Output saved to: $OutputFile" -ForegroundColor Green
        Write-Host "File size: $([math]::Round((Get-Item $OutputFile).Length / 1MB, 2)) MB" -ForegroundColor Cyan
    } else {
        Write-Host "✗ Conversion failed!" -ForegroundColor Red
        Write-Host "Status Code: $($response.StatusCode)" -ForegroundColor Red
        $errorContent = $response.Content.ReadAsStringAsync().Result
        Write-Host "Error Response: $errorContent" -ForegroundColor Red
    }
} catch {
    Write-Host "✗ Request failed: $($_.Exception.Message)" -ForegroundColor Red
} finally {
    $fileStream.Close()
    $client.Dispose()
}
