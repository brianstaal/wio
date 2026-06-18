# MakeRelease.ps1
# PowerShell script to build and package the release version of wio (tio for Windows)

# Stop script execution on any error
$ErrorActionPreference = "Stop"

# Define directories relative to this script
$ArtifactsDir = $PSScriptRoot
$ProjectDir = Resolve-Path "$PSScriptRoot\.."
$OutputDir = Join-Path $ArtifactsDir "Release"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " Building wio (tio for Windows) Release Build     " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "Project Root: $ProjectDir"
Write-Host "Output Dir:   $OutputDir"
Write-Host ""

# Verify dotnet CLI is available
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "dotnet CLI was not found. Please install the .NET SDK (version 10.0 or later)."
}

# Navigate to project root
Push-Location $ProjectDir

try {
    # Clean up previous output directory if it exists
    if (Test-Path $OutputDir) {
        Write-Host "Cleaning up previous release files..." -ForegroundColor Gray
        Remove-Item -Path $OutputDir -Recurse -Force
    }
    
    # 1. Build and Publish Self-Contained Single-File Executable
    Write-Host "Publishing self-contained wio.exe..." -ForegroundColor Green
    dotnet publish -r win-x64 -c Release -p:PublishAot=false -p:PublishSingleFile=true -p:SelfContained=true --output $OutputDir
    
    # Check if executable was successfully created
    $ExePath = Join-Path $OutputDir "wio.exe"
    if (Test-Path $ExePath) {
        $FileSizeMB = [Math]::Round(((Get-Item $ExePath).Length / 1MB), 2)
        Write-Host ""
        Write-Host "Build Succeeded!" -ForegroundColor Green
        Write-Host "Executable: $ExePath" -ForegroundColor Yellow
        Write-Host "File Size:  $FileSizeMB MB" -ForegroundColor Yellow
        Write-Host "Description: Single-file, self-contained executable with zero dependencies."
    } else {
        Write-Error "Build finished but wio.exe was not found in the output folder."
    }
}
catch {
    Write-Host ""
    Write-Host "Build Failed!" -ForegroundColor Red
    Write-Error $_.Exception.Message
}
finally {
    # Restore original directory
    Pop-Location
}
