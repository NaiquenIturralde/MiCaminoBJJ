using System.Text.Json;
using FluentAssertions;
using MiCaminoBJJ.Web.Services;
using MiCaminoBJJ.Web.Services.Recorrido;
using Xunit;

namespace MiCaminoBJJ.Web.Tests.Recorrido;

/// <summary>
/// Etapa 6A — pruebas UNITARIAS del catálogo del cinturón blanco.
/// Son puras (sin base de datos, sin HTTP): validan la estructura pedagógica,
/// los videos autorizados y que las fichas NO dupliquen ni reetiqueten
/// técnicas existentes de la biblioteca.
/// </summary>
public class CinturonBlancoCatalogoTests
{
    /// <summary>Los ÚNICOS videos autorizados para esta etapa (los aportó la usuaria).</summary>
    private static readonly string[] VideosPermitidos =
    {
        "ynSK1oYc_gg", // Escape de montada con codos (Shorts)
        "eDUjKxV6wb0", // Escape de control lateral recuperando guardia
        "yLGmZxSf2Vo", // Escape de armbar hacia pase de guardia
        "sCmv4Jc0qlU"  // Recuperación de guardia desde media guardia
    };

    private static readonly string[] SlugsModulos =
    {
        "primeros-pasos",
        "fundamentos-defensivos",
        "supervivencia-en-montada",
        "supervivencia-en-control-lateral",
        "escapes-basicos"
    };

    // ── Encabezado ────────────────────────────────────────────────────────

    [Fact]
    public void Encabezado_TitulosYDescripcion_LosQuePideElRecorrido()
    {
        CinturonBlancoCatalogo.Titulo.Should().Be("Cinturón Blanco");
        CinturonBlancoCatalogo.Subtitulo.Should().Be("Supervivencia y defensa básica");
        CinturonBlancoCatalogo.Descripcion.Should().Contain("Todo camino comienza con una base sólida");
        CinturonBlancoCatalogo.Descripcion.Should().Contain("Antes de atacar, aprendé a sobrevivir");
        CinturonBlancoCatalogo.Introduccion.Should().Contain("paciencia");
        CinturonBlancoCatalogo.Introduccion.Should().Contain("supervisada");
    }

    [Fact]
    public void Concepto_SupervivenciaYEscape_EstanDiferenciados()
    {
        CinturonBlancoCatalogo.NotaSupervivenciaVsEscape.Should().Contain("SUPERVIVENCIA");
        CinturonBlancoCatalogo.NotaSupervivenciaVsEscape.Should().Contain("ESCAPE");
        CinturonBlancoCatalogo.NotaSupervivenciaVsEscape.Should().Contain("No son lo mismo");
    }

    // ── Módulos ───────────────────────────────────────────────────────────

