using FluentAssertions;
using GustosApp.Domain.Model;

namespace GustosApp.Domain.Tests;

public class ValoracionPrincipalRestaurantePruebas
{
    [Fact]
    public void ConResenasLocalesYGoogle_UsaSoloElPromedioLocalComoPrincipal()
    {
        var restaurante = CrearRestaurante(ratingGoogle: 4.9, cantidadGoogle: 120);
        restaurante.Reviews =
        [
            CrearOpinionLocal(3),
            CrearOpinionLocal(5),
            new OpinionRestaurante { Valoracion = 1, EsImportada = true }
        ];

        restaurante.ObtenerPuntuacionPrincipal().Should().Be(4);
        restaurante.ObtenerFuentePuntuacionPrincipal().Should().Be("GustosApp");
        restaurante.ObtenerCantidadResenasLocales().Should().Be(2);
        restaurante.CantidadResenas.Should().Be(120);
    }

    [Fact]
    public void SoloConValoracionLocalPersistida_UsaGustosApp()
    {
        var restaurante = CrearRestaurante();
        restaurante.Valoracion = 4.25m;

        restaurante.ObtenerPuntuacionPrincipal().Should().Be(4.25);
        restaurante.ObtenerFuentePuntuacionPrincipal().Should().Be("GustosApp");
    }

    [Fact]
    public void SoloConValoracionGoogle_UsaGoogle()
    {
        var restaurante = CrearRestaurante(ratingGoogle: 4.6, cantidadGoogle: 30);

        restaurante.ObtenerPuntuacionPrincipal().Should().Be(4.6);
        restaurante.ObtenerFuentePuntuacionPrincipal().Should().Be("Google");
        restaurante.ObtenerCantidadResenasLocales().Should().Be(0);
    }

    [Fact]
    public void SinValoraciones_NoFabricaUnCeroNiUnaFuente()
    {
        var restaurante = CrearRestaurante(ratingGoogle: 0);
        restaurante.Valoracion = 0;

        restaurante.ObtenerPuntuacionPrincipal().Should().BeNull();
        restaurante.ObtenerFuentePuntuacionPrincipal().Should().BeNull();
    }

    private static Restaurante CrearRestaurante(double? ratingGoogle = null, int? cantidadGoogle = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Nombre = "Restaurante de prueba",
            PlaceId = Guid.NewGuid().ToString("N"),
            Rating = ratingGoogle,
            CantidadResenas = cantidadGoogle
        };

    private static OpinionRestaurante CrearOpinionLocal(double valoracion) => new()
    {
        Valoracion = valoracion,
        UsuarioId = Guid.NewGuid(),
        EsImportada = false
    };
}
