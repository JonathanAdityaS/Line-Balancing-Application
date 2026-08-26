// ============================================================
// KpiOptions — Konfigurasi KPI dari appsettings.json (section "Kpi").
// Prinsip NFR: nilai target TIDAK di-hardcode di kode,
// melainkan dibaca via IOptions<KpiOptions> (mudah diubah per environment).
// ============================================================

namespace SrsLiba.Api.Services;

/// <summary>
/// Binding konfigurasi "Kpi" dari appsettings.json.
/// Contoh appsettings.json: { "Kpi": { "TargetTaktSeconds": 50 } }
/// </summary>
public sealed class KpiOptions
{
    /// <summary>Nama section di appsettings.json yang di-bind ke class ini.</summary>
    public const string SectionName = "Kpi";

    /// <summary>
    /// Target takt time dalam detik — acuan status station:
    /// cycle time &gt; target = overload; &gt; 90% target = warning.
    /// Default 50 detik bila config tidak ada.
    /// </summary>
    public double TargetTaktSeconds { get; set; } = 50;
}
