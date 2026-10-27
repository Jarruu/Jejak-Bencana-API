# Specs Backend: Jejak Bencana

Dokumen ini adalah versi khusus backend dari PRD Teknis Jejak Bencana v2.1 (22 Oktober 2026). Tujuannya agar AI agent yang mengerjakan API, database, dan autentikasi bisa bekerja dari satu sumber yang ringkas.

Jika ada hal yang tidak jelas, **tanyakan dulu. Jangan menebak** nominal, periode, tanggal, atau aturan bisnis yang tidak tertulis di sini.

---

## 1. Konteks singkat

- **Produk:** website informasi Dana Siap Pakai (DSP) untuk penanggulangan bencana, dibatasi pada **satu kasus bencana**.
- **Bukan:** sistem pencatatan transaksi pemerintah, platform donasi, alat penyaluran bantuan, alat audit AI, atau sistem pelaporan penyalahgunaan.
- **Peran backend:** menyediakan API untuk
  1. halaman publik, yang hanya boleh membaca data **published**;
  2. panel admin, yang membuat dan mengubah data secara manual, lalu menerbitkannya setelah validasi.

---

## 2. Stack backend

| Komponen | Pilihan | Catatan |
|---|---|---|
| Framework | ASP.NET Core Web API (Minimal API) | Keputusan tim. |
| Database | PostgreSQL | Data terstruktur. |
| ORM | Entity Framework Core + Npgsql | Migrasi dan LINQ. Menggantikan Prisma dari spesifikasi v1.0. |
| Autentikasi admin | ASP.NET Core Identity dengan cookie | Hash kata sandi sudah tersedia. |
| Hosting backend | Azure App Service, Railway, atau VPS dengan Docker | **Belum dipilih.** Backend tidak dapat berjalan di Vercel. |
| Hosting database | PostgreSQL terkelola | **Belum dipilih.** Tergantung anggaran. |

Stack ini adalah rekomendasi tim. Jika tim memutuskan lain, spesifikasi ini harus diselaraskan dulu sebelum implementasi.

---

## 3. Repository

Backend ada di repository **terpisah**: `jejak-bencana-api`. Bukan monorepo.

```
jejak-bencana-api/
├── Domain/          # entitas dan enum
├── Data/            # AppDbContext dan migrasi
├── Endpoints/       # PublicEndpoints.cs, AdminEndpoints.cs
├── Program.cs
├── appsettings.json # tanpa secret
└── README.md
```

Kontrak API (bentuk request dan response) didokumentasikan lewat **Swagger/OpenAPI** di repository ini. Repository web (`jejak-bencana-web`) mengacu ke dokumentasi tersebut. **Perubahan kontrak harus disepakati tim dulu** sebelum diterapkan.

---

## 4. Aturan wajib (selalu berlaku)

1. **Validasi dilakukan di server.** Validasi di frontend hanya membantu, tidak menggantikan.
2. **Endpoint publik hanya mengembalikan data `Published`.** Draft tidak boleh bocor lewat endpoint publik mana pun.
3. **Semua endpoint admin wajib memeriksa sesi dan otorisasi di server.**
4. **Nominal disimpan sebagai bilangan bulat rupiah utuh.** Format seperti "Rp537,22 miliar" hanya untuk tampilan, tidak untuk disimpan.
5. **Rencana/pengajuan (`planned`) tidak boleh dijumlahkan ke realisasi.**
6. **Periode dan tanggal hanya disimpan jika didukung sumber.** Jangan menebak tanggal awal atau akhir.
7. **Jangan menyimpulkan** bahwa dana sudah diterima individu hanya dari angka agregat realisasi.
8. **Jangan menyimpulkan** penyalahgunaan atau ketiadaan akuntabilitas dari dokumen yang tidak ditemukan.
9. **Secret tidak boleh masuk repository.** Kredensial database dan secret lewat environment variable. `appsettings.json` tanpa secret.
10. **Jangan simpan data pribadi penerima bantuan** yang tidak diperlukan.

---

## 5. Model data

### 5.1 Konvensi

