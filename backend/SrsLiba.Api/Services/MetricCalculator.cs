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
    /// Ringkasan garis (kartu KPI atas) — nilai agregat seluruh station:
    /// - AveragePerStation = rata-rata dari rata-rata cycle time semua station;
    /// - TotalTest = jumlah baris log;
    /// - TotalUniqueUnits = jumlah unit unik SUDAH dites (distinct CellName+SN);
    /// - TotalRegisteredUnits = unit terdaftar di work order (UnitMaster, dalam scope filter);
    /// - UntestedUnits = terdaftar tapi belum punya log sama sekali;
    /// - OverallWaitingAvgSeconds = rata-rata waktu tunggu FISIK semua unit
    ///   (StartTime - ArrivalTime per log, bukan jarak antar unit);
    /// - OverallUtilizationPercent = rata-rata utilization semua station;
    /// - OverallVaPercent = total VA / (total VA + total NVA) x 100.
    /// </summary>
    public static KpiSummary ComputeSummary(
        IReadOnlyList<TaktLogRowDto> rows,
        IReadOnlyList<StationAverageDto> averages,
        IReadOnlyList<UtilizationDto> utilization,
        IReadOnlyList<VaNvaDto> vaNva,
        IReadOnlyList<UnitMasterRowDto> registered)
    {
        var avg = averages.Count == 0 ? 0m : averages.Average(x => x.AverageCycleTimeSeconds);

        // Unit unik SUDAH dites: distinct kombinasi Cell+SN (scope per cell)
        var testedKeys = rows
            .Select(x => $"{x.CellName}|{x.SerialNumber}")
            .ToHashSet();
        var totalTested = testedKeys.Count;

        // Registered: unit terdaftar dalam scope filter; untested = tidak ada di tested set
        var totalRegistered = registered.Count;
        var untested = registered.Count(r => !testedKeys.Contains($"{r.CellName}|{r.SerialNumber}"));

        // Waiting fisik: setiap log punya nilai tunggu sendiri (Start - Arrival)
        var allWaits = rows
            .Select(x => (x.StartTime - x.ArrivalTime).TotalSeconds)
            .ToList();
        var overallWaiting = allWaits.Count > 0 ? allWaits.Average() : 0;

        var overallUtil = utilization.Count > 0 ? utilization.Average(x => x.UtilizationPercent) : 0;

        // VA global: total semua VA dibanding total waktu (VA+NVA) semua station
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
            .OrderBy(x => long.Parse(x.StationId))
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
            // Waiting time per log (bukan jarak antar unit) — tidak berubah saat difilter per meter
            var waits = group
                .Select(x => (x.StartTime - x.ArrivalTime).TotalSeconds)
                .ToList();

            var avgWait = waits.Count > 0 ? waits.Average() : 0;
            var totalWait = waits.Sum();

            result.Add(new WaitingTimeDto(
                group.Key.StationId.ToString(),
                group.Key.StationName,
                avgWait,
                totalWait));
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
            // Guard: span 0 (station cuma 1 log instan) → utilization 0 agar tidak divide-by-zero
            var pct = span > 0 ? Math.Round(active / span * 100, 2) : 0;

            result.Add(new UtilizationDto(
                group.Key.StationId.ToString(),
                group.Key.StationName,
                active,
                span,
                pct));
        }

        return result.OrderBy(x => long.Parse(x.StationId)).ToList();
    }

    /// <summary>
    /// FR-05 VA vs NVA per station:
    /// VA = durasi proses (End - Start); NVA = waktu tunggu fisik (Start - Arrival);
    /// VaPercent = proporsi VA dari total waktu (VA+NVA).
    /// </summary>
    public static IReadOnlyList<VaNvaDto> ComputeVaNva(IReadOnlyList<TaktLogRowDto> rows)
    {
        var result = new List<VaNvaDto>();

        foreach (var group in rows.GroupBy(x => new { x.StationId, x.StationName }))
        {
            // VA: waktu benar-benar memproses unit
            var va = group.Sum(x => (x.EndTime - x.StartTime).TotalSeconds);
            // NVA: waktu tunggu fisik unit sebelum diproses
            var nva = group.Sum(x => (x.StartTime - x.ArrivalTime).TotalSeconds);

            var total = va + nva;
            var vaPct = total > 0 ? Math.Round(va / total * 100, 2) : 0;

            result.Add(new VaNvaDto(group.Key.StationId.ToString(), group.Key.StationName, va, nva, vaPct));
        }

        return result.OrderBy(x => long.Parse(x.StationId)).ToList();
    }

    /// <summary>
    /// FR-09 Takt Comparison: bandingkan rata-rata cycle time vs target takt time (config).
    /// Status: overload (> 100% target), warning (> 90% target), normal (sisanya).
    /// </summary>
    public static IReadOnlyList<TaktComparisonDto> ComputeTaktComparison(IReadOnlyList<TaktLogRowDto> rows, double targetTaktSeconds)
    {
        return rows
            .GroupBy(x => new { x.StationId, x.StationName, x.CellName })
            .Select(g =>
            {
                var avg = (decimal)g.Average(x => (x.EndTime - x.StartTime).TotalSeconds);
                var status = avg > (decimal)targetTaktSeconds ? "overload"
                    : avg > (decimal)(targetTaktSeconds * 0.9) ? "warning"
                    : "normal";
                return new TaktComparisonDto(g.Key.StationId.ToString(), g.Key.StationName, g.Key.CellName, avg, status);
            })
            .OrderBy(x => long.Parse(x.StationId))
            .ToList();
    }

    /// <summary>
    /// Ringkasan KPI per Cell:
    /// TotalTest = jumlah baris; AvgDuration = rata-rata durasi proses;
    /// AvgWaitingTime = rata-rata waktu tunggu FISIK unit di cell (Start - Arrival);
    /// Utilization = RATA-RATA utilization station-station di cell itu;
    /// TotalVa/TotalNva = akumulasi durasi proses & waktu tunggu seluruh station dalam cell.
    /// </summary>
    public static IReadOnlyList<CellSummaryDto> ComputeCellSummary(
        IReadOnlyList<TaktLogRowDto> rows,
        IReadOnlyList<WaitingTimeDto> waitingPerStation,
        IReadOnlyList<UtilizationDto> utilizationPerStation)
    {
        // Peta stationId → nama cell (untuk mengelompokkan utilization per cell)
        var cellOfStation = rows
            .GroupBy(x => x.StationId)
            .ToDictionary(g => g.Key, g => g.First().CellName);

        // Utilization cell = rata-rata utilization station-station di cell tersebut
        var utilByCell = utilizationPerStation
            .GroupBy(x => cellOfStation.GetValueOrDefault(long.Parse(x.StationId), string.Empty))
            .ToDictionary(g => g.Key, g => g.Average(x => x.UtilizationPercent));

        var result = new List<CellSummaryDto>();

        foreach (var group in rows.GroupBy(x => x.CellName))
        {
            var durations = group.Select(x => (x.EndTime - x.StartTime).TotalSeconds).ToList();
            // Waiting fisik semua unit di cell ini (per log)
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

    /// <summary>
    /// Unit Flow per cell — kelulusan unit antar station:
    /// - Unit dikelompokkan per (CellName + SerialNumber);
    /// - TestCount = jumlah station DISTINCT yang dilewati unit;
    /// - Lolos 5/5 bila TestCount >= jumlah station pada cell tersebut (5);
    /// - Distribusi UnitsWith1Test..5Test = histogram berapa unit melewati 1..5 station;
    /// - RegisteredUnits = unit TERDAFTAR di cell (dari UnitMaster/work order),
    ///   bisa lebih besar dari TotalUniqueUnits bila ada unit yang belum dites.
    /// </summary>
    public static IReadOnlyList<UnitFlowDto> ComputeUnitFlow(IReadOnlyList<TaktLogRowDto> rows, IReadOnlyList<UnitMasterRowDto> registered)
    {
        // Jumlah station per cell diambil dari data (harusnya 5 per cell)
        var stationsPerCell = rows
            .GroupBy(x => x.CellName)
            .ToDictionary(g => g.Key, g => g.Select(x => x.StationId).Distinct().Count());

        // Jumlah unit TERDAFTAR per cell (dari work order UnitMaster)
        var registeredPerCell = registered
            .GroupBy(x => x.CellName)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new List<UnitFlowDto>();

        foreach (var cellGroup in rows.GroupBy(x => x.CellName))
        {
            var expected = stationsPerCell.GetValueOrDefault(cellGroup.Key, 5);
            var units = cellGroup.GroupBy(x => x.SerialNumber).ToList();

            // Histogram: index 0 = unit dengan 1 test, index 4 = 5 test
            var dist = new int[5];
            var completed = 0;

            foreach (var unit in units)
            {
                // Distinct station agar log ganda di station sama tidak dihitung dobel
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

    /// <summary>
    /// Tabel ringkasan akhir all-in-one: gabungkan semua metrik per station
    /// dari 5 dataset (average, waiting, utilization, VA/NVA, takt) menjadi
    /// satu baris per station — lookup via dictionary by StationId.
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

        var result = new List<StationSummaryDto>();

        foreach (var avg in averages)
        {
            var id = avg.StationId;
            // Station yang tidak punya data metrik tertentu tetap masuk dengan nilai 0
            waitById.TryGetValue(id, out var w);
            utilById.TryGetValue(id, out var u);
            vaById.TryGetValue(id, out var v);
            taktById.TryGetValue(id, out var t);

            result.Add(new StationSummaryDto(
                id,
                avg.StationName,
                avg.CellName,
                avg.AverageCycleTimeSeconds,
                avg.TotalTest,
                w?.AverageWaitingTimeSeconds ?? 0,
                u?.UtilizationPercent ?? 0,
                v?.VaTimeSeconds ?? 0,
                v?.NvaTimeSeconds ?? 0,
                v?.VaPercent ?? 0,
                t?.Status ?? "normal"));
        }

        return result;
    }

    /// <summary>
    /// Detail Unit Flow per SerialNumber: SN, cell, jumlah test,
    /// dan daftar nama station yang dilewati (urut nomor station).
    /// </summary>
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
