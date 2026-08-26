# Software Requirements Specification (SRS)

**Project Name:** Line Balancing Application  
**Version:** 2.1  
**Date:** 2026-08-20  
**Standard:** IEEE 830-1998  
**Technology Stack:** Angular, .NET, SQLite

---

## 1. Introduction

### 1.1 Purpose
Dokumen ini mendefinisikan kebutuhan perangkat lunak untuk Line Balancing Application. Dokumen ini menjadi acuan bersama antara stakeholder, analis, desainer, dan developer dalam merancang, membangun, menguji, dan menerima sistem.

### 1.2 Scope
Line Balancing Application adalah aplikasi web untuk lingkungan manufaktur/produksi yang mengambil data produksi dari database existing yang sudah tersedia di perusahaan. Sistem digunakan untuk memonitor performa line produksi, menghitung metrik operasional, menampilkan insight visual, menyediakan histori, dan mengekspor laporan. Sistem fokus pada monitoring dan analytics, bukan kontrol mesin. Sistem tidak menulis atau mengubah data di database existing — hanya membaca (read-only).

### 1.3 Definitions, Acronyms, and Abbreviations
- **DB**: Database, sistem penyimpanan data existing perusahaan
- **Station**: Titik proses atau workstation pada line produksi
- **Cycle Time**: Lama waktu proses unit pada station
- **Waiting Time**: Lama waktu tunggu sebelum unit diproses atau saat station idle
- **Takt Log Time**: Waktu target per unit yang disimpan pada tabel log utama sebagai acuan pembanding cycle time aktual
- **Utilization**: Persentase waktu station digunakan dibanding waktu tersedia
- **Value-Add Time (VA)**: Waktu aktivitas yang menambah nilai pada produk
- **Non-Value-Add Time (NVA)**: Waktu aktivitas yang tidak menambah nilai, seperti menunggu atau idle
- **Dashboard**: Antarmuka visual untuk menampilkan KPI dan grafik
- **KPI**: Key Performance Indicator
- **API**: Application Programming Interface, antarmuka komunikasi antar sistem
- **ETL**: Extract, Transform, Load — proses mengambil, mengolah, dan memuat data dari sumber ke sistem analitik
- **Query**: Perintah pengambilan data ke database

### 1.4 References
- IEEE Std 830-1998, Recommended Practice for Software Requirements Specifications

### 1.5 Overview
Dokumen ini terdiri dari:
- Introduction
- Overall Description
- Specific Requirements
- Use Case Specification
- Acceptance Criteria
- Appendices

---

## 2. Overall Description

### 2.1 Product Perspective
Sistem adalah aplikasi web dashboard yang berfungsi sebagai lapisan analitik di atas database existing. Data produksi — termasuk waktu proses tiap station, status unit, dan event produksi — telah tersimpan di database perusahaan. Sistem membaca data tersebut melalui koneksi database langsung atau melalui API/service layer, mengolahnya menjadi metrik performa, lalu menampilkan hasilnya pada dashboard frontend.

Sistem juga membaca tabel utama `Takt Log Time` yang berisi `ID`, `Cell`, `MeterType`, `SerialNumber`, `StationID`, `StartTime`, dan `EndTime`. Tabel ini menjadi sumber log utama yang menggabungkan data dari beberapa database terpisah, seperti database `MeterType`, `StationID`, dan referensi lain yang menjadi kandidat foreign key.

Sistem tidak menggantikan atau memodifikasi sistem pencatatan data yang ada. Sistem berperan sebagai alat visualisasi dan analitik.

### 2.2 Product Functions
Sistem harus menyediakan fungsi berikut:
1. Menampilkan **Average Per Station**
2. Menampilkan **Total Test**
3. Menampilkan **Waiting Time**
4. Menampilkan **Utilization**
5. Menampilkan **Value-Add Time dan Non Value-Add Time**
6. Menyediakan **Historical Data**
7. Menyediakan **Chart/Visualisasi**
8. Menyediakan **Export Laporan**
9. Menampilkan dan membandingkan **Takt Log Time** dengan cycle time aktual
9. Menampilkan **Takt Log Time** sebagai baseline perbandingan cycle time aktual

### 2.3 User Classes and Characteristics
- **Production Supervisor**
  - Memantau performa line harian
  - Membutuhkan tampilan cepat dan mudah dibaca
- **Industrial/Process Engineer**
  - Menganalisis bottleneck, efisiensi, dan tren historis berdasarkan data DB
