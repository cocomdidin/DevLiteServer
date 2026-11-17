<div align="center">

# ⚡ Dev Lite Server

**High-performance, portable, and modern local web server for Windows.**  
A clean, lightweight alternative to Laragon and XAMPP for web developers.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0%20LTS-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Windows](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20x64-0078D6?logo=windows)](https://microsoft.com/windows)
[![Release](https://img.shields.io/github/v/release/cocomdidin/DevLiteServer?color=success&include_prereleases)](https://github.com/cocomdidin/DevLiteServer/releases)

[Quick Start](#-quick-start) •
[Why Dev Lite Server?](#-why-dev-lite-server) •
[Features](#-features-at-a-glance) •
[Default Stack & Ports](#-default-stack--ports) •
[Configuration](#-configuration) •
[Roadmap](#-roadmap--future-vision)

---

</div>

## 🚀 Quick Start

### 1. Download & Install
Download the latest installer (`DevLiteServer-Setup-vX.Y.Z.exe`) or portable archive from [GitHub Releases](https://github.com/cocomdidin/DevLiteServer/releases).

### 2. Launch
Run **Dev Lite Server**. On the very first launch, the built-in wizard will offer to download and set up core dependencies (Nginx, PHP 8.4, Composer CLI, and MSVCRT) with a single click.

### 3. Start Coding
- Put your projects in the `www/` folder or create custom virtual hosts in the **Sites** tab.
- Click **Start All** on the Dashboard.
- Open your browser at `http://localhost` or `http://yourproject.test`.

---

## 💡 Why Dev Lite Server?

- **True Portability (Zero-Registry)**:  
  No Windows Services installed. No permanent registry entries. Move your entire server folder to another drive or PC anytime.
- **Zero-UAC Dynamic Domains**:  
  Thanks to the built-in Micro DNS server, any `*.test` domain works instantly without needing administrator permission or manual `hosts` file editing.
- **No Orphan Processes**:  
  When you close Dev Lite Server, every single daemon (Nginx, PHP-CGI workers, MySQL, PostgreSQL, Redis, Mailpit, MinIO, and background workers) cleanly terminates via Windows Job Objects.
- **Multi-Worker PHP (No More 502 Bad Gateway)**:  
  PHP runs in an isolated worker pool with Nginx load balancing, keeping your local sites fast and responsive under concurrent requests.
- **Clean, Modern Dark UI**:  
  Fast, responsive interface with Dark Obsidian & Electric Indigo theme, designed to keep your local stack intuitive and out of your way.

---

## ✨ Features at a Glance

### 🌐 Sites, SSL & Virtual Hosts
- **Instant Local Domains & Hybrid Proxying**: Create virtual hosts like `myproject.test` with custom document roots, individual PHP versions, or Node.js/SSR reverse proxy with WebSocket HMR support (Vite, Next.js, Nuxt).
- **Zero-Config Local & Outbound SSL**: 1-click trusted local Root CA & per-domain HTTPS certificates for Nginx, plus automatic Mozilla `cacert.pem` provisioning (`curl.cainfo` & `openssl.cafile`) so PHP cURL/Guzzle outbound HTTPS requests work out of the box.
- **1-Click Cloudflare Tunnel**: Share local sites instantly via public HTTPS URLs (`*.trycloudflare.com`) powered by `cloudflared`.
- **One-Click Domain Sync**: Single "Sync Domains" button that routes domains via local DNS (Zero-UAC) with an automatic fallback to the Windows hosts file.
- **Open in Editor**: Launch your configured code editor (VS Code, Cursor, PhpStorm, etc.) directly into your project directory with a single click.

### 🐘 PHP & Extension Manager
- **Extension Manager GUI**: Enable or disable PHP extensions (`pdo_mysql`, `curl`, `mbstring`, `intl`, `xdebug`, `redis`, etc.) with simple checkboxes—no manual `php.ini` editing needed.
- **Multi-Version Support**: Switch between PHP 7.4, 8.1, 8.2, 8.3, and 8.4 per site or globally.
- **Download Catalogs**: Download and install additional PHP versions directly from within the app.

### 🗄️ Database & Clients
- **Pre-configured MySQL 8.4 & PostgreSQL 17**: Ready out of the box with default credentials for instant local development.
- **Database Client Selector**: Choose your favorite client—**HeidiSQL**, **AdminerEvo**, **phpMyAdmin**, or **Tiny RDM** for Redis—with auto-detection for existing tools like DBeaver and TablePlus.

### 📬 Built-in Local Mailpit
- Local SMTP server listening on `127.0.0.1:1025` to safely capture test emails sent by your applications.
- Clean web inbox viewer at `http://localhost:8025` to inspect email contents, headers, and HTML formatting.
- Persistent SQLite inbox storage enabled out of the box (`bin/mailpit/data/mailpit.db`).

### ⚙️ Background Workers & Task Manager
- **Windows Supervisor (`Workers` tab)**: Run and monitor persistent project tasks such as `php artisan queue:work`, `php artisan horizon`, or `npm run dev` directly inside Dev Lite Server.
- **Live Logs & Clean Shutdown**: View real-time stdout/stderr logs, configure auto-start per worker, and guarantee zero orphaned processes on exit.

### 🪣 Built-in MinIO S3 Mock
- Local S3-compatible object storage running on `127.0.0.1:9100` (API) with Web Console at `http://127.0.0.1:9101` (`minioadmin` / `minioadmin`), avoiding conflicts with PHP FastCGI port `9000`.

### 💻 Developer Tools & Terminal
- **Isolated Developer Terminal**: Launch PowerShell or CMD with pre-configured environment paths for PHP, Composer, Node.js, Git, and MySQL clients without altering your global Windows environment.
- **Interactive Port Conflict Resolver**: Automatically detects occupied ports on startup (80, 3306, etc.) and suggests free alternatives with 1-click reassignment.

---

## 📦 Default Stack & Ports

Dev Lite Server uses lightweight, standard local ports:

| Component | Default Port / Access | Notes |
|---|---|---|
| **Nginx** | `http://localhost:80` / `https://localhost:443` | High-performance reverse proxy & web server (HTTP/HTTPS) |
| **Local Domains** | `http://*.test` / `https://*.test` | Handled automatically via local DNS & trusted local CA |
| **PHP FastCGI** | `127.0.0.1:9000-9003` | Multi-worker pool (default 4 workers per version) |
| **MySQL** | `localhost:3306` | User: `root` (no password) |
| **PostgreSQL** | `localhost:5432` | User: `postgres` (trust auth, no password) |
| **Redis** | `127.0.0.1:6379` | In-memory key-value cache |
| **Mailpit Web UI**| `http://localhost:8025` | Local webmail viewer |
| **Mailpit SMTP** | `127.0.0.1:1025` | Local test mail capture |
| **MinIO S3 API** | `http://127.0.0.1:9100` | S3-compatible local storage (`minioadmin` / `minioadmin`) |
| **MinIO Console** | `http://127.0.0.1:9101` | MinIO web admin dashboard |

---

## ⚙️ Configuration

You can configure ports, default editors, and auto-start behaviors directly from the **Settings** tab in the application.

Advanced options can also be adjusted in `config.ini`:

```ini
[General]
StartWithWindows=false
MinimizeToTray=true
AutoStartServices=false
CheckUpdatesOnStart=true
PreferredEditor=vscode

[Networking]
EnableLocalDns=true
HttpPort=80
HttpsPort=443

[Services]
Nginx=true
Php=true
Mysql=true
Postgresql=false
Redis=false
Mailpit=true
Minio=false
```

---

## 🗺️ Roadmap & Future Vision

We are actively expanding Dev Lite Server to be the best zero-virtualization development suite on Windows. See [ROADMAP.md](ROADMAP.md) for detailed technical specifications and upcoming milestones:

- ✅ **Smart Framework Auto-Detection**: Instant docroot detection for Laravel (`/public`), WordPress, and Symfony.
- ✅ **Hybrid Proxying (Node.js/SSR)**: Nginx reverse proxy with full WebSocket HMR support for Vite, Next.js, and Nuxt.
- ✅ **Cloudflare Tunnel (1-Click Share)**: Instant public HTTPS demo URLs (`*.trycloudflare.com`) powered by `cloudflared`.
- ✅ **Windows Supervisor (Background Workers)**: GUI task manager to run queues (`artisan queue:work`) and dev servers (`npm run dev`) with zero orphaned processes.
- ✅ **Local S3 Storage (MinIO)**: Built-in S3-compatible object storage mock.
- 🔲 **Mini Scheduler Runner**: Lightweight cron/task scheduler for `php artisan schedule:run`.
- 🔲 **Search Engine Mock & SQLite GUI Viewer**: Local Meilisearch/Typesense integration and built-in SQLite table browser.
- 🔲 **Unified Log Viewer & Dump Interceptor**: Centralized real-time viewer for Nginx, PHP, MySQL, and Laravel logs.

---

## ❓ Frequently Asked Questions (FAQ)

<details>
<summary><strong>Do I need to run Dev Lite Server as Administrator?</strong></summary>

No! Standard features (Nginx, PHP, MySQL, Mailpit) run entirely under normal user privileges. An elevation prompt (UAC) is only requested once if you configure Windows DNS for Zero-UAC local domains or when using the hosts file fallback.
</details>

<details>
<summary><strong>Port 80 or 3306 is already in use by another program. What should I do?</strong></summary>

Dev Lite Server includes an automatic port conflict resolver. When a conflict is detected on startup, the app will notify you and offer to switch to a recommended free port (e.g., port 8080 or 3307) with a single click.
</details>

<details>
<summary><strong>How do I connect to MySQL from my application?</strong></summary>

- **Host**: `127.0.0.1` or `localhost`
- **Port**: `3306`
- **Username**: `root`
- **Password**: *(leave blank)*
</details>

---

## 🤝 Contributing & Building from Source

Interested in contributing or building the app from source? Please see [CONTRIBUTING.md](CONTRIBUTING.md).

---

## 📄 License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.
