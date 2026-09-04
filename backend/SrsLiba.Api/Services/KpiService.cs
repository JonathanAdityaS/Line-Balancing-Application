// ============================================================
// KpiService — Orkestrator logika bisnis KPI (antara Controller dan Repository).
// Tugas:
//   1. Dashboard: ambil logs → hitung semua metrik via MetricCalculator → cache 30 detik
//   2. History: ambil logs terpaginasi → map ke DTO siap tampil
//   3. Heatmap Takt: return data takt comparison per cell/station
//   4. Target Takt Config: get/set konfigurasi target takt per cell/station
// ============================================================

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SrsLiba.Api.Contracts;
using SrsLiba.Api.Repositories;

namespace SrsLiba.Api.Services;

/// <summary>
/// Ringkasan KPI utama untuk dashboard Angular (kartu + chart).
/// </summary>
public sealed record KpiSummary(
    decimal AveragePerStation,
    int TotalTest,
    int TotalUniqueUnits,
    int TotalRegisteredUnits,
    int UntestedUnits,
    double OverallWaitingAvgSeconds,
    double OverallUtilizationPercent,
    double OverallVaPercent);

/// <summary>
/// Ringkasan KPI per Cell (setara tabel "summary_per_cell" di notebook):
/// total test, rata-rata durasi, rata-rata waiting, utilization rata-rata, total VA/NVA.
/// </summary>
public sealed record CellSummaryDto(string CellName, int TotalTest, double AvgDurationSeconds, double AvgWaitingTimeSeconds, double UtilizationPercent, double TotalVaSeconds, double TotalNvaSeconds);

/// <summary>
/// Response lengkap endpoint dashboard — berisi semua KPI untuk kartu + chart,
/// termasuk analisis Unit Flow (kelulusan unit antar station).
/// </summary>
public sealed record KpiDashboardResult(
    KpiSummary Summary,
    IReadOnlyList<StationAverageDto> AveragePerStation,
    IReadOnlyList<WaitingTimeDto> WaitingTime,
    IReadOnlyList<UtilizationDto> Utilization,
    IReadOnlyList<VaNvaDto> VaNva,
    IReadOnlyList<TaktComparisonDto> TaktComparison,
    IReadOnlyList<CellSummaryDto> CellSummary,
    IReadOnlyList<UnitFlowDto> UnitFlow,
    IReadOnlyList<UnitFlowDetailDto> UnitFlowDetail,
    IReadOnlyList<StationSummaryDto> StationSummary);

/// <summary>Kontrak service KPI.</summary>
public interface IKpiService
{
    Task<KpiDashboardResult> GetDashboardAsync(KpiFilter filter, CancellationToken ct = default);
    Task<PagedResult<TaktLogDetailDto>> GetHistoricalAsync(KpiFilter filter, int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<TaktComparisonDto>> GetTaktHeatmapAsync(KpiFilter filter, CancellationToken ct = default);
    SrsLiba.Api.Services.TaktTargetConfig GetTaktTargetsAsync(CancellationToken ct = default);
    Task UpdateTaktTargetsAsync(SrsLiba.Api.Services.TaktTargetConfig config, CancellationToken ct = default);
}

/// <summary>Implementasi KPI dengan caching hasil dashboard (hemat query berulang).</summary>
public sealed class KpiService : IKpiService
{
    private readonly IProductionRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly KpiOptions _options;

    // Hasil dashboard di-cache singkat (30 detik) agar refresh berulang
    // tidak memukul database terus-menerus, tapi data tetap relatif segar
    private static readonly TimeSpan DashboardCacheDuration = TimeSpan.FromSeconds(30);

    public KpiService(IProductionRepository repository, IMemoryCache cache, IOptions<KpiOptions> options)
    {
        _repository = repository;
        _cache = cache;
        _options = options.Value;
    }

