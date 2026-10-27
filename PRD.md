# PRD Teknis: Jejak Bencana

Dokumen kerja tim • Versi 2.1 • 22 Oktober 2026

| Item | Keterangan |
|---|---|
| Nama produk | Jejak Bencana |
| Jenis dokumen | PRD dan spesifikasi MVP untuk koordinasi tim |
| Versi | 2.1 |
| Pengguna utama | Masyarakat/pengunjung publik |
| Pengguna pengelola | Admin konten |
| Ruang lingkup | Satu kasus bencana; data dikelola manual |
| Status kasus | Belum final; menunggu audit kecukupan sumber |
| Prinsip | Sederhana, mudah dipahami, dan tidak membuat klaim di luar sumber |

## Catatan untuk AI agent

- Dokumen ini adalah acuan utama implementasi. Ikuti bagian 7.1 (validasi publikasi), bagian 11 (aturan data), dan bagian 12 (keamanan) sebagai aturan wajib.
- Stack pada bagian 8 adalah rekomendasi tim, bukan ketentuan mutlak. Jika ada perubahan, selaraskan dokumen ini sebelum implementasi.
- Perubahan kontrak API harus disepakati tim sebelum diterapkan di kedua repository.
- Jangan menebak nominal, periode, atau tanggal yang tidak didukung sumber. Jangan menampilkan data contoh seolah-olah resmi.

## Daftar isi

1. Ringkasan Produk
2. Masalah, Tujuan, dan Batasan
3. Pengguna dan Kebutuhan
4. Ruang Lingkup MVP
5. Struktur Halaman dan Spesifikasi UI
6. Alur Pengguna
7. Spesifikasi Admin
8. Rancangan Teknis
9. Rancangan Data
10. API Minimum
11. Aturan Data dan Sumber
12. Keamanan dan Validasi
13. Kriteria Penerimaan
14. Tahapan Implementasi
15. Risiko dan Hal yang Belum Diputuskan
16. Referensi Awal

---

## 1. Ringkasan Produk

**Tujuan utama:** membantu masyarakat memahami informasi Dana Siap Pakai (DSP) dari sumber resmi melalui penjelasan sederhana, konteks yang jelas, visualisasi seperlunya, dan tautan ke sumber aslinya.

Jejak Bencana adalah website informasi yang membantu masyarakat memahami informasi Dana Siap Pakai (DSP) untuk penanggulangan bencana. Produk ini **bukan** sistem pencatatan transaksi pemerintah, **bukan** platform donasi, dan **bukan** alat untuk menyimpulkan penyalahgunaan dana. Nilai utamanya adalah menyajikan informasi resmi dengan bahasa yang lebih mudah dipahami serta menunjukkan sumber yang dapat diperiksa.

MVP dibatasi pada satu kasus bencana yang dipilih setelah audit sumber, pengelolaan data manual, halaman publik tanpa login, dan admin sederhana untuk mengelola draft serta publikasi.

## 2. Masalah, Tujuan, dan Batasan

### 2.1 Tujuan

- Membantu pengunjung memahami arti angka DSP dan konteksnya.
- Menyediakan ringkasan yang menghubungkan nominal, tujuan, wilayah/periode, dan sumber resmi.
- Mempermudah pengunjung membuka sumber asli.
- Menyediakan cara sederhana bagi admin untuk memperbarui informasi secara manual.

### 2.2 Hipotesis masalah produk

Informasi DSP dapat tersebar dalam dashboard dan publikasi resmi. Jejak Bencana akan menguji apakah penyajian satu kasus dengan ringkasan, definisi istilah, konteks, dan tautan sumber membuat informasi lebih mudah dipahami. Ini adalah hipotesis produk, bukan klaim bahwa data pemerintah tidak transparan.

### 2.3 Batasan wajib

- Satu kasus bencana untuk MVP.
- Data dimasukkan dan diperiksa manual oleh admin.
- Setiap angka penting memiliki sumber dan konteks.
- Data yang belum tersedia atau belum cocok antar-sumber diberi catatan, bukan ditebak.
- Hanya informasi yang diterbitkan dapat dilihat di halaman publik.

### 2.4 Di luar ruang lingkup

- Integrasi otomatis atau real-time dengan sistem pemerintah.
- Pelacakan semua transaksi sampai penerima individual.
- AI untuk memverifikasi atau mengaudit dana.
- Fitur donasi, pembayaran, atau penyaluran bantuan.
- Sistem laporan dugaan penyalahgunaan dana.
- Banyak kasus sekaligus, peta interaktif kompleks, atau analitik lanjutan.
- Role admin bertingkat dan workflow persetujuan berlapis.

