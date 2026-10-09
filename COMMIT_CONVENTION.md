# Commit Message Convention

Proyek **Local Lite Server** menerapkan standar [Conventional Commits v1.0.0](https://www.conventionalcommits.org/) yang terintegrasi langsung dengan Semantic Versioning otomatis pada CI/CD GitHub Actions.

---

## 1. Struktur Pesan Commit

```text
<type>(<scope>): <subject>

[optional body: penjelasan latar belakang & detail teknis]

[optional footer(s): Co-authored-by, issue refs, breaking change]
```

### Contoh:

```text
feat(installer): add inno setup windows installer script

- Generate LocalLiteServer-Setup-vX.Y.Z.exe for clean double-click installation
- Support desktop shortcut and uninstaller

Co-Authored-By: Claude Code <noreply@anthropic.com>
```

---

## 2. Pemetaan Tipe Commit ke Semantic Version

| Tipe | Penjelasan | Dampak SemVer |
|---|---|---|
| `feat` | Penambahan fitur baru ke sistem | **MINOR** (`1.0.0` -> `1.1.0`) |
| `fix` | Perbaikan bug atau penanganan error | **PATCH** (`1.0.0` -> `1.0.1`) |
| `perf` | Peningkatan performa komputasi atau memori | **PATCH** (`1.0.0` -> `1.0.1`) |
| `refactor` | Perubahan kode yang bukan bugfix dan bukan fitur baru | **PATCH** (`1.0.0` -> `1.0.1`) |
| `docs` | Perubahan dokumentasi saja | **PATCH** (`1.0.0` -> `1.0.1`) |
| `chore` | Perubahan build tools, dependencies, gitignore | **PATCH** (`1.0.0` -> `1.0.1`) |
| `ci` | Modifikasi file GitHub Actions workflow | **PATCH** (`1.0.0` -> `1.0.1`) |
| `test` | Penambahan / perbaikan pengujian | **PATCH** (`1.0.0` -> `1.0.1`) |
| `BREAKING CHANGE` atau `!` | Perubahan arsitektur yang tidak backward-compatible | **MAJOR** (`1.0.0` -> `2.0.0`) |

---

## 3. Scope yang Tersedia

Gunakan scope dalam tanda kurung untuk memperjelas modul yang terdampak:
- `core`: Resolver path, JobObject, ConfigManager.
- `ui`: WinForms, Custom Controls, System Tray.
- `nginx`: Daemon Nginx, konfig templating.
- `php`: FastCGI worker supervisor, switcher versi PHP.
- `mysql`: Database server daemon & initial data.
- `mailpit`: SMTP server & web UI.
- `postgres`: PostgreSQL daemon service.
- `redis`: Redis server daemon.
- `bundle`: Script provisioning runtime binaries.
- `installer`: Inno Setup compiler script & wizard.
- `release`: GitHub Actions workflow release pipeline.

---

## 4. Validasi Git Hook Otomatis

Untuk mencegah salah format commit yang merusak versi rilis, aktifkan pre-commit hook lokal dengan menjalankan:
```powershell
.\scripts\install-git-hooks.ps1
```
Hook akan memeriksa baris pertama commit Anda dan menolak commit jika format tidak sesuai standar Conventional Commits.
