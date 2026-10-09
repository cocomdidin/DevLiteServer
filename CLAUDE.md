# Dev Lite Server - Workspace Rules & Commit Convention

## Semantic Versioning & Commit Rules

Repository ini menggunakan **Automated Semantic Versioning** berbasis Conventional Commits pada GitHub Actions.
Setiap commit ke branch `main` langsung menentukan versi rilis aplikasi:

| Tipe Commit | Format Contoh | Dampak SemVer | Kapan Digunakan |
|---|---|---|---|
| **MAJOR** | `feat!: ubah struktur config` atau `BREAKING CHANGE: ...` | **v2.0.0** (Major bump) | Perubahan arsitektur besar yang mematahkan backward compatibility. DILARANG digunakan sembarangan. |
| **MINOR** | `feat: tambah service postgresql`<br>`feat(php): support multi-version switch` | **v1.1.0** (Minor bump) | Penambahan fitur baru yang backward-compatible. |
| **PATCH** | `fix: atasi crash port 80`<br>`fix(nginx): perbaiki prefix trailing slash`<br>`perf: percepat startup supervisor`<br>`refactor: ekstrak base process service`<br>`chore: update bin download url`<br>`docs: update petunjuk install` | **v1.0.1** (Patch bump) | Bugfix, perbaikan kecil, peningkatan performa, maintenance script, atau dokumentasi. |

---

## Aturan Format Commit (Wajib Dipatuhi)

Format baris pertama (Subject Line):
```text
<type>(<scope>): <pesan singkat huruf kecil, imperatif, tanpa titik>
```

### 1. Tipe yang Diizinkan:
- `feat`: Fitur baru untuk pengguna (Memicu Minor Release).
- `fix`: Perbaikan bug atau error (Memicu Patch Release).
- `perf`: Optimasi performa tanpa mengubah fitur (Memicu Patch Release).
- `refactor`: Restrukturisasi kode tanpa bugfix/fitur baru (Memicu Patch Release).
- `docs`: Dokumentasi (README, PRD, docs).
- `chore`: Update konfigurasi internal, file build, atau tool release.
- `ci`: Perubahan workflow GitHub Actions / build pipeline.
- `test`: Penambahan atau perbaikan unit test.

### 2. Scope yang Direkomendasikan:
- `(nginx)`
- `(php)`
- `(mysql)`
- `(mailpit)`
- `(postgres)`
- `(redis)`
- `(ui)`
- `(core)`
- `(installer)`
- `(bundle)`

### 3. Larangan Keras:
- ❌ **DILARANG** menggunakan pesan ambigu seperti `update`, `fix error`, `test`, `wip`.
- ❌ **DILARANG** menyertakan tanda seru `!` atau teks `BREAKING CHANGE` jika commit tersebut hanya perbaikan biasa atau penambahan fitur reguler.
- ❌ **DILARANG** melakukan git commit otomatis tanpa perintah eksplisit dari user.

---

## Standard Attribution (Claude Code)

Setiap git commit message yang dibuat oleh asisten wajib diakhiri dengan:
```text
Co-Authored-By: Claude Code <noreply@anthropic.com>
```

---

## Standar Pipeline Perubahan (Definition of Done / DoD)

Setiap ada **penambahan fitur baru**, **revisi**, **refactoring**, atau **perubahan konfigurasi**, wajib melalui dan memvalidasi alur sinkronisasi berikut sebelum pekerjaan dianggap selesai:

```
[1. Code/Config] ➔ [2. Sync Docs (README/PRD)] ➔ [3. Sync Installer/Bundle] ➔ [4. Build Verify] ➔ [5. Commit Approval]
```

### Checklist Wajib Sinkronisasi (Synchronous Checklist):
1. **Sinkronisasi Kode & Config**:
   - Jika menambah port/service baru: perbarui `config.ini`, `Core/AppConfig.cs`, dan `templates/`.
   - Pastikan path resolution selalu melalui `Core/AppPaths.cs`.
2. **Sinkronisasi Dokumentasi (`README.md` & `PRD.md`)**:
   - Fitur baru / opsi baru wajib dicatat pada daftar fitur di `README.md`.
   - Matriks port atau service baru wajib dicatat pada tabel Stack & Ports di `README.md`.
   - Perubahan arsitektur/scope wajib di-update pada `PRD.md`.
3. **Sinkronisasi Distribusi & Installer**:
   - Jika ada file/folder baru di luar `bin/` yang perlu di-bundle: perbarui `installer/setup.iss`, `.github/workflows/release.yml`, dan `scripts/setup-bundle.ps1`.
4. **Verifikasi Kompilasi**:
   - Jalankan `rtk dotnet build` dan pastikan **0 error, 0 warning**.
5. **Konfirmasi & Commit**:
   - Rangkum perubahan kepada user dan minta persetujuan commit sesuai Conventional Commits.
   - DILARANG commit otomatis tanpa persetujuan eksplisit.