- **Plant/Operations Manager**
  - Membutuhkan ringkasan KPI dan laporan periodik
- **Database/IT Administrator**
  - Mengelola koneksi dan hak akses sistem ke database existing
- **Production Planner / Industrial Engineer**
  - Menentukan dan memantau target `Takt Log Time` per line, cell, atau station

### 2.4 Operating Environment
- Platform: Web Application
- Frontend: Angular
- Backend: .NET Web API
- Application Database: SQLite
- Browser: Google Chrome, Microsoft Edge, Mozilla Firefox versi modern
- Jaringan: Intranet perusahaan
- Sumber data: Database existing perusahaan (read-only)

### 2.5 Technology Architecture
- **Angular:** Membangun dashboard, filter, chart, tabel, dan fitur export pada sisi frontend.
- **.NET:** Menyediakan REST API, business logic, kalkulasi metrik, validasi, autentikasi, dan akses data.
- **SQLite:** Menyimpan konfigurasi aplikasi, cache, hasil agregasi, metadata, dan/atau salinan data yang diperlukan untuk analitik lokal.
- **Database Existing:** Sumber utama data produksi. Sistem membaca data menggunakan koneksi read-only melalui .NET.
- **Data Flow:** Database Existing → .NET API → SQLite cache/agregasi bila diperlukan → Angular Dashboard.

### 2.6 Design and Implementation Constraints
- Sistem hanya boleh melakukan operasi READ terhadap database existing
- Query ke database tidak boleh membebani performa sistem produksi existing
- Koneksi ke database existing harus mengikuti kebijakan keamanan IT perusahaan
- Dashboard harus tetap mudah dibaca pada layar monitor produksi
- Sistem harus mampu menangani data multi-station
- SQLite digunakan sebagai database lokal aplikasi dan tidak menggantikan database produksi existing

### 2.5 Design and Implementation Constraints
- Sistem hanya boleh melakukan operasi READ terhadap database existing
- Query ke database tidak boleh membebani performa sistem produksi existing
- Koneksi ke database existing harus mengikuti kebijakan keamanan IT perusahaan
- Dashboard harus tetap mudah dibaca pada layar monitor produksi
- Sistem harus mampu menangani data multi-station
- SQLite digunakan sebagai database lokal aplikasi dan tidak menggantikan database produksi existing

### 2.6 Assumptions and Dependencies
- Database existing memiliki tabel atau view yang menyimpan data produksi per station
- Tiap station memiliki ID unik di dalam database
- Data di database memuat timestamp event yang akurat
- Terdapat definisi shift operasional di dalam database atau konfigurasi sistem
- Terdapat aturan klasifikasi aktivitas VA dan NVA
- Tim IT perusahaan menyediakan akun DB dengan hak akses read-only

---

## 3. Specific Requirements

### 3.1 Functional Requirements

#### FR-01 Average Per Station
- Sistem harus membaca data waktu proses dari database existing.
- Sistem harus menghitung rata-rata cycle time per station berdasarkan data aktual.
- Sistem harus menampilkan average per station untuk periode yang dipilih.
- Sistem harus mendukung perhitungan per shift, per hari, per minggu, dan per bulan.

**Input:**
- Query ke tabel `Takt Log Time` dan tabel referensi terkait
- Field minimal: `StationID`, `StartTime`, `EndTime`

**Output:**
- Nilai rata-rata waktu proses per station

#### FR-02 Total Test
- Sistem harus menghitung total unit yang selesai diproses atau diuji berdasarkan data di database.
- Sistem harus menampilkan total test per station dan total keseluruhan line.
- Sistem harus mendukung filter berdasarkan periode.

**Input:**
- Query ke tabel event produksi di database existing (Station ID, Unit ID, Status, Timestamp)

**Output:**
- Total test per station
- Total test seluruh line

#### FR-03 Waiting Time
- Sistem harus menghitung waiting time berdasarkan kolom Arrival Time dan Start Process Time yang ada di database.
- Sistem harus menampilkan waiting time per station.
- Sistem harus menampilkan waiting time rata-rata dan total dalam periode tertentu.

**Input:**
- Query kolom Arrival Time dan Start Process Time dari database existing

**Output:**
- Waiting time per unit
- Average waiting time per station
- Total waiting time

