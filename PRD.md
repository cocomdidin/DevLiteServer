# PRODUCT REQUIREMENTS DOCUMENT (PRD)

## 1. Ringkasan Proyek (Project Overview)

- **Nama Produk**: Dev Lite Server
- **Tipe Aplikasi**: Windows Desktop Utility (System Tray Application)
- **Lisensi**: 100% Free & Open-Source Software (FOSS) - MIT License
- **Target OS**: Windows 10 & Windows 11 (64-bit)
- **Tech Stack Inti**: C# WinForms (.NET 10 LTS - `net10.0-windows`)
- **Model Distribusi**:
  - **Starter Bundle (Out-of-the-Box)**: Paket ZIP portabel (~200 MB) berisi `DevLiteServer.exe`, Nginx, PHP 8.4 NTS, Node.js v24 x64, Git for Windows (Portable/MinGit), MySQL 8.4, Mailpit, Composer, dan Adminer. Siap pakai langsung tanpa koneksi internet.
  - **Herd-Style Downloader**: Modul built-in untuk download versi tambahan/alternatif PHP (7.4, 8.1, 8.2, 8.3) dan Node.js (v20, v22, dst.) langsung dari sumber resmi.
- **Tujuan Utama**: Lingkungan pengembangan web lokal super cepat, portabel, modular, zero-registry, dan always-free. Alternatif modern pengganti Laragon (v7+ berbayar) dan XAMPP, dengan kemampuan package downloader mandiri ala Laravel Herd.

---

## 2. Layanan & Komponen yang Didukung (Supported Services)

LiteServer berfokus pada **Nginx** sebagai web server tunggal (Apache dieliminasi untuk efisiensi) dan menyediakan suite service lengkap:

1. **Web Server**: Nginx (Windows binary, FastCGI loopback)
2. **PHP Runtime**: Multi-version PHP-CGI (NTS x64)
3. **Node.js Runtime**: Multi-version Node.js & NPM (Default bundle: Node.js v24 x64)
4. **Version Control**: Git for Windows (PortableGit / MinGit terisolasi)
5. **Relational Databases (Default Credential: user kosongkan password / trust mode)**:
   - MySQL / MariaDB (Port 3306, user: `root`, password: `""`)
   - PostgreSQL (Port 5432, EDB portable binaries, user: `postgres`, password: `""`, auth: `trust`)
6. **In-Memory Cache**: Redis (Port 6379, Windows port binary via `tporadowski/redis` atau `redis-windows`)
7. **Local Mail Testing**: Mailpit (Port 1025 SMTP, Port 8025 Web UI)

---

## 3. Tech Stack & Arsitektur Sistem

### 3.1. Desktop Engine & UI
- **Framework**: C# WinForms (.NET 10 LTS)
- **Distribusi Binary**: Self-Contained Single Executable (`win-x64`, ~40-60 MB, zero runtime install).
- **UI/UX**: Modern Dashboard + System Tray (`NotifyIcon` + `ContextMenuStrip`), tema Dark/Light mode otomatis.

### 3.2. Sub-Sistem Arsitektur
1. **Win32 Job Object (Process Orchestration)**:
   - Seluruh child process (Nginx, PHP-CGI, MySQL, PostgreSQL, Redis, Mailpit) dikunci ke dalam satu Win32 Job Object dengan flag `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`.
   - Menjamin 100% zero zombie process jika LiteServer ditutup atau crash.
2. **FastCGI Supervisor (PHP Worker Loop)**:
   - Mengelola pool process `php-cgi.exe` (port 9000).
   - Auto-respawn worker untuk menangani batas default Windows `PHP_FCGI_MAX_REQUESTS` = 500.
3. **Runtime Downloader Engine (Herd-Style)**:
   - Unduh langsung dari official server tanpa server perantara:
     - **PHP (Active/Current)**: API `windows.php.net/downloads/releases/releases.json` (Varian NTS x64 untuk 8.1 - 8.4).
     - **PHP (Legacy 7.4)**: Direct archive download dari `windows.php.net/downloads/releases/archives/` (cth: `php-7.4.33-nts-Win32-vc15-x64.zip`).
     - **Node.js**: API `nodejs.org/dist/index.json` (Direct `.zip` x64).
     - **Mailpit**: GitHub API releases `axllent/mailpit`.
     - **Composer**: `getcomposer.org/composer.phar` + auto-generate `composer.bat`.
   - Streaming download dengan event progress report (0–100%) dan auto-unzip via `System.IO.Compression`.
4. **Portable Config Template Engine**:
   - Generator dinamis file konfigurasi (`nginx.conf`, `php.ini`, `my.ini`, `postgresql.conf`, `redis.conf`) dari template `.tpl` dengan injeksi variabel absolut path dan port saat boot.
