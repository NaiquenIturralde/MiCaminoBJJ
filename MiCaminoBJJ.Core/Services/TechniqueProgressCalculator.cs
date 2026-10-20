using MiCaminoBJJ.Core.Interfaces;

namespace MiCaminoBJJ.Core.Services;

public class TechniqueProgressCalculator : ITechniqueProgressCalculator
{
    /// <summary>
    /// Calcula el progreso de una técnica basado en:
    /// - Sesiones realizadas (hasta 40 pts)
    /// - Repeticiones totales (hasta 30 pts)
    /// - Minutos totales practicados (hasta 20 pts)
    /// - Confianza promedio (hasta 10 pts)
    /// 
    /// Si la técnica está dominada, devuelve 100.
    /// Sin prácticas, devuelve 0.
    /// </summary>
    public double Calculate(int totalSessions, int totalRepetitions, int totalMinutes, double averageConfidence, bool isDominated)
    {
        if (isDominated)
            return 100.0;

        if (totalSessions <= 0)
            return 0.0;

        var baseScore = Math.Min(totalSessions * 15.0, 40.0);
        var repScore = Math.Min(totalRepetitions * 2.0, 30.0);
        var timeScore = Math.Min(totalMinutes / 10.0, 20.0);
        var confScore = Math.Min(averageConfidence * 10.0, 10.0);

        return Math.Min(baseScore + repScore + timeScore + confScore, 99.0);
    }
}