#### FR-04 Utilization
- Sistem harus menghitung utilization station dari data waktu aktif dan waktu shift yang tersimpan di database.
- Rumus: Utilization = (Active Processing Time / Available Time) × 100%
- Sistem harus menampilkan utilization dalam bentuk angka dan grafik.
- Sistem harus mendukung perhitungan berdasarkan shift/periode.

**Input:**
- Query kolom Processing Time dari `Takt Log Time`
- Data master Shift dari database existing

**Output:**
- Persentase utilization per station

#### FR-05 Value-Add Time and Non Value-Add Time
- Sistem harus mengklasifikasikan durasi aktivitas dari database menjadi VA dan NVA berdasarkan aturan yang dikonfigurasi.
- Sistem harus menampilkan total VA dan NVA per station dan per line.
- Sistem harus menampilkan proporsi VA vs NVA dalam grafik.

**Input:**
- Query kolom Processing Time, Waiting Time, Downtime/Idle Time dari `Takt Log Time`
- Tabel atau konfigurasi aturan klasifikasi VA/NVA

**Output:**
- Total VA time
- Total NVA time
- Rasio atau persentase VA/NVA

#### FR-09 Takt Log Time Monitoring
- Sistem harus membaca data dari tabel utama `Takt Log Time`.
- Sistem harus menampilkan `Takt Log Time` per station, per cell, dan per meter type.
- Sistem harus membandingkan `Takt Log Time` dengan cycle time aktual.
- Sistem harus menandai kondisi overload bila cycle time aktual lebih besar dari `Takt Log Time`.

**Input:**
- `ID`, `Cell`, `MeterType`, `SerialNumber`, `StationID`, `StartTime`, `EndTime`
- Data referensi dari master table/database terpisah

**Output:**
- Nilai `Takt Log Time`
- Selisih `Takt Log Time` vs cycle time aktual
- Status normal / warning / overload

#### FR-06 Historical Data
- Sistem harus memungkinkan user memilih rentang tanggal dan/atau shift untuk memfilter data dari database.
- Sistem harus menampilkan data historis untuk semua metrik utama.
- Sistem harus menyediakan filter berdasarkan station.

**Input:**
- Start Date, End Date, Shift, Station Filter (diteruskan sebagai parameter query ke database)

**Output:**
- Dashboard dan tabel sesuai filter

#### FR-07 Chart/Visualisasi
- Sistem harus menampilkan grafik berdasarkan data yang diambil dari database.
- Minimal grafik:
  - Bar chart untuk Average Per Station
  - Line chart untuk Total Test berdasarkan waktu
  - Pie/Donut chart untuk VA vs NVA
  - Trend chart untuk Utilization dan Waiting Time
  - Comparison chart untuk `Takt Log Time` vs cycle time aktual
  - Heatmap atau matrix untuk distribusi `Takt Log Time` per cell/station

#### FR-08 Export Laporan
- Sistem harus menyediakan export laporan ke PDF dan/atau Excel.
- Laporan harus memuat nilai KPI, grafik, periode data, dan filter yang digunakan.
- User harus dapat mengekspor berdasarkan periode tertentu.
- Laporan harus menyertakan ringkasan `Takt Log Time`, selisih terhadap cycle time aktual, dan status overload bila ada.

### 3.2 External Interface Requirements

#### 3.2.1 User Interface
- Dashboard menampilkan KPI cards, chart, filter tanggal, filter station, dan tombol export
- UI harus mudah dibaca pada layar desktop
- Warna KPI harus membantu identifikasi performa normal/warning/critical

#### 3.2.2 Database Interface
- Sistem harus terhubung ke database existing menggunakan koneksi terenkripsi
- Akses database menggunakan akun read-only yang disediakan tim IT
- Sistem harus mendukung jenis database yang digunakan perusahaan (contoh: MySQL, PostgreSQL, MS SQL Server, atau Oracle)
- Query yang dijalankan harus dioptimalkan agar tidak membebani database existing
- Sistem harus memiliki mekanisme caching untuk mengurangi frekuensi query berulang ke database

#### 3.2.3 Software Interface
- Sistem dapat menggunakan lapisan service/API internal sebagai perantara antara aplikasi dan database existing
- Format pertukaran data internal minimal JSON

#### 3.2.4 Communication Interface
- Koneksi ke database menggunakan protokol standar (TCP/IP) dalam jaringan intranet
- Koneksi menggunakan SSL/TLS bila database server mendukung

### 3.3 Non-Functional Requirements