5. **System Dependency Checker (MSVCRT)**:
   - Cek keberadaan runtime `VCRUNTIME140.dll` (Visual C++ 2015-2022 x64) saat startup LiteServer.
   - Jika belum terpasang, tampilkan dialog 1-klik unduh & jalankan `vc_redist.x64.exe` resmi dari Microsoft untuk mencegah crash silent pada binary PHP.

---

## 4. Persyaratan Fungsional (Functional Requirements)

| ID Fitur | Modul | Deskripsi Fungsional | Prioritas |
|---|---|---|---|
| **FR-01** | Master & Individual Controller | Tombol **Start All / Stop All / Exit**, tombol toggle on/off independen, form dialog khusus **Settings & Configuration** untuk mengelola service aktif (hanya service yang enabled yang tampil di dashboard serta dijalankan oleh Start All & Auto-start), konfigurasi port, auto-start, dan integrasi Windows logon. Tombol Exit menghentikan semua daemon aktif dan menutup aplikasi secara bersih tanpa proses tersisa. | P0 (Kritis) |
| **FR-02** | Process Isolation (Job Object) | Proteksi child processes via Win32 Job Object; garansi pembunuhan proses bersih tanpa port tertinggal. | P0 (Kritis) |
| **FR-03** | Port Conflict Resolver | Deteksi ketersediaan port (80, 9000, 3306, 5432, 6379, 1025, 8025) via `IPGlobalProperties`. Dialog peringatan dan opsi auto-reassign port jika bentrok. | P0 (Kritis) |
| **FR-04** | Dynamic Config Templating | Kompilasi template `.tpl` ke konfigurasi aktif saat start service dengan drive letter dinamis (portabel USB). | P0 (Kritis) |
| **FR-05** | Database Auto-Init | Inisialisasi otomatis: `mysqld.exe --initialize-insecure` jika folder data MySQL kosong; `initdb.exe` jika folder data PostgreSQL kosong. | P0 (Kritis) |
| **FR-06** | Runtime Downloader (Herd-Style) | UI dialog untuk download versi PHP (Legacy 7.4 via archives, Modern 8.1 - 8.4 via releases API) dan Node.js (LTS/Current) langsung dari website resmi. Ekstraksi otomatis dan auto-configure `php.ini` (aktifkan ekstensi populer). | P1 (Tinggi) |
| **FR-07** | Instant Version Switcher | Ganti versi PHP aktif untuk Nginx secara on-the-fly via klik kanan menu tray. Restart service terkait dalam < 1 detik. | P1 (Tinggi) |
| **FR-08** | Virtual Hosts & Batch Sync | Deteksi folder di `/www`. Otomatis generate konfigurasi Nginx vhost untuk domain `http://<folder>.test`. Pembaruan file `hosts` Windows dilakukan secara **Batch** (saat Start All atau tombol klik "Sync Hosts") dengan 1x prompt UAC per sync untuk mencegah spam dialog Admin. | P1 (Tinggi) |
| **FR-09** | Isolated Terminal Environment | Tombol "Open Terminal" yang membuka PowerShell/CMD dengan environment variable `PATH` yang sudah diinjeksi versi aktif PHP, Node.js (v24), Git, Composer, MySQL, PostgreSQL. Tidak merusak environment Windows global. | P1 (Tinggi) |
| **FR-10** | Quick Access Tools | Tombol cepat: Buka Folder `/www`, Buka Mailpit Web UI (`:8025`), Buka Database Manager (Default: Adminer web client via `http://localhost/adminer`, tombol disabled jika Nginx/PHP belum running; Auto-detect jika ada desktop GUI di folder `/tools/`). | P2 (Medium) |
| **FR-11** | Windows Logon Auto-Start | Opsi integrasi autorun saat user logon Windows (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`), dengan argumen `--tray` agar langsung berjalan di background system tray tanpa popup window. Bebas hak administrator/UAC. | P2 (Medium) |

---

## 5. Struktur Direktori Aplikasi (Portable Architecture)

```text
/DevLiteServer
│  DevLiteServer.exe          (C# WinForms .NET 10 Single Executable)
│  config.ini                   (Port, versi aktif, dan flag service)
│
├─/bin
│  ├─/nginx
│  │  └─/nginx-1.26.x           (Nginx binary & conf/nginx.conf.tpl)
│  ├─/php
│  │  ├─/php-8.4.x-nts          (Default bundle PHP runtime & php.ini.tpl)
│  │  └─/php-...-nts            (Versi tambahan hasil download)
│  ├─/nodejs
│  │  ├─/node-v24.x-win-x64     (Default bundle Node.js runtime, npm, npx)
│  │  └─/node-...-win-x64       (Versi tambahan hasil download)
│  ├─/git
│  │  └─/cmd                    (git.exe & tools git terisolasi)
│  ├─/mysql
│  │  └─/mysql-8.4.x            (MySQL binaries & data directory)
│  ├─/postgresql
│  │  └─/pgsql-16.x             (PostgreSQL binaries & data directory)
│  ├─/redis
│  │  └─/redis-7.x              (Redis server & cli binaries)
│  └─/mailpit
│     └─/mailpit.exe            (Mailpit single binary)
│
├─/templates                    (Nginx default conf, vhost.tpl, php.ini.tpl, my.ini.tpl)
├─/tools
│  ├─/adminer                   (adminer.php - web-based DB client untuk MySQL & PostgreSQL)
│  ├─/composer                  (composer.phar & composer.bat)
│  └─/(optional-gui)            (HeidiSQL / Beekeeper Studio portable jika ditaruh user)
└─/www                          (Root folder proyek web pengguna)
```

---

## 6. Persyaratan Non-Fungsional (Non-Functional Requirements)

1. **Ukuran Binary**:
   - File executable utama `LiteServer.exe` berkisar ~40–60 MB (Self-contained .NET 10 single-file, zero dependency).
2. **Konsumsi Memori (RAM)**:
   - Standby memory aplikasi LiteServer di System Tray < 40 MB.
3. **Portabilitas & Integritas OS**:
   - 100% portable: bebas registry write, no global PATH pollution, aman berjalan dari USB flashdrive.
4. **Privilege & Keamanan**:
   - Berjalan normal pada level standard user. Prompt UAC hanya diminta saat proses batch sync file `hosts` Windows (`FR-08`).

---

## 7. Desain Antarmuka & UX

1. **Dashboard Utama**:
   - **Service Grid**: Hanya menampilkan card untuk service yang berstatus **Enabled** (Nginx, PHP, MySQL, Mailpit, dll.) lengkap dengan status running badge, tombol Start/Stop per item, info port, dan version switcher.
   - **Quick Action Bar**: [Start All] [Stop All] [Exit] di header utama; serta secondary toolbar: [Open /www] [Terminal] [Mailpit] [Adminer] [Settings] [Updates].
   - **Settings Dialog**: Form modal khusus untuk konfigurasi visibilitas service dashboard, auto-start per-service, startup Windows logon, dan port jaringan.
   - **Runtime Manager Tab**: Panel untuk melihat, mengunduh, dan menghapus versi PHP & Node.js dengan indikator download progress.
2. **System Tray Integration**:
   - **Single-Instance Enforcement**: Named Mutex + Win32 Registered Window Message mencegah aplikasi terbuka dobel; dobel-klik shortcut desktop/portable otomatis me-restore dan memfokuskan jendela yang sedang diminimize ke tray.
   - Klik kanan ikon tray membuka Context Menu:
     - PHP Version -> Pilih versi terinstall
     - Node Version -> Pilih versi terinstall
     - Open Web Root (`/www`)
     - Open Mailpit Web (`:8025`)
     - Start All / Stop All Services
     - Exit (Otomatis stop semua child process)

---

## 8. Rencana Rilis & Milestone

- **Milestone 1 (Core Foundation)**:
  - Setup C# .NET 10 WinForms project.
  - Implementasi Win32 Job Object & process launcher.
  - Orchestrasi Nginx + PHP 8.4-CGI + MySQL (Start/Stop dasar).
  - Config template engine.
- **Milestone 2 (Expanded Services & Database) [SELESAI]**:
  - Integrasi PostgreSQL, Redis, dan Mailpit ke UI dashboard, card, settings, dan tray menu.
  - First-run auto-initialization untuk MySQL (`--initialize-insecure`) & PostgreSQL (`initdb`) data directory.
  - Port checker & interactive conflict resolver modal dialog (`PortConflictDialog`) dengan 1-klik auto-reassign.
  - System Dependency Checker (`VcRedistDialog`) dengan 1-klik download & auto-install MSVCRT x64 resmi dari Microsoft.
  - Dynamic config templates lengkap: `nginx.conf.tpl`, `my.ini.tpl`, `php.ini.tpl`, `postgresql.conf.tpl`, `redis.conf.tpl`.
- **Milestone 3 (Herd-Style Package Downloader) [SELESAI]**:
  - Downloader client untuk PHP (windows.php.net: 7.4, 8.1, 8.2, 8.3, 8.4) dan Node.js (nodejs.org: v18, v20, v22, v24).
  - Thin Core Bundle architecture: Nginx & core assets bundled (~15 MB), runtime heavy (MySQL, PostgreSQL, Redis, Mailpit) diunduh on-demand.
  - Lifecycle state `NotInstalled` dengan status badge neutral gray dan dynamic 1-klik "Install" button.
  - Auto-unzip, directory placement, single-wrapper folder flattening, dan auto-configure `php.ini`.
  - Isolated Terminal launcher dengan injected dynamic `PATH` untuk PHP, Node, Git, Composer, MySQL, PostgreSQL, Redis.
- **Milestone 4 (Virtual Host & Polish)**:
  - Auto Virtual Host generator (`*.test`) untuk Nginx.
  - System Tray menu lengkap & dark mode UI polish.
  - Publish single-file release script.
