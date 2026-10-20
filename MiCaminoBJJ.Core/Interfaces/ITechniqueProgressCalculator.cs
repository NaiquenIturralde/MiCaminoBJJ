namespace MiCaminoBJJ.Core.Interfaces;

public interface ITechniqueProgressCalculator
{
    double Calculate(int totalSessions, int totalRepetitions, int totalMinutes, double averageConfidence, bool isDominated);
}
