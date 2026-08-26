// ============================================================
// CsvSeeder — Mengisi database SQLite dari 4 file CSV dummy
// (folder Data/DummyCsv) saat aplikasi pertama kali dijalankan.
// Alur: baca CSV → validasi → insert ke Cell/Station/MeterType/TaktLogTime.
// Seeder hanya mengisi bila database masih kosong (idempotent).
// ============================================================

using Microsoft.EntityFrameworkCore;
using SrsLiba.Api.Models;

namespace SrsLiba.Api.Data;

/// <summary>
/// Static class untuk seeding database awal dari CSV.
/// Validasi yang dilakukan: format datetime, foreign key orphan,
/// dan EndTime lebih awal dari StartTime. Row invalid di-skip + dicatat di log.
/// </summary>
public static class CsvSeeder
{
    /// <summary>
    /// Titik masuk seeding — dipanggil dari Program.cs saat startup.
    /// Pastikan schema database sudah dibuat (EnsureCreated), lalu isi
    /// master data dan log dari CSV bila database masih kosong.
    /// </summary>
    /// <param name="db">DbContext database target.</param>
    /// <param name="csvFolder">Path folder berisi 4 file CSV dummy.</param>
    public static async Task SeedAsync(AppDbContext db, string csvFolder)
    {
        // Buat database + tabel bila belum ada (tanpa migration)
        await db.Database.EnsureCreatedAsync();

        // Idempotent: jika sudah pernah di-seed, lewati
        if (await db.Cells.AnyAsync()) return;

        // Baca & parse semua CSV ke entity in-memory
        var cells = ReadCells(Path.Combine(csvFolder, "cell_master.csv"));
        var stations = ReadStations(Path.Combine(csvFolder, "station_master.csv"));
        var meterTypes = ReadMeterTypes(Path.Combine(csvFolder, "meter_type_master.csv"));
        var registeredUnits = ReadUnitMasters(Path.Combine(csvFolder, "unit_master.csv"));
        var logs = ReadTaktLogs(Path.Combine(csvFolder, "takt_log_time.csv"));

        // Set untuk validasi foreign key log (station & meter type harus dikenal)
        var stationIds = stations.Select(x => x.Id).ToHashSet();
        var meterTypeIds = meterTypes.Select(x => x.Id).ToHashSet();

        // Validasi unit terdaftar: CellID & MeterTypeID harus dikenal
        var validUnits = new List<UnitMaster>();
        var skippedUnits = 0;
        foreach (var unit in registeredUnits)
        {
            if (!stationIds.Contains(unit.CellId) && !cells.Any(c => c.Id == unit.CellId))
            {
                skippedUnits++;
                continue;
            }
            if (!meterTypeIds.Contains(unit.MeterTypeId))
            {
                skippedUnits++;
                continue;
            }
            validUnits.Add(unit);
        }
        if (skippedUnits > 0)
            Console.WriteLine($"[CsvSeeder] Skipped {skippedUnits} registered unit(s) with unknown CellID/MeterTypeID.");

        // Validasi tiap log: buang yang FK-nya tidak dikenal (orphan)
        // atau EndTime lebih awal dari StartTime (data tidak konsisten)
        var validLogs = new List<TaktLogTime>();
        var skippedOrphan = 0;
        var skippedInvalid = 0;

        foreach (var log in logs)
        {
            // Skip log yang menunjuk StationID/MeterTypeID yang tidak ada di master
            if (!stationIds.Contains(log.StationId) || !meterTypeIds.Contains(log.MeterTypeId))
            {
                skippedOrphan++;
                continue;
            }
            // Skip log dengan durasi negatif (data tidak valid)
            if (log.EndTime < log.StartTime)
            {
                skippedInvalid++;
                continue;
            }
            validLogs.Add(log);
        }

        // Laporkan jumlah row yang dibuang agar mudah di-troubleshoot
        if (skippedOrphan > 0)
            Console.WriteLine($"[CsvSeeder] Skipped {skippedOrphan} log(s) with unknown StationID/MeterTypeID.");
        if (skippedInvalid > 0)
            Console.WriteLine($"[CsvSeeder] Skipped {skippedInvalid} log(s) with EndTime < StartTime.");

        // Insert master data dulu (Cell → Station → MeterType → UnitMaster),
        // baru log — agar FK constraint tidak dilanggar
        await db.Cells.AddRangeAsync(cells);
        await db.Stations.AddRangeAsync(stations);
        await db.MeterTypes.AddRangeAsync(meterTypes);
        await db.UnitMasters.AddRangeAsync(validUnits);
        await db.SaveChangesAsync();

        await db.TaktLogTimes.AddRangeAsync(validLogs);
        await db.SaveChangesAsync();

        // Ringkasan hasil seeding
        Console.WriteLine($"[CsvSeeder] Seeded {cells.Count} cells, {stations.Count} stations, {meterTypes.Count} meter types, {validUnits.Count} registered units, {validLogs.Count} logs.");
    }

