using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SrsLiba.Api.Models;

/// <summary>
/// Entitas master Cell (area/line produksi).
/// Tabel database: "Cell". Satu Cell memiliki tepat 5 Station (relasi 1:N).
/// Data dummy: 4 row (Cell-A s/d Cell-D) — lihat Data/DummyCsv/cell_master.csv.
/// </summary>
[Table("Cell")]
public sealed class Cell
{
    /// <summary>Primary key tabel Cell.</summary>
    [Key]
    public long Id { get; set; }

    /// <summary>Nama cell produksi (contoh: "Cell-A").</summary>
    [Required]
    [MaxLength(100)]
    public string CellName { get; set; } = string.Empty;

    /// <summary>Navigation property: daftar Station yang berada di cell ini (harusnya 5).</summary>
    public ICollection<Station> Stations { get; set; } = new List<Station>();
}

/// <summary>
/// Entitas master Station (titik proses/workstation pada cell).
/// Tabel database: "Station". Setiap station punya fungsi yang sama di tiap cell,
/// sehingga nama boleh sama (Station-01..05) asalkan ID-nya unik.
/// Data dummy: 20 row (4 cell x 5 station) — lihat Data/DummyCsv/station_master.csv.
/// </summary>
[Table("Station")]
public sealed class Station
{
    /// <summary>Primary key tabel Station (1-20 pada data dummy).</summary>
    [Key]
    public long Id { get; set; }

    /// <summary>Foreign key ke tabel Cell — menentukan cell tempat station ini berada.</summary>
    [Required]
    public long CellId { get; set; }

    /// <summary>Nama station (Station-01 s/d Station-05; nama sama antar cell, ID berbeda).</summary>
    [Required]
    [MaxLength(100)]
    public string StationName { get; set; } = string.Empty;

    /// <summary>Navigation property: Cell induk dari station ini.</summary>
    [ForeignKey("CellId")]
    public Cell? Cell { get; set; }

    /// <summary>Navigation property: semua log pengujian yang tercatat di station ini.</summary>
    public ICollection<TaktLogTime> TaktLogTimes { get; set; } = new List<TaktLogTime>();
}

/// <summary>
/// Entitas master MeterType (jenis/tipe meter atau jenis pengujian).
/// Tabel database: "MeterType". Dipakai untuk membandingkan performa antar jenis produk.
/// Data dummy: 3 row (MT-A, MT-B, MT-C) — lihat Data/DummyCsv/meter_type_master.csv.
/// </summary>
[Table("MeterType")]
public sealed class MeterType
{
    /// <summary>Primary key tabel MeterType.</summary>
    [Key]
    public long Id { get; set; }

    /// <summary>Nama/jenis meter (contoh: "MT-A").</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Navigation property: semua log pengujian dengan tipe meter ini.</summary>
    public ICollection<TaktLogTime> TaktLogTimes { get; set; } = new List<TaktLogTime>();
}

/// <summary>
/// Entitas master unit terdaftar — daftar SEMUA unit yang direncanakan dites
/// (setara "work order" di MES). Tabel database: "UnitMaster".
/// Fungsi utama: membedakan unit SUDAH testes (punya log) vs BELUM dite (terdaftar tapi tidak ada log)
/// — dasar metrik TotalRegistered/Untested.
/// Data dummy: 100 row (SN-0001..SN-0100; 80 sudah dites, 20 menunggu)
/// — lihat Data/DummyCsv/unit_master.csv.
/// </summary>
[Table("UnitMaster")]
public sealed class UnitMaster
{
    /// <summary>Nomor seri unit — Primary Key (sama dengan SerialNumber di log).</summary>
    [Key]
    [MaxLength(100)]
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>Foreign key ke Cell — cell tempat unit direncanakan diproses.</summary>
    [Required]
    public long CellId { get; set; }

    /// <summary>Foreign key ke MeterType — jenis meter dari unit ini.</summary>
    [Required]
    public long MeterTypeId { get; set; }

    /// <summary>Navigation property: Cell tujuan unit.</summary>
    [ForeignKey("CellId")]
    public Cell? Cell { get; set; }

    /// <summary>Navigation property: MeterType dari unit.</summary>
    [ForeignKey("MeterTypeId")]
    public MeterType? MeterType { get; set; }
}