# Context: Jejak Bencana

Dokumen ini adalah konteks singkat untuk AI agent yang bekerja di sistem Jejak Bencana. Baca dokumen ini dulu, lalu buka `specs.md` untuk detail backend atau `PRD.md` untuk konteks produk lengkap.

---

## 1. Apa sistem ini

Jejak Bencana adalah website informasi yang membantu masyarakat memahami informasi **Dana Siap Pakai (DSP)** untuk penanggulangan bencana. Website ini menjelaskan angka DSP dengan bahasa sederhana, memberi konteks, dan menunjukkan sumber resminya.

Sistem ini **bukan**:

- sistem pencatatan transaksi pemerintah;
- platform donasi atau penyaluran bantuan;
- alat audit atau verifikasi dana berbasis AI;
- sistem pelaporan dugaan penyalahgunaan dana.

Nilai utamanya: menyajikan informasi resmi dengan bahasa yang mudah dipahami, dan membuatnya bisa diperiksa lewat tautan sumber.

---

## 2. Ruang lingkup MVP

- **Satu kasus bencana.** Kasus yang dipakai belum final, menunggu audit kecukupan sumber.
- **Data dimasukkan manual** oleh admin. Tidak ada integrasi otomatis atau real-time.
- **Halaman publik tanpa login.** Pengunjung hanya melihat konten yang sudah diterbitkan.
- **Satu jenis akun admin** untuk mengelola draft dan publikasi. Tidak ada role bertingkat atau workflow persetujuan berlapis.

Di luar MVP: banyak kasus sekaligus, peta interaktif kompleks, pencarian lanjutan, analitik lanjutan, integrasi otomatis.

---

## 3. Pengguna

| Pengguna | Tujuan utama |
|---|---|
| Masyarakat/pengunjung publik | Memahami satu kasus DSP, membuka sumber asli, membaca glosarium. |
| Admin konten | Mengisi dan memperbarui kasus, catatan dana, dan sumber tanpa mengubah kode. Menerbitkan setelah validasi. |

---

## 4. Halaman publik

| Halaman | Path | Isi utama |
|---|---|---|
| Beranda | `/` | Penjelasan produk, ringkasan satu kasus, tombol "Lihat detail". |
| Detail kasus | `/kasus/[slug]` | Nominal DSP dengan jenis informasi, tujuan, wilayah dan periode (jika didukung sumber), sumber, dan batasan informasi. |
| Glosarium | `/glosarium` | Definisi istilah inti dengan bahasa awam. |
| Login admin | `/admin/login` | Form email dan kata sandi. |
| Dashboard admin | `/admin/dashboard` | Daftar kasus dengan status, form kasus, catatan dana, sumber, tombol simpan draft dan terbitkan. |

---

## 5. Istilah inti

Istilah berikut dipakai di sistem. Definisi final harus mengikuti dokumen sumber, bukan tebakan.

| Istilah | Makna di sistem |
|---|---|
| DSP (Dana Siap Pakai) | Dana untuk penanggulangan bencana yang menjadi fokus produk. |
| `realization_reported` | Realisasi yang dilaporkan sumber. Tampilkan penerbit, periode, dan definisi sesuai sumber. |
| `disbursement` | Penyaluran. Jangan disamakan otomatis dengan penggunaan akhir oleh penerima. |
| `allocation` | Alokasi atau penetapan. Hanya dipakai jika sumber memang menyebut alokasi atau penetapan. |
| `planned` | Rencana atau pengajuan. **Tidak boleh dijumlahkan ke realisasi.** |
| Bantuan stimulan | Istilah dalam glosarium. Cek definisinya ke sumber resmi sebelum dipakai. |

---

## 6. Aturan yang tidak boleh dilanggar

**Data dan angka**

