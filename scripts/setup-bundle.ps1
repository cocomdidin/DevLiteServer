<#
.SYNOPSIS
    Automated provisioner for Local Lite Server portable binaries.
.DESCRIPTION
    Downloads official, clean binaries for Nginx, PHP 8.4, Node.js v24, Git, MySQL 8.4,
    Mailpit, and Composer into the portable /bin/ and /tools/ directory structure.
.PARAMETER Component
    Specific component to setup: All, Nginx, Php, Node, Git, Mailpit, Composer, Mysql.
#>

[CmdletBinding()]
param (
    [ValidateSet("All", "Core", "Nginx", "Php", "Node", "Git", "Mailpit", "Composer", "Mysql")]
    [string]$Component = "Core"
)

$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $PSScriptRoot
if (-not $ScriptRoot) { $ScriptRoot = Get-Location }

$BinDir = Join-Path $ScriptRoot "bin"
$ToolsDir = Join-Path $ScriptRoot "tools"
$TempDir = Join-Path $ScriptRoot "build\temp_downloads"

if (-not (Test-Path $TempDir)) {
    New-Item -ItemType Directory -Force -Path $TempDir | Out-Null
}

function Download-And-Extract {
    param (
        [string]$Name,
        [string]$Url,
        [string]$ZipName,
        [string]$TargetDir,
        [switch]$FlattenSingleSubdir
    )

    Write-Host "`n[$Name] Starting setup..." -ForegroundColor Cyan
    $ZipPath = Join-Path $TempDir $ZipName

    if (-not (Test-Path $ZipPath)) {
        Write-Host "[$Name] Downloading from: $Url" -ForegroundColor Gray
        $client = New-Object System.Net.WebClient
        $client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) LocalLiteServer/1.0")
        $client.DownloadFile($Url, $ZipPath)
        Write-Host "[$Name] Download complete." -ForegroundColor Green
    } else {
        Write-Host "[$Name] Using cached download: $ZipPath" -ForegroundColor DarkGray
    }

    if (-not (Test-Path $TargetDir)) {
        New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null
    }

    Write-Host "[$Name] Extracting to: $TargetDir..." -ForegroundColor Gray

    if ($FlattenSingleSubdir) {
        $ExtractTemp = Join-Path $TempDir "temp_extract_$Name"
        if (Test-Path $ExtractTemp) { Remove-Item -Recurse -Force $ExtractTemp }
        New-Item -ItemType Directory -Force -Path $ExtractTemp | Out-Null

        Expand-Archive -Path $ZipPath -DestinationPath $ExtractTemp -Force

        $subDirs = Get-ChildItem -Directory -Path $ExtractTemp
        if ($subDirs.Count -eq 1) {
            Get-ChildItem -Path $subDirs[0].FullName | Move-Item -Destination $TargetDir -Force
        } else {
            Get-ChildItem -Path $ExtractTemp | Move-Item -Destination $TargetDir -Force
        }
        Remove-Item -Recurse -Force $ExtractTemp
    } else {
        Expand-Archive -Path $ZipPath -DestinationPath $TargetDir -Force
    }

    Write-Host "[$Name] Ready." -ForegroundColor Green
}

# 1. NGINX
function Setup-Nginx {
    $target = Join-Path $BinDir "nginx\nginx-1.26.3"
    Download-And-Extract -Name "Nginx" `
        -Url "https://nginx.org/download/nginx-1.26.3.zip" `
        -ZipName "nginx-1.26.3.zip" `
        -TargetDir (Join-Path $BinDir "nginx")
}

# 2. PHP 8.4 NTS x64
function Setup-Php {
    $target = Join-Path $BinDir "php\php-8.4"
    Download-And-Extract -Name "PHP 8.4" `
        -Url "https://windows.php.net/downloads/releases/php-8.4.26-nts-Win32-vs17-x64.zip" `
        -ZipName "php-8.4.26-nts.zip" `
        -TargetDir $target

    # Auto-configure php.ini
    $iniDev = Join-Path $target "php.ini-development"
    $iniTarget = Join-Path $target "php.ini"

    if ((Test-Path $iniDev) -and (-not (Test-Path $iniTarget))) {
        Write-Host "[PHP 8.4] Generating initial php.ini with common extensions..." -ForegroundColor Gray
        $content = Get-Content $iniDev -Raw

        # Uncomment extension directory
        $content = $content -replace ';extension_dir = "ext"', 'extension_dir = "ext"'

        # Uncomment required extensions for Laravel, WordPress, standard web apps
        $extensions = @("curl", "fileinfo", "mbstring", "mysqli", "openssl", "pdo_mysql", "pdo_sqlite", "sqlite3")
        foreach ($ext in $extensions) {
            $content = $content -replace ";extension=$ext", "extension=$ext"
        }

        # Max upload and memory tweaks for local dev
        $content = $content -replace 'upload_max_filesize = 2M', 'upload_max_filesize = 128M'
        $content = $content -replace 'post_max_size = 8M', 'post_max_size = 128M'
        $content = $content -replace 'memory_limit = 128M', 'memory_limit = 512M'

        Set-Content -Path $iniTarget -Value $content
        Write-Host "[PHP 8.4] php.ini configured." -ForegroundColor Green
    }
}

