# 📖 Dokumentasi Fungsi — Line Balancing Application

> Referensi lengkap semua fungsi di Backend (.NET) dan Frontend (Angular).
> Data dummy: 360 log test, 80 unit dites, 100 unit terdaftar, 4 cell × 5 station.
> Target takt time: **48 detik** (`appsettings.json` → `Kpi:TargetTaktSeconds`).

---

# BAGIAN 1 — BACKEND (.NET Web API)

## 1.1 `Program.cs` — Titik Masuk Aplikasi

| Fungsi/Bagian | Penjelasan |
|---------------|------------|
| `Main()` — registrasi service | Mendaftarkan semua dependency ke container DI: MVC Controllers, Swagger, `IMemoryCache`, ProblemDetails (error response standar), CORS (khusus origin Angular `localhost:4200`), Health Checks (cek koneksi DB), `AppDbContext` (SQLite), Repository/Services, dan binding `KpiOptions` |
| `QuestPDF.Settings.License` | Set lisensi Community untuk generator PDF |
| Blok `CsvSeeder.SeedAsync()` | Saat startup: baca 5 CSV dari `Data/DummyCsv/` dan isi database SQLite (hanya jika DB kosong) |
| Pipeline middleware | Urutan request: Swagger (dev) → Exception Handler (error rapi tanpa stack trace) → CORS → HTTPS redirect → Routing Controller → `/health` |

## 1.2 `appsettings.json` — Konfigurasi

| Kunci | Penjelasan |
|-------|------------|
| `ConnectionStrings:AppDb` | Path database SQLite lokal (`app.db`) |
| `ConnectionStrings:ExistingDb` | Placeholder koneksi MSSQL perusahaan (belum dipakai, disiapkan untuk SRS) |
| `Kpi:TargetTaktSeconds` | Target takt time = **48 detik** — acuan status normal/warning/overload |

## 1.3 `Contracts/` — Kontrak API

| Tipe | Penjelasan |
|------|------------|
| `KpiFilter(CellId, StationId, MeterTypeId)` | Filter query untuk semua endpoint KPI. Semua opsional (null = tanpa filter). Memakai **ID numeric** agar query membandingkan Primary Key langsung |
| `PagedResult<T>` | Pembungkus response terpaginasi: `Items`, `Page`, `PageSize`, `TotalCount`, + computed `TotalPages` |

## 1.4 `Controllers/` — Endpoint HTTP

### `KpiController` (`api/kpi`)

| Fungsi | Endpoint | Penjelasan |
|--------|----------|------------|
| `GetDashboard()` | `GET /api/kpi/dashboard` | Mengembalikan SEMUA KPI sekaligus: summary, average per station, waiting, utilization, VA/NVA, takt comparison, cell summary, unit flow, unit flow detail, station summary. Hasil di-cache 30 detik |
| `GetHistory()` | `GET /api/kpi/history?page=&pageSize=` | Log mentah terpaginasi. Validasi: `page ≥ 1` dan `1 ≤ pageSize ≤ 100` — dilanggar → response 400 |

### `MasterController` (`api/master`)

| Fungsi | Endpoint | Penjelasan |
|--------|----------|------------|
| `GetCells()` | `GET /api/master/cells` | Daftar cell (sumber dropdown) |
| `GetStations(cellId?)` | `GET /api/master/stations` | Daftar station; bila `cellId` diberikan hanya station milik cell itu (dropdown bertingkat) |
| `GetMeterTypes()` | `GET /api/master/meter-types` | Daftar tipe meter |
| `GetShifts()` | `GET /api/master/shifts` | Hardcoded dummy (Shift-1/2/3) — frontend belum memakainya |

### `ExportController` (`api/export`)

| Fungsi | Endpoint | Penjelasan |
|--------|----------|------------|
| `ExportExcel(filter)` | `GET /api/export/excel` | Hitung dashboard → bentuk file `.xlsx` multi-sheet → kirim sebagai download |
| `ExportPdf(filter)` | `GET /api/export/pdf` | Hitung dashboard → bentuk `.pdf` multi-section → kirim sebagai download |

