# LibreOffice Dependency Diagnostic Script
# Identifies missing DLLs preventing LibreOffice from running

param(
    [string]$LibreOfficePath = "FileConversionApi\LibreOffice\program"
)

$ErrorActionPreference = "Continue"

Write-Host "LibreOffice Dependency Diagnostic" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

$soffice = Join-Path $LibreOfficePath "soffice.exe"

if (!(Test-Path $soffice)) {
    Write-Host "ERROR: soffice.exe not found at $soffice" -ForegroundColor Red
    exit 1
}

Write-Host "LibreOffice Location: $LibreOfficePath" -ForegroundColor White
Write-Host ""

# Check for Visual C++ Runtime DLLs
Write-Host "Checking Visual C++ Runtime DLLs:" -ForegroundColor Cyan

$vcDlls = @(
    "msvcp140.dll",
    "vcruntime140.dll",
    "vcruntime140_1.dll",
    "msvcp140_1.dll",
    "msvcp140_2.dll",
    "concrt140.dll",
    "vccorlib140.dll"
)

$missingDlls = @()
$foundDlls = @()

foreach ($dll in $vcDlls) {
    $localPath = Join-Path $LibreOfficePath $dll
    $systemPath = Join-Path $env:SystemRoot "System32\$dll"

    if (Test-Path $localPath) {
        Write-Host "  [OK] $dll (bundled)" -ForegroundColor Green
        $foundDlls += $dll
    } elseif (Test-Path $systemPath) {
        Write-Host "  [WARN] $dll (system-wide only, not bundled)" -ForegroundColor Yellow
        $foundDlls += $dll
    } else {
        Write-Host "  [MISSING] $dll" -ForegroundColor Red
        $missingDlls += $dll
    }
}

Write-Host ""

# Try to run LibreOffice with version flag (minimal test)
Write-Host "Testing LibreOffice execution:" -ForegroundColor Cyan

try {
    $processInfo = New-Object System.Diagnostics.ProcessStartInfo
    $processInfo.FileName = $soffice
    $processInfo.Arguments = "--version"
    $processInfo.UseShellExecute = $false
    $processInfo.RedirectStandardOutput = $true
    $processInfo.RedirectStandardError = $true
    $processInfo.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $processInfo

    $started = $process.Start()

    if ($started) {
        $output = $process.StandardOutput.ReadToEnd()
        $error = $process.StandardError.ReadToEnd()
        $process.WaitForExit(5000)

        if ($process.HasExited) {
            $exitCode = $process.ExitCode

            if ($exitCode -eq 0) {
                Write-Host "  [OK] LibreOffice started successfully" -ForegroundColor Green
                Write-Host "  Version: $output" -ForegroundColor Gray
            } elseif ($exitCode -eq -1073741515) {
                Write-Host "  [FAIL] Exit code -1073741515 (DLL_NOT_FOUND)" -ForegroundColor Red
                Write-Host "  Additional DLLs are missing beyond the ones checked above" -ForegroundColor Yellow
            } else {
                Write-Host "  [FAIL] Exit code: $exitCode" -ForegroundColor Red
                if ($error) {
                    Write-Host "  Error: $error" -ForegroundColor Gray
                }
            }
        } else {
            $process.Kill()
            Write-Host "  [TIMEOUT] Process did not complete within 5 seconds" -ForegroundColor Yellow
        }
    }
} catch {
    Write-Host "  [ERROR] Failed to start process: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Summary
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  Found DLLs: $($foundDlls.Count)" -ForegroundColor White
Write-Host "  Missing DLLs: $($missingDlls.Count)" -ForegroundColor White

if ($missingDlls.Count -gt 0) {
    Write-Host ""
    Write-Host "Missing DLLs:" -ForegroundColor Red
    foreach ($dll in $missingDlls) {
        Write-Host "  - $dll" -ForegroundColor Red
    }

    Write-Host ""
    Write-Host "To fix, copy missing DLLs from C:\Windows\System32:" -ForegroundColor Yellow
    Write-Host "Copy-Item 'C:\Windows\System32\<dll>' -Destination '$LibreOfficePath\'" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Recommendation:" -ForegroundColor Cyan

if ($missingDlls.Count -eq 0) {
    Write-Host "  All standard VC++ DLLs are present. If LibreOffice still fails," -ForegroundColor White
    Write-Host "  additional dependencies may be needed. Use Dependencies.exe to" -ForegroundColor White
    Write-Host "  analyze soffice.exe: https://github.com/lucasg/Dependencies" -ForegroundColor White
} else {
    Write-Host "  Install Visual C++ Redistributable 2015-2022 x64, then copy" -ForegroundColor White
    Write-Host "  missing DLLs to the LibreOffice\program directory." -ForegroundColor White
    Write-Host "  Download: https://aka.ms/vs/17/release/vc_redist.x64.exe" -ForegroundColor White
}
