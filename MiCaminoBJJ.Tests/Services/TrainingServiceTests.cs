using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Infrastructure.Services;
using MiCaminoBJJ.Tests.Infrastructure;
using Moq;

namespace MiCaminoBJJ.Tests.Services;

/// <summary>
/// Integración: TrainingService sobre SQLite en memoria real.
/// Cubre creación de sesión, asociación de técnicas, persistencia,
/// asociación al usuario, técnicas inválidas e integridad transaccional.
/// </summary>
public class TrainingServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TechniquePracticeService _practiceService;

    private int _userId;
    private int _otherUserId;
    private int _activeTechniqueId;
    private int _secondTechniqueId;
    private int _inactiveTechniqueId;

    public TrainingServiceTests()
    {
        _db = new SqliteTestDatabase();
        _practiceService = new TechniquePracticeService(_db.Context, NullLogger<TechniquePracticeService>.Instance);
        Seed();
    }

    public void Dispose() => _db.Dispose();

    private TrainingService CreateSut() =>
        new(_db.Context, _practiceService, NullLogger<TrainingService>.Instance);

    private void Seed()
    {
        var user = new User
        {
            Name = "Practicante Test",
            Email = "practicante@test.com",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            BeltRank = "Blanco"
        };
        var otherUser = new User
        {
            Name = "Otro Usuario",
            Email = "otro@test.com",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            BeltRank = "Azul"
        };
        var belt = new Belt { Name = "Blanco", DisplayOrder = 1 };
        var category = new TechniqueCategory { Name = "Escape", DisplayOrder = 1 };
        var position = new Position { Name = "Montada", DisplayOrder = 1 };

        _db.Context.AddRange(user, otherUser, belt, category, position);
        _db.Context.SaveChanges();

        _userId = user.Id;
        _otherUserId = otherUser.Id;

        var active = new Technique
        {
            Name = "Escape de montada",
            Description = "Desc",
            Objective = "Obj",
            BeltId = belt.Id,
            CategoryId = category.Id,
            PositionId = position.Id,
            IsActive = true
        };
        var second = new Technique
        {
            Name = "Mata león",
            Description = "Desc",
            Objective = "Obj",
            BeltId = belt.Id,
            CategoryId = category.Id,
            PositionId = position.Id,
            IsActive = true
        };
        var inactive = new Technique
        {
            Name = "Técnica inactiva",
            Description = "Desc",
            Objective = "Obj",
            BeltId = belt.Id,
            CategoryId = category.Id,
            PositionId = position.Id,
            IsActive = false
        };

        _db.Context.AddRange(active, second, inactive);
        _db.Context.SaveChanges();

        _activeTechniqueId = active.Id;
        _secondTechniqueId = second.Id;
        _inactiveTechniqueId = inactive.Id;
    }

    private static TrainingWithTechniques BuildInput(params int[] techniqueIds)
    {
        var input = new TrainingWithTechniques
        {
            TrainingType = TrainingType.Gi,
            Date = DateTime.Today.AddDays(-1),
            DurationMinutes = 90,
            Feeling = Feeling.Buena,
            Notes = "Buena sesión de sparring"
        };

        foreach (var id in techniqueIds)
        {
            input.Techniques.Add(new TechniquePracticeInput
            {
                TechniqueId = id,
                Repetitions = 12,
                DurationMinutes = 8,
                Confidence = ConfidenceLevel.Media,
                Notes = "Trabajado con detalle"
            });
        }

        return input;
    }

    [Fact]
    public async Task CreateAsync_CreaSesionYAsociaTecnicas()
    {
        var sut = CreateSut();

        var session = await sut.CreateAsync(_userId, BuildInput(_activeTechniqueId, _secondTechniqueId));

        session.Id.Should().BeGreaterThan(0);
        session.UserId.Should().Be(_userId);

        using var verify = _db.CreateContext();
        var persisted = await verify.TrainingSessions
            .Include(s => s.Practices)
            .FirstAsync(s => s.Id == session.Id);

        persisted.Practices.Should().HaveCount(2);
        persisted.Practices.Select(p => p.TechniqueId)
            .Should().BeEquivalentTo(new[] { _activeTechniqueId, _secondTechniqueId });
    }

    [Fact]
    public async Task CreateAsync_SinTecnicas_CreaSesionVacia()
    {
        var sut = CreateSut();

        var session = await sut.CreateAsync(_userId, BuildInput());

        session.Id.Should().BeGreaterThan(0);

        using var verify = _db.CreateContext();
        var persisted = await verify.TrainingSessions
            .Include(s => s.Practices)
            .FirstAsync(s => s.Id == session.Id);

        persisted.Practices.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_PersisteNotasYCantidades()
    {
        var sut = CreateSut();
        var input = BuildInput(_activeTechniqueId);
        input.DurationMinutes = 75;
        input.Feeling = Feeling.Dificil;
        input.Notes = "Sesión difícil, mucho cardio";
        input.Techniques[0].Repetitions = 25;
        input.Techniques[0].DurationMinutes = 15;
        input.Techniques[0].Confidence = ConfidenceLevel.Alta;
        input.Techniques[0].Notes = "Mejoró la finalización";

        await sut.CreateAsync(_userId, input);

        using var verify = _db.CreateContext();
        var persisted = await verify.TrainingSessions
            .Include(s => s.Practices)
            .FirstAsync(s => s.UserId == _userId);

        persisted.DurationMinutes.Should().Be(75);
        persisted.Feeling.Should().Be(Feeling.Dificil);
        persisted.Notes.Should().Be("Sesión difícil, mucho cardio");

        var practice = persisted.Practices.Single();
        practice.Repetitions.Should().Be(25);
        practice.DurationMinutes.Should().Be(15);
        practice.ConfidenceLevel.Should().Be(ConfidenceLevel.Alta);
        practice.Notes.Should().Be("Mejoró la finalización");
        practice.UserId.Should().Be(_userId);
    }

    [Fact]
    public async Task CreateAsync_AsociaLaSesionAlUsuarioIndicado()
    {
        var sut = CreateSut();

        await sut.CreateAsync(_userId, BuildInput(_activeTechniqueId));
        await sut.CreateAsync(_otherUserId, BuildInput(_activeTechniqueId));

        using var verify = _db.CreateContext();
        var userSessions = await verify.TrainingSessions
            .Where(s => s.UserId == _userId)
            .ToListAsync();

        userSessions.Should().HaveCount(1);
        userSessions[0].UserId.Should().Be(_userId);
    }

    // CAMBIO DE REGLA DE NEGOCIO (auditoría de cierre de Etapa 2):
    // antes las técnicas inválidas se omitían en silencio; ahora el guardado completo
    // se rechaza informando qué técnica falló, para que el usuario no crea que guardó N
    // técnicas cuando el sistema persistió N-1.
    [Fact]
    public async Task CreateAsync_TecnicaInactiva_RechazaElGuardadoCompleto()
    {
        var sut = CreateSut();

        var act = async () => await sut.CreateAsync(_userId, BuildInput(_inactiveTechniqueId));

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("inactiva");

        using var verify = _db.CreateContext();
        (await verify.TrainingSessions.CountAsync()).Should().Be(0, "no debe guardarse una sesión con técnica inactiva");
        (await verify.TechniquePractices.CountAsync()).Should().Be(0, "no debe guardarse ninguna práctica");
    }

    [Fact]
    public async Task CreateAsync_TecnicaInexistente_RechazaElGuardadoCompleto()
    {
        var sut = CreateSut();

        var act = async () => await sut.CreateAsync(_userId, BuildInput(999999));

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("999999");

        using var verify = _db.CreateContext();
        (await verify.TrainingSessions.CountAsync()).Should().Be(0, "no debe guardarse una sesión con técnica inexistente");
        (await verify.TechniquePractices.CountAsync()).Should().Be(0, "no debe guardarse ninguna práctica");
    }

    [Fact]
    public async Task CreateAsync_FechaFutura_EsRechazada()
    {
        var sut = CreateSut();
        var input = BuildInput();
        input.Date = DateTime.Today.AddDays(1);

        var act = async () => await sut.CreateAsync(_userId, input);

        var ex = await act.Should().ThrowAsync<ArgumentException>();
        ex.Which.Message.Should().Contain("futura");

        using var verify = _db.CreateContext();
        (await verify.TrainingSessions.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(601)]
    [InlineData(1200)]
    public async Task CreateAsync_DuracionFueraDeRango_EsRechazada(int duration)
    {
        var sut = CreateSut();
        var input = BuildInput();
        input.DurationMinutes = duration;

        var act = async () => await sut.CreateAsync(_userId, input);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();

        using var verify = _db.CreateContext();
        (await verify.TrainingSessions.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public async Task CreateAsync_RepeticionesInvalidas_EsRechazadas(int repetitions)
    {
        var sut = CreateSut();
        var input = BuildInput(_activeTechniqueId);
        input.Techniques[0].Repetitions = repetitions;

        var act = async () => await sut.CreateAsync(_userId, input);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();

        using var verify = _db.CreateContext();
        (await verify.TrainingSessions.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(601)]
    public async Task CreateAsync_MinutosFueraDeRango_EsRechazados(int minutes)
    {
        var sut = CreateSut();
        var input = BuildInput(_activeTechniqueId);
        input.Techniques[0].DurationMinutes = minutes;

        var act = async () => await sut.CreateAsync(_userId, input);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();

        using var verify = _db.CreateContext();
        (await verify.TrainingSessions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_TecnicaDuplicada_EsRechazada()
    {
        var sut = CreateSut();

        var act = async () => await sut.CreateAsync(_userId, BuildInput(_activeTechniqueId, _activeTechniqueId));

        var ex = await act.Should().ThrowAsync<ArgumentException>();
        ex.Which.Message.Should().Contain("duplicada");

        using var verify = _db.CreateContext();
        (await verify.TrainingSessions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_CuandoFallaUnaPractice_HaceRollbackCompleto()
    {
        var failingPracticeService = new Mock<ITechniquePracticeService>();
        failingPracticeService
            .SetupSequence(x => x.RegisterPracticeAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<ConfidenceLevel>(),
                It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new TechniquePractice())
            .ThrowsAsync(new InvalidOperationException("Fallo simulado de práctica"));

        var sut = new TrainingService(
            _db.Context,
            failingPracticeService.Object,
            NullLogger<TrainingService>.Instance);

        var act = async () => await sut.CreateAsync(_userId, BuildInput(_activeTechniqueId, _secondTechniqueId));

        await act.Should().ThrowAsync<InvalidOperationException>();

        using var verify = _db.CreateContext();
        (await verify.TrainingSessions.CountAsync()).Should().Be(0, "la sesión debe revertirse");
        (await verify.TechniquePractices.CountAsync()).Should().Be(0, "las prácticas deben revertirse");
    }

    [Fact]
    public async Task GetByUserAsync_SoloDevuelveSesionesDelUsuario()
    {
        var sut = CreateSut();
        await sut.CreateAsync(_userId, BuildInput(_activeTechniqueId));
        await sut.CreateAsync(_userId, BuildInput());
        await sut.CreateAsync(_otherUserId, BuildInput(_activeTechniqueId));

        var mine = await sut.GetByUserAsync(_userId);

        mine.Should().HaveCount(2);
        mine.Should().OnlyContain(s => s.UserId == _userId);
    }

    [Fact]
    public async Task GetTotalCountAsync_CuentaSoloSesionesDelUsuario()
    {
        var sut = CreateSut();
        await sut.CreateAsync(_userId, BuildInput());
        await sut.CreateAsync(_otherUserId, BuildInput());
        await sut.CreateAsync(_otherUserId, BuildInput());

        var total = await sut.GetTotalCountAsync(_userId);

        total.Should().Be(1);
    }

    [Fact]
    public async Task GetByIdAsync_UsuarioObtieneSuPropiaSesion()
    {
        var sut = CreateSut();
        var created = await sut.CreateAsync(_userId, BuildInput(_activeTechniqueId));

        var found = await sut.GetByIdAsync(_userId, created.Id);

        found.Should().NotBeNull();
        found!.Id.Should().Be(created.Id);
        found.UserId.Should().Be(_userId);
    }

    [Fact]
    public async Task GetByIdAsync_UsuarioNoPuedeObtenerSesionAjena()
    {
        var sut = CreateSut();
        var created = await sut.CreateAsync(_userId, BuildInput(_activeTechniqueId));

        var foreign = await sut.GetByIdAsync(_otherUserId, created.Id);

        foreign.Should().BeNull("la sesión pertenece a otro usuario");
    }

    [Fact]
    public async Task GetByIdAsync_SesionInexistente_RetornaNull()
    {
        var sut = CreateSut();

        var missing = await sut.GetByIdAsync(_userId, 999999);

        missing.Should().BeNull();
    }
}
