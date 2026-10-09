### 📝 Deskripsi Perubahan
<!-- Jelaskan secara ringkas perubahan yang dilakukan (fitur baru, refactor, bugfix, dll.) -->

---

### 🏷️ Tipe Perubahan (Pilih salah satu sesuai Conventional Commits)
- [ ] `feat`: Fitur baru (Minor version bump)
- [ ] `fix`: Perbaikan bug (Patch version bump)
- [ ] `perf`: Peningkatan performa (Patch version bump)
- [ ] `refactor`: Restrukturisasi kode tanpa mengubah fungsionalitas
- [ ] `docs`: Perubahan atau pembaruan dokumentasi
- [ ] `chore`: Perubahan tooling, build script, atau dependencies
- [ ] `BREAKING CHANGE`: Perubahan arsitektur besar (Major version bump)

---

### ✅ Synchronous Definition of Done (DoD) Checklist
Harap pastikan semua item di bawah ini telah diperiksa sebelum merge untuk mencegah dokumentasi tidak sinkron:

#### 1. Kode & Konfigurasi
- [ ] Kode C# berhasil dikompilasi dengan `dotnet build` (**0 error, 0 warning**).
- [ ] Jika ada konfigurasi/port baru, sudah disinkronkan ke `config.ini` dan `Core/AppConfig.cs`.
- [ ] Tidak ada hardcoded path; semua path melalui `Core/AppPaths.cs`.

#### 2. Sinkronisasi Dokumentasi (Wajib)
- [ ] **`README.md`**: Fitur baru, tabel service/port, atau instruksi instalasi telah diperbarui.
- [ ] **`PRD.md`**: Spesifikasi atau arsitektur telah diperbarui jika ada perubahan lingkup.
- [ ] Dokumentasi inline / XML docs pada method publik telah ditambahkan/diperbarui.

#### 3. Distribusi & Installer
- [ ] **`installer/setup.iss`**: File atau folder baru telah didaftarkan jika harus masuk ke installer.
- [ ] **`.github/workflows/release.yml`**: Tahap bundling telah diperbarui jika struktur asset berubah.
- [ ] **`scripts/setup-bundle.ps1`**: Binary download script telah diperbarui jika ada komponen baru.

#### 4. Integritas Git & Versioning
- [ ] Pesan commit mengikuti format Conventional Commits (`type(scope): message`).
- [ ] Git commit linter hook lokal telah diuji (`scripts/install-git-hooks.ps1`).
