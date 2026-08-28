// ============================================================
// AdminController — Endpoint admin (UC-04) untuk kelola sinkronisasi
// database. Operasi bersifat analitik/cache, TIDAK menyentuh MSSQL
// existing kecuali membaca (SELECT).
//   POST /api/admin/sync       → tarik ulang MSSQL → SQLite
//   GET  /api/admin/db-status  → status koneksi MSSQL existing
// ============================================================

using Microsoft.AspNetCore.Mvc;
using SrsLiba.Api.Services;

namespace SrsLiba.Api.Controllers;

/// <summary>Controller admin: sinkronisasi data & status koneksi database.</summary>
[ApiController]
[Route("api/admin")]
public sealed class AdminController : ControllerBase
{
    private readonly ISyncService _sync;

    public AdminController(ISyncService sync)
    {
        _sync = sync;
    }

    /// <summary>
    /// Tarik ulang data dari MSSQL existing ke SQLite tanpa restart.
    /// Bila MSSQL tidak nyambung → respons "fallback" (aplikasi tetap
    /// memakai data SQLite/CSV yang sudah ada).
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> Sync(CancellationToken ct)
    {
        var ok = await _sync.TrySyncFromMssqlAsync(ct);
        return ok
            ? Ok(new { status = "ok", message = "Sync selesai dari MSSQL ke SQLite." })
            : Ok(new { status = "fallback", message = "MSSQL tidak tersambung — data dari sumber cadangan (SQLite/CSV) tetap dipakai." });
    }

    /// <summary>Return status koneksi ke MSSQL existing: "connected" / "disconnected".</summary>
    [HttpGet("db-status")]
    public async Task<IActionResult> DbStatus(CancellationToken ct)
    {
        var ok = await _sync.TestConnectionAsync(ct);
        return Ok(new { status = ok ? "connected" : "disconnected" });
    }
}