## 3. Pengguna dan Kebutuhan

| Pengguna | Kebutuhan | Yang disediakan |
|---|---|---|
| Masyarakat/publik | Memahami informasi tanpa istilah teknis yang membingungkan. | Ringkasan, definisi, konteks angka, visualisasi sederhana. |
| Masyarakat/publik | Memeriksa asal informasi. | Penerbit, tanggal publikasi bila tersedia, tautan sumber. |
| Admin | Memperbarui konten tanpa mengubah kode. | Form pengelolaan kasus, catatan dana, dan sumber. |
| Admin | Mencegah data belum siap tampil. | Status draft/published dan validasi field wajib. |

## 4. Ruang Lingkup MVP

| Fitur | Prioritas | Deskripsi |
|---|---|---|
| Beranda | P0, wajib | Penjelasan produk dan ringkasan satu kasus terpilih. |
| Detail kasus | P0, wajib | Nominal, tujuan, periode/wilayah jika tersedia, sumber, keterbatasan. |
| Glosarium | P0, wajib | Penjelasan istilah inti dengan bahasa sederhana. |
| Tautan sumber | P0, wajib | Penerbit, judul, tanggal publikasi jika tersedia, tanggal diperiksa, dan URL sumber. |
| Login admin | P0, wajib | Autentikasi pengelola. |
| Kelola kasus dan catatan dana | P0, wajib | Tambah dan ubah data manual. |
| Kelola sumber | P0, wajib | Simpan URL dan metadata sumber resmi. |
| Draft dan publikasi | P0, wajib | Konten draft tidak tampil ke publik. |
| Grafik sederhana | P1, bila data mendukung | Grafik batang sederhana dengan label dan periode jelas. |
| Pencarian/filter kompleks | Tidak untuk MVP | Belum diperlukan untuk satu kasus. |
| Integrasi data otomatis | Tidak untuk MVP | Menghindari ketergantungan teknis yang belum diperlukan. |

## 5. Struktur Halaman dan Spesifikasi UI

### 5.1 FE-01: Beranda (`/`)

**Tujuan:** menjelaskan produk dan mengarahkan pengunjung ke satu kasus yang sedang dibahas.

- Header/nav: logo Jejak Bencana, Beranda, Detail Kasus, Glosarium.
- Hero: satu kalimat yang menjelaskan bahwa website membantu memahami DSP dari sumber resmi.
- Ringkasan kasus: judul, ringkasan pendek, periode data bila tersedia, tombol "Lihat detail".
- Catatan konteks: data berasal dari sumber resmi dan dapat memiliki keterbatasan.
- Footer: tautan sumber utama dan informasi singkat tentang metodologi.

### 5.2 FE-02: Detail Kasus (`/kasus/[slug]`)

- Judul kasus dan ringkasan konteks bencana.
- Ringkasan nominal DSP dengan jenis informasi yang jelas, misalnya "realisasi yang dilaporkan oleh sumber".
- Tujuan penggunaan dana sesuai dokumen sumber.
- Wilayah dan periode hanya ditampilkan jika sumber mendukungnya.
- Grafik sederhana hanya jika data memiliki definisi, periode, dan satuan yang sebanding.
- Bagian "Sumber informasi": penerbit, judul, tanggal publikasi, tanggal diperiksa, dan tautan.
- Bagian "Batasan informasi": data yang belum tersedia atau belum dapat dicocokkan.

### 5.3 FE-03: Glosarium (`/glosarium`)

- Daftar istilah inti seperti DSP, realisasi, penyaluran, dan bantuan stimulan.
- Definisi singkat dengan bahasa awam dan diperiksa terhadap sumber resmi.
- Jangan menyamakan istilah yang berbeda. Cantumkan rujukan jika definisi bergantung pada dokumen.

### 5.4 FE-04: Login Admin (`/admin/login`)

- Form email dan kata sandi.
- Pesan error umum tanpa membocorkan detail sistem.
- Setelah login berhasil, admin diarahkan ke dashboard.

### 5.5 FE-05: Dashboard Admin (`/admin/dashboard`)

