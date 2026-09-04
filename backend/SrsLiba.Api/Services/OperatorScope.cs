// ============================================================
// OperatorScope — Penegakan batas akses operator di sisi backend.
// Operator memakai role "operator" + claim is_operator/assigned_cell.
// Semua filter KPI yang masuk dari operator DIPAKSA ke AssignedCellId,
// sehingga operator tidak bisa melihat cell lain walau memanipulasi
// query string atau memanggil API langsung.
// ============================================================

using System.Security.Claims;
using SrsLiba.Api.Contracts;
using SrsLiba.Api.Services;

namespace SrsLiba.Api.Services;

/// <summary>Helper statis penegakan scope cell operator.</summary>
public static class OperatorScope
{
    /// <summary>
    /// Ambil AssignedCellId dari claim bila pemanggil adalah operator.
    /// Return null untuk admin/user biasa (tanpa pembatasan).
    /// </summary>
    public static long? GetAssignedCell(ClaimsPrincipal user)
    {
        var isOp = user.FindFirst(TokenService.IsOperatorClaim)?.Value;
        if (!string.Equals(isOp, "true", StringComparison.OrdinalIgnoreCase)) return null;
        var cell = user.FindFirst(TokenService.AssignedCellClaim)?.Value;
        return long.TryParse(cell, out var id) ? id : null;
    }

    /// <summary>
    /// Paksa KpiFilter ke cell operator (record baru via with-expression).
    /// Non-operator dikembalikan tanpa perubahan.
    /// </summary>
    public static KpiFilter EnforceCell(KpiFilter filter, ClaimsPrincipal user)
    {
        var cell = GetAssignedCell(user);
        return cell.HasValue ? filter with { CellId = cell.Value } : filter;
    }

    /// <summary>True bila pemanggil adalah operator.</summary>
    public static bool IsOperator(ClaimsPrincipal user) =>
        string.Equals(user.FindFirst(TokenService.IsOperatorClaim)?.Value, "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True bila pemanggil adalah operator yang BELUM mengonfirmasi identitas
    /// (berdasarkan claim token). Endpoint data wajib menolaknya dengan 403 —
    /// gerbang konfirmasi tidak boleh hanya mengandalkan frontend.
    /// </summary>
    public static bool IsUnconfirmedOperator(ClaimsPrincipal user) =>
        IsOperator(user) && !string.Equals(
            user.FindFirst(TokenService.IdentityConfirmedClaim)?.Value,
            "true", StringComparison.OrdinalIgnoreCase);
}
