# Direct LibreOffice Test Script
# Run this ON THE SERVER to test LibreOffice execution directly

param(
    [string]$LibreOfficePath = "D:\inetpub\wwwroot\Service\FileConversionApi\LibreOffice\program",
    [string]$TestDocPath = ""
)

$ErrorActionPreference = "Continue"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "LibreOffice Direct Execution Test" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

$sofficeExe = Join-Path $LibreOfficePath "soffice.exe"

# Step 1: Verify LibreOffice executable exists
Write-Host "Step 1: Checking LibreOffice executable..." -ForegroundColor Yellow
if (!(Test-Path $sofficeExe)) {
    Write-Host "  ERROR: soffice.exe not found at: $sofficeExe" -ForegroundColor Red
    exit 1
}
Write-Host "  OK: Found soffice.exe" -ForegroundColor Green
Write-Host ""

# Step 2: Check all VC++ Runtime DLLs
Write-Host "Step 2: Checking Visual C++ Runtime DLLs..." -ForegroundColor Yellow

$requiredDlls = @(
    "msvcp140.dll",
    "vcruntime140.dll",
    "vcruntime140_1.dll",
    "msvcp140_1.dll",
    "msvcp140_2.dll",
    "concrt140.dll",
    "vccorlib140.dll"
)

$allFound = $true
foreach ($dll in $requiredDlls) {
    $dllPath = Join-Path $LibreOfficePath $dll
    $systemPath = Join-Path $env:SystemRoot "System32\$dll"

    if (Test-Path $dllPath) {
        $fileInfo = Get-Item $dllPath
        Write-Host "  [OK] $dll (bundled, $($fileInfo.Length) bytes)" -ForegroundColor Green
    } elseif (Test-Path $systemPath) {
        Write-Host "  [WARN] $dll (system32 only)" -ForegroundColor Yellow
    } else {
        Write-Host "  [MISSING] $dll - NOT FOUND ANYWHERE" -ForegroundColor Red
        $allFound = $false
    }
}
Write-Host ""

# Step 3: Check critical LibreOffice DLLs
Write-Host "Step 3: Checking critical LibreOffice DLLs..." -ForegroundColor Yellow

$libreofficeDlls = @(
    "sal3.dll",
    "sofficeapp.dll",
    "uno_sal3.dll",
    "ucpfile1.dll",
    "configmgr.uno.dll"
)

foreach ($dll in $libreofficeDlls) {
    $dllPath = Join-Path $LibreOfficePath $dll
    if (Test-Path $dllPath) {
        Write-Host "  [OK] $dll" -ForegroundColor Green
    } else {
        Write-Host "  [MISSING] $dll" -ForegroundColor Red
        $allFound = $false
    }
}
Write-Host ""

# Step 4: Test execution with --version
Write-Host "Step 4: Testing LibreOffice execution (--version)..." -ForegroundColor Yellow

try {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $sofficeExe
    $psi.Arguments = "--version"
    $psi.WorkingDirectory = $LibreOfficePath
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi

    Write-Host "  Executing: $sofficeExe --version" -ForegroundColor Gray
    Write-Host "  Working Directory: $LibreOfficePath" -ForegroundColor Gray

    $started = $process.Start()

    if ($started) {
        $stdout = $process.StandardOutput.ReadToEnd()
        $stderr = $process.StandardError.ReadToEnd()

        $timeout = $process.WaitForExit(10000) # 10 second timeout

        if ($process.HasExited) {
            $exitCode = $process.ExitCode

            Write-Host ""
            Write-Host "  Exit Code: $exitCode" -ForegroundColor $(if ($exitCode -eq 0) { "Green" } else { "Red" })

            if ($exitCode -eq 0) {
                Write-Host "  SUCCESS!" -ForegroundColor Green
                Write-Host "  Output: $stdout" -ForegroundColor White
            } elseif ($exitCode -eq -1073741515) {
                Write-Host "  FAILURE: Missing DLL (0xC0000135)" -ForegroundColor Red
                Write-Host ""
                Write-Host "  This means soffice.exe tried to load a DLL that doesn't exist." -ForegroundColor Yellow
                Write-Host "  The DLL might be a VC++ runtime or a LibreOffice dependency." -ForegroundColor Yellow
            } else {
                Write-Host "  FAILURE: Unexpected exit code" -ForegroundColor Red
            }

            if ($stderr) {
                Write-Host "  Error Output: $stderr" -ForegroundColor Red
            }
        } else {
            $process.Kill()
            Write-Host "  TIMEOUT: Process didn't complete in 10 seconds" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  ERROR: Failed to start process" -ForegroundColor Red
    }
} catch {
    Write-Host "  EXCEPTION: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "Diagnostic Complete" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

if (!$allFound) {
    Write-Host "RECOMMENDATION: Install missing dependencies" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Option 1: Install Visual C++ Redistributable on server" -ForegroundColor White
    Write-Host "  Download: https://aka.ms/vs/17/release/vc_redist.x64.exe" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Option 2: Copy missing DLLs from another machine" -ForegroundColor White
    Write-Host "  From C:\Windows\System32 to $LibreOfficePath" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Option 3: Analyze with Dependencies.exe" -ForegroundColor White
    Write-Host "  Download: https://github.com/lucasg/Dependencies/releases" -ForegroundColor Gray
    Write-Host "  Run: Dependencies.exe $sofficeExe" -ForegroundColor Gray
    Write-Host "  This will show EXACTLY which DLL is missing" -ForegroundColor Gray
} else {
    Write-Host "All checked DLLs are present." -ForegroundColor Green
    Write-Host "If LibreOffice still fails, run Dependencies.exe to find hidden dependencies." -ForegroundColor Yellow
    Write-Host "Download: https://github.com/lucasg/Dependencies/releases" -ForegroundColor Gray
}
