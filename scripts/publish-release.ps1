<#
.SYNOPSIS
    Automated Single-File Release Publisher for Dev Lite Server.
.DESCRIPTION
    Compiles and publishes DevLiteServer as an optimized, self-contained single executable
    (win-x64), verifies the portable bundle structure, and optionally creates a portable ZIP package.
#>

[CmdletBinding()]
param (
    [string]$Configuration = "Release",
    [switch]$CreateZip
)

$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $PSScriptRoot
if (-not $ScriptRoot) { $ScriptRoot = Get-Location }

$PublishDir = Join-Path $ScriptRoot "build\publish"
$DistDir = Join-Path $ScriptRoot "dist"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Dev Lite Server - Release Publisher    " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1. Clean previous publish artifacts
if (Test-Path $PublishDir) {
    Write-Host "[1/4] Cleaning previous publish directory..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $PublishDir
}
New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null

# 2. Publish Single-File Executable via dotnet publish
Write-Host "[2/4] Compiling and publishing single-file binary (win-x64)..." -ForegroundColor Yellow
$projectFile = Join-Path $ScriptRoot "DevLiteServer.csproj"

& dotnet publish $projectFile `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
}

$exePath = Join-Path $PublishDir "DevLiteServer.exe"
if (-not (Test-Path $exePath)) {
    Write-Error "Expected published executable not found at: $exePath"
}

$exeSizeMB = [math]::Round((Get-Item $exePath).Length / 1MB, 2)
Write-Host "✔ Published Single-File Executable: DevLiteServer.exe ($exeSizeMB MB)" -ForegroundColor Green

# 3. Assemble Portable Release Bundle
Write-Host "[3/4] Assembling portable distribution bundle..." -ForegroundColor Yellow
if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Force -Path $DistDir | Out-Null
}

$BundleDir = Join-Path $DistDir "DevLiteServer"
if (Test-Path $BundleDir) {
    Remove-Item -Recurse -Force $BundleDir
}
New-Item -ItemType Directory -Force -Path $BundleDir | Out-Null

# Copy main binary & assets
Copy-Item $exePath -Destination $BundleDir
Copy-Item (Join-Path $ScriptRoot "config.ini") -Destination $BundleDir -ErrorAction SilentlyContinue
Copy-Item -Recurse -Force (Join-Path $ScriptRoot "templates") -Destination (Join-Path $BundleDir "templates")
Copy-Item -Recurse -Force (Join-Path $ScriptRoot "assets") -Destination (Join-Path $BundleDir "assets")

# Copy core bundled tools and bin
$distBinDir = Join-Path $BundleDir "bin"
New-Item -ItemType Directory -Force -Path $distBinDir | Out-Null
$distToolsDir = Join-Path $BundleDir "tools"
New-Item -ItemType Directory -Force -Path $distToolsDir | Out-Null
$distWwwDir = Join-Path $BundleDir "www"
New-Item -ItemType Directory -Force -Path $distWwwDir | Out-Null

if (Test-Path (Join-Path $ScriptRoot "bin\nginx")) {
    Copy-Item -Recurse -Force (Join-Path $ScriptRoot "bin\nginx") -Destination (Join-Path $distBinDir "nginx")
}
if (Test-Path (Join-Path $ScriptRoot "tools\adminer")) {
    Copy-Item -Recurse -Force (Join-Path $ScriptRoot "tools\adminer") -Destination (Join-Path $distToolsDir "adminer")
}

# Create sample www index.php if empty
$indexFile = Join-Path $distWwwDir "index.php"
if (-not (Test-Path $indexFile)) {
    Set-Content -Path $indexFile -Value @"
<!DOCTYPE html>
<html>
<head>
    <title>Dev Lite Server</title>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #0f172a; color: #f8fafc; display: flex; align-items: center; justify-content: center; height: 100vh; margin: 0; }
        .card { background: #1e293b; padding: 40px; border-radius: 12px; box-shadow: 0 10px 25px rgba(0,0,0,0.5); text-align: center; border: 1px solid #334155; }
        h1 { color: #38bdf8; margin-top: 0; }
        p { color: #94a3b8; }
        .badge { display: inline-block; padding: 6px 12px; background: #0369a1; color: white; border-radius: 9999px; font-weight: bold; font-size: 13px; }
    </style>
</head>
<body>
    <div class="card">
        <span class="badge">Local Dev Server Active</span>
        <h1>Dev Lite Server</h1>
        <p>Your ultra-fast portable development environment is up and running.</p>
        <p>PHP Version: <b><?= phpversion(); ?></b></p>
    </div>
</body>
</html>
"@ -Encoding UTF8
}

Write-Host "✔ Portable bundle assembled at: $BundleDir" -ForegroundColor Green

# 4. Optional Portable ZIP Archive creation
if ($CreateZip) {
    Write-Host "[4/4] Packaging portable ZIP archive..." -ForegroundColor Yellow
    $zipPath = Join-Path $DistDir "DevLiteServer-v1.0.0-win-x64-portable.zip"
    if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
    Compress-Archive -Path "$BundleDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
    $zipSizeMB = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
    Write-Host "✔ Portable archive created: $zipPath ($zipSizeMB MB)" -ForegroundColor Green
}
else {
    Write-Host "[4/4] Skipping ZIP packaging (pass -CreateZip to generate archive)." -ForegroundColor Gray
}

Write-Host "`n[SUCCESS] Dev Lite Server is built and ready for distribution!" -ForegroundColor Cyan
