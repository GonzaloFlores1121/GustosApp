using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;

namespace GustosApp.Domain.Tests;

public class RestauranteMenuTests
{
    [Fact]
    public void CompletarBorradorAnterior_NoReemplazaElBorradorActual()
    {
        var menu = new RestauranteMenu { Json = "{\"publicado\":true}", Version = 2 };
        var tokenAnterior = Guid.NewGuid();
        var tokenActual = Guid.NewGuid();
        menu.IniciarBorrador(tokenAnterior, "menu-anterior.jpg", DateTime.UtcNow.AddMinutes(-1));
        menu.IniciarBorrador(tokenActual, "menu-actual.jpg", DateTime.UtcNow);

        var completo = menu.CompletarBorrador(tokenAnterior, "{\"categorias\":[]}", DateTime.UtcNow);

        Assert.False(completo);
        Assert.Equal(tokenActual, menu.BorradorToken);
        Assert.Equal(EstadoBorradorMenu.Procesando, menu.EstadoBorrador);
        Assert.Equal("{\"publicado\":true}", menu.Json);
        Assert.Equal(2, menu.Version);
    }

    [Fact]
    public void PublicarBorrador_UsaLaRevisionExactaYConservaVersionado()
    {
        var menu = new RestauranteMenu { Json = "{\"publicado\":true}", Version = 3 };
        var token = Guid.NewGuid();
        var revisado = "{\"categorias\":[{\"nombre\":\"Pizzas\",\"items\":[]}]}";
        menu.IniciarBorrador(token, "menu-nuevo.jpg", DateTime.UtcNow);
        menu.CompletarBorrador(token, "{\"categorias\":[]}", DateTime.UtcNow);

        menu.PublicarBorrador(token, revisado, DateTime.UtcNow);

        Assert.Equal(revisado, menu.Json);
        Assert.Equal(4, menu.Version);
        Assert.Equal("menu-nuevo.jpg", menu.ImagenFuentePublicadaUrl);
        Assert.Null(menu.BorradorToken);
        Assert.Equal(EstadoBorradorMenu.Ninguno, menu.EstadoBorrador);
    }

    [Fact]
    public void DescartarBorrador_NoRetiraElMenuPublicado()
    {
        var menu = new RestauranteMenu { Json = "{\"publicado\":true}", Version = 1 };
        var publicado = Guid.NewGuid();
        menu.IniciarBorrador(publicado, "menu-publicado.jpg", DateTime.UtcNow);
        menu.CompletarBorrador(publicado, "{\"categorias\":[]}", DateTime.UtcNow);
        menu.PublicarBorrador(publicado, "{\"categorias\":[]}", DateTime.UtcNow);
        var borrador = Guid.NewGuid();
        menu.IniciarBorrador(borrador, "menu-borrador.jpg", DateTime.UtcNow);

        var imagenDescartada = menu.DescartarBorrador(borrador);

        Assert.Equal("menu-borrador.jpg", imagenDescartada);
        Assert.Equal("menu-publicado.jpg", menu.ImagenFuentePublicadaUrl);
        Assert.Equal("{\"categorias\":[]}", menu.Json);
        Assert.Equal(2, menu.Version);
    }
}
