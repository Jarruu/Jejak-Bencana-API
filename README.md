# Jejak Bencana - API

Backend untuk **Jejak Bencana**, website informasi Dana Siap Pakai (DSP) untuk penanggulangan bencana.
API ini menyediakan data untuk halaman publik (hanya konten terbit) dan panel admin (CRUD manual + publikasi tervalidasi).

Acuan: `PRD.md` v2.1 dan `SPECS.md`. Aturan wajib ada di `SPECS.md`.

- Framework: ASP.NET Core Minimal API (.NET 10)
- Database: PostgreSQL + EF Core (Npgsql)
- Auth: ASP.NET Core Identity, cookie sesi
- Dokumentasi kontrak: OpenAPI + Scalar UI di `/scalar` (Development)

---

## Menjalankan lokal

### 1. Prasyarat

- .NET SDK 10
- PostgreSQL 14+ yang bisa diakses (lokal, Neon, Supabase, Railway, dll.)

### 2. Konfigurasi

Secret **tidak** disimpan di repository. ASP.NET Core **tidak** membaca file `.env` otomatis - nilainya harus ada sebagai environment variable proses.

Pilihan paling sederhana, buat `.env` di root proyek (sudah di `.gitignore`) lalu muat ke shell:

```powershell
Get-Content .env | ForEach-Object {
  if ($_ -match '^\s*([^#=]+)=(.*)$') { Set-Item "env:$($matches[1].Trim())" $matches[2].Trim() }
}
dotnet run
```

Atau set langsung:

```powershell
$env:ConnectionStrings__Default = "Host=localhost;Port=5432;Database=jejak_bencana;Username=postgres;Password=postgres"
$env:ADMIN_EMAIL = "admin@example.com"
$env:ADMIN_PASSWORD = "kata-sandi-panjang-minimal-12"
```

Daftar kunci ada di `.env.example`. `ADMIN_*` dipakai sekali saat startup untuk membuat akun admin pertama, bila tabel user masih kosong (semua environment). Setelah ada admin, nilainya diabaikan.

### 3. Migrasi database

```powershell
dotnet tool restore
dotnet ef database update
```

`dotnet ef` memakai `Data/AppDbContextFactory.cs`, yang membaca `ConnectionStrings__Default` dan **gagal keras** bila variabel itu kosong — supaya migrasi tidak diam-diam masuk ke database lokal.

### 4. Jalankan

```powershell
dotnet run
```

- Health check: `GET http://localhost:5260/health` (mengembalikan 503 bila database tidak terjangkau)
- Dokumen OpenAPI: `GET /openapi/v1.json` (semua environment, termasuk production)
- Scalar UI (coba endpoint dari browser): `http://localhost:5260/scalar/v1` (Development)

Saat pertama jalan di Development, seeder membuat akun admin dari `ADMIN_EMAIL`/`ADMIN_PASSWORD` dan satu **data contoh draft** berlabel `DATA CONTOH - JANGAN DITERBITKAN` (nominal 0). Data itu tidak akan tampil di endpoint publik. Di luar Development hanya akun admin pertama yang dibuat, tanpa data contoh.

### 5. Dokumentasi API

Ada dua bentuk, keduanya dibuat dari kode yang sama:

| Bentuk | URL | Untuk siapa |
|---|---|---|
| OpenAPI 3.1 JSON | `/openapi/v1.json` | Repo `jejak-bencana-web`, generator klien, alat lain. Tersedia di semua environment. |
| Scalar UI | `/scalar/v1` | Manusia: daftar endpoint, skema request/response, tombol coba. Development saja. |

Isi dokumen: 15 endpoint (3 publik + 12 admin), ringkasan dan deskripsi per operasi, skema DTO, serta skema keamanan `cookieAuth` untuk endpoint admin. Judul dokumen: **Jejak Bencana API**.

Untuk mencoba endpoint admin dari Scalar, login dulu lewat `POST /api/admin/login`, lalu browser membawa cookie sesi secara otomatis.

