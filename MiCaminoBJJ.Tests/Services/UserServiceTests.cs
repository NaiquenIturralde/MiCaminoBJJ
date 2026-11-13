using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Infrastructure.Services;
using MiCaminoBJJ.Tests.Infrastructure;

namespace MiCaminoBJJ.Tests.Services;

/// <summary>
/// Integración: UserService sobre SQLite real en memoria.
/// Cubre registro, normalización del email, duplicados insensibles a
/// mayúsculas/minúsculas y validación de credenciales del login.
/// </summary>
public class UserServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _db;

    public UserServiceTests()
    {
        _db = new SqliteTestDatabase();
    }

    public void Dispose() => _db.Dispose();

    private UserService CreateSut() =>
        new(_db.Context, new PasswordService(), NullLogger<UserService>.Instance);

    [Fact]
    public async Task CreateAsync_PersisteUsuarioConRolPracticanteYSettings()
    {
        var sut = CreateSut();

        var user = await sut.CreateAsync(
            "  Ana Gómez  ", "Ana.Gomez@Example.COM ", "Secreta123!", "Azul", "Academia Test");

        user.Id.Should().BeGreaterThan(0);
        user.Name.Should().Be("Ana Gómez");
        user.Email.Should().Be("ana.gomez@example.com");
        user.Role.Should().Be(UserRole.Practicante);
        user.PasswordHash.Should().NotBeNullOrWhiteSpace();
        user.PasswordSalt.Should().NotBeNullOrWhiteSpace();

        var persisted = await sut.GetByIdAsync(user.Id);
        persisted.Should().NotBeNull();
        persisted!.Settings.Should().NotBeNull();
    }

    [Theory]
    [InlineData("ana@example.com")]
    [InlineData("ANA@EXAMPLE.COM")]
    [InlineData("Ana@Example.Com")]
    [InlineData("  ana@example.com  ")]
    public async Task EmailExistsAsync_DetectaDuplicadosSinDistinguirMayusculas(string candidate)
    {
        var sut = CreateSut();
        await sut.CreateAsync("Ana", "Ana@Example.com", "Secreta123!", "Blanco", null);

        (await sut.EmailExistsAsync(candidate)).Should().BeTrue();
    }

    [Fact]
    public async Task EmailExistsAsync_DevuelveFalseParaCorreoDistinto()
    {
        var sut = CreateSut();
        await sut.CreateAsync("Ana", "ana@example.com", "Secreta123!", "Blanco", null);

        (await sut.EmailExistsAsync("otra@example.com")).Should().BeFalse();
    }

    [Theory]
    [InlineData("ana@example.com")]
    [InlineData("ANA@EXAMPLE.COM")]
    [InlineData(" Ana@Example.Com ")]
    public async Task ValidateCredentialsAsync_AceptaEmailConCualquierCasing(string email)
    {
        var sut = CreateSut();
        await sut.CreateAsync("Ana", "ana@example.com", "Secreta123!", "Blanco", null);

        (await sut.ValidateCredentialsAsync(email, "Secreta123!")).Should().BeTrue();
    }

    [Fact]
    public async Task ValidateCredentialsAsync_RechazaPasswordIncorrectaYEmailInexistente()
    {
        var sut = CreateSut();
        await sut.CreateAsync("Ana", "ana@example.com", "Secreta123!", "Blanco", null);

        (await sut.ValidateCredentialsAsync("ana@example.com", "OtraClave1!")).Should().BeFalse();
        (await sut.ValidateCredentialsAsync("nadie@example.com", "Secreta123!")).Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_ConEmailDuplicadoPorCasing_LanzaExcepcionDeUnicidad()
    {
        var sut = CreateSut();
        await sut.CreateAsync("Ana", "ana@example.com", "Secreta123!", "Blanco", null);

        // La normalización hace que ambos correos colisionen en el índice único.
        var act = async () => await sut.CreateAsync("Ana 2", "ANA@EXAMPLE.COM", "Secreta123!", "Azul", null);

        await act.Should().ThrowAsync<Microsoft.EntityFrameworkCore.DbUpdateException>();
    }
}