## 1.5 `Models/` — Entitas Database

| Entitas | Tabel | Field | Keterangan |
|---------|-------|-------|------------|
| `Cell` | `Cell` | `Id` (PK), `CellName` | Area/line produksi. 4 row (Cell-A s/d Cell-D), tiap cell punya 5 station |
| `Station` | `Station` | `Id` (PK 1-20), `CellId` (FK), `StationName` | Titik proses. Nama sama antar cell (Assembly, Calibration, dst.) tapi ID unik |
| `MeterType` | `MeterType` | `Id` (PK), `Name` | Jenis meter (MT-A/B/C) |
| `UnitMaster` | `UnitMaster` | `SerialNumber` (PK), `CellId` (FK), `MeterTypeId` (FK) | **Work order** — daftar unit yang direncanakan dites. 100 row; yang tidak punya log = "menunggu" |
| `TaktLogTime` | `TaktLogTime` | `Id` (PK), `StationId` (FK), `MeterTypeId` (FK), `SerialNumber`, `ArrivalTime`, `StartTime`, `EndTime` | Log utama — 1 row = 1 unit selesai 1 test di 1 station. 360 row |

**Computed property:**

| Property | Rumus | Keterangan |
|----------|-------|------------|
| `TaktLogTime.DurationSeconds` | `EndTime − StartTime` | Durasi proses (detik), tidak disimpan di DB — dihitung on-the-fly |

## 1.6 `Data/` — Akses & Inisialisasi Database

### `AppDbContext`

| Bagian | Penjelasan |
|--------|------------|
| 5 `DbSet` | `Cells`, `Stations`, `MeterTypes`, `UnitMasters`, `TaktLogTimes` |
| Mapping tabel | Nama tabel eksplisit, panjang kolom, required |
| **Index** | `TaktLogTime`: StationId, MeterTypeId, StartTime, EndTime, composite (StationId+StartTime) — percepat filter/agregasi KPI. `Station`: CellId, StationName. `Cell`: CellName. `MeterType`: Name. `UnitMaster`: CellId, MeterTypeId |
| **Foreign Key** | Station→Cell, TaktLogTime→Station, TaktLogTime→MeterType, UnitMaster→Cell, UnitMaster→MeterType |

### `CsvSeeder` (static class)

| Fungsi | Penjelasan |
|--------|------------|
| `SeedAsync(db, csvFolder)` | Orkestrasi seeding: `EnsureCreated` → skip jika DB sudah ada isi → baca 5 CSV → validasi → insert berurutan (master dulu, baru log) → cetak ringkasan + jumlah row yang di-skip |
| `ReadCells(path)` | Parse `cell_master.csv` (ID, CellName) |
| `ReadStations(path)` | Parse `station_master.csv` (ID, CellID, StationName) — 20 row, 5 per cell |
| `ReadMeterTypes(path)` | Parse `meter_type_master.csv` (ID, Name) |
| `ReadUnitMasters(path)` | Parse `unit_master.csv` (SerialNumber, CellID, MeterTypeID) — 100 row |
| `ReadTaktLogs(path)` | Parse `takt_log_time.csv` 7 kolom (termasuk ArrivalTime). **Defensif**: row gagal parse di-skip, bukan crash |

**Validasi seeder:** FK orphan (StationID/MeterTypeID tidak dikenal) → skip; `EndTime < StartTime` → skip; jumlah skip dicetak ke console.

## 1.7 `Repositories/` — Query Database

### `ProductionRepository` (implementasi `IProductionRepository`)

