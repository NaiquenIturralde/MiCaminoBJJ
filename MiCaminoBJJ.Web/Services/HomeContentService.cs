using MiCaminoBJJ.Web.Localization;
using MiCaminoBJJ.Web.ViewModels;
using Microsoft.Extensions.Localization;

namespace MiCaminoBJJ.Web.Services;

/// <summary>
/// Contenido estático del landing de inicio (Etapa 0 Web).
///
/// Singleton, sin dependencias de EF Core: sirve para dejar resueltas las
/// carpetas de ViewModels/Servicios y el pipeline de DI sin riesgo sobre la
/// base SQLite existente.
///
/// Los textos se resuelven con <c>IStringLocalizer</c> en CADA petición
/// (el localizador lee <c>CultureInfo.CurrentUICulture</c>), de modo que el
/// landing se muestra en el idioma elegido por el visitante.
/// </summary>
public sealed class HomeContentService : IHomeContentService
{
    private readonly IStringLocalizer<UiResources> _localizador;

    public HomeContentService(IStringLocalizer<UiResources> localizador)
    {
        _localizador = localizador ?? throw new ArgumentNullException(nameof(localizador));
    }

    public HomeIndexViewModel GetLandingContent() => new()
    {
        Title = _localizador["Home.Title"],
        Eyebrow = _localizador["Home.Eyebrow"],
        Description = _localizador["Home.Description"],

        Features = new[]
        {
            new FeatureCardViewModel
            {
                Icon = "book-open",
                Title = _localizador["Home.Card1.Titulo"],
                Badge = _localizador["Common.Disponible"],
                Description = _localizador["Home.Card1.Descripcion"]
            },
            // Etapa 1 (auditoría de cierre TFI): "Disponible" SOLO para lo que
            // existe hoy en Web; el resto se comunica honestamente como
            // "Próximamente" y estas tarjetas no enlazan a rutas inexistentes.
            new FeatureCardViewModel
            {
                Icon = "calendar",
                Title = _localizador["Home.Card2.Titulo"],
                Badge = _localizador["Common.Proximamente"],
                Description = _localizador["Home.Card2.Descripcion"]
            },
            new FeatureCardViewModel
            {
                Icon = "trending-up",
                Title = _localizador["Home.Card3.Titulo"],
                Badge = _localizador["Common.Proximamente"],
                Description = _localizador["Home.Card3.Descripcion"]
            },
            new FeatureCardViewModel
            {
                Icon = "bookmark",
                Title = _localizador["Home.Card4.Titulo"],
                Badge = _localizador["Common.Proximamente"],
                Description = _localizador["Home.Card4.Descripcion"]
            },
            new FeatureCardViewModel
            {
                Icon = "search",
                Title = _localizador["Home.Card5.Titulo"],
                Badge = _localizador["Common.Disponible"],
                Description = _localizador["Home.Card5.Descripcion"]
            },
            new FeatureCardViewModel
            {
                Icon = "shield",
                Title = _localizador["Home.Card6.Titulo"],
                Badge = _localizador["Common.Disponible"],
                Description = _localizador["Home.Card6.Descripcion"]
            }
        },

        Pillars = new[]
        {
            new FeatureCardViewModel
            {
                Icon = "smartphone",
                Title = _localizador["Home.Pillar1.Titulo"],
                Description = _localizador["Home.Pillar1.Descripcion"]
            },
            new FeatureCardViewModel
            {
                Icon = "globe",
                Title = _localizador["Home.Pillar2.Titulo"],
                Description = _localizador["Home.Pillar2.Descripcion"]
            },
            new FeatureCardViewModel
            {
                Icon = "layers",
                Title = _localizador["Home.Pillar3.Titulo"],
                Description = _localizador["Home.Pillar3.Descripcion"]
            }
        }
    };
}
