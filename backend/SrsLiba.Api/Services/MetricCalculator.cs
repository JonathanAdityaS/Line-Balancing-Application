// ============================================================
// MetricCalculator — Semua RUMUS KPI ada di sini (pure functions, tanpa DB).
// Rumus dibuat IDENTIK dengan notebook "Analisis_Takt_Log_Time" agar
// hasil dashboard = hasil analisis notebook (sudah diverifikasi cocok).
// Input: daftar TaktLogRowDto hasil query; Output: DTO KPI per station/cell.
// ============================================================

using SrsLiba.Api.Repositories;

namespace SrsLiba.Api.Services;

/// <summary>Kumpulan fungsi statis perhitungan metrik line balancing.</summary>
public static class MetricCalculator
{
    /// <summary>
    /// FR-01 Average Per Station: rata-rata durasi (EndTime-StartTime) per station.
    /// Sort numerik agar urutan station benar (1,2,...,10 — bukan 1,10,2).
    /// </summary>
    public static IReadOnlyList<StationAverageDto> ComputeStationAverages(IReadOnlyList<TaktLogRowDto> rows)
    {
        return rows
            .GroupBy(x => new { x.StationId, x.StationName, x.CellName })
            .Select(g => new StationAverageDto(
                g.Key.StationId.ToString(),
                g.Key.StationName,
                g.Key.CellName,
                (decimal)g.Average(x => (x.EndTime - x.StartTime).TotalSeconds),
                g.Count()))
            .OrderBy(x => x.StationId)
            .ToList();
    }

    /// <summary>
    /// FR-03 Waiting Time per station — waktu tunggu FISIK tiap unit:
    /// Waiting(unit) = StartTime - ArrivalTime (sejak tiba di station s/d mulai diproses).
    /// </summary>
    public static IReadOnlyList<WaitingTimeDto> ComputeWaitingTimes(IReadOnlyList<TaktLogRowDto> rows)
    {
        var result = new List<WaitingTimeDto>();

        foreach (var group in rows.GroupBy(x => new { x.StationId, x.StationName }))
        {
            var logs = group.OrderBy(x => x.StartTime).ToList();
            var waitingTimes = new List<double>();

            for (int i = 1; i < logs.Count; i++)
            {
                var wait = (logs[i].StartTime - logs[i - 1].EndTime).TotalSeconds;
                if (wait > 0) waitingTimes.Add(wait);
            }

            var avgWait = waitingTimes.Count > 0 ? waitingTimes.Average() : 0;
            var totalWait = waitingTimes.Sum();

            result.Add(new WaitingTimeDto(group.Key.StationId.ToString(), group.Key.StationName, avgWait, totalWait));
        }

        return result.OrderBy(x => long.Parse(x.StationId)).ToList();
    }

    /// <summary>
    /// FR-04 Utilization per station (span-based, identik notebook):
    /// aktif = SUM(durasi semua log); span = MAX(EndTime) - MIN(StartTime);
    /// utilization = aktif / span x 100.
    /// </summary>
    public static IReadOnlyList<UtilizationDto> ComputeUtilization(IReadOnlyList<TaktLogRowDto> rows)
    {
        var result = new List<UtilizationDto>();

        foreach (var group in rows.GroupBy(x => new { x.StationId, x.StationName }))
        {
            var logs = group.ToList();
            var active = logs.Sum(x => (x.EndTime - x.StartTime).TotalSeconds);
            var span = (logs.Max(x => x.EndTime) - logs.Min(x => x.StartTime)).TotalSeconds;
            var pct = span > 0 ? Math.Round(active / span * 100, 2) : 0;

            result.Add(new UtilizationDto(group.Key.StationId.ToString(), group.Key.StationName, active, span, Math.Round(pct, 2)));
        }

        return result.OrderBy(x => long.Parse(x.StationId)).ToList();
    }

    /// <summary>
    /// FR-05 VA vs NVA per station:
    /// VA = total durasi proses (End - Start); NVA = waktu tunggu antar unit.
    /// VaPercent = proporsi VA dari total waktu (VA+NVA).
    /// </summary>
    public static IReadOnlyList<VaNvaDto> ComputeVaNva(IReadOnlyList<TaktLogRowDto> rows)
    {
        var result = new List<VaNvaDto>();

        foreach (var group in rows.GroupBy(x => new { x.StationId, x.StationName }))
        {
            var logs = group.OrderBy(x => x.StartTime).ToList();
            var va = logs.Sum(x => (x.EndTime - x.StartTime).TotalSeconds);

            double nva = 0;
            for (int i = 1; i < logs.Count; i++)
            {
                nva += (logs[i].StartTime - logs[i - 1].EndTime).TotalSeconds;
            }

            var total = va + nva;
            var vaPct = total > 0 ? Math.Round(va / total * 100, 2) : 0;

            result.Add(new VaNvaDto(group.Key.StationId.ToString(), group.Key.StationName, va, nva, vaPct));
        }

        return result.OrderBy(x => long.Parse(x.StationId)).ToList();
    }

