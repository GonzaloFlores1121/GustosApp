using System.Net;
using FluentAssertions;
using GustosApp.API.Extensiones;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace GustosApp.API.Tests;

public sealed class FotosGoogleEndpointPruebas
{
    [Fact]
    public async Task SinSesion_Responde401SinConsultarProveedor()
    {
        await using var factory = new FabricaApiGustosApp();
        using var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/restaurantes/{Guid.NewGuid()}/foto-google/{new string('a', 64)}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(false, false, HttpStatusCode.Forbidden)]
    [InlineData(true, false, HttpStatusCode.NotFound)]
    [InlineData(true, true, HttpStatusCode.OK)]
    public async Task UsuarioRealYReferenciaInterna_EntregaSoloContenidoSinCache(bool registrado, bool disponible, HttpStatusCode status)
    {
        var fotos = new Mock<IFotoGoogleService>();
        var usuarios = new Mock<IUsuarioRepository>();
        usuarios.Setup(x => x.GetByFirebaseUidAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(registrado ? new Usuario() : null);
        fotos.Setup(x => x.ObtenerAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(disponible ? new FotoGoogleResultado("cHJ1ZWJh", "image/png", [], null) : null);
        await using var baseFactory = new FabricaApiGustosAppAutenticada();
        await using var factory = baseFactory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IFotoGoogleService>(); s.AddSingleton(fotos.Object);
            s.RemoveAll<IUsuarioRepository>(); s.AddSingleton(usuarios.Object);
        }));
        using var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/restaurantes/{Guid.NewGuid()}/foto-google/{new string('a', 64)}?url=https://evil.test");
        response.StatusCode.Should().Be(status);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.Location.Should().BeNull();
        (await response.Content.ReadAsStringAsync()).Should().NotContain("evil.test");
        fotos.Verify(x => x.ObtenerAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), registrado ? Times.Once() : Times.Never());
    }

    [Fact]
    public void Comprobante_ExigeMismoPlaceIdFirmaYVigencia()
    {
        var provider = new EphemeralDataProtectionProvider();
        var servicio = new ComprobanteFotoGoogle(provider);
        var comprobante = servicio.Emitir("place-1");
        servicio.Validar(comprobante, "place-1").Should().BeTrue();
        servicio.Validar(comprobante, "place-2").Should().BeFalse();
        servicio.Validar("modificado" + comprobante, "place-1").Should().BeFalse();
        var vencido = provider.CreateProtector("GustosApp.FotoGoogle.v1").ToTimeLimitedDataProtector().Protect("place-1", DateTimeOffset.UtcNow.AddMinutes(-1));
        servicio.Validar(vencido, "place-1").Should().BeFalse();
    }
}
