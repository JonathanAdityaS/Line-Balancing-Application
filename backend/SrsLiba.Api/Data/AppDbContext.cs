// ============================================================
// AppDbContext — Jembatan aplikasi ke database SQLite (EF Core).
// Mendefinisikan: 4 DbSet (tabel), mapping nama tabel,
// relasi foreign key, dan index untuk mempercepat query KPI.
// ============================================================

using Microsoft.EntityFrameworkCore;
using SrsLiba.Api.Models;

namespace SrsLiba.Api.Data;

/// <summary>
/// DbContext utama aplikasi — mewakili database SQLite lokal (app.db).
/// Database ini menyimpan data dummy hasil seed CSV + konfigurasi aplikasi.
/// BUKAN database produksi existing (yang itu read-only via MSSQL).
/// </summary>
public sealed class AppDbContext : DbContext
{
    /// <summary>Injeksi options (connection string) dari DI container.</summary>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // --- DbSet: satu property = satu tabel di database ---

    /// <summary>Tabel master Cell (4 row dummy).</summary>
    public DbSet<Cell> Cells => Set<Cell>();

    /// <summary>Tabel master Station (20 row dummy, 5 per cell).</summary>
    public DbSet<Station> Stations => Set<Station>();

    /// <summary>Tabel master MeterType (3 row dummy).</summary>
    public DbSet<MeterType> MeterTypes => Set<MeterType>();

    /// <summary>Tabel master unit terdaftar (100 row dummy) — 80 sudah dites, 20 menunggu.</summary>
    public DbSet<UnitMaster> UnitMasters => Set<UnitMaster>();

    /// <summary>Tabel log utama TaktLogTime (360 row dummy) — sumber semua KPI.</summary>
    public DbSet<TaktLogTime> TaktLogTimes => Set<TaktLogTime>();

    /// <summary>
    /// Konfigurasi mapping entity → tabel database:
    /// nama tabel, panjang kolom, relasi FK, dan index.
    /// Index penting agar filter/agregasi KPI tidak full-table-scan.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // --- Tabel Cell ---
        modelBuilder.Entity<Cell>(entity =>
        {
            entity.ToTable("Cell");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CellName).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.CellName); // Percepat filter berdasarkan nama cell
        });

        // --- Tabel Station (child dari Cell) ---
        modelBuilder.Entity<Station>(entity =>
        {
            entity.ToTable("Station");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StationName).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.CellId);      // Percepat lookup station per cell
            entity.HasIndex(x => x.StationName); // Percepat filter nama station
            // Relasi: banyak Station milik satu Cell
            entity.HasOne(x => x.Cell)
                .WithMany(x => x.Stations)
                .HasForeignKey(x => x.CellId);
        });

        // --- Tabel MeterType ---
        modelBuilder.Entity<MeterType>(entity =>
        {
            entity.ToTable("MeterType");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.Name); // Percepat filter nama meter type
        });

        // --- Tabel UnitMaster (unit terdaftar / work order) ---
        modelBuilder.Entity<UnitMaster>(entity =>
        {
            entity.ToTable("UnitMaster");
            // PK = SerialNumber (bukan auto increment) — SN unik per unit fisik
            entity.HasKey(x => x.SerialNumber);
            entity.Property(x => x.SerialNumber).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.CellId);      // Percepat hitung terdaftar per cell
            entity.HasIndex(x => x.MeterTypeId); // Percepat hitung terdaftar per meter type
            // Relasi: unit terdaftar milik satu Cell
            entity.HasOne(x => x.Cell)
                .WithMany()
                .HasForeignKey(x => x.CellId);
            // Relasi: unit terdaftar punya satu MeterType
            entity.HasOne(x => x.MeterType)
                .WithMany()
                .HasForeignKey(x => x.MeterTypeId);
        });

        // --- Tabel TaktLogTime (fakta utama) ---
        modelBuilder.Entity<TaktLogTime>(entity =>
        {
            entity.ToTable("TaktLogTime");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SerialNumber).HasMaxLength(100).IsRequired();

            // Index untuk mempercepat filter & agregasi KPI:
            entity.HasIndex(x => x.StationId);                  // Filter per station
            entity.HasIndex(x => x.MeterTypeId);                // Filter per meter type
            entity.HasIndex(x => x.StartTime);                  // Filter rentang tanggal
            entity.HasIndex(x => x.EndTime);                    // Filter rentang tanggal
            entity.HasIndex(x => new { x.StationId, x.StartTime }); // Composite: sort waiting time per station

            // Relasi: banyak log milik satu Station
            entity.HasOne(x => x.Station)
                .WithMany(x => x.TaktLogTimes)
                .HasForeignKey(x => x.StationId);
            // Relasi: banyak log milik satu MeterType
            entity.HasOne(x => x.MeterType)
                .WithMany(x => x.TaktLogTimes)
                .HasForeignKey(x => x.MeterTypeId);
        });
    }
}