- Nama C#: **PascalCase** (`DisasterCase`, `FundingRecord`).
- Nama JSON ke frontend: **camelCase** (`informationType`, `periodLabel`).
- Tipe ID belum ditentukan di PRD. Ikuti konvensi tim, dan tanyakan jika belum jelas.

### 5.2 Entitas

**DisasterCase** (satu kasus yang dibahas)

| Field | Tipe usulan | Wajib | Aturan |
|---|---|---|---|
| id | sesuai konvensi tim | ya | |
| title | string | ya (untuk publikasi) | |
| slug | string | ya | **unik** |
| summary | string | ya (untuk publikasi) | |
| region | string | tidak | wilayah, opsional |
| status | enum `CaseStatus` | ya | `Draft` atau `Published` |
| createdAt | DateTime | ya | |
| updatedAt | DateTime | ya | |

**FundingRecord** (catatan informasi dana)

| Field | Tipe usulan | Wajib | Aturan |
|---|---|---|---|
| id | sesuai konvensi tim | ya | |
| caseId | FK ke DisasterCase | ya | |
| informationType | enum `InformationType` | ya (untuk publikasi) | lihat 5.3 |
| amount | **`long` (bigint)** | ya (untuk publikasi) | bilangan bulat, **tidak boleh nol** saat publikasi |
| currency | string | ya | usulan default `IDR` (perlu dikonfirmasi) |
| purpose | string | ya (untuk publikasi) | tujuan penggunaan sesuai sumber |
| periodLabel | string | tidak | hanya jika didukung sumber |
| contextNote | string | tidak | catatan konteks dan keterbatasan |

> **Penting:** gunakan `long`, bukan `int`. Nilai `int` maksimal sekitar 2,1 miliar rupiah, sedangkan kandidat data awal (Rp537,22 miliar = `537220000000`, lihat 13.1) jauh melampauinya.

**Source** (sumber resmi atau dokumen pendukung)

| Field | Tipe usulan | Wajib | Aturan |
|---|---|---|---|
| id | sesuai konvensi tim | ya | |
| title | string | ya | |
| url | string | ya (untuk publikasi) | **URL valid** |
| publisher | string | ya | penerbit |
| publishedAt | DateTime? | tidak | hanya jika tersedia |
| checkedAt | DateTime | ya (untuk publikasi) | tanggal tim memeriksa URL |
| note | string | tidak | |

**RecordSource** (relasi banyak-ke-banyak `FundingRecord` ↔ `Source`)

| Field | Keterangan |
|---|---|
| fundingRecordId | FK |
| sourceId | FK |
| supportingNote | catatan pendukung untuk pasangan ini |

**GlossaryTerm** (istilah glosarium)

| Field | Tipe usulan | Keterangan |
|---|---|---|
| id | sesuai konvensi tim | |
| term | string | |
| definition | string | |
| sourceId | FK?, opsional | |

**AdminUser:** dikelola oleh ASP.NET Core Identity (`IdentityUser`). Jangan menyimpan kata sandi dalam teks biasa.

### 5.3 Enum

**`CaseStatus`:** `Draft`, `Published`

**`InformationType`** (disimpan sebagai string di database lewat konversi enum EF Core, misalnya `HasConversion<string>()`, supaya nilainya tetap terbaca dan tidak berubah jika urutan enum diubah):

| Kode | Makna di UI | Aturan |
|---|---|---|
| `allocation` | Alokasi/penetapan | Hanya jika sumber memang menyebut alokasi/penetapan. |
| `disbursement` | Penyaluran | Jangan otomatis disamakan dengan penggunaan akhir oleh penerima. |
| `realization_reported` | Realisasi yang dilaporkan sumber | Cantumkan penerbit, periode, dan definisi sesuai sumber. |
| `planned` | Rencana/pengajuan | **Jangan dijumlahkan ke realisasi.** |

Kode di atas adalah label internal awal. Istilah final mengikuti dokumen sumber.

### 5.4 Relasi