#### NFR-01 Performance
- Dashboard harus memuat data untuk periode 1 bulan dalam waktu maksimal 5 detik
- Query ke database harus selesai dalam waktu maksimal 3 detik untuk rentang data normal
- Sistem harus menggunakan mekanisme caching agar query yang sama tidak diulang ke database dalam interval singkat

#### NFR-02 Availability
- Sistem harus tersedia minimal 99.5% selama jam operasional
- Bila koneksi ke database terputus, sistem harus menampilkan pesan error yang jelas kepada user

#### NFR-03 Reliability
- Sistem harus menangani kondisi database tidak tersedia tanpa crash
- Sistem harus menampilkan data cache terakhir atau pesan informasi bila koneksi database gagal

#### NFR-04 Security
- Akses user ke dashboard harus melalui autentikasi
- Kredensial koneksi database tidak boleh disimpan dalam kode aplikasi (harus menggunakan environment variable atau secrets manager)
- Sistem hanya boleh menjalankan query SELECT — tidak boleh INSERT, UPDATE, DELETE, atau DDL terhadap database existing

#### NFR-05 Usability
- User utama harus dapat memahami dashboard tanpa pelatihan panjang
- KPI utama harus terlihat dalam 1 layar utama

#### NFR-06 Scalability
- Sistem harus dapat menangani penambahan jumlah station tanpa perubahan besar pada arsitektur
- Sistem harus dapat dihubungkan ke lebih dari satu database atau schema bila diperlukan

#### NFR-07 Maintainability
- Query pengambilan data harus terpisah dari logika bisnis (tidak di-hardcode dalam UI)
- Struktur sistem harus memudahkan penambahan KPI baru di masa depan

### 3.4 Data Requirements
Data yang dibutuhkan dari database existing (minimal):

#### 3.4.1 Takt Log Time Table (Main Table)
| Kolom           | Tipe Data  | Keterangan                                                     |
|-----------------|------------|----------------------------------------------------------------|
| id              | INT/BIGINT | Primary key tabel log                                          |
| cell            | VARCHAR    | Cell / area produksi                                           |
| metertype       | VARCHAR    | Referensi jenis meter/test                                     |
| serialnumber    | VARCHAR    | Nomor seri unit / produk                                       |
| stationid       | VARCHAR    | Referensi station                                               |
| starttime       | DATETIME   | Waktu mulai proses / logging                                    |
| endtime         | DATETIME   | Waktu selesai proses / logging                                  |

#### 3.4.2 Referenced Master Data / Lookup Tables
| Tabel Referensi | Keterangan |
|-----------------|------------|
| MeterType       | Master atau database terpisah untuk daftar tipe meter |
| StationID       | Master atau database terpisah untuk daftar station |
| Cell            | Master atau database terpisah untuk daftar cell |
| SerialNumber    | Master atau database terpisah atau hasil mapping unit |

#### 3.4.3 Analytical Fields
| Kolom           | Tipe Data  | Keterangan                                      |
|-----------------|------------|-------------------------------------------------|
| station_name    | VARCHAR    | Nama stasiun kerja                              |
| event_type      | VARCHAR    | Jenis event (arrived, start, finish, idle, dll) |
| arrival_time    | DATETIME   | Waktu unit tiba di station                      |
| start_time      | DATETIME   | Waktu proses dimulai                            |
| end_time        | DATETIME   | Waktu proses selesai                            |
| shift           | VARCHAR    | Nama atau kode shift                            |
| status          | VARCHAR    | Status event (valid, invalid, dll)              |
| duration        | FLOAT      | Durasi proses dalam detik                       |
| takt_log_time   | FLOAT      | Target waktu per unit untuk perbandingan KPI    |

Catatan: Nama kolom di atas bersifat referensi. Pemetaan ke nama kolom aktual di database existing dilakukan saat tahap implementasi. `Takt Log Time` menjadi tabel utama/log utama yang menghubungkan data dari beberapa database referensi melalui foreign key atau mapping key.

---

## 4. Use Case Specification

### 4.1 Actors
- **Production Supervisor** — monitor performa line dari dashboard
- **Industrial Engineer** — analisis performa dan histori dari database
- **Plant Manager** — melihat KPI dan laporan
- **DB/IT Administrator** — mengelola koneksi dan akses database

