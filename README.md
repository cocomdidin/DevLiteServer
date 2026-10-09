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
- 🛡️ **Win32 Job Object Process Supervision**:
  All child daemon processes are assigned to a kernel Job Object with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`. Even in the event of an abrupt crash, zero orphan daemons remain.
- 🔒 **Single-Instance Mutex & Focus Activation**:
  Prevents duplicate application instances. Double-clicking desktop or portable shortcuts while minimized automatically restores and focuses the active window.
- 🔄 **Nginx & FastCGI Auto-Pairing**:
  Eliminates `502 Bad Gateway` errors. Starting Nginx automatically boots upstream `php-cgi.exe` workers with auto-recovery loops (`PHP_FCGI_MAX_REQUESTS`).
- 💻 **Isolated Developer Terminal**:
  One-click launcher for PowerShell / CMD with an isolated `PATH` containing PHP, Node.js, Git, Composer, and MySQL client binaries without polluting your Windows environment.
- 📬 **Built-in Mailpit Suite**:
  Zero-configuration local SMTP testing (`127.0.0.1:1025`) and beautiful web inbox viewer (`http://localhost:8025`).
- 🗄️ **Zero-Fuss MySQL 8.4**:
  Out-of-the-box local database with empty root password preset for instant development.
- 🚀 **In-App Auto-Update System**:
  Built-in GitHub Release checker with one-click direct download, progress bar, graceful daemon shutdown, and installer execution.
- 🎨 **Modern Dark OLED UI & Graceful Exit**:
  High-DPI optimized, responsive WinForms dashboard with live status indicators, per-service auto-start controls, port conflict detection, and dedicated one-click **Exit** button that cleanly terminates all daemons and the application.
- 🪟 **Windows Logon Auto-Start**:
  Optional single-click startup on Windows logon (`StartWithWindows=true`) registering into `HKCU` Run key, launching minimized to tray with zero UAC prompts.

---

## ⚙️ Configuration (`config.ini`)

Dev Lite Server is fully configured via `config.ini`:

```ini
[General]
AppName=Dev Lite Server
StartWithWindows=false
MinimizeToTray=true
AutoStartServices=false
CheckUpdatesOnStart=true

[AutoStart]
Nginx=false
Php=false
Mysql=false
Mailpit=false
Postgresql=false
Redis=false
```

> **Note**: By default, no daemons auto-start until enabled. You can toggle auto-start behavior per-service and Windows logon startup directly from the System Tray context menu under **Auto-start Services** and **Start with Windows**.

---

## 📦 Default Stack & Ports

| Service / Tool | Version | Port / Access | Notes |
|---|---|---|---|
| **Nginx** | 1.26.x | `http://localhost:80` | High-performance reverse proxy & web server |
| **PHP (NTS x64)** | 8.4.x | `127.0.0.1:9000` | FastCGI daemon with auto-supervision |
| **MySQL** | 8.4.x | `3306` | Default user: `root` (no password) |
| **Mailpit Web UI** | Latest | `http://localhost:8025` | Webmail inbox viewer |
| **Mailpit SMTP** | Latest | `127.0.0.1:1025` | Local mail capture daemon |
| **Node.js** | v24.x | CLI | Portable runtime with NPM & NPX |
| **MinGit** | Latest | CLI | Isolated Git for Windows CLI |
| **Composer** | Latest | CLI | PHP package manager |
| **Adminer** | Latest | Web | Single-file database management client |

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
dotnet publish DevLiteServer.csproj -c Release -r win-x64 --self-contained true -o ./dist/app
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
