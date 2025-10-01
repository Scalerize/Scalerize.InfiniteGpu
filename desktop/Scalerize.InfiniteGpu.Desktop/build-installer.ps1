<#
.SYNOPSIS
    Builds the Scalerize InfiniteGpu Desktop MSI installer.

.DESCRIPTION
    This script performs the following steps:
    1. Publishes the desktop application for x64 platform
    2. Uses WiX heat.exe to harvest files from the published output
    3. Builds the WiX installer project to generate the MSI file

.PARAMETER Configuration
    Build configuration (Debug or Release). Default is Release.

.PARAMETER Platform
    Target platform (x64, x86, ARM64). Default is x64.

.EXAMPLE
    .\build-installer.ps1
    .\build-installer.ps1 -Configuration Release -Platform x64
#>

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("x64", "x86", "ARM64")]
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"

# Define paths
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionDir = $scriptDir
$desktopProjectDir = Join-Path $solutionDir "Scalerize.InfiniteGpu.Desktop"
$installerProjectDir = Join-Path $solutionDir "Scalerize.InfiniteGpu.Desktop.Installer"
$desktopCsproj = Join-Path $desktopProjectDir "Scalerize.InfiniteGpu.Desktop.csproj"
$publishDir = Join-Path $desktopProjectDir "bin\$Configuration\net10.0-windows10.0.19041.0\win-$($Platform.ToLower())\publish"

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Building Scalerize InfiniteGpu Desktop MSI Installer" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host "Platform: $Platform" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Check if WiX Toolset v6.0 is installed
Write-Host "[1/6] Checking WiX Toolset v6.0 installation..." -ForegroundColor Yellow

# Check if wix.exe is available globally (installed via dotnet tool)
$wixExe = $null
try {
    $wixExe = Get-Command wix.exe -ErrorAction SilentlyContinue
    if ($wixExe) {
        Write-Host "WiX Toolset v6.0 found (global dotnet tool)" -ForegroundColor Green
    }
} catch {
    # Continue to check other locations
}

# If not found as global tool, check traditional install locations
if (-not $wixExe) {
    $wixPath = "${env:ProgramFiles}\dotnet\tools"
    $wixExePath = Join-Path $wixPath "wix.exe"
    
    if (Test-Path $wixExePath) {
        $wixExe = $wixExePath
        Write-Host "WiX Toolset v6.0 found at: $wixPath" -ForegroundColor Green
    }
}

if (-not $wixExe) {
    Write-Host "ERROR: WiX Toolset v6.0 not found!" -ForegroundColor Red
    Write-Host "Please install WiX Toolset v6.0 using:" -ForegroundColor Red
    Write-Host "  dotnet tool install --global wix" -ForegroundColor Yellow
    Write-Host "" -ForegroundColor Red
    Write-Host "Or download from: https://wixtoolset.org/" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Step 2: Clean previous builds
Write-Host "[2/6] Cleaning previous builds..." -ForegroundColor Yellow
if (Test-Path $publishDir) {
    Remove-Item -Path $publishDir -Recurse -Force
    Write-Host "Cleaned publish directory" -ForegroundColor Green
}

$installerObjDir = Join-Path $installerProjectDir "obj"
$installerBinDir = Join-Path $installerProjectDir "bin"
if (Test-Path $installerObjDir) {
    Remove-Item -Path $installerObjDir -Recurse -Force
}
if (Test-Path $installerBinDir) {
    Remove-Item -Path $installerBinDir -Recurse -Force
}
Write-Host "Cleaned installer build directories" -ForegroundColor Green
Write-Host ""

# Step 3: Publish the desktop application
Write-Host "[3/6] Publishing desktop application..." -ForegroundColor Yellow
$publishArgs = @(
    "publish",
    $desktopCsproj,
    "-c", $Configuration,
    "-r", "win-$($Platform.ToLower())",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:PublishReadyToRun=true"
)

Write-Host "Command: dotnet $($publishArgs -join ' ')" -ForegroundColor Gray
& dotnet $publishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to publish desktop application" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "Desktop application published successfully" -ForegroundColor Green
Write-Host ""

# Step 4: Copy Assets folder to publish directory
Write-Host "[4/6] Copying Assets folder to publish directory..." -ForegroundColor Yellow
$assetsSourceDir = Join-Path $desktopProjectDir "Assets"
$assetsDestDir = Join-Path $publishDir "Assets"

if (Test-Path $assetsSourceDir) {
    # Create Assets directory in publish folder if it doesn't exist
    if (-not (Test-Path $assetsDestDir)) {
        New-Item -ItemType Directory -Force -Path $assetsDestDir | Out-Null
    }
    
    # Copy all files from Assets folder
    Copy-Item -Path "$assetsSourceDir\*" -Destination $assetsDestDir -Recurse -Force
    Write-Host "Assets folder copied successfully" -ForegroundColor Green
    
    # List copied files for verification
    $copiedFiles = Get-ChildItem -Path $assetsDestDir -Recurse -File
    Write-Host "Copied $($copiedFiles.Count) file(s) from Assets folder" -ForegroundColor Gray
} else {
    Write-Host "WARNING: Assets folder not found at: $assetsSourceDir" -ForegroundColor Yellow
}
Write-Host ""

# Step 5: Harvest files using WiX heat command
Write-Host "[5/6] Harvesting files from published output..." -ForegroundColor Yellow
$harvestWxs = Join-Path $installerProjectDir "HarvestedFiles.wxs"

# WiX v6.0 uses: wix extension add WixToolset.Heat.Extension
# Then: wix build ... with heat directory
Write-Host "Note: Using WiX v6.0 - file harvesting will be done during build" -ForegroundColor Gray
Write-Host ""

# Step 6: Build the MSI installer using WiX v6.0
Write-Host "[6/6] Building MSI installer with WiX v6.0..." -ForegroundColor Yellow

$productWxs = Join-Path $installerProjectDir "Package.wxs"
$outputDir = Join-Path $installerProjectDir "bin\$Configuration\$Platform"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$msiFile = Join-Path $outputDir "ScalerizeInfiniteGpuDesktopSetup.msi"

# WiX v6.0 unified build command
$wixArgs = @(
    "build",
    $productWxs,
    "-arch", $Platform.ToLower(),
    "-out", $msiFile,
    "-d", "Configuration=$Configuration",
    "-d", "PublishDir=$publishDir",
    "-d", "Scalerize.InfiniteGpu.Desktop.TargetDir=$publishDir\",
    "-d", "ProjectDir=$installerProjectDir\",
    "-ext", "WixToolset.UI.wixext",
    "-ext", "WixToolset.Util.wixext"
)

Write-Host "Building MSI with WiX v6.0..." -ForegroundColor Gray
Write-Host "Command: wix $($wixArgs -join ' ')" -ForegroundColor Gray

if ($wixExe -is [string]) {
    & $wixExe $wixArgs
} else {
    & wix $wixArgs
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to build MSI installer" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Green
Write-Host "SUCCESS!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""
Write-Host "MSI Installer created at:" -ForegroundColor Green
Write-Host $msiFile -ForegroundColor Cyan
Write-Host ""
Write-Host "File size: $([math]::Round((Get-Item $msiFile).Length / 1MB, 2)) MB" -ForegroundColor Gray
Write-Host ""