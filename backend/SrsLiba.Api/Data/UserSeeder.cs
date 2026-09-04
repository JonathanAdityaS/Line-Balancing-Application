// ============================================================
// UserSeeder — Membuat tabel AppUser bila belum ada (untuk app.db lama
// yang dibuat sebelum fitur operator) lalu mengisi 2 akun bawaan:
// admin/admin (role admin) dan operator/operator (role operator,
// Cell 1, belum konfirmasi). Konsep "user" umum sudah dihapus.
// Idempotent: tidak menambah duplikat bila akun sudah ada.
// ============================================================

using Microsoft.EntityFrameworkCore;
using SrsLiba.Api.Models;
using SrsLiba.Api.Services;

namespace SrsLiba.Api.Data;

/// <summary>Seeder akun pengguna lokal.</summary>
public static class UserSeeder
{
    /// <summary>
    /// Pastikan tabel AppUser ada lalu isi akun bawaan yang belum ada.
    /// Dipanggil dari Program.cs setiap startup (aman diulang).
    /// </summary>
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        // app.db lama (dibuat via EnsureCreated sebelum fitur ini ada)
        // tidak punya tabel AppUser — buat manual agar query tidak gagal.
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "AppUser" (
                "Username" TEXT NOT NULL CONSTRAINT "PK_AppUser" PRIMARY KEY,
                "PasswordHash" TEXT NOT NULL,
                "Role" TEXT NOT NULL,
                "IsOperator" INTEGER NOT NULL,
                "AssignedCellId" INTEGER NULL,
                "IdentityConfirmed" INTEGER NOT NULL,
                "ConfirmedAt" TEXT NULL
            )
            """, ct);

        // Migrasi app.db lama: hapus akun demo "user" (hanya bila password-nya
        // masih bawaan, agar akun sungguhan tidak ikut terhapus) dan ubah
        // role operator lama ("user" → "operator").
        var legacyUser = await db.Users.FindAsync(new object[] { "user" }, ct);
        if (legacyUser is not null && !legacyUser.IsOperator
            && PasswordHasher.Verify("user", legacyUser.PasswordHash))
        {
            db.Users.Remove(legacyUser);
        }
        var legacyOperator = await db.Users.FindAsync(new object[] { "operator" }, ct);
        if (legacyOperator is not null && legacyOperator.IsOperator && legacyOperator.Role == "user")
        {
            legacyOperator.Role = "operator";
        }

        await EnsureUserAsync(db, "admin", "admin", "admin", isOperator: false, assignedCellId: null, confirmed: true, ct);
        await EnsureUserAsync(db, "operator", "operator", "operator", isOperator: true, assignedCellId: 1, confirmed: false, ct);

        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureUserAsync(
        AppDbContext db, string username, string password, string role,
        bool isOperator, long? assignedCellId, bool confirmed, CancellationToken ct)
    {
        var existing = await db.Users.FindAsync(new object[] { username }, ct);
        if (existing is not null) return;

        db.Users.Add(new AppUser
        {
            Username = username,
            PasswordHash = PasswordHasher.Hash(password),
            Role = role,
            IsOperator = isOperator,
            AssignedCellId = assignedCellId,
            IdentityConfirmed = confirmed,
            ConfirmedAt = confirmed ? DateTime.UtcNow : null
        });
    }
}