- Daftar kasus beserta status draft/published.
- Form tambah/ubah kasus.
- Form tambah/ubah catatan dana dan pengaitan sumber.
- Form tambah/ubah sumber.
- Tombol simpan draft dan terbitkan.
- Konfirmasi sebelum publikasi. Dialog konfirmasi menampilkan daftar catatan yang belum lengkap jika validasi gagal.

## 6. Alur Pengguna

### 6.1 Pengunjung publik

1. Membuka beranda tanpa akun.
2. Membaca ringkasan satu kasus.
3. Membuka detail kasus.
4. Memahami nominal dan konteksnya.
5. Membuka tautan sumber resmi.
6. Membuka glosarium jika menemukan istilah yang belum dipahami.

### 6.2 Admin

1. Login ke halaman admin.
2. Memilih atau membuat kasus.
3. Memasukkan catatan dana: jenis informasi, nominal, periode, tujuan, dan konteks.
4. Menambahkan setidaknya satu sumber pendukung.
5. Menyimpan sebagai draft.
6. Memeriksa konsistensi angka, konteks, dan tautan.
7. Menerbitkan konten setelah siap.
8. Jika ada kesalahan, memperbaiki konten dan menerbitkan pembaruan.

## 7. Spesifikasi Admin

| Form/aksi | Field/aturan minimum |
|---|---|
| Kasus | Judul, slug unik, ringkasan, wilayah (opsional), status. |
| Catatan dana | Kasus, jenis informasi, nominal rupiah, tujuan, periode/tanggal, catatan konteks. |
| Sumber | Judul, URL, penerbit, tanggal publikasi (opsional), tanggal diperiksa. |
| Glosarium | Istilah, definisi, sumber pendukung bila tersedia. |
| Publikasi | Draft atau published. Hanya admin terautentikasi yang dapat mengubah status. |

MVP tidak membutuhkan role admin bertingkat. Satu jenis akun admin cukup selama endpoint dan halaman pengelolaan dilindungi.

### 7.1 Validasi saat publikasi

Endpoint publikasi (`POST /api/admin/cases/:id/publish`) hanya mengubah status jika semua syarat berikut terpenuhi. Jika tidak, respons berstatus **422** dan daftar pelanggaran dikembalikan.

- Judul, slug, dan ringkasan kasus terisi.
- Setiap catatan dana memiliki jenis informasi, nominal bilangan bulat yang tidak nol, dan tujuan.
- Setiap catatan dana memiliki minimal satu sumber.
- Setiap sumber memiliki URL yang valid dan tanggal diperiksa.

Pemeriksaan ini dilakukan di server. Tampilan di frontend hanya membantu, tidak menggantikan.

## 8. Rancangan Teknis

### 8.1 Stack

| Lapisan | Pilihan | Alasan |
|---|---|---|
| Frontend | Next.js + TypeScript | Satu proyek untuk halaman publik dan admin. |
| UI | Tailwind CSS | Memudahkan pembuatan layout responsif. |
| Backend/API | ASP.NET Core Web API (Minimal API) | Keputusan tim. Menggantikan Next.js Route Handlers dari spesifikasi v1.0. |
| Database | PostgreSQL | Menyimpan data terstruktur. |
| ORM | Entity Framework Core (Npgsql) | Menggantikan Prisma. Migrasi dan query LINQ. |
| Autentikasi admin | ASP.NET Core Identity dengan cookie | Hash kata sandi dan manajemen peran sudah tersedia. |
| Hosting frontend | Vercel (opsi) | — |
| Hosting backend | Azure App Service, Railway, atau VPS dengan Docker | Backend tidak dapat dijalankan di Vercel. |
| Hosting database | PostgreSQL terkelola yang kompatibel | Dipilih berdasarkan anggaran dan kebutuhan tim. |

> Catatan: stack ini rekomendasi tim, bukan ketentuan mutlak. Jika tim memutuskan lain, dokumen ini perlu diselaraskan sebelum implementasi.

### 8.2 Hubungan frontend dan backend

Next.js meneruskan `/api/*` ke backend ASP.NET Core lewat rewrites di `next.config`, dengan URL backend diatur melalui environment variable di repository web. Dengan begitu browser hanya berhadapan dengan satu origin, sehingga cookie sesi admin berfungsi tanpa konfigurasi CORS khusus. Jika pilihan ini tidak dipakai, CORS dan cookie lintas domain harus diatur secara eksplisit.

### 8.3 Struktur repository

Backend dan frontend berada di **dua repository terpisah**, bukan monorepo.

**Repository `jejak-bencana-api` (ASP.NET Core)**

