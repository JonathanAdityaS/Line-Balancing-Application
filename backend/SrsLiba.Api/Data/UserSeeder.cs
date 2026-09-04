// ============================================================
// UserSeeder — Membuat tabel AppUser bila belum ada (untuk app.db lama
// yang dibuat sebelum fitur operator) lalu mengisi 3 akun bawaan:
// admin/admin (role admin), user/user (role user),
// operator/operator (role user + IsOperator, Cell 1, belum konfirmasi).
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

        await EnsureUserAsync(db, "admin", "admin", "admin", isOperator: false, assignedCellId: null, confirmed: true, ct);
        await EnsureUserAsync(db, "user", "user", "user", isOperator: false, assignedCellId: null, confirmed: true, ct);
        await EnsureUserAsync(db, "operator", "operator", "user", isOperator: true, assignedCellId: 1, confirmed: false, ct);

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
