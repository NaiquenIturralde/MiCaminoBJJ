using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Web.Persistence;
using MiCaminoBJJ.Web.ViewModels.Admin;
using Microsoft.EntityFrameworkCore;

namespace MiCaminoBJJ.Web.Services.Admin;

/// <summary>
/// Implementación SQL Server de las consultas multimedia administrativas:
/// <see cref="WebTfDbContext"/> con AsNoTracking, sobre los MISMOS
/// <c>MediaAsset</c> + <c>TechniqueMedia</c> que usa la ficha pública.
/// El mapeo a ViewModel delega en <see cref="AdminMediaRecursoViewModel.Desde"/>
/// con el parser compartido de YouTube/Vimeo.
/// </summary>
public sealed class AdminTecnicasMediaQueryService : IAdminTecnicasMediaQueryService
{
    private readonly WebTfDbContext _db;
    private readonly IExternalMediaParser _parser;

    public AdminTecnicasMediaQueryService(WebTfDbContext db, IExternalMediaParser parser)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public async Task<IReadOnlyList<AdminMediaRecursoViewModel>> ObtenerMultimediaAsync(int tecnicaId)
    {
        var vinculos = await _db.TechniqueMedia
            .AsNoTracking()
            .Include(tm => tm.MediaAsset)
            .Where(tm => tm.TechniqueId == tecnicaId &&
                         tm.MediaAsset != null &&
                         !tm.MediaAsset.IsDeleted)
            .OrderByDescending(tm => tm.IsCover)
            .ThenBy(tm => tm.DisplayOrder)
            .ThenBy(tm => tm.Id)
            .ToListAsync();

        return vinculos
            .Select(tm => AdminMediaRecursoViewModel.Desde(tm, _parser))
            .ToList();
    }
}
