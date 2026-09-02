// ============================================================
// KpiOptions — Konfigurasi KPI dari appsettings.json (section "Kpi").
// Prinsip NFR: nilai target TIDAK di-hardcode di kode,
// melainkan dibaca via IOptions<KpiOptions> (mudah diubah per environment).
// ============================================================

namespace SrsLiba.Api.Services;

/// <summary>
/// Konfigurasi target takt time per cell/station.
/// Key: "CellName|StationName" atau "CellName" untuk default cell-wide.
/// Value: target takt time in detik.
/// Jika tidak ada mapping, fallback ke TargetTaktSeconds global.
/// </summary>
public sealed class TaktTargetConfig
{
    /// <summary>
    /// Target takt time default global (detik) - fallback bila tidak ada mapping spesifik.
    /// Default 48 detik.
    /// </summary>
    public double TargetTaktSeconds { get; set; } = 48;

    /// <summary>
    /// Mapping target takt per cell.
    /// Key: nama cell (e.g., "Cell-A"), Value: target takt seconds.
    /// Jika cell tidak ada di mapping, gunakan TargetTaktSeconds global.
    /// </summary>
    public Dictionary<string, double> PerCell { get; set; } = new();

    /// <summary>
    /// Mapping target takt per station.
    /// Key: "CellName|StationName" (contoh: "Cell-A|Station-01").
    /// Priority tertinggi - override perCell dan global.
    /// </summary>
    public Dictionary<string, double> PerStation { get; set; } = new();
}

/// <summary>
/// Binding konfigurasi "Kpi" dari appsettings.json.
/// Contoh appsettings.json: { "Kpi": { "TargetTaktSeconds": 50 } }
/// </summary>
public sealed class KpiOptions
{
    /// <summary>Nama section di appsettings.json yang di-bind ke class ini.</summary>
    public const string SectionName = "Kpi";

    /// <summary>
    /// Target takt time global default (detik) — acuan status station:
    /// cycle time > target = overload; > 90% target = warning.
    /// Default 48 detik bila config tidak ada.
    /// </summary>
    public double TargetTaktSeconds { get; set; } = 48;

    /// <summary>
    /// Konfigurasi target takt per cell/station.
    /// Jika tidak diisi, gunakan TargetTaktSeconds global.
    /// </summary>
    public TaktTargetConfig TaktTargets { get; set; } = new();
}
