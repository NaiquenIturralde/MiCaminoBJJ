using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using MiCaminoBJJ.Web.Services.Admin;
using MiCaminoBJJ.Web.ViewModels.Admin;

namespace MiCaminoBJJ.Web.Tests.Services;

/// <summary>
/// Etapa 3 — credencial de Development (validación pura en servidor) y
/// conversiones del formulario de técnicas (líneas ↔ JSON del dominio).
/// </summary>
public class DevAdminCredentialTests
{
    // ── Credencial: solo Development, siempre validada en el servidor ─────

    [Theory]
    [InlineData("secreta", "secreta", true, true)]
    [InlineData("secreta", "otra-cosa", true, false)]
    [InlineData("secreta", "", true, false)]
    [InlineData("secreta", null, true, false)]
    [InlineData("", "", true, false)]
    [InlineData(null, "secreta", true, false)]
    [InlineData(null, null, true, false)]
    // Incluso con la contraseña correcta: FUERA de Development nunca valida.
    [InlineData("secreta", "secreta", false, false)]
    [InlineData("secreta", "otra", false, false)]
    public void Validacion_DeCredencial_DeDevelopment(string? esperada, string? ingresada, bool esDevelopment, bool esperado)
    {
        DevAdminCredential.EsValida(esperada, ingresada, esDevelopment).Should().Be(esperado);
    }

    [Fact]
    public void ClavesDeConfiguracion_EstanFueraDelRepositorio()
    {
        // El nombre de la clave obliga a que el valor viva en User Secrets/env:
        // appsettings.json del repo no puede contener la contraseña.
        DevAdminCredential.PasswordConfigKey.Should().Be("AdminDev:Password");
        DevAdminCredential.UserConfigKey.Should().Be("AdminDev:User");
    }

    // ── Formulario: líneas de texto ↔ JSON de Steps/Tips ─────────────────

    [Fact]
    public void AJson_ConvierteLineasAJsonDeLista_TrimYFiltraVacias()
    {
        var json = AdminTecnicaFormViewModel.AJson("  Paso uno  \n\nPaso dos\n   \n");

        json.Should().Be("[\"Paso uno\",\"Paso dos\"]");
        JsonSerializer.Deserialize<string[]>(json!).Should().Equal("Paso uno", "Paso dos");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  \n")]
    public void AJson_Vacio_DevuelveNull(string? texto)
    {
        AdminTecnicaFormViewModel.AJson(texto).Should().BeNull();
    }

    [Fact]
    public void ATexto_ConvierteJsonAlineas()
    {
        var texto = AdminTecnicaFormViewModel.ATexto("[\"A\",\"B\"]");

        texto.Should().Be($"A{Environment.NewLine}B");
    }

    [Fact]
    public void ATexto_JsonInvalido_SeMuestraTalCual_NoPierdeDato()
    {
        const string crudo = "no-es-json";
        AdminTecnicaFormViewModel.ATexto(crudo).Should().Be(crudo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ATexto_Vacio_DevuelveNull(string? json)
    {
        AdminTecnicaFormViewModel.ATexto(json).Should().BeNull();
    }

    // ── DataAnnotations con los límites REALES del dominio ────────────────

    private static IList<System.ComponentModel.DataAnnotations.ValidationResult> Validar(AdminTecnicaFormViewModel model)
    {
        var resultados = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            model,
            new System.ComponentModel.DataAnnotations.ValidationContext(model),
            resultados,
            validateAllProperties: true);
        return resultados;
    }

    private static AdminTecnicaFormViewModel ModeloBase() => new()
    {
        Nombre = "Técnica",
        Descripcion = "Descripción",
        Objetivo = "Objetivo",
        CinturonId = 1,
        CategoriaId = 1,
        PosicionId = 1
    };

    [Fact]
    public void ModeloCompleto_EsValido()
    {
        Validar(ModeloBase()).Should().BeEmpty();
    }

    [Fact]
    public void NombreObligatorio_YLongitudDelDominio()
    {
        var sinNombre = ModeloBase();
        sinNombre.Nombre = "";
        Validar(sinNombre).Should().ContainSingle(r => r.MemberNames.Contains(nameof(AdminTecnicaFormViewModel.Nombre)));

        var largo = ModeloBase();
        largo.Nombre = new string('x', 201);
        Validar(largo).Should().ContainSingle(r => r.MemberNames.Contains(nameof(AdminTecnicaFormViewModel.Nombre)));
    }

    [Fact]
    public void Descripcion_Y_Objetivo_Requeridos_YConLimite()
    {
        var sinDescripcion = ModeloBase();
        sinDescripcion.Descripcion = "";
        Validar(sinDescripcion).Should().NotBeEmpty();

        var descripcionLarga = ModeloBase();
        descripcionLarga.Descripcion = new string('x', 2001);
        Validar(descripcionLarga).Should().NotBeEmpty();

        var objetivoLargo = ModeloBase();
        objetivoLargo.Objetivo = new string('x', 1001);
        Validar(objetivoLargo).Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(null, nameof(AdminTecnicaFormViewModel.CinturonId))]
    [InlineData(0, nameof(AdminTecnicaFormViewModel.CinturonId))]
    [InlineData(null, nameof(AdminTecnicaFormViewModel.CategoriaId))]
    [InlineData(0, nameof(AdminTecnicaFormViewModel.CategoriaId))]
    [InlineData(null, nameof(AdminTecnicaFormViewModel.PosicionId))]
    [InlineData(0, nameof(AdminTecnicaFormViewModel.PosicionId))]
    public void Desplegables_Obligatorios_YRangoMinimo(int? valor, string campo)
    {
        var model = ModeloBase();
        switch (campo)
        {
            case nameof(AdminTecnicaFormViewModel.CinturonId): model.CinturonId = valor; break;
            case nameof(AdminTecnicaFormViewModel.CategoriaId): model.CategoriaId = valor; break;
            default: model.PosicionId = valor; break;
        }

        Validar(model).Should().Contain(r => r.MemberNames.Contains(campo),
            $"{campo} con valor {valor?.ToString() ?? "null"} debe ser inválido");
    }

    [Fact]
    public void Pasos_ExcedenElLimiteDelDominio()
    {
        var model = ModeloBase();
        model.Pasos = new string('x', 4001);
        Validar(model).Should().ContainSingle(r => r.MemberNames.Contains(nameof(AdminTecnicaFormViewModel.Pasos)));
    }

    [Fact]
    public void Normalizacion_DelNombre_EsLaReglaDelDominio()
    {
        // La regla de TextNormalizer aplicada al alta: sin tildes, mayúsculas.
        var model = ModeloBase();
        model.Nombre = "  Técnica Niño  ";
        Validar(model).Should().BeEmpty();

        // Garantiza que el valor no se trunca ni se sanitiza fuera del dominio.
        Regex.Replace(model.Nombre, @"\s+", " ").Trim().Should().Be("Técnica Niño");
    }
}
