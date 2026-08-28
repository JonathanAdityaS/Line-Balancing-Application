# Line Balancing Application

Aplikasi **web dashboard** untuk menganalisis keseimbangan line produksi (line balancing). Dibangun dengan **Angular (frontend)** + **.NET Web API (backend)** + **SQLite (database analitik)**.

> Monitoring & analytics read-only — tidak menulis/mengubah data produksi existing.

---

## ✨ Fitur

- **8 Kartu KPI**: Average/Station, Total Test, Unit Dites, Unit Terdaftar (progress), Waiting Time, Utilization, VA %, Takt Status (agregat worst-case)
- **5 Grafik**: Avg cycle per station, Total test trend, Donut VA/NVA, Utilization & Waiting trend, Unit Flow (1–5 test)
- **Unit Flow**: per cell (Terdaftar / Dites / Menunggu / Lolos 5/5 / Completion Rate)
- **Ringkasan Akhir** per station (click = drill-down filter) + baris TOTAL
- **History** terpaginasi (25/halaman)
- **Export** Excel (4 sheet) & PDF (5 section) dengan info filter
- **Auto-refresh** 15/30/60 detik · **Light/Dark theme** · Filter Cell/Station/Meter Type
- **Sinkronisasi MSSQL → SQLite** (read-only) dengan fallback CSV

---

## 🔧 Teknologi

| Layer | Stack |
|-------|-------|
| Backend | .NET 10 Web API, EF Core SQLite, Dapper (MSSQL), ClosedXML (Excel), QuestPDF (PDF) |
| Frontend | Angular 21, Chart.js, SCSS (design tokens) |
| Database | SQLite (analitik) + MSSQL existing (sumber, read-only) |

---

## 📁 Struktur

```
Line-Balancing-Application/
├── backend/SrsLiba.Api/      # .NET Web API
├── frontend/                 # Angular 21
├── SRS_Line_Balancing_Application.md   # Dokumen requirement
├── DOKUMENTASI_FUNGSI.md     # Penjelasan setiap fungsi
├── class-diagram.drawio      # Diagram kelas (buka dgn draw.io)
└── README.md                 # Panduan ini
```

---

## ✅ Persyaratan

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org) (untuk frontend)
- **Opsional** — SQL Server untuk sinkronisasi data existing

---

## 🚀 Cara Menjalankan

### 1. Backend (port `5121`)

```bash
cd backend/SrsLiba.Api
dotnet run
```

- Swagger UI: `http://localhost:5121/swagger`
- Endpoint health: `http://localhost:5121/health`

### 2. Frontend (port `4200`)

```bash
cd frontend
npm install
npm start
```

- Buka: `http://localhost:4200`

> Backend & frontend jalan bersamaan di 2 terminal terpisah.

---

## 🗄️ Database

### Alur (sesuai SRS)

```
MSSQL Existing (read-only) ──sync──→ SQLite (cache analitik) ──query──→ Dashboard
       │ gagal
       ▼
 CSV dummy (fallback, untuk demo/clone)
```

- **Tanpa MSSQL** → otomatis pakai data CSV dummy (aplikasi tetap jalan)
- **Dengan MSSQL** → edit koneksi di `backend/SrsLiba.Api/appsettings.json` (atau env var):

```json
"ExistingDb": "Server=<host>;Database=<nama>;User Id=<user>;Password=<pass>;..."
```

Gunakan akun **read-only** (aplikasi hanya SELECT, tidak pernah menulis ke MSSQL).

### Sinkronisasi manual

| Endpoint | Fungsi |
|----------|--------|
| `POST /api/admin/sync` | Tarik ulang MSSQL → SQLite tanpa restart |
| `GET /api/admin/db-status` | Status koneksi MSSQL (`connected` / `disconnected`) |

---

## 📚 Dokumentasi Terkait

- `SRS_Line_Balancing_Application.md` — daftar kebutuhan lengkap (FR/NFR/AC)
- `DOKUMENTASI_FUNGSI.md` — penjelasan setiap fungsi backend & frontend
- `class-diagram.drawio` — diagram kelas arsitektur backend

---

## 🛠️ Catatan Teknis

- Kredensial DB disimpan di **configuration/environment**, bukan hardcode di kode
- Query KPI dioptimasi: single-pass, index database, caching (dashboard 30s, master 5m)
- Data seed: 5 CSV di `backend/SrsLiba.Api/Data/DummyCsv/`
