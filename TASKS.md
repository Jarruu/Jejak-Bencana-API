# TASKS — Jejak Bencana API

Progress implementasi backend sesuai `PRD.md` v2.1 dan `SPECS.md`.
Tahapan mengikuti `SPECS.md` §12 (A → G). Satu tahap selesai dan diuji sebelum tahap berikutnya.

Legenda: `[x]` selesai · `[~]` selesai sebagian · `[ ]` belum

**Status ringkas:** tahap A–G untuk backend selesai, kecuali butir yang butuh PostgreSQL/hosting nyata (lihat "Sisa pekerjaan"). 64 test lulus.

---

## Keputusan yang sudah diambil

Pertanyaan terbuka di `SPECS.md` §15 yang memblokir implementasi sudah diputuskan:

| # | Hal | Keputusan | Alasan |
|---|---|---|---|
| 2 | Tipe ID | **`Guid` (UUID v7)** | Unik, tidak bisa dienumerasi di URL admin, tetap terurut untuk index PostgreSQL. |
| 3 | Payload `sources` saat buat/ubah catatan dana | `sources: [{ sourceId, supportingNote }]` — mengirim array **mengganti** seluruh relasi record tersebut | Satu bentuk payload, idempoten, tanpa endpoint terpisah untuk unlink. |
| 4 | Endpoint admin glosarium | `GET/POST/PATCH/DELETE /api/admin/glossary` | Form glosarium ada di PRD §5.5 tetapi tidak ada endpoint-nya di §10. |
| 5 | Endpoint ubah/hapus sumber | `GET/PATCH/DELETE /api/admin/sources` | Tanpa ini admin tidak bisa memperbaiki sumber yang salah. |
| 6 | Kasus Published diubah | **Kembali ke `Draft`** | Perubahan belum lolos validasi publikasi tidak boleh tampil ke publik. |
| 7 | Bentuk body error | `{ "errors": [{ "code", "field", "message" }] }` | Sesuai usulan `SPECS.md` §8. |
| 8 | Pembatasan login | **ASP.NET Core Identity lockout**: 5 percobaan gagal → terkunci 5 menit, per akun | Tanpa kode tambahan, tersimpan di database, dan tetap benar di belakang reverse proxy (rate limit per-IP akan salah karena semua request datang dari server Next.js). Respons `429` saat terkunci. |
| 9 | Versi .NET | **.NET 10 (LTS)** | SDK terpasang di mesin pengembangan; `Microsoft.AspNetCore.OpenApi` 10.x tersedia. |
| 10 | Default `currency` | **`IDR`** | Sesuai usulan; dapat diubah per record. |

Keputusan implementasi kecil lain (asumsi, silakan tinjau):

- `checkedAt` / `publishedAt` memakai `DateOnly` (keduanya tanggal, bukan waktu) — di PostgreSQL bertipe `date`, di JSON `"2026-04-06"`.
- `createdAt` / `updatedAt` memakai `DateTimeOffset` — di PostgreSQL bertipe `timestamptz`.
- Publikasi **ditolak** jika kasus belum punya satu pun catatan dana (`no_funding_records`). Kasus tanpa angka tidak memenuhi tujuan produk.
- `amount` saat publikasi harus **> 0** (bukan hanya "tidak nol"), karena nominal DSP negatif tidak bermakna.
- Field `string?` pada PATCH: `null`/tidak dikirim = tidak diubah; kirim `""` untuk mengosongkan.
- `UseHttpsRedirection` tidak dipakai; TLS diterminasi di reverse proxy/hosting.
- Cookie sesi `SameSite=Lax` + `HttpOnly`; request lintas-situs tidak membawa cookie sehingga CSRF tertahan untuk MVP.

---

## A. Fondasi