    [Fact]
    public void Modulos_SonCinco_EnOrdenDelRecorrido()
    {
        CinturonBlancoCatalogo.Modulos.Should().HaveCount(5);
        CinturonBlancoCatalogo.Modulos.Select(m => m.Slug).Should().Equal(SlugsModulos);
        CinturonBlancoCatalogo.Modulos.Select(m => m.Id).Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void Modulos_PrimerosCuatro_SonTeoricos_SinFichasPropias_YVideoPendiente()
    {
        foreach (var modulo in CinturonBlancoCatalogo.Modulos.Take(4))
        {
            modulo.Fichas.Should().BeEmpty($"el módulo {modulo.Slug} es teórico");
            modulo.Puntos.Should().NotBeEmpty($"el módulo {modulo.Slug} debe tener contenido");
            modulo.EstadoVideo.Should().Be(
                EstadoVideoModulo.Pendiente,
                $"el módulo {modulo.Slug} no tiene demostración confirmada");
        }
    }

    [Fact]
    public void Modulo5_TieneCuatroFichas_YVideoConfirmado()
    {
        var modulo5 = CinturonBlancoCatalogo.Modulos.Single(m => m.Id == 5);

        modulo5.Fichas.Should().HaveCount(4);
        modulo5.EstadoVideo.Should().Be(
            EstadoVideoModulo.ConVideo,
            "el módulo 5 es el único con demostraciones confirmadas");
    }

    [Fact]
    public void Modulos_3y4_EnlazanSusFichasRelacionadas()
    {
        var montada = CinturonBlancoCatalogo.ModuloPorSlug("supervivencia-en-montada");
        var lateral = CinturonBlancoCatalogo.ModuloPorSlug("supervivencia-en-control-lateral");

        montada!.FichasRelacionadas.Should().ContainSingle()
            .Which.Should().Be("escape-de-montada-con-codos");
        lateral!.FichasRelacionadas.Should().ContainSingle()
            .Which.Should().Be("escape-de-control-lateral-recuperando-guardia");

        foreach (var slug in montada.FichasRelacionadas.Concat(lateral.FichasRelacionadas))
            CinturonBlancoCatalogo.FichaPorSlug(slug).Should().NotBeNull();
    }

    [Fact]
    public void Navegacion_Modulos_EncadenaAnteriorYSiguiente()
    {
        var primero = CinturonBlancoCatalogo.ModuloPorSlug("primeros-pasos")!;
        var ultimo = CinturonBlancoCatalogo.ModuloPorSlug("escapes-basicos")!;

        CinturonBlancoCatalogo.ModuloAnterior(primero).Should().BeNull();
        CinturonBlancoCatalogo.ModuloSiguiente(primero)!.Slug.Should().Be("fundamentos-defensivos");
        CinturonBlancoCatalogo.ModuloSiguiente(ultimo).Should().BeNull();
        CinturonBlancoCatalogo.ModuloAnterior(ultimo)!.Slug.Should().Be("supervivencia-en-control-lateral");
    }

    // ── Fichas ────────────────────────────────────────────────────────────

    [Fact]
    public void Fichas_SonCuatro_SinDuplicados()
    {
        var fichas = CinturonBlancoCatalogo.Fichas;

        fichas.Should().HaveCount(4);
        fichas.Select(f => f.Slug).Should().OnlyHaveUniqueItems();
        fichas.Select(f => f.Nombre).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void CadaFicha_TieneEntre3y6Pasos()
    {
        foreach (var ficha in CinturonBlancoCatalogo.Fichas)
        {
            // Nota: FluentAssertions 8 no expone HaveCountGreaterOrEqualTo sobre
            // StringCollectionAssertions; se compara sobre Count (equivalente exacto).
            ficha.Pasos.Count.Should().BeGreaterThanOrEqualTo(3, $"ficha {ficha.Slug}");
            ficha.Pasos.Count.Should().BeLessThanOrEqualTo(6, $"ficha {ficha.Slug}");
            ficha.Pasos.Should().NotContain(paso => string.IsNullOrWhiteSpace(paso));
        }
    }

    [Fact]
    public void CadaFicha_TieneTodasLasSeccionesRequeridas()
    {
        foreach (var ficha in CinturonBlancoCatalogo.Fichas)
        {
            ficha.PosicionInicial.Should().NotBeNullOrWhiteSpace(ficha.Slug);
            ficha.Objetivo.Should().NotBeNullOrWhiteSpace(ficha.Slug);
            ficha.Introduccion.Should().NotBeNullOrWhiteSpace(ficha.Slug);
            ficha.Principios.Should().NotBeEmpty(ficha.Slug);
            ficha.ErroresFrecuentes.Should().NotBeEmpty(ficha.Slug);
            ficha.Precauciones.Should().NotBeEmpty(ficha.Slug);
            ficha.TipoContenido.Should().NotBeNullOrWhiteSpace(ficha.Slug);
            ficha.ModuloId.Should().Be(5, "las cuatro fichas pertenecen al módulo 5");
        }
    }

    // ── Videos ────────────────────────────────────────────────────────────

    [Fact]
    public void Videos_SonExclusivamenteLosCuatroConfirmados()
    {
        var ids = CinturonBlancoCatalogo.Fichas
            .Where(f => f.Video is not null)
            .Select(f => f.Video!.VideoId)
            .ToList();

        ids.Should().BeEquivalentTo(VideosPermitidos);
    }

    [Fact]
    public void Videos_UrlsDerivadasDelIdValidado()
    {
        foreach (var ficha in CinturonBlancoCatalogo.Fichas)
        {
            var video = ficha.Video;
            video.Should().NotBeNull($"la ficha {ficha.Slug} tiene video confirmado");

            video!.VideoId.Should().MatchRegex("^[a-zA-Z0-9_-]{11}$");
            video.EmbedUrl.Should().Be($"https://www.youtube.com/embed/{video.VideoId}");
            video.WatchUrl.Should().Be($"https://www.youtube.com/watch?v={video.VideoId}");
            video.MiniaturaUrl.Should().Be($"https://i.ytimg.com/vi/{video.VideoId}/hqdefault.jpg");
            video.Nota.Should().Contain("pendiente de validación");
        }
    }

    [Fact]
    public void Videos_URLInvalida_LanzaErrorDeFallaTemprana()
    {
        var parser = new MiCaminoBJJ.Core.Services.ExternalMediaParser();

        var accion = () => VideoRecorrido.Crear(
            "https://ejemplo.com/video/123", "Título", "Nota", parser);

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage("*no es un enlace de YouTube válido*");
    }

    // ── Relación con la biblioteca (sin duplicar entidades) ───────────────

    [Fact]
    public void TecnicasRelacionadas_ExistentenEnLaBiblioteca()
    {
        var biblioteca = new TecnicasDemoConsultaService()
            .SearchAsync(new MiCaminoBJJ.Core.Interfaces.TechniqueSearchFilter())
            .GetAwaiter().GetResult();

        var nombresBiblioteca = biblioteca.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var ficha in CinturonBlancoCatalogo.Fichas.Where(f => f.TecnicaRelacionada is not null))
        {
            nombresBiblioteca.Should().Contain(
                ficha.TecnicaRelacionada,
                $"la ficha {ficha.Slug} referencia una técnica que debe existir sin duplicarla");
        }
    }

    [Fact]
    public void CinturonPrincipal_CoincideConElDeLaBiblioteca_NoSeReetiqueta()
    {
        var biblioteca = new TecnicasDemoConsultaService()
            .SearchAsync(new MiCaminoBJJ.Core.Interfaces.TechniqueSearchFilter())
            .GetAwaiter().GetResult();

        foreach (var ficha in CinturonBlancoCatalogo.Fichas.Where(f => f.TecnicaRelacionada is not null))
        {
            var tecnica = biblioteca.Single(t =>
                string.Equals(t.Name, ficha.TecnicaRelacionada, StringComparison.OrdinalIgnoreCase));

            ficha.CinturonPrincipal.Should().Be(
                tecnica.Belt.Name,
                $"la ficha {ficha.Slug} no puede cambiarle el cinturón principal a la técnica");
        }
    }

    [Fact]
    public void FichaSinTecnicaRelacionada_NoAsignaCinturonPrincipal()
    {
        var ficha = CinturonBlancoCatalogo.FichaPorSlug("recuperacion-de-guardia-desde-media-guardia")!;

        ficha.TecnicaRelacionada.Should().BeNull();
        ficha.CinturonPrincipal.Should().BeNull(
            "no se le atribuye automáticamente un cinturón principal nuevo");
    }

    [Fact]
    public void Pasos_DeFichaDeMontada_ProvienenDelCatalogoExistente()
    {
        var biblioteca = new TecnicasDemoConsultaService()
            .SearchAsync(new MiCaminoBJJ.Core.Interfaces.TechniqueSearchFilter())
            .GetAwaiter().GetResult();

        var escape = biblioteca.Single(t => t.Name == "Escape de montada");
        var pasosCatalogo = JsonSerializer.Deserialize<string[]>(escape.Steps!)!;

        var ficha = CinturonBlancoCatalogo.FichaPorSlug("escape-de-montada-con-codos")!;

        foreach (var paso in ficha.Pasos)
            pasosCatalogo.Should().Contain(paso, "los pasos no pueden ser inventados fuera del catálogo");
    }

    // ── Honestidad del contenido ──────────────────────────────────────────

    [Fact]
    public void TodasLasFichas_MarcanRevisionDelInstructor()
    {
        foreach (var ficha in CinturonBlancoCatalogo.Fichas)
        {
            ficha.RequiereRevisionInstructor.Should().BeTrue(ficha.Slug);
            ficha.EstadoRevision.Should().Contain("Pendiente de revisión del instructor", ficha.Slug);
        }
    }

    [Fact]
    public void TodasLasFichas_SinReferenciaBibliograficaNoVerificada()
    {
        foreach (var ficha in CinturonBlancoCatalogo.Fichas)
        {
            ficha.ReferenciaBibliografica.Should().BeNull(
                $"solo se admite referencia verificada; la ficha {ficha.Slug} no tiene una verificada");
        }

        CinturonBlancoCatalogo.ReferenciaSinVerificar.Should().Contain("Sin referencia bibliográfica verificada");
    }

    [Fact]
    public void Navegacion_Fichas_EncadenaAnteriorYSiguiente()
    {
        var fichas = CinturonBlancoCatalogo.Fichas;

        CinturonBlancoCatalogo.FichaAnterior(fichas[0]).Should().BeNull();
        CinturonBlancoCatalogo.FichaSiguiente(fichas[0])!.Slug.Should().Be(fichas[1].Slug);
        CinturonBlancoCatalogo.FichaSiguiente(fichas[^1]).Should().BeNull();
        CinturonBlancoCatalogo.FichaAnterior(fichas[^1])!.Slug.Should().Be(fichas[^2].Slug);
    }
}