### 4.2 Use Case Diagram
Use case utama:
- Monitor Dashboard
- Melihat Average Per Station
- Melihat Total Test
- Melihat Waiting Time
- Melihat Utilization
- Melihat VA/NVA Time
- Melihat Historical Data
- Melihat `Takt Log Time`
- Export Laporan

### 4.3 UC-01 Monitor Dashboard
- **Actor:** Production Supervisor
- **Precondition:** User telah login; koneksi ke database existing aktif
- **Main Flow:**
  1. User membuka dashboard.
  2. Sistem menjalankan query ke database existing untuk data shift berjalan.
  3. Sistem menghitung metrik dari hasil query.
  4. Sistem menampilkan KPI dan chart.
- **Alternative Flow:** Jika koneksi database gagal, sistem menampilkan pesan error dan data cache terakhir bila tersedia.
- **Postcondition:** Dashboard menampilkan data terbaru dari database.

### 4.4 UC-02 Melihat Historical Data
- **Actor:** Industrial Engineer
- **Main Flow:**
  1. User memilih tanggal, shift, dan station.
  2. Sistem memvalidasi filter.
  3. Sistem menjalankan query historis ke database existing dengan parameter filter.
  4. Sistem menampilkan KPI, chart, dan tabel berdasarkan hasil query.
- **Postcondition:** Data historis dari database tampil sesuai filter.

### 4.5 UC-03 Export Laporan
- **Actor:** Plant Manager
- **Main Flow:**
  1. User memilih periode dan station.
  2. User memilih format PDF atau Excel.
  3. Sistem menjalankan query ke database untuk periode tersebut.
  4. Sistem membentuk laporan dari hasil query.
  5. Sistem menyediakan file untuk diunduh.
- **Alternative Flow:** Jika tidak ada data di database untuk periode tersebut, sistem menampilkan pesan bahwa laporan tidak tersedia.

### 4.6 UC-04 Konfigurasi Koneksi Database
- **Actor:** DB/IT Administrator
- **Main Flow:**
  1. Administrator menyediakan kredensial koneksi database (host, port, nama DB, user, password).
  2. Sistem menyimpan konfigurasi koneksi secara aman.
  3. Sistem melakukan uji koneksi ke database.
  4. Sistem mengonfirmasi koneksi berhasil atau menampilkan pesan error.
- **Alternative Flow:** Jika koneksi gagal, sistem menampilkan detail error untuk keperluan troubleshooting.

### 4.7 UC-05 Lihat Takt Log Time
- **Actor:** Production Supervisor / Industrial Engineer
- **Precondition:** Data `Takt Log Time` tersedia di database existing
- **Main Flow:**
  1. User memilih periode, cell, station, atau meter type.
  2. Sistem membaca data dari tabel utama `Takt Log Time`.
  3. Sistem mengambil referensi dari database master terkait (`MeterType`, `StationID`, dan lainnya).
  4. Sistem menghitung selisih antara `Takt Log Time` dan cycle time aktual.
  5. Sistem menampilkan status normal, warning, atau overload.
- **Postcondition:** User melihat `Takt Log Time` dan perbandingannya dengan performa aktual.

---

## 5. Acceptance Criteria

### AC-01 Dashboard dan Koneksi Database
- Dashboard menampilkan data dari database existing saat pertama kali dibuka.
- Sistem menampilkan status koneksi database (connected/disconnected).
- Jika koneksi terputus, sistem menampilkan pesan informatif, bukan halaman error kosong.

### AC-02 Average Per Station
- Sistem menghitung rata-rata cycle time berdasarkan data di database untuk station dan periode yang dipilih.
- Hasil menampilkan satuan detik atau menit.
- Station tanpa data di database menampilkan `N/A`, bukan nilai `0`.

### AC-03 Total Test
- Total hanya menghitung record dengan status valid dan selesai dari database.
- Unit ID yang duplikat di database tidak dihitung dua kali.
- Total dapat difilter berdasarkan periode dan station.

### AC-04 Waiting Time
- Sistem menghitung selisih kolom Arrival Time dan Start Time dari database.
- Nilai waiting time negatif (data tidak konsisten di DB) ditolak atau ditandai sebagai anomali.
- Dashboard menampilkan total dan rata-rata waiting time.

### AC-05 Utilization
- Sistem menghitung utilization berdasarkan data Processing Time dan data master Shift dari database.
- Hasil berada pada rentang 0–100%.
- Perhitungan mendukung filter shift dan periode.

