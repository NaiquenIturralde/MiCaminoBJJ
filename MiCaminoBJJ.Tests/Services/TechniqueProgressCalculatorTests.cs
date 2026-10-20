using FluentAssertions;
using MiCaminoBJJ.Core.Services;

namespace MiCaminoBJJ.Tests.Services;

public class TechniqueProgressCalculatorTests
{
    private readonly TechniqueProgressCalculator _sut;

    public TechniqueProgressCalculatorTests()
    {
        _sut = new TechniqueProgressCalculator();
    }

    [Fact]
    public void Calculate_NoSessions_ReturnsZero()
    {
        // Act
        var result = _sut.Calculate(0, 0, 0, 0.0, false);

        // Assert
        result.Should().Be(0.0);
    }

    [Fact]
    public void Calculate_Dominated_Returns100()
    {
        // Act
        var result = _sut.Calculate(5, 50, 60, 2.0, true);

        // Assert
        result.Should().Be(100.0);
    }

    [Fact]
    public void Calculate_ReturnsValueBetween0And100()
    {
        // Act
        var result = _sut.Calculate(10, 100, 120, 1.5, false);

        // Assert
        result.Should().BeInRange(0.0, 100.0);
    }

    [Theory]
    [InlineData(1, 0, 0, 0.0, 15.0)]     // 1 session = 15 base
    [InlineData(3, 0, 0, 0.0, 40.0)]     // 3 sessions = 40 (capped)
    [InlineData(0, 10, 0, 0.0, 0.0)]     // 0 sessions, 10 reps = 0 (no base)
    [InlineData(1, 5, 10, 1.0, 36.0)]    // 15(base) + 10(rep=5*2) + 1(time=10/10=1) + 10(conf=1*10) = 36
    [InlineData(10, 50, 300, 2.0, 99.0)] // Capped at 99
    public void Calculate_VariousInputs_ReturnsExpectedValue(
        int sessions, int reps, int minutes, double confidence, double expected)
    {
        // Act
        var result = _sut.Calculate(sessions, reps, minutes, confidence, false);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Calculate_Confidence0_ReturnsCorrectValue()
    {
        // Arrange: 1 session, 0 reps, 0 min, confidence 0
        // Expected: Min(1*15, 40) + 0 + 0 + 0 = 15
        // Act
        var result = _sut.Calculate(1, 0, 0, 0.0, false);

        // Assert
        result.Should().Be(15.0);
    }

    [Fact]
    public void Calculate_MaxSessions_ReturnsCappedValue()
    {
        // Arrange: 10 sessions, 0 reps, 10 min, confidence 0
        // Expected: 40 (capped) + 0 + Min(10/10, 20) + 0 = 40 + 1 = 41
        var result = _sut.Calculate(10, 0, 10, 0.0, false);

        result.Should().Be(41.0);
    }

    [Fact]
    public void Calculate_DominatedOverridesAllInput()
    {
        // Even with zero sessions, dominated = 100
        var result = _sut.Calculate(0, 0, 0, 0.0, true);

        result.Should().Be(100.0);
    }
}
