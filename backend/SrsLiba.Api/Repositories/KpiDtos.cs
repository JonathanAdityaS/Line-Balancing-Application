// ============================================================
// KpiDtos.cs — Kumpulan DTO (Data Transfer Object) hasil perhitungan KPI.
// DTO ini yang dikirim sebagai JSON ke frontend Angular.
// Dipisah dari repository agar bisa dipakai lintas layer (service/controller).
// ============================================================

namespace SrsLiba.Api.Repositories;

/// <summary>
/// Rata-rata cycle time per station (FR-01).
/// AverageCycleTimeSeconds = rata-rata (EndTime - StartTime); TotalTest = jumlah log.
/// </summary>
public sealed record StationAverageDto(string StationId, string StationName, string CellName, decimal AverageCycleTimeSeconds, int TotalTest);

/// <summary>
/// Waiting time per station (FR-03).
/// Rumus: waktu tunggu fisik tiap unit = StartTime - ArrivalTime,
/// lalu dirata-rata / dijumlah per station.
/// </summary>
public sealed record WaitingTimeDto(string StationId, string StationName, double AverageWaitingTimeSeconds, double TotalWaitingTimeSeconds);

/// <summary>
/// Utilization per station (FR-04).
/// Rumus (identik notebook, span-based): SUM(durasi aktif) / (MAX(EndTime) - MIN(StartTime)) x 100.
/// AvailableTimeSeconds = rentang operasi aktual station (bukan shift 8 jam).
/// </summary>
public sealed record UtilizationDto(string StationId, string StationName, double ActiveTimeSeconds, double AvailableTimeSeconds, double UtilizationPercent);

/// <summary>
/// Value-Add vs Non-Value-Add time per station (FR-05).
/// VA = waktu pengujian (durasi log); NVA = waktu tunggu antar unit.
/// VaPercent = VA / (VA + NVA) x 100.
/// </summary>
public sealed record VaNvaDto(string StationId, string StationName, double VaTimeSeconds, double NvaTimeSeconds, double VaPercent);

/// <summary>
/// Perbandingan cycle time aktual vs target takt time (FR-09).
/// Status: "normal" (<= 90% target), "warning" (90-100%), "overload" (> target).
/// </summary>
public sealed record TaktComparisonDto(string StationId, string StationName, string CellName, decimal AverageCycleTimeSeconds, string Status);

/// <summary>
/// Detail satu baris log untuk tabel history di frontend.
/// Semua ID sudah diterjemahkan jadi nama agar siap ditampilkan.
/// ArrivalTime = waktu unit tiba di station (dasar waiting time fisik).
/// </summary>
public sealed record TaktLogDetailDto(long Id, string StationName, string CellName, string MeterTypeName, string SerialNumber, DateTime ArrivalTime, DateTime StartTime, DateTime EndTime, double DurationSeconds);

/// <summary>
/// Unit Flow per cell — analisis kelulusan unit antar station.
/// - RegisteredUnits: unit TERDAFTAR di cell (work order, termasuk belum dites)
/// - TotalUniqueUnits: unit unik yang SUDAH dites minimal 1x
/// - CompletedAllStations: unit yang lolos SEMUA station cell (5/5)
/// - CompletionRatePercent: completed / tested x 100
/// - UnitsWith1Test..5Test: distribusi berapa unit yang melewati 1..5 station
/// </summary>
public sealed record UnitFlowDto(string CellName, int RegisteredUnits, int TotalUniqueUnits, int CompletedAllStations, double CompletionRatePercent, int UnitsWith1Test, int UnitsWith2Tests, int UnitsWith3Tests, int UnitsWith4Tests, int UnitsWith5Tests);

/// <summary>
/// Detail Unit Flow per SerialNumber: unit ini melewati berapa station
/// dan station apa saja (untuk tabel detail di frontend).
/// </summary>
public sealed record UnitFlowDetailDto(string SerialNumber, string CellName, int TestCount, string StationsPassed);

/// <summary>
/// Ringkasan akhir all-in-one per station — gabungan SEMUA metrik dalam satu baris
/// (untuk tabel ringkasan di frontend, menggantikan keharusan membaca semua chart):
/// AvgCycleTime + TotalTest + Waiting + Utilization + VA/NVA + TaktStatus.
/// </summary>
public sealed record StationSummaryDto(
    string StationId,
    string StationName,
    string CellName,
    decimal AvgCycleTimeSeconds,
    int TotalTest,
    double AvgWaitingTimeSeconds,
    double UtilizationPercent,
    double VaTimeSeconds,
    double NvaTimeSeconds,
    double VaPercent,
    string TaktStatus);