    /// <summary>
    /// FR-09 Takt Comparison: bandingkan rata-rata cycle time vs target takt time (FR-09).
    /// Status: overload (> 100% target), warning (> 90% target), normal (sisanya).
    /// Target takt time bisa per station/cell via taktTargets config, fallback ke global target.
    /// </summary>
    public static IReadOnlyList<TaktComparisonDto> ComputeTaktComparison(
        IReadOnlyList<TaktLogRowDto> rows,
        double globalTargetTaktSeconds,
        IReadOnlyDictionary<string, double>? perStationTargets = null,
        IReadOnlyDictionary<string, double>? perCellTargets = null)
    {
        return rows
            .GroupBy(x => new { x.StationId, x.StationName, x.CellName })
            .Select(g =>
            {
                var avg = (decimal)g.Average(x => (x.EndTime - x.StartTime).TotalSeconds);

                // Determine target takt for this station
                // Priority: station-specific > cell-specific > global default
                var stationKey = $"{g.Key.CellName}|{g.Key.StationName}";
                var targetTakt = globalTargetTaktSeconds;

                if (perStationTargets?.TryGetValue($"{g.Key.CellName}|{g.Key.StationName}", out var stationTarget) == true)
                {
                    targetTakt = stationTarget;
                }
                else if (perCellTargets?.TryGetValue(g.Key.CellName, out var cellTarget) == true)
                {
                    targetTakt = cellTarget;
                }

                var status = avg > (decimal)targetTakt ? "overload"
                    : avg > (decimal)(targetTakt * 0.9) ? "warning"
                    : "normal";
                return new TaktComparisonDto(g.Key.StationId.ToString(), g.Key.StationName, g.Key.CellName, avg, status, targetTakt);
            })
            .OrderBy(x => long.Parse(x.StationId))
            .ToList();
    }

    /// <summary>
    /// Tabel ringkasan akhir all-in-one: gabungkan semua metrik per station
    /// dari 5 dataset (average, waiting, utilization, VA/NVA, takt) menjadi
    /// satu baris per station — lookup via dictionary by StationId.
    /// Station yang tidak punya data log tetap dimasukkan dengan HasData=false
    /// agar frontend bisa tampilkan N/A.
    /// </summary>
    public static IReadOnlyList<StationSummaryDto> ComputeStationSummary(
        IReadOnlyList<StationAverageDto> averages,
        IReadOnlyList<WaitingTimeDto> waiting,
        IReadOnlyList<UtilizationDto> utilization,
        IReadOnlyList<VaNvaDto> vaNva,
        IReadOnlyList<TaktComparisonDto> takt)
    {
        // Index tiap metrik by StationId agar lookup O(1)
        var waitById = waiting.ToDictionary(x => x.StationId);
        var utilById = utilization.ToDictionary(x => x.StationId);
        var vaById = vaNva.ToDictionary(x => x.StationId);
        var taktById = takt.ToDictionary(x => x.StationId);

        // Kumpulkan semua stationId yang pernah muncul di mana saja (avg, waiting, util, va, takt)
        var allStationIds = new HashSet<string>();
        foreach (var avg in averages) allStationIds.Add(avg.StationId);
        foreach (var w in waiting) allStationIds.Add(w.StationId);
        foreach (var u in utilization) allStationIds.Add(u.StationId);
        foreach (var v in vaNva) allStationIds.Add(v.StationId);
        foreach (var t in takt) allStationIds.Add(t.StationId);

        var result = new List<StationSummaryDto>();

        foreach (var id in allStationIds.OrderBy(x => long.Parse(x)))
        {
            // Cek apakah station punya data log (ada di averages = punya log)
            var hasData = averages.Any(a => a.StationId == id);

            // Ambil data dari tiap metrik (bisa null jika tidak ada)
            waitById.TryGetValue(id, out var w);
            utilById.TryGetValue(id, out var u);
            vaById.TryGetValue(id, out var v);
            taktById.TryGetValue(id, out var t);

            // Dapatkan nama station dan cell dari metrik mana saja yang tersedia
            var stationName = averages.FirstOrDefault(a => a.StationId == id)?.StationName ?? 
                              takt.FirstOrDefault(tc => t != null && tc.StationId == id)?.StationName ?? "";
            var cellName = averages.FirstOrDefault(a => a.StationId == id)?.CellName ?? 
                           takt.FirstOrDefault(tc => t != null && tc.StationId == id)?.CellName ?? "";

            result.Add(new StationSummaryDto(
                id,
                stationName,
                cellName,
                averages.FirstOrDefault(a => a.StationId == id)?.AverageCycleTimeSeconds ?? 0,
                averages.FirstOrDefault(a => a.StationId == id)?.TotalTest ?? 0,
                w?.AverageWaitingTimeSeconds ?? 0,
                u?.UtilizationPercent ?? 0,
                v?.VaTimeSeconds ?? 0,
                v?.NvaTimeSeconds ?? 0,
                v?.VaPercent ?? 0,
                t?.Status ?? "normal",
                hasData));
        }

        return result.OrderBy(x => long.Parse(x.StationId)).ToList();
    }

