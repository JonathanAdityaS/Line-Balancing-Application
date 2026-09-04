// ============================================================
// ExportService — Membuat file laporan (FR-08) dari hasil dashboard:
//   - Excel (ClosedXML) multi-sheet: Ringkasan, Ringkasan Station,
//     Unit Flow, Unit Flow Detail
//   - PDF (QuestPDF) multi-section: judul + info filter, KPI summary,
//     tabel Ringkasan Station, Cell Summary, Unit Flow
// Input: KpiDashboardResult (metrik terhitung) + KpiFilter (untuk
//        mencetak filter yang dipakai ke dalam laporan — sesuai AC-08).
// Service ini TIDAK melakukan query database.
// ============================================================

using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SrsLiba.Api.Contracts;
using SrsLiba.Api.Repositories;

namespace SrsLiba.Api.Services;

/// <summary>Kontrak export laporan dalam format Excel dan PDF.</summary>
public interface IExportService
{
    /// <summary>Buat workbook Excel multi-sheet berisi semua metrik dashboard.</summary>
    byte[] ExportExcel(KpiDashboardResult dashboard, KpiFilter filter);

    /// <summary>Buat laporan PDF multi-section berisi semua metrik dashboard.</summary>
    byte[] ExportPdf(KpiDashboardResult dashboard, KpiFilter filter);
}

/// <summary>Implementasi export dengan ClosedXML (Excel) dan QuestPDF (PDF).</summary>
public sealed class ExportService : IExportService
{
    /// <summary>
    /// Format teks periode tanggal untuk header laporan.
    /// Contoh: "2026-08-01 s/d 2026-08-31", ">= 2026-08-01", atau "Semua tanggal".
    /// </summary>
    private static string FormatPeriode(KpiFilter filter)
    {
        var from = filter.DateFrom?.ToString("yyyy-MM-dd");
        var to = filter.DateTo?.ToString("yyyy-MM-dd");
        if (from is null && to is null) return "Semua tanggal";
        if (from is not null && to is not null) return $"{from} s/d {to}";
        if (from is not null) return $">= {from}";
        return $"s/d {to}";
    }

    // ================================================================
    // ============================ EXCEL =============================
    // ================================================================