| Fungsi | Penjelasan |
|--------|------------|
| `GetCellsAsync()` | Ambil semua cell, urut ID (dropdown) |
| `GetStationsAsync(cellId?)` | Ambil station, opsional filter per cell (dropdown bertingkat) |
| `GetMeterTypesAsync()` | Ambil semua tipe meter |
| `GetRegisteredUnitsAsync(filter)` | Ambil unit terdaftar dari `UnitMaster` (JOIN Cell + MeterType) sesuai filter — dasar metrik registered/tested/untested |
| `GetLogsAsync(filter)` | **Query inti**: JOIN 4 tabel (log+station+cell+meter) dengan projection minimal kolom → `TaktLogRowDto`. Dipakai perhitungan dashboard (single-pass) |
| `GetLogsPagedAsync(filter, page, pageSize)` | Sama dengan di atas + `LongCountAsync` (total) + `Skip/Take` (hanya 1 halaman) — dipakai endpoint history |

**Catatan:** filter membandingkan PK langsung (`cell.Id == filter.CellId`), bukan nama string.

### `KpiDtos.cs` — DTO Hasil Perhitungan

| DTO | Isi |
|-----|-----|
| `MasterLookupDto` | Id + Name (dropdown cell/meter) |
| `StationLookupDto` | Id, Name, CellId, CellName (dropdown station bertingkat) |
| `TaktLogRowDto` | Baris log hasil JOIN — bahan mentah semua KPI |
| `UnitMasterRowDto` | Unit terdaftar + nama cell/meter |
| `StationAverageDto` | Avg cycle time + TotalTest per station (FR-01) |
| `WaitingTimeDto` | Avg + total waiting fisik per station (FR-03) |
| `UtilizationDto` | Active time, span, utilization % (FR-04) |
| `VaNvaDto` | VA, NVA, VA% per station (FR-05) |
| `TaktComparisonDto` | Avg cycle + status normal/warning/overload (FR-09) |
| `TaktLogDetailDto` | Baris history siap tampil (termasuk ArrivalTime) |
| `UnitFlowDto` | Registered, tested unik, lolos 5/5, rate, distribusi 1-5 test per cell |
| `UnitFlowDetailDto` | Per SN: jumlah test + daftar station dilewati |
| `StationSummaryDto` | **All-in-one** 11 kolom per station (untuk Ringkasan Akhir) |

## 1.8 `Services/` — Logika Bisnis

### `KpiService` — Orkestrator + Cache

| Fungsi | Penjelasan |
|--------|------------|
| `GetDashboardAsync(filter)` | Cek cache (`kpi:dashboard:{CellId}:{StationId}:{MeterTypeId}`, TTL 30 detik). Cache miss → `ComputeDashboardAsync` |
| `GetHistoricalAsync(filter, page, pageSize)` | Ambil log terpaginasi dari repo → map ke `TaktLogDetailDto` |
| `ComputeDashboardAsync()` (private) | 2 query (logs + registered) → semua metrik dihitung `MetricCalculator` dari data yang sama → susun `KpiDashboardResult` |

### `MetricCalculator` (static) — SEMUA RUMUS KPI

| Fungsi | Rumus | Output |
|--------|-------|--------|
| `ComputeStationAverages(rows)` | Rata-rata `(End − Start)` per station | Avg cycle + TotalTest per station |
| `ComputeWaitingTimes(rows)` | Per log: `Start − Arrival` (waktu tunggu fisik), lalu rata-rata & total per station | Avg + total waiting |
| `ComputeUtilization(rows)` | `Σ durasi ÷ (Max End − Min Start) × 100` per station | Utilization % (span-based) |
| `ComputeVaNva(rows)` | VA = `End − Start`; NVA = `Start − Arrival`; `VA% = VA ÷ (VA+NVA) × 100` | VA, NVA, VA% per station |
| `ComputeTaktComparison(rows, target)` | `avg > target` → overload; `> 90%×target` → warning; sisanya normal | Status per station |
| `ComputeCellSummary(rows, ...)` | Agregat per cell: total test, avg durasi, avg waiting fisik, rata-rata utilization, total VA/NVA | Summary per cell |
| `ComputeUnitFlow(rows, registered)` | Group by Cell+SN → distinct station per unit; lolos 5/5 bila ≥ jumlah station cell; histogram 1-5 test; + registered per cell | Unit flow per cell |
| `ComputeUnitFlowDetail(rows)` | Per SN: test count + daftar nama station dilewati | Detail per unit |
| `ComputeStationSummary(...)` | Gabung 5 dataset per StationId (dictionary lookup) | Tabel all-in-one |
| `ComputeSummary(rows, averages, util, vaNva, registered)` | Agregat global: avg antar station, row count, **tested unik (distinct Cell+SN)**, registered, untested = registered − tested, avg waiting fisik, avg utilization, VA% global | Kartu KPI |

