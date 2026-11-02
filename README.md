<div align="center">

# ⚡ Dev Lite Server

**High-performance, portable, lightweight, and zero-registry local WEMP stack for Windows.**

An open-source, modern alternative to Laragon and XAMPP powered by .NET 10 LTS and Win32 Job Objects.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0%20LTS-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Windows](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20x64-0078D6?logo=windows)](https://microsoft.com/windows)
[![Release](https://img.shields.io/github/v/release/cocomdidin/DevLiteServer?color=success&include_prereleases)](https://github.com/cocomdidin/DevLiteServer/releases)
[![Conventional Commits](https://img.shields.io/badge/Conventional%20Commits-1.0.0-%23FE5196?logo=conventionalcommits)](COMMIT_CONVENTION.md)

[Features](#-key-features) •
[Quick Start](#-quick-start) •
[Default Stack & Ports](#-default-stack--ports) •
[Architecture](#-architecture) •
[Building from Source](#-building-from-source) •
[Contributing](#-contributing)

---

</div>

## 📖 Overview

**Dev Lite Server** is designed for web developers who want a blazing-fast local server environment without system bloat, registry pollution, or rogue background services. 

Unlike traditional solutions, Dev Lite Server does not install Windows Services or alter global system environment variables. It operates with a **portable-first philosophy** and relies on **Win32 Job Object kernel primitives** to guarantee that every daemon (Nginx, PHP-CGI, MySQL, Mailpit) cleanly terminates the moment you close the application.

---

## ✨ Key Features

- ⚡ **True Portability & Zero-Registry**:
  Run from anywhere (`C:\DevLiteServer`, a secondary SSD, or a USB drive). No Windows services registered, no leftover registry entries.
- 🚀 **First-Run Core Auto-Setup (Herd-Style)**:
  On fresh launch, automatically detects missing core dependencies (MSVCRT x64, Nginx 1.26, PHP 8.4 NTS, Composer CLI, MinGit Portable, AdminerEvo) and provisions them in a sequential 1-click wizard with 1-click Windows User PATH registration and starter dashboard initialization.
- 🛡️ **Win32 Job Object Process Supervision**:
  All child daemon processes are assigned to a kernel Job Object with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`. Even in the event of an abrupt crash, zero orphan daemons remain.
- 🔒 **Single-Instance Mutex & Focus Activation**:
  Prevents duplicate application instances. Double-clicking desktop or portable shortcuts while minimized automatically restores and focuses the active window.
- ⚡ **PHP Multi-Worker Concurrency Pool & FastCGI Auto-Pairing (Herd-Style)**:
  Eliminates `502 Bad Gateway` and freeze/hang under heavy or concurrent traffic. Runs multi-process `php-cgi.exe` worker pools (default 4 workers per PHP version) coupled with Nginx dynamic `upstream` round-robin load balancing, persistent `fastcgi_keep_conn on`, and FastCGI buffer & timeout tuning. Auto-recovery loop independently respawns dead workers without downtime.
- 🔌 **Interactive Port Conflict Resolver**:
  Automatically detects port collisions on startup (80, 9000, 3306, 5432, 6379, 1025, 8025) and offers 1-click reassignment to recommended free ports without manual config editing.
- 🧩 **System Dependency Checker (MSVCRT)**:
  Scans for `VCRUNTIME140.dll` on startup with 1-click automated download and installation of the official Microsoft Visual C++ 2015-2022 x64 Redistributable.
- 💻 **Isolated Developer Terminal**:
  One-click launcher for PowerShell / CMD with an isolated `PATH` containing PHP, Node.js, Git, Composer, MySQL, PostgreSQL, and Redis client binaries without polluting your Windows environment.
- 📬 **Built-in Mailpit Suite**:
  Zero-configuration local SMTP testing (`127.0.0.1:1025`) and beautiful web inbox viewer (`http://localhost:8025`).
- 🗄️ **Zero-Fuss MySQL 8.4 & PostgreSQL 17**:
  Out-of-the-box local database with empty root password / trust auth preset for instant development.
- 🗃️ **Multi-Persona Database Client Manager**:
  Interactive DB client selector tailored to all user levels: **phpMyAdmin** (classic web UI for MySQL), **AdminerEvo** (lightweight multi-engine web manager for MySQL, PostgreSQL, SQLite), **HeidiSQL Portable** (high-performance native desktop GUI connecting directly via TCP), **Tiny RDM Portable** (modern desktop GUI for Redis key-value stores), and **redis-cli** (interactive loopback terminal). Auto-detects existing system clients (DBeaver, TablePlus, Beekeeper Studio, RedisInsight).
- 🚀 **In-App Auto-Update System**:
  Built-in GitHub Release checker with one-click direct download, progress bar, graceful daemon shutdown, and installer execution.
- 🎨 **Modern Dark OLED UI, Dedicated Settings & Clean Dashboard**:
  High-DPI optimized, responsive WinForms dashboard that displays only currently enabled services. Includes a dedicated **Settings & Configuration** dialog with port management, per-service dashboard visibility toggles, auto-start options, and dedicated one-click **Exit** button that cleanly terminates all daemons and the application.
- 💻 **Configurable Code Editor & Sites Shortcut**:
  Set your preferred text/code editor (VS Code, Cursor, Antigravity, PhpStorm, or custom executable) in Settings. Open any site's codebase or `/www` web root directly into your editor with a single click from the Sites tab.
- 🪟 **Windows Logon Auto-Start**:
  Optional single-click startup on Windows logon (`StartWithWindows=true`) registering into `HKCU` Run key, launching minimized to tray with zero UAC prompts.

---

## ⚙️ Configuration (`config.ini` & Settings Dialog)

Dev Lite Server can be configured either via the built-in **Settings** UI dialog (click the `Settings` button on the toolbar or right-click the System Tray icon) or directly via `config.ini`:

```ini
[General]
AppName=Dev Lite Server
StartWithWindows=false
MinimizeToTray=true
AutoStartServices=false
CheckUpdatesOnStart=true
PreferredEditor=vscode
CustomEditorPath=

[AutoStart]
Nginx=false
Php=false
Mysql=false
Mailpit=false
Postgresql=false
Redis=false

[Services]
Nginx=true
Php=true
Mysql=true
Postgresql=false
Redis=false
Mailpit=true

[Php]
WorkerCount=4

[Tools]
DefaultMySqlClient=ask
DefaultPgClient=ask
DefaultRedisClient=ask
```

> **Note**:
> - **Enabled Services (`[Services]`)**: Controls whether a service is active in the environment. Services marked `false` (Disabled) are hidden from the dashboard and skipped by **Start All** and **Auto-start**. You can toggle active services directly from the **Settings** dialog or the System Tray menu under **Enabled Services**.
> - **Auto-Start (`[AutoStart]`)**: By default, no services auto-start until enabled. You can toggle auto-start behavior per-service and Windows logon startup from the **Settings** dialog or System Tray context menu under **Auto-start Services** and **Start with Windows**.

---

## 📦 Stack & Ports

### Slim Zero-Bundle Architecture
Dev Lite Server ships as an ultra-compact starter package (~16 MB). All services and runtimes are provisioned on-demand with 1-click installation directly from official sources:

| Service / Tool | Version | Port / Access | Notes |
|---|---|---|---|
| **Nginx** | 1.26.x | `http://localhost:80` | High-performance reverse proxy & web server (1-click install) |
| **PHP (NTS x64)** | 7.4, 8.1, 8.2, 8.3, 8.4 | `127.0.0.1:9000-9003` | FastCGI multi-worker pool (default 4 workers) with auto-supervision & multi-version switcher |
| **Node.js** | v18, v20, v22, v24 | CLI | Portable runtime with NPM & NPX or System Node |
| **MySQL** | 8.4.x | `3306` | Default user: `root` (no password) |
| **PostgreSQL** | 17.x | `5432` | Default user: `postgres` (trust auth, no password) |
| **Redis** | 5.x / 7.x | `6379` | In-memory key-value cache store |
| **Mailpit Web UI** | Latest | `http://localhost:8025` | Webmail inbox viewer |
| **Mailpit SMTP** | Latest | `127.0.0.1:1025` | Local mail capture daemon |
| **MinGit** | Latest | CLI | Isolated Git for Windows CLI |
| **Composer** | Latest | CLI | PHP package manager |
| **Database Clients** | phpMyAdmin, AdminerEvo, HeidiSQL, Tiny RDM, redis-cli | GUI / Web / CLI | Selectable on-the-fly or saved preference per database engine |

---

## 🚀 Quick Start

### Option A: Windows Installer (Recommended)
1. Download the latest `DevLiteServer-Setup-vX.Y.Z.exe` from [GitHub Releases](https://github.com/cocomdidin/DevLiteServer/releases).
2. Double-click the installer and choose your destination (default: `C:\DevLiteServer`).
3. Launch **Dev Lite Server** from the Desktop shortcut or Start Menu.
4. Open your browser and navigate to `http://localhost`.

### Option B: Portable ZIP
1. Download `DevLiteServer-vX.Y.Z-portable.zip` from [GitHub Releases](https://github.com/cocomdidin/DevLiteServer/releases).
2. Extract the archive to any folder.
3. Run `DevLiteServer.exe`.

---

## 📁 Directory Structure

```text
C:\DevLiteServer/
├── DevLiteServer.exe          # .NET 10 application supervisor
├── config.ini                 # Active ports, versions, and service flags
├── assets/                    # Application icons (app.ico, app.png)
├── bin/
│   ├── nginx/                 # Nginx binaries and configurations
│   ├── php/
│   │   └── php-8.4/           # PHP FastCGI binary & php.ini
│   ├── mysql/                 # MySQL daemon & database data/
│   ├── nodejs/
│   │   └── node-v24/          # Node.js and npm binaries
│   ├── git/                   # Isolated Portable MinGit
│   └── mailpit/               # Mailpit executable
├── templates/                 # Dynamic config templates (nginx.conf.tpl, php.ini.tpl)
├── tools/
│   ├── composer/              # Composer binary & runner script
│   └── adminer/               # Adminer database management UI
└── www/                       # Web root (DocumentRoot for http://localhost)
    └── index.php              # Starter dashboard landing page
```

---

## 🛠️ Architecture

```
┌────────────────────────────────────────────────────────┐
│            Dev Lite Server (WinForms .NET 10)          │
│  ┌──────────────────────┐    ┌──────────────────────┐  │
│  │   UI & System Tray   │    │  JobObject (Kernel)  │  │
│  └──────────┬───────────┘    └──────────┬───────────┘  │
│             │                           │              │
│  ┌──────────▼───────────────────────────▼───────────┐  │
│  │           Process Supervisor & Lifecycle          │  │
│  └──────┬─────────────┬─────────────┬─────────────┬─┘  │
└─────────┼─────────────┼─────────────┼─────────────┼────┘
          ▼             ▼             ▼             ▼
     ┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐
     │  Nginx  │   │ PHP-CGI │   │  MySQL  │   │ Mailpit │
     │ (80)    │   │ (9000)  │   │ (3306)  │   │ (8025)  │
     └─────────┘   └─────────┘   └─────────┘   └─────────┘
```

- **Runtime Target**: .NET 10 LTS (`net10.0-windows`)
- **Process Management**: Native Win32 API P/Invoke (`CreateJobObject`, `SetInformationJobObject`, `AssignProcessToJobObject`)
- **Template Engine**: Dynamic token interpolation (`{{HTTP_PORT}}`, `{{ROOT_DIR}}`, `{{PHP_PORT}}`)
- **CI/CD Pipeline**: GitHub Actions with automated Semantic Versioning (`paulhatch/semantic-version`), automated binary packaging, and Inno Setup compilation.

---

## 💻 Building from Source

### Prerequisites
- Windows 10 / 11 (64-bit)
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PowerShell 7+ (pwsh) or Windows PowerShell 5.1
- (Optional) [Inno Setup 6](https://jrsoftware.org/isdl.php) for building the installer

### 1. Clone the Repository
```powershell
git clone https://github.com/cocomdidin/DevLiteServer.git
cd DevLiteServer
```

### 2. Provision Runtime Binaries
Run the provisioning script to download clean upstream binaries into `/bin` and `/tools`:
```powershell
.\scripts\setup-bundle.ps1 -Component All
```

### 3. Build & Run
```powershell
dotnet run
```

### 4. Build Installer Locally (Optional)
```powershell
dotnet publish DevLiteServer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./dist/app
iscc.exe /DMyAppVersion=1.0.0 /DSourceDir=..\dist\app installer\setup.iss
```

---

## 🤝 Contributing

We welcome contributions! Please review our commit guidelines before submitting pull requests:

1. **Commit Convention**: This project strictly enforces [Conventional Commits](https://www.conventionalcommits.org/). Automated Semantic Versioning depends on clean commit types (`feat`, `fix`, `perf`, `refactor`, `chore`, `docs`). See [COMMIT_CONVENTION.md](COMMIT_CONVENTION.md).
2. **Install Local Git Hooks**:
   ```powershell
   .\scripts\install-git-hooks.ps1
   ```
3. Create a feature branch: `git checkout -b feat/my-new-feature`
4. Commit your changes: `git commit -m "feat(ui): add dark mode toggle"`
5. Push to your fork and submit a Pull Request.

---

## 📄 License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.