- [x] Hapus template WeatherForecast
- [x] Ganti namespace menjadi `JejakBencana.Api`
- [x] Tambah paket: Npgsql EF Core, Identity EF Core, EF Core Design, Scalar
- [x] Konfigurasi `AppDbContext` + connection string dari environment variable (fail-fast bila kosong)
- [x] OpenAPI (`/openapi/v1.json`, semua environment) + Scalar UI (Development)
- [x] Health check `GET /health`
- [x] `appsettings.json` tanpa secret + `.env.example`
- [x] `README.md` (setup, migrasi, seeder, endpoint, keputusan)
- [ ] Verifikasi `dotnet run` terhadap PostgreSQL nyata (butuh connection string dari tim)

## B. Model data

- [x] Enum `CaseStatus`, `InformationType` (disimpan sebagai string, kode snake_case)
- [x] Entitas `DisasterCase`, `FundingRecord`, `Source`, `RecordSource`, `GlossaryTerm`
- [x] Relasi: Case 1–N FundingRecord, FundingRecord N–N Source via RecordSource, GlossaryTerm N–0..1 Source
- [x] `AppDbContext` (Identity + konfigurasi enum/kolom/index unik slug)
- [x] Migrasi awal
- [x] Seeder data contoh berlabel "DATA CONTOH — JANGAN DITERBITKAN", status `Draft`, `amount` 0 (hanya Development)
- [x] Admin pertama dari environment variable bila tabel user kosong (semua environment)
- [ ] Jalankan migrasi terhadap PostgreSQL nyata

## C. API publik

- [x] `GET /api/cases` — hanya `Published`
- [x] `GET /api/cases/{slug}` — 404 bila draft/tidak ada
- [x] `GET /api/glossary`
- [x] DTO publik tanpa field internal; tidak ada field total agregat (agar `planned` tidak pernah dijumlahkan)

## D. Halaman publik

Di luar repo ini (pekerjaan `jejak-bencana-web`).

## E. Admin

- [x] Identity + cookie, `401` (bukan redirect) untuk API
- [x] `POST /api/admin/login` — pesan error umum, lockout, `429` saat terkunci
- [x] `POST /api/admin/logout`
- [x] `GET/POST /api/admin/cases`, `GET/PATCH /api/admin/cases/{id}`
- [x] `POST /api/admin/cases/{id}/publish` — validasi §6, `422` + daftar pelanggaran
- [x] `POST /api/admin/cases/{id}/unpublish`
- [x] `POST/PATCH/DELETE /api/admin/funding-records`
- [x] `GET/POST/PATCH/DELETE /api/admin/sources`
- [x] `GET/POST/PATCH/DELETE /api/admin/glossary`
- [x] Semua endpoint admin di balik `RequireAuthorization()`
- [x] Edit pada kasus Published menurunkan status ke `Draft`

## F. Dukungan verifikasi

- [x] Validator publikasi memisahkan pelanggaran per field dengan kode error
- [x] Validasi URL absolut `http/https`
- [x] Validasi slug unik + format
- [x] Tidak ada endpoint yang bisa menerbitkan tanpa lolos validasi

## G. Uji dan deploy

- [x] Proyek uji xUnit (`tests/JejakBencana.Api.Tests`)
- [x] Unit test validator publikasi (AC10)
- [x] Integration test endpoint publik & admin (AC01, AC03, AC04, AC05, AC06, AC07)
- [x] Test kontrak OpenAPI (semua endpoint + skema cookie terdokumentasi)
- [x] Test regresi kebijakan cookie (`Secure`/`HttpOnly` di production)
- [x] Semua test lulus (59)
- [x] `Dockerfile` untuk deployment backend
- [x] Migrasi dijalankan ke PostgreSQL nyata (`dotnet ef database update`)
- [x] Uji alur admin end-to-end ke PostgreSQL nyata (`scripts/e2e.sh`, 11 langkah)
- [ ] Deployment ke hosting (belum dipilih — di luar keputusan repo ini)

---

## Sisa pekerjaan (butuh tim)

