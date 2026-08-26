namespace SrsLiba.Api.Contracts;

/// <summary>
/// Wrapper generik untuk response yang dipaginasi (dipotong per halaman).
/// Dipakai endpoint GET /api/kpi/history agar tidak semua log dimuat
/// sekaligus ke memory (mencegah boros RAM pada data besar).
/// </summary>
/// <typeparam name="T">Tipe item dalam satu halaman (misal TaktLogDetailDto).</typeparam>
/// <param name="Items">Daftar item pada halaman ini.</param>
/// <param name="Page">Nomor halaman saat ini (mulai dari 1).</param>
/// <param name="PageSize">Jumlah item maksimal per halaman.</param>
/// <param name="TotalCount">Total seluruh item yang cocok dengan filter (semua halaman).</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    /// <summary>Total jumlah halaman yang tersedia (dihitung dari TotalCount / PageSize).</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
}