### `MasterDataService` — Master Lookup + Cache

| Fungsi | Penjelasan |
|--------|------------|
| `GetCellsAsync()` | Cell untuk dropdown — cache key `master:cells`, TTL 5 menit |
| `GetStationsAsync(cellId?)` | Station per cell — cache key per cell (`master:stations:2`, dst.) |
| `GetMeterTypesAsync()` | Tipe meter — cache key `master:meterTypes` |

### `ExportService` — Laporan Excel & PDF

| Fungsi | Penjelasan |
|--------|------------|
| `ExportExcel(dashboard, filter)` | Workbook 4 sheet: **Ringkasan** (filter+timestamp, KPI, cell summary), **Ringkasan Station** (11 kolom + baris TOTAL), **Unit Flow** (per cell), **Unit Flow Detail** (per SN). Kolom auto-width |
| `ExportPdf(dashboard, filter)` | PDF A4 5 section: judul + **filter + timestamp** (AC-08), KPI summary, tabel Ringkasan Station, tabel Cell Summary, tabel Unit Flow |

### `KpiOptions` — Binding Konfigurasi

| Field | Penjelasan |
|-------|------------|
| `TargetTaktSeconds = 48` | Dibaca dari `appsettings.json` via `IOptions<T>` — ubah target tanpa rebuild kode |

---

---

# BAGIAN 2 — FRONTEND (Angular 21)

## 2.1 Bootstrap

| File | Fungsi |
|------|--------|
| `index.html` | Script **anti-flash**: baca theme dari `localStorage` dan set `data-theme` sebelum Angular jalan |
| `main.ts` | Bootstrap `App` component |
| `app.config.ts` | Provider: error listeners, `HttpClient`, Router |
| `styles.scss` | **Design tokens** CSS variables (`--bg`, `--panel`, `--accent`, dst.) untuk dark (default) & light (`[data-theme='light']`) |

## 2.2 `api.service.ts` — Komunikasi HTTP (base: `http://127.0.0.1:5121`)

| Fungsi | Penjelasan |
|--------|------------|
| `getCells()` | `GET /api/master/cells` — isi dropdown cell |
| `getStations(cellId?)` | `GET /api/master/stations` — dropdown station (difilter per cell) |
| `getMeterTypes()` | `GET /api/master/meter-types` — dropdown meter type |
| `getDashboard(filter)` | `GET /api/kpi/dashboard` — semua KPI |
| `getHistory(filter, page, pageSize)` | `GET /api/kpi/history` — history terpaginasi |
| `exportExcel(filter)` / `exportPdf(filter)` | URL endpoint export (dibuka via `window.open`) |
| `toParams()` / `toQuery()` (private) | Ubah object filter → query string; nilai kosong dilewati |

## 2.3 `api.models.ts` — Tipe Data

Interface yang mencerminkan DTO backend: `KpiFilter`, `PagedResult<T>`, `KpiDashboardResult` (summary + 9 array metrik), `StationAverageDto`, `WaitingTimeDto`, `UtilizationDto`, `VaNvaDto`, `TaktComparisonDto`, `CellSummaryDto`, `UnitFlowDto`, `UnitFlowDetailDto`, `StationSummaryDto`, `MasterLookupDto`, `StationLookupDto`, `TaktLogDetailDto`.

## 2.4 `app.ts` — Logika Komponen Utama

### State (signal)