- `DisasterCase` 1 — N `FundingRecord`
- `FundingRecord` N — N `Source` lewat `RecordSource`
- `GlossaryTerm` N — 0..1 `Source`
- `AdminUser` lewat Identity

---

## 6. Validasi publikasi (wajib)

Endpoint `POST /api/admin/cases/:id/publish` **hanya** mengubah status menjadi `Published` jika **semua** syarat berikut terpenuhi. Jika ada yang gagal, kembalikan **422** beserta daftar pelanggaran.

1. Kasus: `title`, `slug`, dan `summary` terisi.
2. Setiap `FundingRecord` milik kasus tersebut memiliki:
   - `informationType` yang valid (salah satu dari 4 kode di 5.3);
   - `amount` berupa bilangan bulat yang **tidak nol**;
   - `purpose` terisi;
   - minimal **satu** `Source` yang terkait lewat `RecordSource`.
3. Setiap `Source` yang terkait memiliki `url` yang valid dan `checkedAt` terisi.

Validasi ini harus berjalan di server. Jangan hanya mengandalkan frontend.

---

## 7. Endpoint

### 7.1 Publik (tanpa login)

| Method | Endpoint | Fungsi | Perilaku |
|---|---|---|---|
| GET | `/api/cases` | Daftar kasus | Hanya `Published`. |
| GET | `/api/cases/:slug` | Detail kasus | Hanya `Published`: kasus, catatan dana, dan sumber terkait. **404** jika tidak ditemukan atau masih draft. |
| GET | `/api/glossary` | Daftar glosarium | Publik. |

### 7.2 Admin

| Method | Endpoint | Fungsi | Catatan |
|---|---|---|---|
| POST | `/api/admin/login` | Mulai sesi | Publik tetapi **dibatasi terhadap percobaan berulang**. Pesan error umum. |
| POST | `/api/admin/logout` | Akhiri sesi | Wajib sesi. |
| GET | `/api/admin/cases` | Daftar kasus termasuk draft | Wajib sesi. |
| POST | `/api/admin/cases` | Buat kasus | Status awal `Draft`. |
| PATCH | `/api/admin/cases/:id` | Ubah kasus | |
| POST | `/api/admin/funding-records` | Buat catatan dana | |
| PATCH | `/api/admin/funding-records/:id` | Ubah catatan dana | |
| POST | `/api/admin/sources` | Buat sumber | |
| POST | `/api/admin/cases/:id/publish` | Terbitkan kasus | Jalankan validasi bagian 6. |

**Semua endpoint admin wajib sesi dan otorisasi di server.** Akses tanpa sesi harus ditolak.

---

## 8. Format respons (usulan, perlu disepakati)

PRD belum mendefinisikan bentuk body error. Usulan berikut bisa dipakai sampai tim memutuskan lain.

| Kondisi | Status | Keterangan |
|---|---|---|
| Berhasil | 200 / 201 / 204 | Sesuai jenis operasi. |
| Input tidak valid (dasar) | 400 | |
| Belum login (admin) | 401 | |
| Login tanpa hak | 403 | |
| Data tidak ditemukan | 404 | Termasuk slug kasus yang tidak published. |
| Publikasi gagal validasi | 422 | Body berisi daftar pelanggaran. |
| Login terlalu sering | 429 | Usulan untuk pembatasan percobaan login. |

**Usulan bentuk body 422:**

```json
{
  "errors": [
    { "code": "missing_source", "field": "fundingRecords[0].sources", "message": "Catatan dana belum memiliki sumber." }
  ]
}
```

Pesan error **tidak boleh** membocorkan kredensial, stack trace, atau detail internal.

---

## 9. Autentikasi dan keamanan

- **Identity:** ASP.NET Core Identity dengan cookie. Keputusan ini sudah final.
- **Same-origin:** Next.js meneruskan `/api/*` ke backend lewat rewrite. Browser hanya melihat satu origin, jadi cookie sesi berfungsi tanpa konfigurasi CORS khusus.
  - Jika pola rewrite **tidak** dipakai, CORS dan cookie lintas domain harus diatur eksplisit.
