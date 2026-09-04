// ============================================================
// KpiController — Endpoint KPI utama untuk dashboard Angular.
//   GET /api/kpi/dashboard              → semua KPI (kartu + chart)
//   GET /api/kpi/history?page=&pageSize= → data log terpaginasi (tabel)
//   GET /api/kpi/heatmap/takt          → heatmap Takt per cell/station
//   GET /api/kpi/takt-targets           → konfigurasi target takt
//   POST /api/kpi/takt-targets          → update target takt config
// Termasuk validasi input pagination (400 bila tidak valid.
// ============================================================

using Microsoft.AspNetCore.Mvc;
using SrsLiba.Api.Contracts;
using SrsLiba.Api.Repositories;
using SrsLiba.Api.Services;

namespace SrsLiba.Api.Controllers;

/// <summary>Controller endpoint KPI dashboard & history.</summary>
[ApiController]
[Route("api/kpi")]
public sealed class KpiController : ControllerBase
{
    private readonly IKpiService _kpiService;

    // Batas aman ukuran halaman — cegah client meminta data raksa sekaligus
    private const int MaxPageSize = 100;

    public KpiController(IKpiService kpiService)
    {
        _kpiService = kpiService;
    }

    /// <summary>
    /// Semua KPI dashboard sekaligus (summary, average, waiting, utilization,
    /// VA/NVA, takt comparison, ringkasan per cell). Filter opsional via query:
    /// ?cellId=1&stationId=3&meterTypeId=2. Hasil di-cache 30 detik di service.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<KpiDashboardResult>> GetDashboard([FromQuery] KpiFilter filter, CancellationToken ct)
    {
        var invalid = ValidateDateRange(filter);
        if (invalid is not null) return invalid;
        var result = await _kpiService.GetDashboardAsync(filter, ct);
        return Ok(result);
    }

    /// <summary>
    /// Heatmap Takt per cell/station — data untuk visualisasi chart heatmap.
    /// Filter opsional: ?cellId=&stationId=.
    /// Setiap cell/station memiliki warna berdasarkan status takt:
    /// - hijau = normal (<=90% target)
    /// - kuning = warning (90-100% target)
    /// - merah = overload (>100% target)
    /// </summary>
    [HttpGet("heatmap/takt")]
    public async Task<ActionResult<IReadOnlyList<TaktComparisonDto>>> GetTaktHeatmap([FromQuery] KpiFilter filter, CancellationToken ct)
    {
        var invalid = ValidateDateRange(filter);
        if (invalid is not null) return invalid;
        var takt = await _kpiService.GetTaktHeatmapAsync(filter, ct);
        return Ok(takt);
    }

    /// <summary>
    /// Ambil konfigurasi target takt per cell/station.
    /// </summary>
    [HttpGet("takt-targets")]
    public async Task<ActionResult<SrsLiba.Api.Services.TaktTargetConfig>> GetTaktTargets(CancellationToken ct)
    {
        return Ok(_kpiService.GetTaktTargetsAsync(ct));
    }

    /// <summary>
    /// Update konfigurasi target takt per cell/station.
    /// Body: { "PerCell": {"Cell-A": 45}, "PerStation": {"Cell-A|Station-01": 45} }
    /// </summary>
    [HttpPost("takt-targets")]
    public async Task<IActionResult> UpdateTaktTargets([FromBody] SrsLiba.Api.Services.TaktTargetConfig config, CancellationToken ct)
    {
        await _kpiService.UpdateTaktTargetsAsync(config, ct);
        return Ok(new { status = "updated", message = "Target takt configuration updated." });
    }

    /// <summary>
    /// History log terpaginasi. Query: ?page=1&pageSize=25 plus filter opsional.
    /// Validasi: page >= 1 dan 1 <= pageSize <= 100 — bila dilanggar → 400.
    /// </summary>
    [HttpGet("history")]
    public async Task<ActionResult<PagedResult<TaktLogDetailDto>>> GetHistory(
        [FromQuery] KpiFilter filter,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        // Validasi nomor halaman (harus mulai dari 1)
        if (page < 1)
        {
            return Problem(statusCode: 400, title: "Invalid page", detail: "page harus lebih besar atau sama dengan 1.");
        }

        // Validasi ukuran halaman (1..100) agar server tidak dibebani request raksa
        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            return Problem(statusCode: 400, title: "Invalid pageSize", detail: $"pageSize harus antara 1 dan {MaxPageSize}.");
        }

        var invalidRange = ValidateDateRange(filter);
        if (invalidRange is not null) return invalidRange;

        var result = await _kpiService.GetHistoricalAsync(filter, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// Validasi rentang tanggal: bila DateFrom &gt; DateTo → 400.
    /// Perbandingan memakai tanggal saja (abaikan komponen jam).
    /// </summary>
    private ActionResult? ValidateDateRange(KpiFilter filter)
    {
        if (filter.DateFrom.HasValue && filter.DateTo.HasValue
            && filter.DateFrom.Value.Date > filter.DateTo.Value.Date)
        {
            return Problem(statusCode: 400, title: "Invalid date range", detail: "DateFrom tidak boleh lebih besar dari DateTo.");
        }
        return null;
    }
}