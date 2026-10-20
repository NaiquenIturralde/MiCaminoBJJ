using System.Security.Cryptography;
using MiCaminoBJJ.Core.Interfaces;

namespace MiCaminoBJJ.Infrastructure.Services;

public class PasswordService : IPasswordHasher
{
    private const int SaltSize = 32;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public string HashPassword(string password, out string salt)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        salt = Convert.ToBase64String(saltBytes);
        return HashPassword(password, salt);
    }

    public string HashPassword(string password, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);
        return Convert.ToBase64String(hash);
    }

    public bool VerifyPassword(string password, string hash, string salt)
    {
        var computedHash = HashPassword(password, salt);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(computedHash),
            Convert.FromBase64String(hash));
    }
}
