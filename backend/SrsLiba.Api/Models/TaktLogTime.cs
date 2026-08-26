namespace SrsLiba.Api.Models;

/// <summary>
/// Entitas log utama produksi — satu row = satu unit selesai diproses di satu station.
/// Tabel database: "TaktLogTime". Ini tabel fakta yang menjadi sumber semua perhitungan KPI.
/// Data dummy: 405 row — lihat Data/DummyCsv/takt_log_time.csv.
/// </summary>
public sealed class TaktLogTime
{
    /// <summary>Primary key tabel log.</summary>
    public long Id { get; set; }

    /// <summary>Foreign key ke Station — station tempat unit ini diproses.</summary>
    public long StationId { get; set; }

    /// <summary>Foreign key ke MeterType — jenis meter/unit yang diuji.</summary>
    public long MeterTypeId { get; set; }

    /// <summary>Nomor seri unit/produk yang diproses (contoh: "SN-0001").</summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>
    /// Waktu unit TIBA di station (masuk antrean) sebelum diproses.
    /// Dasar perhitungan waiting time fisik: Waiting = StartTime - ArrivalTime (SRS FR-03).
    /// </summary>
    public DateTime ArrivalTime { get; set; }

    /// <summary>Waktu mulai proses/pengujian unit di station.</summary>
    public DateTime StartTime { get; set; }

    /// <summary>Waktu selesai proses/pengujian unit di station.</summary>
    public DateTime EndTime { get; set; }

    /// <summary>Navigation property: Station tempat log ini tercatat.</summary>
    public Station? Station { get; set; }

    /// <summary>Navigation property: MeterType dari unit yang diuji.</summary>
    public MeterType? MeterType { get; set; }

    /// <summary>
    /// Durasi proses dalam detik (computed, tidak disimpan di database).
    /// Rumus: EndTime - StartTime. Ini adalah "cycle time" / Value-Add time.
    /// </summary>
    public double DurationSeconds => (EndTime - StartTime).TotalSeconds;
}
