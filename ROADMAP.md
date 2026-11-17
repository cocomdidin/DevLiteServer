# 🗺️ DevLiteServer Technical Roadmap & Future Vision

Dokumen ini mencatat rencana strategis dan teknis pengembangan **DevLiteServer** ke depan. Tujuannya adalah menjadikan DevLiteServer sebagai solusi *local development environment* nomor 1 di Windows yang ringan, hemat resource (tanpa virtualisasi/Docker VM), portabel, dan siap untuk stack web modern (PHP + Node.js/SSR + Cloud Services).

---

## 🎯 Positioning & Core Differentiators
1. **Native Performance & Ultra-low Memory**: Menggunakan proses native Windows yang diisolasi via Windows `JobObject` (bukan VM WSL2/Vmmem).
2. **Zero Orphan Processes**: Tidak ada service/daemon tertinggal saat aplikasi ditutup.
3. **Modern Hybrid Stack**: Mendukung PHP (FastCGI) sekaligus Node.js/SSR Reverse Proxy di domain lokal `.test` (lengkap dengan SSL otomatis).
4. **Zero-Lockout / 100% Free Core**: Fitur-fitur vital (Node switcher, Mail catcher, Tunneling) tersedia tanpa biaya langganan.

---

## 📅 Roadmap Pelaksanaan

### Phase 1: Smart VHost & Hybrid Proxying (Priority: High)
- [ ] **Smart Framework Auto-Detection**:
  - Deteksi otomatis struktur project (Laravel `public/`, WordPress root, Symfony, Next.js/Nuxt) saat menambahkan site baru atau scanning folder `www/`.
- [ ] **Reverse Proxy VHost (Node.js / SSR / Vite)**:
  - Dukungan vhost tipe `Proxy` di samping tipe `PHP`.
  - Nginx template dengan WebSocket upgrade (`Upgrade`, `Connection: "upgrade"`) untuk Vite HMR dan Next.js SSR.
- [ ] **Auto-SSL per Domain**:
  - Integrasi auto-generate SSL local cert untuk setiap domain `.test` yang ditambahkan.

### Phase 2: Remote Sharing & Public Tunnel (Priority: High)
- [ ] **Cloudflare Tunnel (`cloudflared`) Integration**:
  - Download & kelola binary `cloudflared.exe` via Package Catalog.
  - Fitur "One-Click Share" di menu Site/Dashboard untuk mendapatkan URL publik HTTPS (`https://*.trycloudflare.com`) instan tanpa port forwarding atau akun berbayar.
  - Status monitor tunnel dan tombol copy URL langsung dari GUI/Tray.

### Phase 3: Background Worker & Task Manager ("Windows Supervisor") (Priority: Medium-High)
- [ ] **Process Monitor Dashboard**:
  - Menjalankan background long-running command seperti `php artisan queue:listen`, `npm run dev`, atau `php artisan octane:start`.
  - Pengelolaan lifecycle (Start/Stop/Restart) via Windows `JobObject` agar proses mati total saat aplikasi ditutup.
  - Streaming stdout/stderr log sederhana per worker di GUI.
- [ ] **Mini Scheduler Runner**:
  - Scheduler emulator (misal trigger `php artisan schedule:run` tiap 60 detik) tanpa perlu menyentuh Windows Task Scheduler.

### Phase 4: Extended Cloud & Storage Mocks (Priority: Medium)
- [ ] **Local S3 Object Storage Mock (MinIO)**:
  - Integrasi binary MinIO sebagai service opsional untuk testing upload file S3 lokal.
- [ ] **Search Engine Mock (Meilisearch / Typesense)**:
  - Binary service opsional untuk search engine lokal yang ringan.
- [ ] **SQLite GUI Viewer**:
  - Shortcut/integrasi viewer SQLite ringan (DB Browser for SQLite) untuk framework modern (default di Laravel 11+).

### Phase 5: Observability & Developer UX (Priority: Medium)
- [ ] **Unified Log Viewer**:
  - Tab log terpusat dengan filter realtime: Nginx access/error, PHP error, MySQL, dan App Logs.
- [ ] **Dump Interceptor / Debugging Helper**:
  - Catcher untuk var-dump/Ray/debugging output lokal.

---

## 📌 Status Pelacakan Fitur
| Fitur | Status | Target Milestone | Catatan |
|---|---|---|---|
| Nginx + PHP Multi-worker | ✅ Selesai | v1.0.0 | Isolasi FastCGI pool |
| MySQL, PostgreSQL, Redis | ✅ Selesai | v1.0.0 | Auto-init directory & credential |
| Mailpit SMTP + Web UI | ✅ Selesai | v1.0.0 | Database SQLite persisten aktif |
| Micro DNS + Zero-UAC `*.test` | ✅ Selesai | v1.0.0 | Fallback hosts file |
| Smart Framework Detection | ✅ Selesai | v1.1.0 | Laravel / WordPress auto-docroot & framework detection |
| Node.js Reverse Proxy VHost | ✅ Selesai | v1.1.0 | Dukungan HMR WebSocket & custom proxy port |
| Cloudflare Tunnel 1-Click Share | ✅ Selesai | v1.1.0 | Alternatif Ngrok gratis via `cloudflared` (One-click public URL) |
| Windows Supervisor (Worker Manager) | ⏳ Direncanakan | v1.2.0 | Queue & Vite background runner |
| MinIO S3 Mock | ⏳ Direncanakan | v1.3.0 | Service catalog baru |