```
jejak-bencana-api/
├── Domain/          # entitas dan enum
├── Data/            # AppDbContext dan migrasi
├── Endpoints/       # PublicEndpoints.cs, AdminEndpoints.cs
├── Program.cs
├── appsettings.json # tanpa secret
└── README.md
```

**Repository `jejak-bencana-web` (Next.js + TypeScript)**

```
jejak-bencana-web/
├── src/
│   ├── app/
│   │   ├── page.tsx
│   │   ├── kasus/[slug]/page.tsx
│   │   ├── glosarium/page.tsx
│   │   └── admin/
│   │       ├── login/page.tsx
│   │       └── dashboard/page.tsx
│   └── components/
│       ├── Navbar.tsx
│       ├── FundingSummary.tsx
│       └── SourceLink.tsx
├── next.config.ts   # rewrite /api/* ke URL backend
└── package.json
```

Kontrak antarmuka (bentuk request dan response) didokumentasikan di repository API, misalnya lewat Swagger/OpenAPI, dan menjadi acuan bagi repository web. Perubahan kontrak harus disepakati tim sebelum diterapkan di kedua repository.

## 9. Rancangan Data

| Entitas | Field utama | Keterangan |
|---|---|---|
| DisasterCase | id, title, slug, summary, region, status, createdAt, updatedAt | Satu kasus yang dibahas. |
| FundingRecord | id, caseId, informationType, amount, currency, purpose, periodLabel, contextNote | Catatan informasi dana. `amount` disimpan sebagai rupiah utuh. |
| Source | id, title, url, publisher, publishedAt, checkedAt, note | Sumber resmi atau dokumen pendukung. |
| RecordSource | fundingRecordId, sourceId, supportingNote | Relasi banyak-ke-banyak antara catatan dana dan sumber. |
| GlossaryTerm | id, term, definition, sourceId (opsional) | Istilah dan definisi. |
| AdminUser | Dikelola ASP.NET Core Identity (`IdentityUser`) | Akun admin. Kata sandi tidak disimpan dalam bentuk teks biasa. |

**Konvensi nama:** C# memakai PascalCase (`DisasterCase`), JSON ke frontend memakai camelCase (`informationType`).

### 9.1 Jenis informasi dana

| Kode internal | Makna di UI | Aturan |
|---|---|---|
| `allocation` | Alokasi/penetapan | Gunakan hanya jika sumber memang menyebut alokasi/penetapan. |
| `disbursement` | Penyaluran | Jangan otomatis menyamakan dengan penggunaan akhir oleh penerima. |
| `realization_reported` | Realisasi yang dilaporkan sumber | Cantumkan penerbit, periode, dan definisi sesuai sumber. |
| `planned` | Rencana/pengajuan | Jangan dijumlahkan ke realisasi. |

Kode adalah label internal awal. Istilah final harus mengikuti istilah dan definisi dalam dokumen sumber.

Di database, kode disimpan sebagai string (misalnya `realization_reported`) melalui konversi enum di EF Core. Dengan begitu nilainya tetap terbaca dan tidak berubah jika urutan enum diubah.

## 10. API Minimum

| Method | Endpoint | Akses | Fungsi |
|---|---|---|---|
| GET | `/api/cases` | Publik | Mengambil kasus published. |
| GET | `/api/cases/:slug` | Publik | Detail kasus published, catatan dana, dan sumber terkait. Mengembalikan 404 jika tidak ditemukan. |
| GET | `/api/glossary` | Publik | Mengambil glosarium. |
| POST | `/api/admin/login` | Publik, dengan proteksi | Memulai sesi admin. Dibatasi terhadap percobaan berulang. |
| POST | `/api/admin/logout` | Admin | Mengakhiri sesi admin. |
| GET | `/api/admin/cases` | Admin | Melihat kasus termasuk draft. |
| POST | `/api/admin/cases` | Admin | Membuat kasus. |
| PATCH | `/api/admin/cases/:id` | Admin | Mengubah kasus. |
| POST | `/api/admin/funding-records` | Admin | Membuat catatan dana. |
| PATCH | `/api/admin/funding-records/:id` | Admin | Mengubah catatan dana. |
| POST | `/api/admin/sources` | Admin | Membuat sumber. |
| POST | `/api/admin/cases/:id/publish` | Admin | Menerbitkan kasus setelah validasi (bagian 7.1). |

Semua endpoint admin wajib memeriksa sesi dan otorisasi di server. Endpoint publik hanya mengembalikan konten published.

