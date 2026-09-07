namespace SrsLiba.Api.Contracts;

/// <summary>
/// Filter query untuk semua endpoint KPI (dashboard, history, export).
/// Semua filter bersifat opsional (null = tanpa filter / tampilkan semua).
/// Menggunakan ID numeric (bukan nama string) agar query JOIN langsung
/// membandingkan Primary Key — lebih cepat dan bebas salah ketik nama.
/// </summary>
/// <param name="CellId">Filter berdasarkan ID Cell (lihat tabel Cell). Null = semua cell.</param>
/// <param name="StationId">Filter berdasarkan ID Station (lihat tabel Station). Null = semua station.</param>
/// <param name="MeterTypeId">Filter berdasarkan ID MeterType (lihat tabel MeterType). Null = semua tipe meter.</param>
/// <param name="DateFrom">Batas awal rentang tanggal (inklusif, berdasarkan StartTime). Null = tanpa batas awal.</param>
/// <param name="DateTo">Batas akhir rentang tanggal (inklusif, berdasarkan StartTime). Null = tanpa batas akhir.</param>
/// <param name="SerialNumber">Cari serial number mengandung teks (case-insensitive). Null/kosong = semua unit.</param>
public sealed record KpiFilter(long? CellId, long? StationId, long? MeterTypeId, DateTime? DateFrom = null, DateTime? DateTo = null, string? SerialNumber = null);
