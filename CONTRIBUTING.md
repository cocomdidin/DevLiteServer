# Contributing to Dev Lite Server

Terima kasih atas ketertarikan Anda untuk berkontribusi pada **Dev Lite Server**! 

Untuk menjaga kualitas kode, kestabilan Semantic Versioning, dan agar **dokumentasi tidak pernah tertinggal dari implementasi kode**, seluruh kontribusi wajib mengikuti alur kerja dan checklist sinkronisasi di bawah ini.

---

## 🔄 Alur Standar Pengembangan (Feature & Revision Pipeline)

Setiap pengerjaan fitur baru, revisi, atau perbaikan bug harus melalui siklus 5 tahap:

```text
┌─────────────────┐     ┌─────────────────────┐     ┌───────────────────────┐
│ 1. Implementasi │ ──> │ 2. Sinkron Dokumen  │ ──> │ 3. Sinkron Installer  │
│    Kode/Config  │     │    README & PRD     │     │    & Release Bundle   │
└─────────────────┘     └─────────────────────┘     └───────────────────────┘
                                                                │
                                                                ▼
┌─────────────────┐     ┌─────────────────────┐     ┌───────────────────────┐
│ 5. Conventional │ <── │ 4. Verifikasi Build │ <── │                       │
│    Commit       │     │    0 Warn / 0 Err   │     │                       │
└─────────────────┘     └─────────────────────┘     └───────────────────────┘
```

---

## 📋 Checklist Sinkronisasi Wajib (Definition of Done)

Sebelum mengajukan Pull Request atau membuat commit, verifikasi checklist berikut:

### 1. Sinkronisasi Kode & Konfigurasi
- **Konfigurasi Baru**: Jika menambahkan service atau port baru, tambahkan key default di `config.ini`, baca di `Core/AppConfig.cs`, dan buat template di `templates/`.
- **Portable Paths**: Jangan pernah menggunakan path absolut hardcoded. Gunakan `Core/AppPaths.ResolveRoot()`.
- **Process Management**: Setiap daemon eksternal wajib didaftarkan ke `Core/JobObject.cs` agar tidak menjadi orphan process.

### 2. Sinkronisasi Dokumentasi (`README.md` & `PRD.md`)
- **`README.md`**:
  - Tambahkan fitur baru ke section **Key Features**.
  - Jika menambah service atau port, perbarui tabel **Default Stack & Ports**.
  - Jika struktur folder berubah, perbarui diagram **Directory Structure**.
- **`PRD.md`**:
  - Perbarui scope, matriks arsitektur, atau spesifikasi teknis jika ada perubahan fondasi.

### 3. Sinkronisasi Distribusi & Installer
- Jika menambahkan folder atau tools baru yang wajib ada di installer:
  - Perbarui `installer/setup.iss` agar folder ter-copy saat instalasi.
  - Perbarui `.github/workflows/release.yml` di step `Assemble Distribution Bundle`.
  - Perbarui `scripts/setup-bundle.ps1` jika memerlukan download binary upstream.

### 4. Verifikasi Kompilasi
Pastikan proyek berhasil dikompilasi tanpa peringatan:
```powershell
dotnet build
```
Target: **0 errors, 0 warnings**.

---

## 🏷️ Aturan Commit (Conventional Commits)

Repository ini menggunakan GitHub Actions yang otomatis membaca commit untuk menaikkan versi aplikasi (Semantic Versioning):

| Tipe Commit | Format Contoh | Dampak SemVer |
|---|---|---|
| `feat` | `feat(mailpit): add auto-open inbox button` | Bump **MINOR** (`v1.1.0`) |
| `fix` | `fix(nginx): resolve port 80 conflict error` | Bump **PATCH** (`v1.0.1`) |
| `perf` | `perf(core): accelerate process discovery` | Bump **PATCH** (`v1.0.1`) |
| `refactor` | `refactor(ui): extract card status renderer` | Bump **PATCH** (`v1.0.1`) |
| `docs` | `docs: sync readme with postgres service ports` | Bump **PATCH** (`v1.0.1`) |
| `chore` | `chore(bundle): bump php runtime to 8.4.2` | Bump **PATCH** (`v1.0.1`) |
| `BREAKING CHANGE` | `feat!: restructure entire config.ini format` | Bump **MAJOR** (`v2.0.0`) |

### Pasang Git Hook Lokal
Jalankan perintah ini sekali untuk mencegah penolakan format commit:
```powershell
.\scripts\install-git-hooks.ps1
```

---

## 🚀 Mengajukan Pull Request

1. Fork repository dan buat branch baru (`feat/nama-fitur` atau `fix/nama-bug`).
2. Selesaikan pekerjaan dan lengkapi **Checklist Sinkronisasi**.
3. Push branch ke fork Anda.
4. Buka Pull Request. Template checklist di `.github/pull_request_template.md` akan otomatis muncul.
5. Centang seluruh item checklist pada form PR.
