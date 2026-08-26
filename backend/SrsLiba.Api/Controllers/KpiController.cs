// ============================================================
// KpiController — Endpoint KPI utama untuk dashboard Angular.
//   GET /api/kpi/dashboard              → semua KPI (kartu + chart)
//   GET /api/kpi/history?page=&pageSize= → data log terpaginasi (tabel)
// Termasuk validasi input pagination (400 bila tidak valid).
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

    // Batas aman ukuran halaman — cegah client meminta data raksasa sekaligus
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
        var result = await _kpiService.GetDashboardAsync(filter, ct);
        return Ok(result);
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

        // Validasi ukuran halaman (1..100) agar server tidak dibebani request raksasa
        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            return Problem(statusCode: 400, title: "Invalid pageSize", detail: $"pageSize harus antara 1 dan {MaxPageSize}.");
        }

        var result = await _kpiService.GetHistoricalAsync(filter, page, pageSize, ct);
        return Ok(result);
    }
}