- **Seeder admin awal:**
  - email dan kata sandi dibaca dari environment variable;
  - **hanya** dijalankan di lingkungan Development.
- **Login:** dibatasi terhadap percobaan berulang. Pesan error umum, tanpa membedakan email salah dan kata sandi salah.
- **Validasi URL sumber:** pastikan format URL valid sebelum disimpan. Tampilan tautan di frontend memakai `rel="noopener noreferrer"`.

---

## 10. Aturan data dan sumber yang ditegakkan backend

- Setiap angka penting di halaman publik harus bisa ditelusuri ke minimal satu sumber. Ini ditegakkan oleh validasi publikasi (bagian 6).
- Jika dua sumber berbeda untuk hal yang sama, beri catatan perbedaan atau tunda publikasi.
- Jika rincian wilayah, jumlah penerima, atau transaksi tidak tersedia, simpan kosong dan biarkan frontend menampilkan keterbatasannya. Jangan diisi tebakan.
- **Jangan** menerbitkan data contoh atau placeholder (lihat bagian 13.2).

---

## 11. Kriteria penerimaan backend

| ID | Kriteria | Cara uji |
|---|---|---|
| AC01 | Endpoint publik bisa diakses tanpa login. | Panggil `/api/cases` dan `/api/cases/:slug` tanpa sesi. |
| AC03 | Draft tidak muncul di endpoint publik. | Buat draft, lalu panggil endpoint publik. Harus 404 atau tidak ada di daftar. |
| AC04 | Admin bisa membuat dan mengubah kasus, catatan dana, dan sumber. | Uji endpoint admin dan cek database. |
| AC05 | Endpoint admin ditolak tanpa sesi. | Panggil endpoint admin tanpa cookie. Harus 401. |
| AC06 | `planned` tidak dijumlahkan ke realisasi. | Uji data dengan jenis berbeda. |
| AC07 | URL sumber valid dan `checkedAt` tersimpan. | Uji validasi input dan publikasi. |
| AC10 | Publikasi ditolak jika ada catatan dana tanpa sumber atau field wajib kosong. | Coba publish kasus yang belum lengkap. Harus 422 dengan daftar pelanggaran. |

AC02 (angka sesuai sumber) dan AC08 (responsivitas) adalah tanggung jawab pengecekan data dan frontend, bukan backend.

---

## 12. Tahapan backend

Kerjakan **berurutan**. Selesaikan dan uji satu tahap sebelum lanjut.

| Tahap | Pekerjaan backend | Selesai jika |
|---|---|---|
| A. Fondasi | Buat repository, konfigurasi PostgreSQL dan EF Core, siapkan Swagger. | Aplikasi berjalan lokal. |
| B. Model data | Buat entitas dan enum (bagian 5), konfigurasi `AppDbContext`, migrasi, seeder data contoh. | Migrasi berjalan. Data contoh masuk sebagai draft. |
| C. API publik | `GET /api/cases`, `GET /api/cases/:slug`, `GET /api/glossary`. Hanya `Published`. | Frontend bisa membaca data. AC01 dan AC03 lolos. |
| E. Admin | Login (Identity), CRUD kasus/catatan/sumber, endpoint publish dengan validasi. | AC04, AC05, AC10 lolos. |
| F. Dukungan verifikasi | Pastikan data yang dimasukkan admin lolos validasi. Data nyata dimasukkan oleh tim setelah audit sumber. | Kasus pertama siap diterbitkan. |
| G. Uji dan deploy | Uji penerimaan, keamanan dasar, deployment. | Backend siap didemokan. |

Tahap D (halaman publik) adalah pekerjaan frontend.

---

## 13. Data contoh dan kandidat data

### 13.1 Kandidat data awal (belum final)

Satu publikasi BNPB tentang pemulihan pascabencana Sumatra menyebut realisasi DSP sebesar **Rp537,22 miliar** untuk bantuan stimulan rumah rusak hingga 6 April 2026. Ini **hanya kandidat audit**, bukan keputusan final.

Jika kandidat ini dipakai:

