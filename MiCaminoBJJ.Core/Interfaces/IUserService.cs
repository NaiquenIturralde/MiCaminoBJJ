using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Core.Interfaces;

public interface IUserService
{
    Task<User?> GetByIdAsync(int userId);
    Task<User?> GetByEmailAsync(string email);
    Task<bool> EmailExistsAsync(string email);
    Task<User> CreateAsync(string name, string email, string password, string beltRank, string? academy);
    Task UpdateAsync(User user);
    Task<bool> ValidateCredentialsAsync(string email, string password);
}