Karena frontend memakai rewrite `/api/*` (PRD §8.2), repo web dapat mengambil dokumen ini lewat backend secara langsung, atau menyimpan salinannya saat build.

### 6. Uji

```powershell
dotnet test tests/JejakBencana.Api.Tests
```

Test memakai EF Core InMemory dan host asli, jadi tidak butuh PostgreSQL.

---

## Endpoint

### Publik (tanpa login)

| Method | Endpoint | Keterangan |
|---|---|---|
| GET | `/api/cases` | Hanya kasus `published`. |
| GET | `/api/cases/{slug}` | Kasus, catatan dana, dan sumber. `404` untuk draft/tidak ada. |
| GET | `/api/glossary` | Daftar istilah. |
| GET | `/health` | Status aplikasi. |

Tidak ada angka agregat di kontrak publik. Setiap catatan dana tampil terpisah dengan `informationType`-nya sendiri, sehingga `planned` tidak pernah ikut terjumlah ke realisasi (AC06).

### Admin (wajib sesi)

| Method | Endpoint | Keterangan |
|---|---|---|
| POST | `/api/admin/login` | `{ email, password }`. `401` pesan umum, `429` saat terkunci. |
| POST | `/api/admin/logout` | Akhiri sesi. |
| GET | `/api/admin/cases` | Termasuk draft. |
| GET | `/api/admin/cases/{id}` | Detail + catatan dana + relasi sumber. |
| POST | `/api/admin/cases` | Status awal `draft`. |
| PATCH | `/api/admin/cases/{id}` | Ganti field kasus. Kasus `published` kembali ke `draft`. |
| POST | `/api/admin/cases/{id}/publish` | Validasi SPECS §6. `422` + daftar pelanggaran. |
| POST | `/api/admin/cases/{id}/unpublish` | Turunkan ke `draft`. |
| POST | `/api/admin/funding-records` | Buat catatan dana + pengaitan sumber. |
| PATCH | `/api/admin/funding-records/{id}` | Ganti isi catatan dana (termasuk daftar sumber). |
| DELETE | `/api/admin/funding-records/{id}` | Hapus catatan dana. |
| GET/POST | `/api/admin/sources` | Daftar / buat sumber. |
| PATCH/DELETE | `/api/admin/sources/{id}` | Ubah / hapus sumber. |
| GET/POST | `/api/admin/glossary` | Daftar / buat istilah. |
| PATCH/DELETE | `/api/admin/glossary/{id}` | Ubah / hapus istilah. |

Bentuk payload sumber pada catatan dana:

```json
{
  "caseId": "0199...",
  "informationType": "realization_reported",
  "amount": 537220000000,
  "purpose": "Bantuan stimulan rumah rusak",
  "periodLabel": "hingga 6 April 2026",
  "contextNote": "Realisasi yang dilaporkan sumber; bukan rincian per penerima.",
  "sources": [{ "sourceId": "0199...", "supportingNote": "Tabel realisasi" }]
}
```

`PATCH` mengganti seluruh field yang dapat diedit. Mengirim `sources` mengganti seluruh relasi catatan dana tersebut; mengirim `null` (atau tidak mengirim field itu) membiarkan sumber yang ada, sedangkan `[]` mengosongkannya. Kasus yang menjadi `draft` karena perubahan ini harus diterbitkan ulang.

### Bentuk error

```json
{ "errors": [{ "code": "missing_source", "field": "fundingRecords[0].sources", "message": "Catatan dana belum memiliki sumber." }] }
```

Kode status: `400` input dasar, `401` belum login, `403` tidak berhak, `404` tidak ada / belum terbit, `422` gagal validasi publikasi, `429` login terkunci.

---

## Keputusan implementasi

Pertanyaan terbuka di `SPECS.md` §15 sudah diputuskan. Ringkasannya (detail di `TASKS.md`):

