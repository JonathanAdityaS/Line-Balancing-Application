// ============================================================
// ProductionRepository — Satu-satunya layer yang menulis query ke database.
// Prinsip: logika query TERPISAH dari logika bisnis (NFR-07 Maintainability).
// Semua query KPI memakai JOIN + projection minimal kolom (hemat memory),
// filter memakai ID numeric (bandingkan PK langsung, cepat).
// ============================================================

using Microsoft.EntityFrameworkCore;
using SrsLiba.Api.Contracts;
using SrsLiba.Api.Data;

namespace SrsLiba.Api.Repositories;

// --- DTO lookup master (untuk dropdown filter di frontend) ---

/// <summary>DTO generik lookup: ID + nama (untuk Cell dan MeterType).</summary>
public sealed record MasterLookupDto(long Id, string Name);

/// <summary>DTO lookup station: ID, nama, plus cell induknya (untuk dropdown bertingkat).</summary>
public sealed record StationLookupDto(long Id, string Name, long CellId, string CellName);

/// <summary>
/// DTO baris log hasil projection JOIN 4 tabel (log+station+cell+metertype).
/// Kolom dibatasi yang benar-benar dipakai KPI agar transfer data hemat.
/// </summary>
public sealed record TaktLogRowDto(
    long Id,
    long StationId,
    string StationName,
    string CellName,
    long MeterTypeId,
    string MeterTypeName,
    string SerialNumber,
    DateTime ArrivalTime,
    DateTime StartTime,
    DateTime EndTime);

/// <summary>
/// DTO unit terdaftar dari tabel UnitMaster (work order) —
/// dasar perhitungan registered / tested / untested.
/// </summary>
public sealed record UnitMasterRowDto(string SerialNumber, long CellId, string CellName, long MeterTypeId, string MeterTypeName);

public interface IProductionRepository
{
    Task<IReadOnlyList<MasterLookupDto>> GetCellsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<StationLookupDto>> GetStationsAsync(long? cellId = null, CancellationToken ct = default);
    Task<IReadOnlyList<MasterLookupDto>> GetMeterTypesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UnitMasterRowDto>> GetRegisteredUnitsAsync(KpiFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<TaktLogRowDto>> GetLogsAsync(KpiFilter filter, CancellationToken ct = default);
    Task<PagedResult<TaktLogRowDto>> GetLogsPagedAsync(KpiFilter filter, int page, int pageSize, CancellationToken ct = default);
}

/// <summary>Implementasi query EF Core ke SQLite untuk semua kebutuhan KPI & master data.</summary>
public sealed class ProductionRepository : IProductionRepository
{
    private readonly AppDbContext _db;

    public ProductionRepository(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Ambil semua Cell (untuk dropdown "All cells").</summary>
    public async Task<IReadOnlyList<MasterLookupDto>> GetCellsAsync(CancellationToken ct = default)
    {
        return await _db.Cells
            .OrderBy(x => x.Id)
            .Select(x => new MasterLookupDto(x.Id, x.CellName))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Ambil daftar Station, opsional difilter per Cell
    /// (dipakai dropdown station bertingkat: pilih cell dulu → station-nya muncul).
    /// </summary>
    public async Task<IReadOnlyList<StationLookupDto>> GetStationsAsync(long? cellId = null, CancellationToken ct = default)
    {
        var query = _db.Stations.AsQueryable();
        if (cellId.HasValue) query = query.Where(x => x.CellId == cellId.Value);

        return await query
            .OrderBy(x => x.Id)
            .Select(x => new StationLookupDto(x.Id, x.StationName, x.CellId, x.Cell!.CellName))
            .ToListAsync(ct);
    }

    /// <summary>Ambil semua MeterType (untuk dropdown "All meter types").</summary>
    public async Task<IReadOnlyList<MasterLookupDto>> GetMeterTypesAsync(CancellationToken ct = default)
    {
        return await _db.MeterTypes
            .OrderBy(x => x.Id)
            .Select(x => new MasterLookupDto(x.Id, x.Name))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Membentuk query dasar log dengan JOIN 4 tabel + filter + projection.
    /// Satu definisi dipakai bersama oleh GetLogsAsync dan GetLogsPagedAsync
    /// agar rumus filter tidak duplikat.
    /// Filter null = tanpa filter (tampilkan semua).
    /// </summary>
    private IQueryable<TaktLogRowDto> ProjectLogs(KpiFilter filter)
    {
        return from log in _db.TaktLogTimes
               join station in _db.Stations on log.StationId equals station.Id
               join cell in _db.Cells on station.CellId equals cell.Id
               join meter in _db.MeterTypes on log.MeterTypeId equals meter.Id
               where (filter.CellId == null || cell.Id == filter.CellId)
                  && (filter.StationId == null || log.StationId == filter.StationId)
                  && (filter.MeterTypeId == null || log.MeterTypeId == filter.MeterTypeId)
               orderby log.StartTime
               select new TaktLogRowDto(
                   log.Id,
                   log.StationId,
                   station.StationName,
                   cell.CellName,
                   log.MeterTypeId,
                   meter.Name,
                   log.SerialNumber,
                   log.ArrivalTime,
                   log.StartTime,
                   log.EndTime);
    }

    /// <summary>Ambil SEMUA log sesuai filter (dipakai perhitungan dashboard KPI).</summary>
    public async Task<IReadOnlyList<TaktLogRowDto>> GetLogsAsync(KpiFilter filter, CancellationToken ct = default)
    {
        return await ProjectLogs(filter).ToListAsync(ct);
    }

    /// <summary>
    /// Ambil unit TERDAFTAR (dari UnitMaster) sesuai filter —
    /// daftar unit yang direncanakan dites, terlepas sudah/belum ada log-nya.
    /// Dipakai untuk metrik registered / tested / untested.
    /// </summary>
    public async Task<IReadOnlyList<UnitMasterRowDto>> GetRegisteredUnitsAsync(KpiFilter filter, CancellationToken ct = default)
    {
        var query =
            from unit in _db.UnitMasters
            join cell in _db.Cells on unit.CellId equals cell.Id
            join meter in _db.MeterTypes on unit.MeterTypeId equals meter.Id
            where (filter.CellId == null || cell.Id == filter.CellId)
               && (filter.MeterTypeId == null || meter.Id == filter.MeterTypeId)
            orderby unit.SerialNumber
            select new UnitMasterRowDto(
                unit.SerialNumber,
                unit.CellId,
                cell.CellName,
                unit.MeterTypeId,
                meter.Name);

        return await query.ToListAsync(ct);
    }

    /// <summary>
    /// Ambil log TERPAGINASI (dipakai endpoint history).
    /// LongCountAsync menghitung total data (untuk info pagination),
    /// lalu Skip/Take mengambil hanya halaman yang diminta — hemat memory.
    /// </summary>
    public async Task<PagedResult<TaktLogRowDto>> GetLogsPagedAsync(KpiFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        var query = ProjectLogs(filter);
        var totalCount = await query.LongCountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize) // Lewati halaman-halaman sebelumnya
            .Take(pageSize)              // Ambil sebatas ukuran halaman
            .ToListAsync(ct);

        return new PagedResult<TaktLogRowDto>(items, page, pageSize, (int)totalCount);
    }
}
