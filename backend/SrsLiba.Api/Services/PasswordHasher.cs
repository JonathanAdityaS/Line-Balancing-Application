// ============================================================
// PasswordHasher — Hash & verifikasi password pakai PBKDF2-SHA256.
// Tidak memerlukan package eksternal (semua bawaan .NET runtime).
// Format hash: {iterations}.{salt}.{hash} (semua base64).
// ============================================================

using System.Security.Cryptography;

namespace SrsLiba.Api.Services;

public static class PasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;   // 128 bit
    private const int HashSize = 32;   // 256 bit

    /// <summary>Hash password → string tersimpan.</summary>
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        // Format: "iterations.{base64 salt}.{base64 hash}"
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    /// <summary>Verifikasi password terhadap hash tersimpan.</summary>
    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations)) return false;

        var salt = Convert.FromBase64String(parts[1]);
        var expectedHash = Convert.FromBase64String(parts[2]);

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);

        // Constant-time compare agar tidak vulnerable timing attack
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