| Hal | Keputusan |
|---|---|
| Tipe ID | `Guid` (UUID v7) |
| Payload `sources` | `[{ sourceId, supportingNote }]`, mengganti seluruh relasi saat PATCH |
| Endpoint glosarium & ubah/hapus sumber | Ditambahkan - tanpa itu form admin di PRD §5.5 tidak bisa dipakai |
| Kasus `published` diubah | Kembali ke `draft` |
| Pembatasan login | Identity lockout: 5 gagal → 5 menit, per akun, respons `429` |
| .NET | 10 (LTS) |
| Mata uang | `IDR` (satu-satunya nilai yang diterima saat ini) |

Catatan tambahan:

- `checkedAt`/`publishedAt` bertipe `DateOnly` (`date` di PostgreSQL); `createdAt`/`updatedAt` bertipe `DateTimeOffset` (`timestamptz`).
- Publikasi ditolak bila kasus belum punya catatan dana, dan nominal harus `> 0`.
- `UseHttpsRedirection` tidak dipakai; TLS diterminasi di reverse proxy. Cookie `SameSite=Lax` + `HttpOnly`.
- Cookie sesi hanya berfungsi bila frontend meneruskan `/api/*` ke backend (pola rewrite PRD §8.2). Jika tidak, CORS + cookie lintas domain harus diatur eksplisit.
- Semua endpoint admin memakai `RequireAuthorization()` di level grup, sehingga endpoint baru otomatis terlindungi.

---

## Deployment

`Dockerfile` tersedia (multi-stage, user non-root, port 8080).

```powershell
docker build -t jejak-bencana-api .
docker run -p 8080:8080 `
  -e ConnectionStrings__Default="..." `
  jejak-bencana-api
```

Jalankan migrasi saat deploy:

```powershell
dotnet ef migrations script --idempotent -o migrate.sql
```

atau `dotnet ef database update` dari pipeline dengan connection string production. Migrasi otomatis saat startup hanya dilakukan di Development; di luar itu app hanya mencatat error bila ada migrasi tertunda, supaya beberapa instance tidak balapan memigrasi database yang sama.

Akun admin production dibuat dari `ADMIN_EMAIL`/`ADMIN_PASSWORD` saat startup pertama, bila belum ada user sama sekali. Hapus kedua variabel itu setelah login pertama berhasil. Data contoh tidak pernah dibuat di luar Development.

---

## Verifikasi

Perintah yang dijalankan pada implementasi ini:

| Perintah | Hasil |
|---|---|
| `dotnet build JejakBencana.slnx` | Berhasil, 0 warning |
| `dotnet ef migrations script` | SQL PostgreSQL terbentuk |
| `dotnet test tests/JejakBencana.Api.Tests` | 64 test lulus |
| `dotnet ef database update` | Migrasi `InitialCreate` diterapkan ke PostgreSQL nyata |
| `bash scripts/e2e.sh` | 11 langkah alur admin lulus terhadap PostgreSQL nyata |

`scripts/e2e.sh` menguji alur penuh ke database sungguhan: login, buat sumber, buat kasus, draft tersembunyi dari publik, publish tanpa catatan dana ditolak `422`, catatan dana `537220000000` tersimpan utuh sebagai `bigint`, publish berhasil, detail publik memuat angka dan penerbit, lalu edit sumber menurunkan kasus kembali ke `draft`.

Jalankan dengan aplikasi sudah hidup dan `.env` berisi kredensial:

```powershell
bash scripts/e2e.sh
```

Default `BASE` adalah `http://localhost:5260`; timpa dengan `BASE=http://host:port bash scripts/e2e.sh` bila perlu. Skrip ini menulis data uji ke database yang sedang dipakai — jangan arahkan ke production.

---

## Struktur

```
Domain/       entitas, enum, konversi kode
Data/         AppDbContext, migrasi, design-time factory
Contracts/    DTO request/response (camelCase ke frontend)
Endpoints/    PublicEndpoints, AdminEndpoints, AdminMapping
Validation/   aturan URL/slug dan validator publikasi
scripts/      e2e.sh - uji alur admin ke database nyata
Program.cs    konfigurasi host, Identity, cookie, seeder
tests/        xUnit: unit validator + integration test endpoint
```
