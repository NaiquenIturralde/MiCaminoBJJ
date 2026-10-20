namespace MiCaminoBJJ.Core.Interfaces;

public interface IPasswordHasher
{
    string HashPassword(string password, out string salt);
    string HashPassword(string password, string salt);
    bool VerifyPassword(string password, string hash, string salt);
}