### AC-06 Value-Add dan Non-Value-Add Time
- Sistem mengklasifikasikan data dari database sesuai aturan VA/NVA yang dikonfigurasi.
- Processing Time diklasifikasikan sebagai VA.
- Waiting, idle, dan downtime dari database diklasifikasikan sebagai NVA.
- Grafik menampilkan perbandingan VA dan NVA.

### AC-07 Historical Data
- User dapat memfilter tanggal, shift, dan station; sistem meneruskan filter sebagai parameter query ke database.
- Semua KPI mengikuti filter yang dipilih.
- Sistem menampilkan pesan jika tidak ada data di database untuk filter tersebut.

### AC-08 Export Laporan
- User dapat mengekspor laporan PDF dan Excel berdasarkan data yang diambil dari database.
- File memuat periode, filter, KPI, grafik, dan data per station.
- Sistem menolak export jika periode tidak valid atau tidak ada data.

### AC-09 Keamanan Akses Database
- Sistem hanya menjalankan query SELECT ke database existing.
- Kredensial database tidak tampil di log atau antarmuka user.
- Akses ke konfigurasi koneksi database hanya tersedia untuk administrator.

### AC-10 Takt Log Time
- Sistem menampilkan data `Takt Log Time` dari tabel utama.
- Sistem menampilkan perbandingan `Takt Log Time` vs cycle time aktual.
- Jika cycle time aktual lebih besar dari `Takt Log Time`, sistem menampilkan status warning atau overload.
- User dapat memfilter `Takt Log Time` berdasarkan cell, station, dan meter type.

---

## 6. Appendices

### 6.1 Example Formulas
- **Average Per Station**  
  Average = SUM(end_time - start_time) / COUNT(unit_id) per station

- **Total Test**  
  Total Test = COUNT(unit_id) WHERE status = 'finished' per station

- **Waiting Time**  
  Waiting Time = start_time - arrival_time per unit

- **Utilization**  
  Utilization = SUM(duration) / total_shift_duration × 100% per station

- **Value-Add Time**  
  VA = SUM(duration) WHERE event_type IN (daftar aktivitas VA)

- **Non Value-Add Time**  
  NVA = SUM(waiting_time + idle_time + downtime) per station

### 6.2 Contoh Query Referensi (SQL)

```sql
-- Average Per Station untuk periode tertentu
SELECT
    station_id,
    station_name,
    AVG(duration) AS avg_cycle_time,
    COUNT(unit_id) AS total_test
FROM production_events
WHERE
    event_type = 'process_finished'
    AND start_time >= :start_date
    AND end_time <= :end_date
    AND shift = :shift
GROUP BY station_id, station_name;

-- Utilization per Station
SELECT
    station_id,
    SUM(duration) / :shift_duration * 100 AS utilization_pct
FROM production_events
WHERE
    event_type IN ('process_started', 'process_finished')
    AND start_time >= :start_date
    AND end_time <= :end_date
GROUP BY station_id;

-- VA vs NVA per Station
SELECT
    station_id,
    SUM(CASE WHEN event_type IN ('assembling','testing') THEN duration ELSE 0 END) AS va_time,
    SUM(CASE WHEN event_type IN ('waiting','idle','downtime') THEN duration ELSE 0 END) AS nva_time
FROM production_events
WHERE start_time >= :start_date AND end_time <= :end_date
GROUP BY station_id;
```

### 6.3 Suggested Dashboard Layout
- Header: Judul aplikasi, status koneksi DB, filter tanggal, filter shift, filter station, export button
- KPI Cards:
  - Average Per Station
  - Total Test
  - Waiting Time
  - Utilization
  - VA vs NVA
  - `Takt Log Time`
- Charts:
  - Bar chart Average Per Station
  - Line chart Total Test trend
  - Pie/Donut chart VA vs NVA
  - Trend chart Utilization/Waiting Time
  - Comparison chart `Takt Log Time` vs cycle time aktual
- Table:
  - Detail data per station per periode (diambil langsung dari DB)

### 6.4 Data Relationship Assumption
- `Takt Log Time` adalah tabel utama yang menghubungkan data log produksi.
- `MeterType`, `StationID`, dan entitas referensi lain dianggap sebagai master table atau database terpisah.
- Sistem dapat melakukan join atau mapping terhadap foreign key untuk menampilkan nama, kategori, dan atribut referensi.
- Jika foreign key tidak tersedia secara fisik, sistem dapat menggunakan logical key mapping berdasarkan kolom referensi yang konsisten.