    /// <summary>
    /// Susun workbook Excel 4 sheet:
    ///   1. "Ringkasan"        — KPI summary + tabel Cell Summary
    ///   2. "Ringkasan Station"— tabel all-in-one per station + baris TOTAL
    ///   3. "Unit Flow"        — kelulusan unit per cell + distribusi 1-5 test
    ///   4. "Unit Flow Detail" — detail per SerialNumber
    /// Output sebagai byte[] agar langsung dikirim sebagai file download.
    /// </summary>
    public byte[] ExportExcel(KpiDashboardResult dashboard, KpiFilter filter)
    {
        using var workbook = new XLWorkbook();

        BuildSummarySheet(workbook, dashboard, filter);
        BuildStationSummarySheet(workbook, dashboard);
        BuildUnitFlowSheet(workbook, dashboard);
        BuildUnitFlowDetailSheet(workbook, dashboard);

        // Simpan workbook ke memory stream lalu kembalikan sebagai bytes
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>Sheet 1 "Ringkasan": info filter, KPI utama, dan ringkasan per cell.</summary>
    private static void BuildSummarySheet(XLWorkbook workbook, KpiDashboardResult dashboard, KpiFilter filter)
    {
        var ws = workbook.Worksheets.Add("Ringkasan");

        // --- Info laporan & filter yang dipakai (AC-08) ---
        ws.Cell(1, 1).Value = "Line Balancing Dashboard — Ringkasan";
        ws.Cell(1, 1).Style.Font.SetBold();
        ws.Cell(2, 1).Value = $"Filter: Cell={filter.CellId?.ToString() ?? "All"} | Station={filter.StationId?.ToString() ?? "All"} | MeterType={filter.MeterTypeId?.ToString() ?? "All"} | Periode={FormatPeriode(filter)}";
        ws.Cell(3, 1).Value = $"Dibuat: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

        // --- KPI utama ---
        ws.Cell(5, 1).Value = "AveragePerStation (s)";
        ws.Cell(5, 2).Value = dashboard.Summary.AveragePerStation;
        ws.Cell(6, 1).Value = "TotalTest";
        ws.Cell(6, 2).Value = dashboard.Summary.TotalTest;
        ws.Cell(7, 1).Value = "Avg Waiting (s)";
        ws.Cell(7, 2).Value = dashboard.Summary.OverallWaitingAvgSeconds;
        ws.Cell(8, 1).Value = "Utilization (%)";
        ws.Cell(8, 2).Value = dashboard.Summary.OverallUtilizationPercent;
        ws.Cell(9, 1).Value = "VA (%)";
        ws.Cell(9, 2).Value = dashboard.Summary.OverallVaPercent;

        // --- Header tabel Cell Summary ---
        var row = 11;
        ws.Cell(row, 1).Value = "Cell";
        ws.Cell(row, 2).Value = "TotalTest";
        ws.Cell(row, 3).Value = "AvgDuration (s)";
        ws.Cell(row, 4).Value = "AvgWaiting (s)";
        ws.Cell(row, 5).Value = "Utilization (%)";
        ws.Cell(row, 6).Value = "TotalVA (s)";
        ws.Cell(row, 7).Value = "TotalNVA (s)";
        ws.Cell(row, 1).Style.Font.SetBold();

        // --- Isi tabel Cell Summary ---
        foreach (var cell in dashboard.CellSummary)
        {
            row++;
            ws.Cell(row, 1).Value = cell.CellName;
            ws.Cell(row, 2).Value = cell.TotalTest;
            ws.Cell(row, 3).Value = cell.AvgDurationSeconds;
            ws.Cell(row, 4).Value = cell.AvgWaitingTimeSeconds;
            ws.Cell(row, 5).Value = cell.UtilizationPercent;
            ws.Cell(row, 6).Value = cell.TotalVaSeconds;
            ws.Cell(row, 7).Value = cell.TotalNvaSeconds;
        }

        // Lebar kolom menyesuaikan isi agar mudah dibaca
        ws.Columns().AdjustToContents();
    }

    /// <summary>Sheet 2 "Ringkasan Station": tabel all-in-one per station + baris TOTAL/OVERALL.</summary>
    private static void BuildStationSummarySheet(XLWorkbook workbook, KpiDashboardResult dashboard)
    {
        var ws = workbook.Worksheets.Add("Ringkasan Station");

        // --- Header tabel all-in-one (11 kolom) ---
        string[] headers = { "StationId", "Station", "Cell", "AvgCycle (s)", "TotalTest", "AvgWaiting (s)", "Utilization (%)", "VA (s)", "NVA (s)", "VA (%)", "TaktStatus" };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
        }
        ws.Row(1).Style.Font.SetBold();

        // --- Isi: satu baris per station ---
        var row = 1;
        foreach (var s in dashboard.StationSummary)
        {
            row++;
            ws.Cell(row, 1).Value = s.StationId;
            ws.Cell(row, 2).Value = s.StationName;
            ws.Cell(row, 3).Value = s.CellName;
            ws.Cell(row, 4).Value = s.AvgCycleTimeSeconds;
            ws.Cell(row, 5).Value = s.TotalTest;
            ws.Cell(row, 6).Value = s.AvgWaitingTimeSeconds;
            ws.Cell(row, 7).Value = s.UtilizationPercent;
            ws.Cell(row, 8).Value = s.VaTimeSeconds;
            ws.Cell(row, 9).Value = s.NvaTimeSeconds;
            ws.Cell(row, 10).Value = s.VaPercent;
            ws.Cell(row, 11).Value = s.TaktStatus;
        }

        // --- Baris TOTAL/OVERALL (dari summary global) ---
        row++;
        ws.Cell(row, 2).Value = "TOTAL / OVERALL";
        ws.Cell(row, 4).Value = dashboard.Summary.AveragePerStation;
        ws.Cell(row, 5).Value = dashboard.Summary.TotalTest;
        ws.Cell(row, 6).Value = dashboard.Summary.OverallWaitingAvgSeconds;
        ws.Cell(row, 8).Value = dashboard.Summary.OverallUtilizationPercent;
        ws.Cell(row, 11).Value = dashboard.Summary.OverallVaPercent;
        ws.Row(row).Style.Font.SetBold();

        ws.Columns().AdjustToContents();
    }

    /// <summary>Sheet 3 "Unit Flow": kelulusan unit per cell + distribusi jumlah test 1-5.</summary>
    private static void BuildUnitFlowSheet(XLWorkbook workbook, KpiDashboardResult dashboard)
    {
        var ws = workbook.Worksheets.Add("Unit Flow");

        string[] headers = { "Cell", "UnitUnik", "Lolos5/5", "Rate (%)", "1 Test", "2 Test", "3 Test", "4 Test", "5 Test" };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
        }
        ws.Row(1).Style.Font.SetBold();

