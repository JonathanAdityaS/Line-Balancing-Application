// ============================================================
// MasterDataService — Wrapper master lookup (Cell/Station/MeterType)
// dengan caching IMemoryCache (TTL 5 menit).
// Tujuan: dropdown filter di frontend sering dimuat ulang — dengan cache,
// query master hanya jalan maksimal sekali per 5 menit.
// ============================================================

using Microsoft.Extensions.Caching.Memory;
using SrsLiba.Api.Repositories;

namespace SrsLiba.Api.Services;

/// <summary>Kontrak service master data (hasil cache).</summary>
public interface IMasterDataService
{
    Task<IReadOnlyList<MasterLookupDto>> GetCellsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<StationLookupDto>> GetStationsAsync(long? cellId = null, CancellationToken ct = default);
    Task<IReadOnlyList<MasterLookupDto>> GetMeterTypesAsync(CancellationToken ct = default);
}

/// <summary>Implementasi dengan IMemoryCache — data master jarang berubah, aman di-cache.</summary>
public sealed class MasterDataService : IMasterDataService
{
    private readonly IProductionRepository _repository;
    private readonly IMemoryCache _cache;

    // Master data relatif statis → cache boleh lebih lama (5 menit)
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public MasterDataService(IProductionRepository repository, IMemoryCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    /// <summary>Ambil semua Cell (cache key: "master:cells").</summary>
    public async Task<IReadOnlyList<MasterLookupDto>> GetCellsAsync(CancellationToken ct = default)
    {
        return (await _cache.GetOrCreateAsync("master:cells", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await _repository.GetCellsAsync(ct);
        }))!;
    }

    /// <summary>
    /// Ambil Station, opsional per Cell. Cache key memuat cellId
    /// sehingga tiap pilihan cell punya cache sendiri ("master:stations:2", dst).
    /// </summary>
    public async Task<IReadOnlyList<StationLookupDto>> GetStationsAsync(long? cellId = null, CancellationToken ct = default)
    {
        var key = $"master:stations:{cellId?.ToString() ?? "all"}";
        return (await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await _repository.GetStationsAsync(cellId, ct);
        }))!;
    }

    /// <summary>Ambil semua MeterType (cache key: "master:meterTypes").</summary>
    public async Task<IReadOnlyList<MasterLookupDto>> GetMeterTypesAsync(CancellationToken ct = default)
    {
        return (await _cache.GetOrCreateAsync("master:meterTypes", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await _repository.GetMeterTypesAsync(ct);
        }))!;
    }
}