| # | Pekerjaan | Kenapa belum selesai |
|---|---|---|
| 1 | Isi data kasus nyata | Tahap F: audit sumber dilakukan tim. Seeder hanya menghasilkan data contoh draft. |
| 2 | Pilih hosting backend + database | Keputusan tim (SPECS §15 no.1). |
| 3 | Akun admin production | Dibuat otomatis dari `ADMIN_EMAIL`/`ADMIN_PASSWORD` saat startup pertama; hapus variabelnya setelah login berhasil. |
| 4 | Bersihkan data uji | `scripts/e2e.sh` meninggalkan kasus/sumber uji di database. Hapus sebelum demo. |

---

## Catatan verifikasi

Perintah yang dijalankan pada mesin ini:

| Perintah | Hasil |
|---|---|
| `dotnet build JejakBencana.slnx` | Berhasil, 0 warning, 0 error |
| `dotnet ef migrations add InitialCreate` | Migrasi dibuat di `Data/Migrations` |
| `dotnet ef migrations script` | SQL PostgreSQL terbentuk |
| `dotnet ef migrations has-pending-model-changes` | Tidak ada perubahan model yang belum dimigrasikan |
| `dotnet test tests/JejakBencana.Api.Tests` | **64 test lulus**, 0 gagal |
| `dotnet run` tanpa `ConnectionStrings__Default` | Gagal cepat dengan pesan konfigurasi (sesuai harapan) |
| `dotnet ef database update` | `InitialCreate` diterapkan ke PostgreSQL nyata |
| `dotnet run` (Development, PostgreSQL nyata) | Seeder membuat akun admin + data contoh draft; `/api/cases` kosong (draft tidak bocor) |
| `bash scripts/e2e.sh` | **11/11 langkah lulus** terhadap PostgreSQL nyata |

Temuan dari uji PostgreSQL nyata yang tidak terlihat di InMemory:

- `Amount` benar-benar `bigint` dan `537220000000` tersimpan utuh (bukan `int` yang terpotong).
- `InformationType` tersimpan sebagai string `realization_reported`, bukan angka enum.
- Edit sumber menurunkan kasus `published` ke `draft`, dan detail publik langsung `404`.


Test yang menutup kriteria penerimaan SPECS §11:

| AC | Test |
|---|---|
| AC01 | `Anonymous_Client_Can_Read_Public_Endpoints`, `Complete_Case_Can_Be_Published_And_Read_Publicly` |
| AC03 | `New_Case_Starts_As_Draft_And_Is_Hidden_From_Public`, `Editing_A_Published_Case_Returns_It_To_Draft` |
| AC04 | `AdminFlowTests`, `FundingRecordTests` |
| AC05 | `Admin_Endpoints_Reject_Anonymous_With_401`, `Logout_Ends_The_Session` |
| AC06 | `Planned_Record_Is_Not_Mixed_Into_Realization` |
| AC07 | `Invalid_Source_Url_Is_Rejected_On_Create`, `Invalid_Source_Url_Is_Rejected` (validator), `Complete_Case_Can_Be_Published_And_Read_Publicly` |
| AC10 | `Publish_Without_Funding_Record_Returns_422`, `Publish_With_Zero_Amount_Returns_422_And_Keeps_Draft`, `Publish_With_Record_Without_Source_Returns_422`, `PublicationValidatorTests` |

AC02 dan AC08 adalah tanggung jawab verifikasi data dan frontend.

---

## Catatan desain untuk repo web

- `GET /api/cases` **tidak** memuat `periodLabel`. Ringkasan di beranda sebaiknya mengambil periode dari `GET /api/cases/{slug}`, karena periode berbeda antar jenis informasi dana dan tidak boleh digabung dalam satu label ringkasan.
- Kontrak publik tidak punya angka total. Frontend tidak boleh menjumlahkan sendiri catatan dana dengan jenis berbeda.
- Tautan sumber harus dirender dengan `rel="noopener noreferrer"` (PRD §12).

