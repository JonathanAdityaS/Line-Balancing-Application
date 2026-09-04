// ============================================================
// ExportController — Endpoint download laporan (FR-08).
//   GET /api/export/excel → file .xlsx (application/vnd...spreadsheetml.sheet)
//   GET /api/export/pdf   → file .pdf  (application/pdf)
// Alur: hitung dashboard (dengan filter yang sama seperti endpoint KPI),
// lalu bentuk file-nya via ExportService dan kirim sebagai download.
// ============================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SrsLiba.Api.Contracts;
using SrsLiba.Api.Services;

namespace SrsLiba.Api.Controllers;

/// <summary>Controller export laporan Excel & PDF.</summary>
[Authorize]
[ApiController]
[Route("api/export")]
public sealed class ExportController : ControllerBase
{
    private readonly IKpiService _kpiService;
    private readonly IExportService _exportService;

    public ExportController(IKpiService kpiService, IExportService exportService)
    {
        _kpiService = kpiService;
        _exportService = exportService;
    }

    /// <summary>
    /// Export dashboard ke Excel multi-sheet (Ringkasan, Ringkasan Station,
    /// Unit Flow, Unit Flow Detail). Filter sama dengan endpoint dashboard
    /// (?cellId=&stationId=&meterTypeId=). Hanya dapat diakses oleh pengguna dengan role "admin".
    /// Mengembalikan file untuk diunduh.
    /// </summary>
    [HttpGet("excel")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ExportExcel([FromQuery] KpiFilter filter, CancellationToken ct)
    {
        filter = OperatorScope.EnforceCell(filter, User);
        if (filter.DateFrom.HasValue && filter.DateTo.HasValue
            && filter.DateFrom.Value.Date > filter.DateTo.Value.Date)
        {
            return Problem(statusCode: 400, title: "Invalid date range", detail: "DateFrom tidak boleh lebih besar dari DateTo.");
        }
        // Hitung KPI dulu (tercache bila baru saja dimuat), lalu bentuk file
        var dashboard = await _kpiService.GetDashboardAsync(filter, ct);
        var bytes = _exportService.ExportExcel(dashboard, filter);

        // Content-Type resmi file .xlsx + nama file default saat diunduh
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "line-balancing-dashboard.xlsx");
    }

    /// <summary>
    /// Export dashboard ke PDF multi-section (KPI, Ringkasan Station,
    /// Cell Summary, Unit Flow). Filter sama dengan endpoint dashboard.
    /// Hanya dapat diakses oleh pengguna dengan role "admin".
    /// Mengembalikan file PDF untuk diunduh.
    /// </summary>
    [HttpGet("pdf")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ExportPdf([FromQuery] KpiFilter filter, CancellationToken ct)
    {
        filter = OperatorScope.EnforceCell(filter, User);
        if (filter.DateFrom.HasValue && filter.DateTo.HasValue
            && filter.DateFrom.Value.Date > filter.DateTo.Value.Date)
        {
            return Problem(statusCode: 400, title: "Invalid date range", detail: "DateFrom tidak boleh lebih besar dari DateTo.");
        }
        var dashboard = await _kpiService.GetDashboardAsync(filter, ct);
        var bytes = _exportService.ExportPdf(dashboard, filter);

        return File(bytes, "application/pdf", "line-balancing-dashboard.pdf");
    }
}