    /// <summary>Baca cell_master.csv (format: ID,CellName).</summary>
    private static List<Cell> ReadCells(string path)
    {
        var lines = File.ReadAllLines(path).Skip(1); // Skip baris header
        return lines
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(line =>
            {
                var parts = line.Split(',');
                return new Cell { Id = long.Parse(parts[0].Trim()), CellName = parts[1].Trim() };
            }).ToList();
    }

    /// <summary>Baca station_master.csv (format: ID,CellID,StationName).</summary>
    private static List<Station> ReadStations(string path)
    {
        var lines = File.ReadAllLines(path).Skip(1); // Skip baris header
        return lines
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(line =>
            {
                var parts = line.Split(',');
                return new Station
                {
                    Id = long.Parse(parts[0].Trim()),
                    CellId = long.Parse(parts[1].Trim()), // FK ke Cell
                    StationName = parts[2].Trim()
                };
            }).ToList();
    }

    /// <summary>Baca meter_type_master.csv (format: ID,Name).</summary>
    private static List<MeterType> ReadMeterTypes(string path)
    {
        var lines = File.ReadAllLines(path).Skip(1); // Skip baris header
        return lines
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(line =>
            {
                var parts = line.Split(',');
                return new MeterType { Id = long.Parse(parts[0].Trim()), Name = parts[1].Trim() };
            }).ToList();
    }

    /// <summary>
    /// Baca unit_master.csv (format: SerialNumber,CellID,MeterTypeID).
    /// Daftar unit yang direncanakan dites — dasar metrik registered/tested/untested.
    /// Row gagal parse di-skip.
    /// </summary>
    private static List<UnitMaster> ReadUnitMasters(string path)
    {
        var lines = File.ReadAllLines(path).Skip(1); // Skip baris header
        var result = new List<UnitMaster>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',');
            if (parts.Length < 3) continue;

            if (!long.TryParse(parts[1].Trim(), out var cellId)) continue;
            if (!long.TryParse(parts[2].Trim(), out var meterTypeId)) continue;

            result.Add(new UnitMaster
            {
                SerialNumber = parts[0].Trim(),
                CellId = cellId,
                MeterTypeId = meterTypeId
            });
        }

        return result;
    }

    /// <summary>
    /// Baca takt_log_time.csv (format: ID,StationID,MeterTypeID,SerialNumber,StartTime,EndTime,ArrivalTime).
    /// Parsing defensif: row yang formatnya salah (datetime/angka invalid)
    /// di-skip tanpa membuat aplikasi crash.
    /// </summary>
    private static List<TaktLogTime> ReadTaktLogs(string path)
    {
        var lines = File.ReadAllLines(path).Skip(1); // Skip baris header
        var result = new List<TaktLogTime>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',');
            // Struktur kolom harus lengkap (7 kolom, ArrivalTime kolom terakhir)
            if (parts.Length < 7) continue;

            // TryParse semua field: row gagal parse di-skip, bukan crash
            if (!long.TryParse(parts[0].Trim(), out var id)) continue;
            if (!long.TryParse(parts[1].Trim(), out var stationId)) continue;
            if (!long.TryParse(parts[2].Trim(), out var meterTypeId)) continue;
            if (!DateTime.TryParse(parts[4].Trim(), out var startTime)) continue;
            if (!DateTime.TryParse(parts[5].Trim(), out var endTime)) continue;
            if (!DateTime.TryParse(parts[6].Trim(), out var arrivalTime)) continue;

            result.Add(new TaktLogTime
            {
                Id = id,
                StationId = stationId,
                MeterTypeId = meterTypeId,
                SerialNumber = parts[3].Trim(),
                StartTime = startTime,
                EndTime = endTime,
                ArrivalTime = arrivalTime
            });
        }

        return result;
    }
}