        var row = 1;
        foreach (var f in dashboard.UnitFlow)
        {
            row++;
            ws.Cell(row, 1).Value = f.CellName;
            ws.Cell(row, 2).Value = f.TotalUniqueUnits;
            ws.Cell(row, 3).Value = f.CompletedAllStations;
            ws.Cell(row, 4).Value = f.CompletionRatePercent;
            ws.Cell(row, 5).Value = f.UnitsWith1Test;
            ws.Cell(row, 6).Value = f.UnitsWith2Tests;
            ws.Cell(row, 7).Value = f.UnitsWith3Tests;
            ws.Cell(row, 8).Value = f.UnitsWith4Tests;
            ws.Cell(row, 9).Value = f.UnitsWith5Tests;
        }

        ws.Columns().AdjustToContents();
    }

    /// <summary>Sheet 4 "Unit Flow Detail": detail per SerialNumber + station yang dilewati.</summary>
    private static void BuildUnitFlowDetailSheet(XLWorkbook workbook, KpiDashboardResult dashboard)
    {
        var ws = workbook.Worksheets.Add("Unit Flow Detail");

        string[] headers = { "SerialNumber", "Cell", "JmlTest", "Station Dilewati" };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
        }
        ws.Row(1).Style.Font.SetBold();

        var row = 1;
        foreach (var d in dashboard.UnitFlowDetail)
        {
            row++;
            ws.Cell(row, 1).Value = d.SerialNumber;
            ws.Cell(row, 2).Value = d.CellName;
            ws.Cell(row, 3).Value = d.TestCount;
            ws.Cell(row, 4).Value = d.StationsPassed;
        }

        ws.Columns().AdjustToContents();
    }

    // ================================================================
    // ============================= PDF ==============================
    // ================================================================

    /// <summary>
    /// Susun laporan PDF A4 multi-section:
    ///   1. Judul + filter + timestamp
    ///   2. KPI summary
    ///   3. Tabel Ringkasan Station (all-in-one)
    ///   4. Tabel Cell Summary
    ///   5. Tabel Unit Flow per cell
    /// </summary>
    public byte[] ExportPdf(KpiDashboardResult dashboard, KpiFilter filter)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Content().Column(col =>
                {
                    // --- Section 1: Judul + filter + timestamp (AC-08) ---
                    col.Item().Text("Line Balancing Dashboard — Laporan").FontSize(16).SemiBold();
                    col.Item().Text($"Filter: Cell={filter.CellId?.ToString() ?? "All"} | Station={filter.StationId?.ToString() ?? "All"} | MeterType={filter.MeterTypeId?.ToString() ?? "All"} | Periode={FormatPeriode(filter)}");
                    col.Item().Text($"Dibuat: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    col.Item().PaddingBottom(6);

                    // --- Section 2: KPI summary ---
                    col.Item().Text("Ringkasan KPI").FontSize(12).SemiBold();
                    col.Item().Text($"Average Per Station: {dashboard.Summary.AveragePerStation:F2} s   |   Total Test: {dashboard.Summary.TotalTest}");
                    col.Item().Text($"Avg Waiting: {dashboard.Summary.OverallWaitingAvgSeconds:F2} s   |   Utilization: {dashboard.Summary.OverallUtilizationPercent:F2} %   |   VA: {dashboard.Summary.OverallVaPercent:F2} %");
                    col.Item().PaddingBottom(8);

                    // --- Section 3: Tabel Ringkasan Station (all-in-one) ---
                    col.Item().Text("Ringkasan Station").FontSize(12).SemiBold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(22);  // ID
                            columns.RelativeColumn(2);   // Station
                            columns.RelativeColumn(1.4f);// Cell
                            columns.RelativeColumn(1.2f);// Cycle
                            columns.RelativeColumn(1);   // Test
                            columns.RelativeColumn(1.2f);// Waiting
                            columns.RelativeColumn(1.2f);// Util%
                            columns.RelativeColumn(1);   // VA%
                            columns.RelativeColumn(1.4f);// Status
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("ID").SemiBold();
                            header.Cell().Text("Station").SemiBold();
                            header.Cell().Text("Cell").SemiBold();
                            header.Cell().Text("Cycle(s)").SemiBold();
                            header.Cell().Text("Test").SemiBold();
                            header.Cell().Text("Wait(s)").SemiBold();
                            header.Cell().Text("Util%").SemiBold();
                            header.Cell().Text("VA%").SemiBold();
                            header.Cell().Text("Status").SemiBold();
                        });

                        foreach (var s in dashboard.StationSummary)
                        {
                            table.Cell().Text(s.StationId);
                            table.Cell().Text(s.StationName);
                            table.Cell().Text(s.CellName);
                            table.Cell().Text($"{s.AvgCycleTimeSeconds:F2}");
                            table.Cell().Text($"{s.TotalTest}");
                            table.Cell().Text($"{s.AvgWaitingTimeSeconds:F1}");
                            table.Cell().Text($"{s.UtilizationPercent:F2}");
                            table.Cell().Text($"{s.VaPercent:F2}");
                            table.Cell().Text(s.TaktStatus);
                        }
                    });
                    col.Item().PaddingBottom(8);

                    // --- Section 4: Tabel Cell Summary ---
                    col.Item().Text("Ringkasan Per Cell").FontSize(12).SemiBold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.4f); // Cell
                            columns.RelativeColumn(1);    // Test
                            columns.RelativeColumn(1.2f); // AvgDur
                            columns.RelativeColumn(1.2f); // AvgWait
                            columns.RelativeColumn(1);    // Util%
                            columns.RelativeColumn(1.2f); // VA
                            columns.RelativeColumn(1.2f); // NVA
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Cell").SemiBold();
                            header.Cell().Text("Test").SemiBold();
                            header.Cell().Text("Dur(s)").SemiBold();
                            header.Cell().Text("Wait(s)").SemiBold();
                            header.Cell().Text("Util%").SemiBold();
                            header.Cell().Text("VA(s)").SemiBold();
                            header.Cell().Text("NVA(s)").SemiBold();
                        });

                        foreach (var c in dashboard.CellSummary)
                        {
                            table.Cell().Text(c.CellName);
                            table.Cell().Text($"{c.TotalTest}");
                            table.Cell().Text($"{c.AvgDurationSeconds:F2}");
                            table.Cell().Text($"{c.AvgWaitingTimeSeconds:F1}");
                            table.Cell().Text($"{c.UtilizationPercent:F2}");
                            table.Cell().Text($"{c.TotalVaSeconds:F0}");
                            table.Cell().Text($"{c.TotalNvaSeconds:F0}");
                        }
                    });
                    col.Item().PaddingBottom(8);

                    // --- Section 5: Tabel Unit Flow per cell ---
                    col.Item().Text("Unit Flow Per Cell").FontSize(12).SemiBold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.4f); // Cell
                            columns.RelativeColumn(1);    // Unit unik
                            columns.RelativeColumn(1);    // Lolos 5/5
                            columns.RelativeColumn(1);    // Rate
                            columns.RelativeColumn(0.8f); // 1
                            columns.RelativeColumn(0.8f); // 2
                            columns.RelativeColumn(0.8f); // 3
                            columns.RelativeColumn(0.8f); // 4
                            columns.RelativeColumn(0.8f); // 5
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Cell").SemiBold();
                            header.Cell().Text("Unik").SemiBold();
                            header.Cell().Text("5/5").SemiBold();
                            header.Cell().Text("Rate%").SemiBold();
                            header.Cell().Text("1x").SemiBold();
                            header.Cell().Text("2x").SemiBold();
                            header.Cell().Text("3x").SemiBold();
                            header.Cell().Text("4x").SemiBold();
                            header.Cell().Text("5x").SemiBold();
                        });

                        foreach (var f in dashboard.UnitFlow)
                        {
                            table.Cell().Text(f.CellName);
                            table.Cell().Text($"{f.TotalUniqueUnits}");
                            table.Cell().Text($"{f.CompletedAllStations}");
                            table.Cell().Text($"{f.CompletionRatePercent:F1}");
                            table.Cell().Text($"{f.UnitsWith1Test}");
                            table.Cell().Text($"{f.UnitsWith2Tests}");
                            table.Cell().Text($"{f.UnitsWith3Tests}");
                            table.Cell().Text($"{f.UnitsWith4Tests}");
                            table.Cell().Text($"{f.UnitsWith5Tests}");
                        }
                    });
                });
            });
        });

        return doc.GeneratePdf();
    }
}