## 11. Aturan Data dan Sumber

- Setiap angka penting pada halaman publik harus dapat ditelusuri ke satu atau lebih sumber.
- Simpan nominal sebagai bilangan rupiah utuh. Format seperti "Rp537,22 miliar" hanya untuk tampilan.
- Simpan periode/tanggal yang didukung sumber. Jangan menebak tanggal awal atau akhir.
- Bedakan realisasi, penyaluran, alokasi, dan rencana sesuai definisi sumber.
- Jangan menyimpulkan bahwa dana telah diterima individu hanya dari angka agregat realisasi.
- Jangan menyimpulkan penyalahgunaan atau ketiadaan akuntabilitas hanya karena dokumen tidak ditemukan.
- Jika dua sumber berbeda, beri catatan perbedaan atau tunda publikasi sampai perbedaan dipahami.
- Catat tanggal tim memeriksa URL sumber.
- Jika rincian wilayah, jumlah penerima, atau transaksi tidak tersedia, nyatakan keterbatasannya dengan jelas.
- Jangan menampilkan angka contoh atau placeholder seolah-olah resmi (lihat Lampiran A).

## 12. Keamanan dan Validasi

- Kata sandi admin disimpan dalam bentuk hash melalui ASP.NET Core Identity.
- Kredensial database dan secret disimpan di environment variables, bukan di repository.
- Validasi field dilakukan di server, bukan hanya di frontend.
- Operasi create, update, dan publish dibatasi pada admin terautentikasi.
- Endpoint publik hanya mengembalikan data published.
- Validasi URL sumber dan tampilkan tautan eksternal dengan aman, misalnya `rel="noopener noreferrer"`.
- Pesan error tidak boleh membocorkan kredensial atau detail internal.
- Hindari menyimpan data pribadi penerima bantuan yang tidak diperlukan.
- Akun admin awal dibuat lewat seeder yang membaca email dan kata sandi dari environment variable, dan hanya dijalankan di lingkungan pengembangan.

## 13. Kriteria Penerimaan

| ID | Kriteria | Cara uji |
|---|---|---|
| AC01 | Pengunjung membuka halaman publik tanpa login. | Buka beranda dan detail tanpa sesi. |
| AC02 | Detail menampilkan nominal, jenis informasi, konteks, dan sumber yang tersedia. | Bandingkan angka dengan sumber. |
| AC03 | Draft tidak tampil pada halaman atau endpoint publik. | Buat draft lalu akses endpoint publik. |
| AC04 | Admin membuat dan mengubah kasus, catatan dana, dan sumber. | Uji form dan database. |
| AC05 | Hanya admin yang dapat melakukan perubahan. | Panggil endpoint admin tanpa sesi; harus ditolak. |
| AC06 | Rencana/pengajuan tidak dijumlahkan ke realisasi. | Uji record dengan jenis berbeda. |
| AC07 | Tautan sumber dapat dibuka dan identitas sumber terlihat. | Klik setiap tautan. |
| AC08 | Tampilan berfungsi di ponsel. | Uji layar ponsel dan desktop. |
| AC09 | Keterbatasan data ditampilkan dengan jelas. | Tinjau halaman detail. |
| AC10 | Publikasi ditolak jika ada catatan dana tanpa sumber atau field wajib kosong. | Coba terbitkan kasus yang belum lengkap; respons harus 422 dengan daftar pelanggaran. |

## 14. Tahapan Implementasi

| Tahap | Pekerjaan | Hasil |
|---|---|---|
| A. Fondasi | Inisialisasi repository `jejak-bencana-api` dan `jejak-bencana-web`, konfigurasi PostgreSQL dan EF Core, rewrite `/api`. | Aplikasi berjalan lokal. |
| B. Model data | Buat entitas, konfigurasi `AppDbContext`, migrasi, dan seeder data contoh. | Database menyimpan data contoh. |
| C. API publik | Endpoint daftar dan detail kasus, glosarium. Hanya published. | Data bisa dibaca frontend. |
| D. Halaman publik | Beranda, detail kasus, glosarium, komponen sumber. | Pengunjung memahami satu kasus. |
| E. Admin | Login (Identity), form CRUD, draft, publikasi dengan validasi. | Admin mengelola konten. |
| F. Verifikasi konten | Audit sumber dan masukkan data yang sudah diperiksa. | Kasus pertama siap diterbitkan. |
| G. Uji dan deploy | Uji penerimaan, responsivitas, keamanan dasar, deployment. | MVP siap didemokan. |