- Setiap angka penting di halaman publik harus bisa ditelusuri ke minimal satu sumber resmi.
- Nominal disimpan sebagai **bilangan rupiah utuh** (`long` di C#). Format seperti "Rp537,22 miliar" hanya untuk tampilan.
- Periode dan tanggal hanya ditampilkan jika didukung sumber. **Jangan menebak** tanggal awal atau akhir.
- Jika dua sumber berbeda, beri catatan perbedaan atau tunda publikasi.
- Jika data tidak tersedia atau belum cocok antar-sumber, nyatakan keterbatasannya. Jangan mengisi dengan tebakan.

**Kesimpulan**

- Jangan menyimpulkan bahwa dana sudah diterima individu hanya dari angka agregat realisasi.
- Jangan menyimpulkan penyalahgunaan atau ketiadaan akuntabilitas hanya karena dokumen tidak ditemukan.
- Sistem ini membantu memahami informasi, bukan menghakimi.

**Publikasi**

- Draft **tidak boleh** muncul di halaman atau endpoint publik.
- Kasus hanya bisa diterbitkan jika lolos validasi di server (lihat `specs.md` bagian 6).
- Angka contoh atau placeholder tidak boleh diterbitkan seolah-olah resmi.

**Keamanan**

- Validasi dilakukan di server. Validasi frontend hanya membantu.
- Semua endpoint admin wajib memeriksa sesi dan otorisasi di server.
- Secret dan kredensial tidak boleh masuk repository. Gunakan environment variable.
- Kata sandi disimpan sebagai hash lewat ASP.NET Core Identity.
- Pesan error tidak boleh membocorkan kredensial atau detail internal.
- Jangan menyimpan data pribadi penerima bantuan yang tidak diperlukan.

---

## 7. Arsitektur

Dua repository terpisah, bukan monorepo:

| Repository | Teknologi | Isi |
|---|---|---|
| `jejak-bencana-api` | ASP.NET Core Web API (Minimal API), EF Core + Npgsql, PostgreSQL, ASP.NET Core Identity | Domain, Data (AppDbContext dan migrasi), Endpoints, Program.cs |
| `jejak-bencana-web` | Next.js + TypeScript, Tailwind CSS | Halaman publik dan admin, komponen sumber |

- Next.js meneruskan `/api/*` ke backend lewat rewrites di `next.config`. Browser hanya berhadapan dengan satu origin, sehingga cookie sesi admin berfungsi tanpa CORS khusus.
- Backend tidak bisa berjalan di Vercel. Hosting backend dan database **belum dipilih**.
- Kontrak API didokumentasikan lewat Swagger/OpenAPI di repository API. Perubahan kontrak **harus disepakati tim** sebelum diterapkan di kedua repository.

---

## 8. Konvensi

| Hal | Aturan |
|---|---|
| Nama C# | PascalCase (`DisasterCase`, `FundingRecord`) |
| Nama JSON ke frontend | camelCase (`informationType`, `periodLabel`) |
| Nilai enum di database | String, lewat konversi enum EF Core (`HasConversion<string>()`) |
| Kode jenis informasi | `allocation`, `disbursement`, `realization_reported`, `planned` |
| Status kasus | `Draft`, `Published` |
| Slug kasus | Unik |
| Nominal | Bilangan bulat rupiah utuh |

---

## 9. Status saat ini

- **Kasus bencana final:** belum diputuskan.
- **Kandidat data awal:** satu publikasi BNPB menyebut realisasi DSP Rp537,22 miliar untuk bantuan stimulan rumah rusak hingga 6 April 2026. Ini **hanya kandidat audit**. Jangan diterbitkan sebelum audit sumber selesai (tahap F).
- **Akses detail SISTALA-DSP** (https://dsp.bnpb.go.id/): perlu diverifikasi, termasuk apa yang terbuka tanpa login.
- **Desain visual final:** belum dikunci. Utamakan UI bersih, mudah dibaca, dan tidak berlebihan grafiknya.

Keputusan yang masih terbuka dan berdampak ke implementasi ada di `specs.md` bagian 15.

---

## 10. Cara bekerja sebagai AI agent di sistem ini

1. **Jika tidak yakin, tanyakan.** Jangan menebak nominal, periode, tanggal, atau aturan bisnis yang tidak tertulis.
2. **Ikuti urutan tahapan.** Selesaikan dan uji satu tahap sebelum lanjut ke tahap berikutnya.
3. **Jangan ubah kontrak API sendiri.** Usulkan perubahan dan tunggu kesepakatan tim.
4. **Jangan menerbitkan data.** Tahap F dan publikasi data nyata dilakukan tim setelah audit sumber.
5. **Gunakan data contoh dengan benar.** Label "DATA CONTOH — JANGAN DITERBITKAN", status `Draft`, nilai `amount` 0. Lihat `specs.md` bagian 13.2.
6. **Jaga bahasa produk.** Teks untuk pengunjung harus sederhana, tidak teknis, dan tidak membuat klaim di luar sumber.
7. **Tandai asumsi.** Jika harus mengambil keputusan kecil, sebutkan asumsinya agar tim bisa meninjau.

---

## 11. Dokumen terkait

| Dokumen | Isi | Kapan dibaca |
|---|---|---|
| `context.md` | Konteks ringkas (dokumen ini) | Selalu, pertama kali. |
| `specs.md` | Spesifikasi backend: model data, endpoint, validasi, tahapan | Saat mengerjakan API, database, atau autentikasi. |
| `PRD.md` | PRD teknis lengkap v2.1, termasuk UI, alur pengguna, dan referensi | Saat butuh konteks produk atau detail UI. |

Versi dokumen: PRD v2.1 (22 Oktober 2026).