# 3. MAILPIT
function Setup-Mailpit {
    $target = Join-Path $BinDir "mailpit"
    Download-And-Extract -Name "Mailpit" `
        -Url "https://github.com/axllent/mailpit/releases/latest/download/mailpit-windows-amd64.zip" `
        -ZipName "mailpit-windows-amd64.zip" `
        -TargetDir $target
}

# 4. COMPOSER
function Setup-Composer {
    $target = Join-Path $ToolsDir "composer"
    if (-not (Test-Path $target)) { New-Item -ItemType Directory -Force -Path $target | Out-Null }

    $pharPath = Join-Path $target "composer.phar"
    $batPath = Join-Path $target "composer.bat"

    Write-Host "`n[Composer] Setting up..." -ForegroundColor Cyan
    if (-not (Test-Path $pharPath)) {
        Write-Host "[Composer] Downloading composer.phar..." -ForegroundColor Gray
        Invoke-WebRequest -Uri "https://getcomposer.org/composer.phar" -OutFile $pharPath
    }

    $batContent = "@echo off`r`nphp `"%~dp0composer.phar`" %*"
    Set-Content -Path $batPath -Value $batContent
    Write-Host "[Composer] Ready with composer.bat wrapper." -ForegroundColor Green
}

# 5. NODE.JS v24 x64
function Setup-Node {
    $target = Join-Path $BinDir "nodejs\node-v24"
    Download-And-Extract -Name "Node.js v24" `
        -Url "https://nodejs.org/dist/v24.21.0/node-v24.21.0-win-x64.zip" `
        -ZipName "node-v24.21.0-win-x64.zip" `
        -TargetDir $target `
        -FlattenSingleSubdir
}

# 6. MinGit
function Setup-Git {
    $target = Join-Path $BinDir "git"
    Download-And-Extract -Name "MinGit" `
        -Url "https://github.com/git-for-windows/git/releases/download/v2.56.0.windows.2/MinGit-2.56.0.2-64-bit.zip" `
        -ZipName "MinGit-2.56.0.2-64-bit.zip" `
        -TargetDir $target
}

# 7. MYSQL 8.4 (~259MB)
function Setup-Mysql {
    $target = Join-Path $BinDir "mysql\mysql-8.4"
    Download-And-Extract -Name "MySQL 8.4" `
        -Url "https://cdn.mysql.com/archives/mysql-8.4/mysql-8.4.4-winx64.zip" `
        -ZipName "mysql-8.4.4-winx64.zip" `
        -TargetDir $target `
        -FlattenSingleSubdir
}

# Execution Dispatcher
switch ($Component) {
    "Core" {
        Setup-Nginx
        Setup-Php
        Setup-Mailpit
        Setup-Composer
    }
    "All" {
        Setup-Nginx
        Setup-Php
        Setup-Mailpit
        Setup-Composer
        Setup-Node
        Setup-Git
        Setup-Mysql
    }
    "Nginx"    { Setup-Nginx }
    "Php"      { Setup-Php }
    "Mailpit"  { Setup-Mailpit }
    "Composer" { Setup-Composer }
    "Node"     { Setup-Node }
    "Git"      { Setup-Git }
    "Mysql"    { Setup-Mysql }
}

Write-Host "`n[OK] Setup completed successfully for component: $Component" -ForegroundColor Green
