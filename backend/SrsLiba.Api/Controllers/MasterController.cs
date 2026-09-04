// ============================================================
// MasterController — Endpoint data master untuk dropdown filter UI.
//   GET /api/master/cells         → daftar cell
//   GET /api/master/stations      → daftar station (opsional ?cellId=)
//   GET /api/master/meter-types   → daftar tipe meter
//   GET /api/master/shifts        → daftar shift (hardcoded, data dummy)
// Semua lookup melewati MasterDataService (cache 5 menit).
// ============================================================

using Microsoft.AspNetCore.Mvc;
using SrsLiba.Api.Repositories;
using SrsLiba.Api.Services;

namespace SrsLiba.Api.Controllers;

/// <summary>Controller data master (sumber dropdown filter frontend).</summary>
[ApiController]
[Route("api/master")]
public sealed class MasterController : ControllerBase
{
    private readonly IMasterDataService _masterData;

    public MasterController(IMasterDataService masterData)
    {
        _masterData = masterData;
    }

    /// <summary>
    /// Daftar Cell. Operator hanya menerima cell yang ditugaskan kepadanya
    /// (difilter di controller agar tidak meracuni cache global service).
    /// </summary>
    [HttpGet("cells")]
    public async Task<ActionResult<IReadOnlyList<MasterLookupDto>>> GetCells(CancellationToken ct)
    {
        if (OperatorScope.IsUnconfirmedOperator(User))
            return Problem(statusCode: 403, title: "Identity not confirmed", detail: "Operator wajib mengonfirmasi identitas terlebih dahulu.");
        var result = await _masterData.GetCellsAsync(ct);
        var assigned = OperatorScope.GetAssignedCell(User);
        if (assigned.HasValue)
            result = result.Where(x => x.Id == assigned.Value).ToList();
        return Ok(result);
    }

    /// <summary>
    /// Daftar Station — bila query ?cellId= diberikan, hanya station
    /// milik cell tersebut (dropdown bertingkat: pilih cell → station muncul).
    /// Operator dipaksa ke cell-nya (parameter cellId diabaikan).
    /// </summary>
    [HttpGet("stations")]
    public async Task<ActionResult<IReadOnlyList<StationLookupDto>>> GetStations([FromQuery] long? cellId, CancellationToken ct)
    {
        if (OperatorScope.IsUnconfirmedOperator(User))
            return Problem(statusCode: 403, title: "Identity not confirmed", detail: "Operator wajib mengonfirmasi identitas terlebih dahulu.");
        var assigned = OperatorScope.GetAssignedCell(User);
        if (assigned.HasValue) cellId = assigned.Value;
        var result = await _masterData.GetStationsAsync(cellId, ct);
        return Ok(result);
    }

    /// <summary>Daftar semua MeterType (MT-A, MT-B, MT-C).</summary>
    [HttpGet("meter-types")]
    public async Task<ActionResult<IReadOnlyList<MasterLookupDto>>> GetMeterTypes(CancellationToken ct)
    {
        var result = await _masterData.GetMeterTypesAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// Daftar shift — masih hardcoded dummy karena data CSV belum punya master shift.
    /// TODO: pindahkan ke tabel/database bila shift jadi data nyata.
    /// </summary>
    [HttpGet("shifts")]
    public ActionResult<IReadOnlyList<MasterLookupDto>> GetShifts()
    {
        IReadOnlyList<MasterLookupDto> result = new[]
        {
            new MasterLookupDto(1, "Shift-1"),
            new MasterLookupDto(2, "Shift-2"),
            new MasterLookupDto(3, "Shift-3")
        };

        return Ok(result);
    }
}
