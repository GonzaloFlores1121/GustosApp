using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.RestauranteUseCases;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using Moq;

namespace GustosApp.Application.Tests;

public class GestionarBorradorMenuRestauranteUseCaseTests
{
    private readonly Mock<IRestauranteRepository> _restaurantes = new();
    private readonly Mock<IRestauranteMenuRepository> _menus = new();
    private readonly Mock<IFileStorageService> _archivos = new();
    private readonly Mock<IHttpDownloader> _downloader = new();
    private readonly Mock<IOcrService> _ocr = new();
    private readonly Mock<IMenuParser> _parser = new();

    private GestionarBorradorMenuRestauranteUseCase CrearSut() => new(
        _restaurantes.Object,
        _menus.Object,
        _archivos.Object,
        _downloader.Object,
        _ocr.Object,
        _parser.Object);

    [Fact]
    public async Task FalloDeOcr_ConservaMenuPublicadoYGustos()
    {
        var usuarioId = Guid.NewGuid();
        var restaurante = new Restaurante
        {
            Id = Guid.NewGuid(),
            DuenoId = usuarioId,
            MenuProcesado = true,
            GustosQueSirve = [new Gusto { Id = Guid.NewGuid(), Nombre = "Pizza" }]
        };
        var menu = CrearMenuPublicado(restaurante.Id);
        _restaurantes.Setup(r => r.GetRestauranteConImagenesAsync(restaurante.Id, default))
            .ReturnsAsync(restaurante);
        _menus.Setup(r => r.GetByRestauranteIdAsync(restaurante.Id, default)).ReturnsAsync(menu);
        _menus.Setup(r => r.ReloadAsync(menu, default)).Returns(Task.CompletedTask);
        _archivos.Setup(a => a.UploadFileAsync(It.IsAny<Stream>(), "nuevo.jpg", "restaurantes/menus-borrador"))
            .ReturnsAsync("nuevo.jpg");
        _downloader.Setup(d => d.DownloadAsync("nuevo.jpg", default))
            .ThrowsAsync(new InvalidOperationException("detalle técnico"));
        using var contenido = new MemoryStream([1, 2, 3]);

        var resultado = await CrearSut().CrearDesdeArchivoAsync(
            restaurante.Id,
            usuarioId,
            new ArchivoEntrada(contenido, "nuevo.jpg"),
            default);

        Assert.Equal(EstadoBorradorMenu.Error, resultado.EstadoBorrador);
        Assert.Equal("{\"categorias\":[]}", resultado.Json);
        Assert.Equal("publicado.jpg", resultado.ImagenFuentePublicadaUrl);
        Assert.True(restaurante.MenuProcesado);
        Assert.Equal("Pizza", Assert.Single(restaurante.GustosQueSirve).Nombre);
        Assert.DoesNotContain("detalle técnico", resultado.BorradorError);
    }

    [Fact]
    public async Task PublicarConTokenVencido_NoModificaMenuPublicado()
    {
        var usuarioId = Guid.NewGuid();
        var restaurante = new Restaurante { Id = Guid.NewGuid(), DuenoId = usuarioId, MenuProcesado = true };
        var menu = CrearMenuPublicado(restaurante.Id);
        var tokenActual = Guid.NewGuid();
        menu.IniciarBorrador(tokenActual, "nuevo.jpg", DateTime.UtcNow);
        menu.CompletarBorrador(tokenActual, "{\"categorias\":[]}", DateTime.UtcNow);
        _restaurantes.Setup(r => r.GetRestauranteConImagenesAsync(restaurante.Id, default)).ReturnsAsync(restaurante);
        _menus.Setup(r => r.GetByRestauranteIdAsync(restaurante.Id, default)).ReturnsAsync(menu);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CrearSut().PublicarAsync(
            restaurante.Id,
            usuarioId,
            Guid.NewGuid(),
            "{\"categorias\":[]}",
            default));

        Assert.Equal("publicado.jpg", menu.ImagenFuentePublicadaUrl);
        Assert.Equal(1, menu.Version);
        Assert.Equal(tokenActual, menu.BorradorToken);
        _menus.Verify(r => r.UpdateAsync(It.IsAny<RestauranteMenu>(), default), Times.Never);
    }

    [Fact]
    public async Task PublicarBorrador_NoModificaLosGustos()
    {
        var usuarioId = Guid.NewGuid();
        var gusto = new Gusto { Id = Guid.NewGuid(), Nombre = "Vegano" };
        var restaurante = new Restaurante
        {
            Id = Guid.NewGuid(),
            DuenoId = usuarioId,
            GustosQueSirve = [gusto]
        };
        var menu = new RestauranteMenu { RestauranteId = restaurante.Id, Version = 0 };
        var token = Guid.NewGuid();
        menu.IniciarBorrador(token, "nuevo.jpg", DateTime.UtcNow);
        menu.CompletarBorrador(token, "{\"categorias\":[]}", DateTime.UtcNow);
        _restaurantes.Setup(r => r.GetRestauranteConImagenesAsync(restaurante.Id, default)).ReturnsAsync(restaurante);
        _menus.Setup(r => r.GetByRestauranteIdAsync(restaurante.Id, default)).ReturnsAsync(menu);

        await CrearSut().PublicarAsync(
            restaurante.Id,
            usuarioId,
            token,
            "{\"categorias\":[]}",
            default);

        Assert.True(restaurante.MenuProcesado);
        Assert.Same(gusto, Assert.Single(restaurante.GustosQueSirve));
        Assert.Equal(1, menu.Version);
    }

    private static RestauranteMenu CrearMenuPublicado(Guid restauranteId)
    {
        var menu = new RestauranteMenu { RestauranteId = restauranteId, Version = 0 };
        var token = Guid.NewGuid();
        menu.IniciarBorrador(token, "publicado.jpg", DateTime.UtcNow);
        menu.CompletarBorrador(token, "{\"categorias\":[]}", DateTime.UtcNow);
        menu.PublicarBorrador(token, "{\"categorias\":[]}", DateTime.UtcNow);
        return menu;
    }
}