    public static KpiSummary ComputeSummary(
        IReadOnlyList<TaktLogRowDto> rows,
        IReadOnlyList<StationAverageDto> averages,
        IReadOnlyList<UtilizationDto> utilization,
        IReadOnlyList<VaNvaDto> vaNva,
        IReadOnlyList<UnitMasterRowDto> registered)
    {
        var avg = averages.Count == 0 ? 0m : averages.Average(x => x.AverageCycleTimeSeconds);

        var testedKeys = rows
            .Select(x => $"{x.CellName}|{x.SerialNumber}")
            .ToHashSet();
        var totalTested = testedKeys.Count;

        var totalRegistered = registered.Count;
        var untested = registered.Count(r => !testedKeys.Contains($"{r.CellName}|{r.SerialNumber}"));

        var allWaits = rows
            .Select(x => (x.StartTime - x.ArrivalTime).TotalSeconds)
            .ToList();
        var overallWaiting = allWaits.Count > 0 ? allWaits.Average() : 0;

        var overallUtil = utilization.Count > 0 ? utilization.Average(x => x.UtilizationPercent) : 0;

        var totalVa = vaNva.Sum(x => x.VaTimeSeconds);
        var totalNva = vaNva.Sum(x => x.NvaTimeSeconds);
        var overallVaPct = (totalVa + totalNva) > 0 ? Math.Round(totalVa / (totalVa + totalNva) * 100, 2) : 0;

        return new KpiSummary(
            avg,
            rows.Count,
            totalTested,
            totalRegistered,
            untested,
            Math.Round(overallWaiting, 2),
            Math.Round(overallUtil, 2),
            overallVaPct);
    }

    public static IReadOnlyList<CellSummaryDto> ComputeCellSummary(
        IReadOnlyList<TaktLogRowDto> rows,
        IReadOnlyList<WaitingTimeDto> waitingPerStation,
        IReadOnlyList<UtilizationDto> utilizationPerStation)
    {
        var cellOfStation = rows
            .GroupBy(x => x.StationId)
            .ToDictionary(g => g.Key, g => g.First().CellName);

        var utilByCell = utilizationPerStation
            .GroupBy(x => cellOfStation.GetValueOrDefault(long.Parse(x.StationId), string.Empty))
            .ToDictionary(g => g.Key, g => g.Average(x => x.UtilizationPercent));

        var result = new List<CellSummaryDto>();

        foreach (var group in rows.GroupBy(x => x.CellName))
        {
            var durations = group.Select(x => (x.EndTime - x.StartTime).TotalSeconds).ToList();
            var waits = group.Select(x => (x.StartTime - x.ArrivalTime).TotalSeconds).ToList();

            result.Add(new CellSummaryDto(
                group.Key,
                group.Count(),
                Math.Round(durations.Average(), 2),
                waits.Count > 0 ? Math.Round(waits.Average(), 2) : 0,
                Math.Round(utilByCell.GetValueOrDefault(group.Key, 0), 2),
                durations.Sum(),
                waits.Sum()));
        }

        return result.OrderBy(x => x.CellName).ToList();
    }

    public static IReadOnlyList<UnitFlowDto> ComputeUnitFlow(IReadOnlyList<TaktLogRowDto> rows, IReadOnlyList<UnitMasterRowDto> registered)
    {
        var stationsPerCell = rows
            .GroupBy(x => x.CellName)
            .ToDictionary(g => g.Key, g => g.Select(x => x.StationId).Distinct().Count());

        var registeredPerCell = registered
            .GroupBy(x => x.CellName)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new List<UnitFlowDto>();

        foreach (var cellGroup in rows.GroupBy(x => x.CellName))
        {
            var expected = stationsPerCell.GetValueOrDefault(cellGroup.Key, 5);
            var units = cellGroup.GroupBy(x => x.SerialNumber).ToList();

            var dist = new int[5];
            var completed = 0;

            foreach (var unit in units)
            {
                var testCount = Math.Min(unit.Select(x => x.StationId).Distinct().Count(), 5);
                if (testCount >= 1) dist[testCount - 1]++;
                if (testCount >= expected) completed++;
            }

            var total = units.Count;
            result.Add(new UnitFlowDto(
                cellGroup.Key,
                registeredPerCell.GetValueOrDefault(cellGroup.Key, 0),
                total,
                completed,
                total > 0 ? Math.Round(completed * 100.0 / total, 2) : 0,
                dist[0], dist[1], dist[2], dist[3], dist[4]));
        }

        return result.OrderBy(x => x.CellName).ToList();
    }

    public static IReadOnlyList<UnitFlowDetailDto> ComputeUnitFlowDetail(IReadOnlyList<TaktLogRowDto> rows)
    {
        return rows
            .GroupBy(x => new { x.CellName, x.SerialNumber })
            .Select(g => new UnitFlowDetailDto(
                g.Key.SerialNumber,
                g.Key.CellName,
                g.Select(x => x.StationId).Distinct().Count(),
                string.Join(", ", g
                    .GroupBy(x => x.StationId)
                    .OrderBy(s => s.Key)
                    .Select(s => s.First().StationName))))
            .OrderBy(x => x.CellName).ThenBy(x => x.SerialNumber)
            .ToList();
    }
}