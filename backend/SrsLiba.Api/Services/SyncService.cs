// ============================================================
// SyncService — Menarik data dari database MSSQL existing (read-only)
// dan menyimpannya ke SQLite lokal (AppDb) sebagai cache analitik.
//
// Alur (sesuai SRS 2.5): Database Existing → .NET → SQLite cache → Dashboard.
// - Hanya operasi READ ke MSSQL (SELECT), tidak pernah INSERT/UPDATE/DELETE.
// - Kredensial dari connection string (appsettings/environment), bukan hardcode.
// - Gagal koneksi → return false → caller fallback ke CSV seeder.
// ============================================================

using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SrsLiba.Api.Data;
using SrsLiba.Api.Models;

namespace SrsLiba.Api.Services;

/// <summary>Kontrak sinkronisasi MSSQL existing → SQLite lokal.</summary>
public interface ISyncService
{
    /// <summary>
    /// Baca 5 tabel dari MSSQL (SELECT saja) lalu upsert ke SQLite.
    /// Return true bila berhasil; false bila koneksi/query gagal (untuk fallback CSV).
    /// </summary>
    Task<bool> TrySyncFromMssqlAsync(CancellationToken ct = default);

    /// <summary>Uji koneksi ke MSSQL existing (tanpa query data). Return true bila nyambung.</summary>
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
}

/// <summary>Implementasi sinkronisasi MSSQL → SQLite dengan Dapper (read-only).</summary>
public sealed class SyncService : ISyncService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<SyncService> _logger;

    public SyncService(AppDbContext db, IConfiguration config, ILogger<SyncService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    /// <summary>Buka koneksi secure ke MSSQL existing.</summary>
    private SqlConnection CreateConnection()
    {
        // Credential ada di connection string (appsettings / env var) — bukan hardcode di kode
        return new SqlConnection(_config.GetConnectionString("ExistingDb"));
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Uji koneksi MSSQL existing gagal: {Message}", ex.Message);
            return false;
        }
    }

    public async Task<bool> TrySyncFromMssqlAsync(CancellationToken ct = default)
    {
        // Mapping nama tabel aktual di MSSQL (bisa disesuaikan tanpa rebuild kode)
        var map = _config.GetSection("TableMapping").Get<Dictionary<string, string>>()
                  ?? new Dictionary<string, string>();
        var cellTable = map.GetValueOrDefault("Cell", "Cell");
        var stationTable = map.GetValueOrDefault("Station", "Station");
        var meterTable = map.GetValueOrDefault("MeterType", "MeterType");
        var unitTable = map.GetValueOrDefault("UnitMaster", "UnitMaster");
        var logTable = map.GetValueOrDefault("TaktLogTime", "TaktLogTime");

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct);

            // --- Baca data dari MSSQL (hanya SELECT, read-only) ---
            // Dapper memetakan kolom ke property entity berdasarkan nama kolom.
            // Asumsi skema: SAMA 5 tabel dengan skema SQLite (1:1 mapping).
            var cells = await conn.QueryAsync<Cell>($"SELECT * FROM [{cellTable}]");
            var stations = await conn.QueryAsync<Station>($"SELECT * FROM [{stationTable}]");
            var meters = await conn.QueryAsync<MeterType>($"SELECT * FROM [{meterTable}]");
            var units = await conn.QueryAsync<UnitMaster>($"SELECT * FROM [{unitTable}]");
            var logs = await conn.QueryAsync<TaktLogTime>($"SELECT * FROM [{logTable}]");

            // --- Salin ke SQLite dalam satu transaksi agar konsisten ---
            await _db.Database.EnsureCreatedAsync(ct);
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            // Hapus semua row dulu (kebalikan urutan FK), lalu insert ulang dari MSSQL
            _db.TaktLogTimes.ExecuteDelete();
            _db.UnitMasters.ExecuteDelete();
            _db.Stations.ExecuteDelete();
            _db.MeterTypes.ExecuteDelete();
            _db.Cells.ExecuteDelete();

            // Insert master dulu (parent), baru log (child)
            _db.Cells.AddRange(cells);
            _db.Stations.AddRange(stations);
            _db.MeterTypes.AddRange(meters);
            _db.UnitMasters.AddRange(units);
            _db.TaktLogTimes.AddRange(logs);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Sync MSSQL → SQLite berhasil: {C} cell, {S} station, {M} meter, {U} unit, {L} log.",
                cells.Count(), stations.Count(), meters.Count(), units.Count(), logs.Count());

            return true;
        }
        catch (Exception ex)
        {
            // Gagal koneksi/query → biarkan caller memutuskan fallback (CSV seeder)
            _logger.LogWarning("Sync MSSQL existing gagal, siap fallback ke CSV: {Message}", ex.Message);
            return false;
        }
    }
}