| Signal | Fungsi |
|--------|--------|
| `dashboard`, `history` | Data utama + baris history halaman aktif |
| `historyPage/TotalPages/TotalCount` | Status pagination |
| `cells`, `stations`, `meterTypes` | Master data dropdown |
| `loading`, `error`, `dbStatus` | Status UI & koneksi |
| `theme` | `'dark'` / `'light'` (persist `localStorage`) |
| `firstLoadDone` | Bedakan skeleton (load pertama) vs spinner (refresh) |
| `lastUpdated`, `now` | Timestamp update terakhir + ticker "x lalu" |
| `kpiHistory` | Riwayat 6 KPI (maks 12 titik) untuk sparkline |
| `autoRefresh`, `refreshInterval` | Status polling |

### Method

| Fungsi | Penjelasan |
|--------|------------|
| `ngOnInit()` | Terapkan theme tersimpan, mulai ticker 5 detik, muat master + dashboard, langganan perubahan filter cell (→ reload station dropdown) |
| `ngOnDestroy()` | Bersihkan timer auto-refresh & ticker |
| `load()` | Muat dashboard + history halaman 1; set `dbStatus`, `lastUpdated`, simpan riwayat KPI, render chart |
| `loadHistory(filter, page)` | Muat 1 halaman history |
| `goToPage(page)` | Navigasi pagination history |
| `selectStation(stationId)` | **Drill-down**: klik baris station di Ringkasan Akhir → set filter + reload |
| `clearFilters()` | Reset semua filter ke "All" |
| `hasActiveFilter` | Property: apakah ada filter aktif (tampilkan tombol reset) |
| `toggleAutoRefresh()` / `onIntervalChange()` / `applyAutoRefresh()` | Kelola polling 15/30/60 detik (interval dibersihkan saat off/destroy) |
| `toggleTheme()` | Ganti dark/light → set atribut `<html>`, persist, re-render chart dengan palet baru |
| `palette()` (private) | Warna chart mengikuti theme aktif |
| `sparklinePoints(idx)` | Konversi riwayat KPI → points polyline SVG (viewBox 100×30) |
| `waitBar(seconds)` | Lebar bar waiting (skala 300s = penuh) |
| `updatedAgoText` | "x detik/menit lalu" dari `lastUpdated` vs `now` |
| `pushKpi(...)` | Simpan 6 nilai KPI ke riwayat sparkline |
| `exportExcel()` / `exportPdf()` | Buka URL export dengan filter aktif |
| `renderCharts(data)` | Panggil 5 renderer chart |
| `renderBarChart` | Bar avg cycle per station (rounded, tanpa legend) |
| `renderLineChart` | Line total test + gradient fill |
| `renderPieChart` | Donut VA/NVA (cutout 68%) + plugin teks tengah |
| `renderTrendChart` | 2 garis: Utilization % & Waiting per station |
| `renderFlowChart` | Bar distribusi 1-5 test (merah→hijau) |
| `baseScales()` (private) | Styling sumbu chart dari palet theme |
| `centerTextPlugin` | Plugin Chart.js: teks VA% di tengah donut |
| `toFilter()` (private) | Form → `KpiFilter` (ID numeric) |

## 2.5 `app.html` — Struktur Tampilan

| Section | Isi & Fungsi |
|---------|--------------|
| **Topbar** (sticky + blur) | Judul, badge status DB, "Diperbarui x lalu", toggle ☀/🌙, auto-refresh, Load Data, Export |
| **Filters** | Dropdown Cell → Station (bertingkat) → Meter Type + tombol Reset (muncul saat filter aktif) |
| **Skeleton / Spinner** | Load pertama = skeleton shimmer; refresh = spinner kecil |
| **Error banner** | Pesan gagal koneksi + saran troubleshooting |
| **8 KPI Cards** | Avg/Station, Total Test, Unit Dites (Unik), Unit Terdaftar (+progress hijau, "x menunggu"), Waiting, Utilization (+progress), VA% (+progress), Takt Status — kartu 1-3 punya sparkline |
| **5 Charts** | Bar avg, line total test, donut VA/NVA, trend util+waiting, bar unit flow |
| **Ringkasan Akhir** | 20 baris × 10 kolom (data-bar di Waiting/Util/VA), badge takt berwarna (overload pulse), baris TOTAL, **klik baris = drill-down** |
| **Unit Flow** | Tabel per cell (Terdaftar/Dites/Menunggu/Lolos/Rate bar) + chart distribusi |
| **Detail Per Unit** | Tiap SN: cell, `x/5`, station dilewati; belum lulus = kuning |
| **History** | Log mentah + kolom Arrival + pager Prev/Next |