- simpan `amount` sebagai `537220000000` (rupiah utuh, `long`);
- `informationType` = `realization_reported`;
- `periodLabel` sesuai sumber;
- **jangan diterbitkan** sebelum tahap F selesai.

### 13.2 Data contoh untuk pengembangan lokal

Gunakan placeholder berlabel **"DATA CONTOH — JANGAN DITERBITKAN"**. Jangan pakai angka yang terlihat resmi.

| Field | Nilai |
|---|---|
| title | Kasus Contoh — belum dipublikasikan |
| informationType | `realization_reported` |
| amount | `0` |
| purpose | Isi setelah sumber diverifikasi |
| periodLabel | Belum ditetapkan |
| source url | Tambahkan URL resmi yang telah diperiksa |
| status | `Draft` |

Seeder hanya boleh menghasilkan data ini dengan status `Draft`.

---

## 14. Di luar scope backend MVP

Jangan dibangun dulu:

- integrasi otomatis atau real-time dengan sistem pemerintah;
- pelacakan transaksi sampai penerima individual;
- AI untuk memverifikasi atau mengaudit dana;
- fitur donasi, pembayaran, atau penyaluran bantuan;
- sistem laporan dugaan penyalahgunaan;
- banyak kasus sekaligus, analitik lanjutan;
- role admin bertingkat dan workflow persetujuan berlapis (MVP cukup satu jenis akun admin).

---

## 15. Hal yang belum diputuskan (berdampak ke backend)

| # | Hal | Dampak | Tindak lanjut |
|---|---|---|---|
| 1 | Hosting backend dan database | Konfigurasi deployment dan koneksi | Tim memilih berdasarkan anggaran. |
| 2 | Tipe ID (int, Guid, atau lainnya) | Skema tabel dan kontrak API | Tim memutuskan. |
| 3 | Format `sources` saat membuat atau mengubah catatan dana | Payload request | PRD menyebut "pengaitan sumber" tetapi belum ada bentuk payload. Usulkan dan sepakati. |
| 4 | Endpoint admin untuk glosarium | Admin perlu mengelola glosarium | Form glosarium ada di bagian admin PRD, tetapi API minimum belum punya endpoint-nya. Perlu diputuskan. |
| 5 | Endpoint ubah/hapus sumber | Admin perlu memperbaiki sumber | API minimum hanya punya `POST /api/admin/sources`. |
| 6 | Perilaku saat kasus yang sudah published diubah | Apakah kembali ke draft, atau langsung tampil? | PRD menyebut "menerbitkan pembaruan" tetapi tidak merinci. |
| 7 | Bentuk body error dan kode error | Konsistensi frontend | Lihat usulan di bagian 8. |
| 8 | Detail pembatasan login (jumlah percobaan, durasi) | Keamanan | Tetapkan angkanya. |
| 9 | Versi .NET | Target framework dan paket | Belum disebut di PRD. |
| 10 | Default `currency` | Field di FundingRecord | Usulan `IDR`, perlu dikonfirmasi. |

---

## 16. Referensi

- SISTALA-DSP BNPB: https://dsp.bnpb.go.id/ (akses dan kelengkapan detail perlu diuji)
- Publikasi BNPB, Finalisasi Rencana Induk Pemulihan Pascabencana Sumatra: https://www.bnpb.go.id/berita/pemerintah-finalisasi-rencana-induk-pemulihan-pascabencana-sumatra
- Peraturan BNPB Nomor 4 Tahun 2020: https://jdih.bnpb.go.id/dokumen/Peraturan/perban-nomor-4-tahun-2020
- PP Nomor 22 Tahun 2008: https://peraturan.bpk.go.id/Download/36843/PP%2022%20Tahun%202008.pdf
- Ombudsman RI, sorotan ketidaksinkronan informasi respons bencana Sumatra: https://ombudsman.go.id/perwakilan/news/r/pwkmedia--ombudsman-soroti-adanya-ketidaksinkronan-informasi-dalam-merespons-bencana-sumatera (konteks umum, bukan bukti langsung masalah DSP)

Sumber lengkap ada di `PRD.md`.