    /// <summary>
    /// Ambil semua KPI dashboard. Hasil di-cache per kombinasi filter
    /// (cache key memuat CellId/StationId/MeterTypeId/DateFrom/DateTo) selama 30 detik.
    /// </summary>
    public async Task<KpiDashboardResult> GetDashboardAsync(KpiFilter filter, CancellationToken ct = default)
    {
        // Cache key unik per kombinasi filter agar hasil tidak tercampur
        var cacheKey = $"kpi:dashboard:{filter.CellId}:{filter.StationId}:{filter.MeterTypeId}:{filter.DateFrom:yyyy-MM-dd}:{filter.DateTo:yyyy-MM-dd}";
        return (await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = DashboardCacheDuration;
            return await ComputeDashboardAsync(filter, ct);
        }))!;
    }

    /// <summary>Ambil heatmap Takt per cell/station.</summary>
    public async Task<IReadOnlyList<TaktComparisonDto>> GetTaktHeatmapAsync(KpiFilter filter, CancellationToken ct = default)
    {
        var rows = await _repository.GetLogsAsync(filter, ct);
        return MetricCalculator.ComputeTaktComparison(rows, _options.TargetTaktSeconds, _options.TaktTargets?.PerStation, _options.TaktTargets?.PerCell);
    }

    /// <summary>Ambil konfigurasi target takt per cell/station.</summary>
    public SrsLiba.Api.Services.TaktTargetConfig GetTaktTargetsAsync(CancellationToken ct = default)
    {
        return _options.TaktTargets;
    }

    /// <summary>Update konfigurasi target takt per cell/station.</summary>
    public async Task UpdateTaktTargetsAsync(SrsLiba.Api.Services.TaktTargetConfig config, CancellationToken ct = default)
    {
        _options.TaktTargets = config;
        // Cache key untuk dashboard akan invalidate otomatis saat GET dashboard berikutnya
    }

    /// <summary>Ambil history log terpaginasi, sudah dimap ke DTO siap tampil.</summary>
    public async Task<PagedResult<TaktLogDetailDto>> GetHistoricalAsync(KpiFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        var rows = await _repository.GetLogsPagedAsync(filter, page, pageSize, ct);

        // Map baris log mentah → DTO detail (ID diterjemahkan jadi nama)
        var items = rows.Items.Select(x => new TaktLogDetailDto(
            x.Id,
            x.StationName,
            x.CellName,
            x.MeterTypeName,
            x.SerialNumber,
            x.ArrivalTime,
            x.StartTime,
            x.EndTime,
            (x.EndTime - x.StartTime).TotalSeconds)).ToList();

        return new PagedResult<TaktLogDetailDto>(items, rows.Page, rows.PageSize, rows.TotalCount);
    }

    /// <summary>
    /// Perhitungan dashboard sesungguhnya (dipanggil hanya saat cache miss).
    /// Dua query: log test + unit terdaftar (work order) — keduanya ikut cache.
    /// Semua rumus KPI dihitung dari data yang sama (single-pass).
    /// </summary>
    private async Task<KpiDashboardResult> ComputeDashboardAsync(KpiFilter filter, CancellationToken ct)
    {
        var rows = await _repository.GetLogsAsync(filter, ct);
        var registered = await _repository.GetRegisteredUnitsAsync(filter, ct);

        // Semua rumus KPI ada di MetricCalculator
        var averages = MetricCalculator.ComputeStationAverages(rows);
        var waiting = MetricCalculator.ComputeWaitingTimes(rows);
        var utilization = MetricCalculator.ComputeUtilization(rows);
        var vaNva = MetricCalculator.ComputeVaNva(rows);
        var takt = MetricCalculator.ComputeTaktComparison(rows, _options.TargetTaktSeconds, _options.TaktTargets?.PerStation, _options.TaktTargets?.PerCell);
        var cellSummary = MetricCalculator.ComputeCellSummary(rows, waiting, utilization);
        var unitFlow = MetricCalculator.ComputeUnitFlow(rows, registered);
        var unitFlowDetail = MetricCalculator.ComputeUnitFlowDetail(rows);
        var summary = MetricCalculator.ComputeSummary(rows, averages, utilization, vaNva, registered);
        var stationSummary = MetricCalculator.ComputeStationSummary(averages, waiting, utilization, vaNva, takt);

        return new KpiDashboardResult(summary, averages, waiting, utilization, vaNva, takt, cellSummary, unitFlow, unitFlowDetail, stationSummary);
    }
}