---

---

# BAGIAN 3 — GLOSARIUM METRIK & RUMUS

## Cycle Time

Waktu proses aktif satu unit di satu station.

```
Per log:      Cycle = EndTime − StartTime                    (contoh: 38 detik)
Per station:  Avg = rata-rata cycle time semua log station   (contoh: 41.85s)
Per line:     Avg = rata-rata antar station                  (contoh: 44.89s)
```

Tidak termasuk waktu antre (itu Waiting Time).

## Waiting Time (Waktu Tunggu Fisik)

```
Waiting = StartTime − ArrivalTime
```

Sejak unit **tiba** di station sampai **mulai diproses**. Tidak berubah saat difilter per meter type (karena dihitung per unit, bukan jarak antar unit).

## Utilization %

```
Utilization = Σ durasi proses ÷ (Max EndTime − Min StartTime) × 100
```

Seberapa sibuk station dalam rentang operasi aktualnya (span-based, bukan shift 8 jam). Rendah = banyak idle.

## VA / NVA / VA %

| Istilah | Sumber | Arti |
|---------|--------|------|
| **VA** (Value-Add) | `End − Start` | Waktu yang benar-benar memproses unit |
| **NVA** (Non Value-Add) | `Start − Arrival` | Waktu menunggu di antrean |
| **VA %** | `VA ÷ (VA+NVA) × 100` | Porsi waktu bernilai — makin tinggi makin efisien |

## Takt Time & Status

**Takt time** = waktu maksimal per unit agar target produksi tercapai. Target saat ini: **48 detik**.

| Status | Range (avg cycle per station) | % Target | Makna |
|--------|-------------------------------|----------|-------|
| 🟢 NORMAL | ≤ 43.2s | ≤ 90% | Ada buffer, aman |
| 🟡 WARNING | > 43.2s s/d 48s | 90–100% | Tanpa cadangan — gangguan kecil = telat |
| 🔴 OVERLOAD | > 48s | > 100% | Tidak mampu kejar takt — bottleneck |

## Unit: Terdaftar / Dites / Menunggu / Lolos

| Istilah | Definisi | Cara Hitung |
|---------|----------|-------------|
| **Terdaftar** | Unit di work order (`unit_master.csv`) — wajib dites 5 station | Baris unit_master dalam scope filter |
| **Dites (Unik)** | Unit yang sudah punya minimal 1 log | `COUNT DISTINCT (Cell + SN)` dari log |
| **Menunggu** | Terdaftar tapi belum punya log sama sekali | Terdaftar − Dites |
| **Lolos 5/5** | Melewati semua 5 station cell-nya | Distinct station per SN = 5 |
| **Completion Rate** | Lolos 5/5 ÷ Dites × 100 | Persen unit yang tuntas penuh |

## Total Test vs Unit Unik

- **Total Test** = jumlah **kejadian test** (baris log). 1 unit lolos 5 station = 5 total test.
- **Unit Unik** = jumlah **produk fisik berbeda** (distinct SN). 1 unit = 1, berapa pun test-nya.

## Alur Logika Inti

```
1 unit terdaftar → wajib 5 station di cell-nya:
  Assembly → Calibration → Functional Test → Inspection → Packing
  - Lolos semua  → 5 log → "Lolos 5/5" ✅
  - Gagal di tengah → berhenti (2-4 log) → terhitung Dites, bukan Lolos
  - Belum giliran → 0 log → "Menunggu"
```

---

*Dokumen ini dibuat otomatis konsisten dengan kode per 26 Agustus 2026. Bila kode berubah, perbarui bagian terkait.*




