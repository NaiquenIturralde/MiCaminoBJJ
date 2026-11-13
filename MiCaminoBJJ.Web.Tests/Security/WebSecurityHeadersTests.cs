using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MiCaminoBJJ.Web.Security;

namespace MiCaminoBJJ.Web.Tests.Security;

/// <summary>
/// Etapa 4 — regresión del hallazgo anti-clickjacking: toda respuesta debe
/// llevar X-Frame-Options: DENY (nadie puede enmarcar el panel admin en un
/// iframe), X-Content-Type-Options: nosniff y Referrer-Policy.
///
/// Referrer-Policy: <c>strict-origin-when-cross-origin</c> (y NO
/// <c>same-origin</c>): con <c>same-origin</c> el navegador omite la cabecera
/// <c>Referer</c> en las peticiones cruzadas y el reproductor de YouTube
/// responde con el <c>Error 153</c> ("Video player configuration error"),
/// dejando sin video la ficha de técnica. La política actual sigue sin
/// filtrar la ruta ni la consulta fuera del sitio.
/// </summary>
public class WebSecurityHeadersTests
{
    [Fact]
    public void Apply_Agrega_LasTresCabecerasDeSeguridad()
    {
        var headers = new HeaderDictionary();

        WebSecurityHeaders.Apply(headers);

        headers[WebSecurityHeaders.FrameOptionsName].ToString().Should().Be("DENY");
        headers[WebSecurityHeaders.ContentTypeOptionsName].ToString().Should().Be("nosniff");
        headers[WebSecurityHeaders.ReferrerPolicyName].ToString()
            .Should().Be("strict-origin-when-cross-origin");
    }

    [Fact]
    public void ReferrerPolicy_NoEsSameOrigin_PorqueYouTubeDevuelveError153SinReferer()
    {
        var headers = new HeaderDictionary();

        WebSecurityHeaders.Apply(headers);

        var politica = headers[WebSecurityHeaders.ReferrerPolicyName].ToString();

        politica.Should().NotBe("same-origin",
            "same-origin omite la cabecera Referer en el embed de YouTube y provoca el Error 153");
        politica.Should().StartWith("strict-origin",
            "el origen se envía a YouTube, pero nunca la ruta ni los parámetros de la URL");
    }

    [Fact]
    public void Apply_NoEliminaCabecerasPreexistentes()
    {
        var headers = new HeaderDictionary();
        headers["Content-Type"] = "text/html; charset=utf-8";

        WebSecurityHeaders.Apply(headers);

        headers["Content-Type"].ToString().Should().Be("text/html; charset=utf-8");
        headers.ContainsKey(WebSecurityHeaders.FrameOptionsName).Should().BeTrue();
    }

    [Fact]
    public void Apply_SinCabeceras_LanzaArgumentNull()
    {
        var accion = () => WebSecurityHeaders.Apply(null!);
        accion.Should().Throw<ArgumentNullException>();
    }
}