Selesaikan dan uji satu tahap sebelum memulai tahap berikutnya.

## 15. Risiko dan Hal yang Belum Diputuskan

| Hal | Status | Tindak lanjut |
|---|---|---|
| Kasus bencana final | Belum diputuskan | Pilih setelah sumber dinilai cukup. |
| Akses detail SISTALA-DSP | Perlu verifikasi | Catat apa yang terbuka tanpa login dan apa yang membutuhkan akun. |
| Kecocokan angka lintas sumber | Belum lengkap | Bandingkan nominal, periode, wilayah, dan definisi. |
| Hosting database | Belum dipilih | Pilih berdasarkan anggaran dan kebutuhan. |
| Hosting backend ASP.NET Core | Belum dipilih | Pilih layanan yang menjalankan .NET dan sesuai anggaran. |
| Metode autentikasi | Diputuskan: ASP.NET Core Identity dengan cookie | Tidak perlu tindak lanjut. |
| Desain visual final | Belum dikunci | Utamakan UI bersih, mudah dibaca, tanpa grafik berlebihan. |

### 15.1 Kandidat data awal, belum final

Satu publikasi BNPB tentang pemulihan pascabencana Sumatra menyebut realisasi DSP sebesar Rp537,22 miliar untuk bantuan stimulan rumah rusak hingga 6 April 2026, dengan rincian rumah rusak sedang dan ringan. Ini hanya kandidat audit, bukan keputusan final bahwa kasus sudah memenuhi kebutuhan MVP. Angka agregat tidak boleh dianggap sebagai rincian transaksi individual.

Jika kandidat ini dipakai, nilainya disimpan sebagai `537220000000` (rupiah utuh), dengan `informationType` `realization_reported` dan `periodLabel` sesuai sumber. Jangan diterbitkan sebelum tahap F selesai.

## 16. Referensi Awal

- **SISTALA-DSP BNPB:** https://dsp.bnpb.go.id/ — Portal resmi terkait informasi DSP. Akses dan kelengkapan detail perlu diuji.
- **Publikasi BNPB, Pemerintah Finalisasi Rencana Induk Pemulihan Pascabencana Sumatra:** https://www.bnpb.go.id/berita/pemerintah-finalisasi-rencana-induk-pemulihan-pascabencana-sumatra — Kandidat sumber data agregat bantuan stimulan rumah rusak.
- **Peraturan BNPB Nomor 4 Tahun 2020:** https://jdih.bnpb.go.id/dokumen/Peraturan/perban-nomor-4-tahun-2020 — Rujukan regulasi penggunaan DSP. Cek ketentuan yang relevan.
- **PP Nomor 22 Tahun 2008:** https://peraturan.bpk.go.id/Download/36843/PP%2022%20Tahun%202008.pdf — Rujukan umum pendanaan dan pengelolaan bantuan bencana.
- **Ombudsman RI, sorotan ketidaksinkronan informasi respons bencana Sumatra:** https://ombudsman.go.id/perwakilan/news/r/pwkmedia--ombudsman-soroti-adanya-ketidaksinkronan-informasi-dalam-merespons-bencana-sumatera — Konteks umum kualitas informasi respons bencana, bukan bukti langsung masalah khusus DSP.

---

## Lampiran A: Data contoh untuk pengembangan lokal

Gunakan placeholder berlabel **"DATA CONTOH — JANGAN DITERBITKAN"** selama pengembangan UI. Jangan mengisi angka contoh dengan angka yang terlihat resmi, kecuali sudah ditautkan ke sumber dan status verifikasinya dicatat.

| Field | Placeholder |
|---|---|
| title | Kasus Contoh — belum dipublikasikan |
| informationType | realization_reported |
| amount | 0 |
| purpose | Isi setelah sumber diverifikasi |
| periodLabel | Belum ditetapkan |
| source URL | Tambahkan URL resmi yang telah diperiksa |
| status | draft |

## Lampiran B: Definisi selesai MVP

MVP siap didemokan apabila pengunjung dapat memahami satu kasus DSP melalui ringkasan dan detail, membuka sumber resmi, dan membaca definisi istilah utama. Admin dapat mengelola data manual dengan mekanisme draft dan publikasi, termasuk validasi di bagian 7.1. Integrasi real-time, pelacakan individual, dan analitik kompleks tidak termasuk dalam MVP.
