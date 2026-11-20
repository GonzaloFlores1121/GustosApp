using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.RestauranteUseCases;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using Moq;

namespace GustosApp.Application.Tests;

public sealed class GaleriaRestaurantePruebas
{
    private readonly Mock<IRestauranteRepository> _repositorio = new();
    private readonly Mock<IFileStorageService> _storage = new();
    private readonly Restaurante _restaurante = new() { Id = Guid.NewGuid(), DuenoId = Guid.NewGuid() };
    private ActualizarImagenesRestauranteUseCase Caso => new(_repositorio.Object, _storage.Object);
    private static ArchivoEntrada Archivo(string nombre = "foto.png") => new(new MemoryStream([1, 2, 3]), nombre);

    public GaleriaRestaurantePruebas()
    {
        _repositorio.Setup(r => r.GetRestauranteConImagenesAsync(_restaurante.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_restaurante);
        _storage.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("https://imagenes.test/nueva.png");
    }

    private RestauranteImagen Existente(TipoImagenRestaurante tipo = TipoImagenRestaurante.Interior)
    {
        var imagen = new RestauranteImagen { RestauranteId = _restaurante.Id, Tipo = tipo, Url = $"https://imagenes.test/{Guid.NewGuid()}.png", Orden = 0 };
        _restaurante.Imagenes.Add(imagen);
        return imagen;
    }

    [Fact]
    public async Task Agregar_ConservaLasAnterioresYOtrasCategorias()
    {
        var anterior = Existente();
        var comida = Existente(TipoImagenRestaurante.Comida);
        var resultado = await Caso.AgregarImagenesAsync(_restaurante.Id, _restaurante.DuenoId!.Value, TipoImagenRestaurante.Interior, [Archivo()], default);
        Assert.Collection(resultado, imagen => Assert.Same(anterior, imagen),
            imagen => Assert.Equal("https://imagenes.test/nueva.png", imagen.Url));
        Assert.Contains(comida, _restaurante.Imagenes);
        _storage.Verify(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ContratoAnterior_RechazaBorradoMasivoYConservaLasFotos()
    {
        var anterior = Existente();
        await Assert.ThrowsAsync<ArgumentException>(() => Caso.ActualizarImagenesColeccionAsync(_restaurante.Id, _restaurante.DuenoId!.Value,
            TipoImagenRestaurante.Interior, null, true, default));
        Assert.Same(anterior, Assert.Single(_restaurante.Imagenes));
        _repositorio.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Agregar_LimiteCuentaFotosConservadasAntesDeSubir()
    {
        Existente();
        await Assert.ThrowsAsync<ArgumentException>(() => Caso.AgregarImagenesAsync(_restaurante.Id, _restaurante.DuenoId!.Value,
            TipoImagenRestaurante.Interior, Enumerable.Range(0, 5).Select(_ => Archivo()).ToList(), default));
        _storage.Verify(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        Assert.Single(_restaurante.Imagenes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Operar_PropietarioAjeno_NoSubeNiElimina(bool modificar)
    {
        var imagen = Existente();
        await Assert.ThrowsAsync<AccesoProhibidoException>(() => modificar
            ? Caso.ModificarImagenAsync(_restaurante.Id, Guid.NewGuid(), TipoImagenRestaurante.Interior, imagen.Id, Archivo(), default)
            : Caso.AgregarImagenesAsync(_restaurante.Id, Guid.NewGuid(), TipoImagenRestaurante.Interior, [Archivo()], default));
        _storage.Verify(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _storage.Verify(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Eliminar_IdentificadorExacto_ConservaLasOtrasFotos()
    {
        var borrar = Existente();
        var conservar = Existente();
        var resultado = await Caso.ModificarImagenAsync(_restaurante.Id, _restaurante.DuenoId!.Value, TipoImagenRestaurante.Interior, borrar.Id, null, default);
        Assert.Same(conservar, Assert.Single(resultado));
        _storage.Verify(s => s.DeleteFileAsync(borrar.Url, $"restaurantes/{_restaurante.Id:N}"), Times.Once);
        _storage.Verify(s => s.DeleteFileAsync(conservar.Url, It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Eliminar_IdDeOtraCategoria_NoModificaLaColeccion()
    {
        var comida = Existente(TipoImagenRestaurante.Comida);
        await Assert.ThrowsAsync<NotFoundException>(() => Caso.ModificarImagenAsync(_restaurante.Id, _restaurante.DuenoId!.Value,
            TipoImagenRestaurante.Interior, comida.Id, null, default));
        Assert.Single(_restaurante.Imagenes);
        _repositorio.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reemplazar_FalloPersistencia_RestauraColeccionYLimpiaSoloLaNueva()
    {
        var anterior = Existente();
        _repositorio.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("persistencia"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Caso.ModificarImagenAsync(_restaurante.Id, _restaurante.DuenoId!.Value,
            TipoImagenRestaurante.Interior, anterior.Id, Archivo(), default));
        Assert.Same(anterior, Assert.Single(_restaurante.Imagenes));
        _storage.Verify(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _storage.Verify(s => s.ScheduleCleanupAsync("https://imagenes.test/nueva.png", $"restaurantes/{_restaurante.Id:N}"), Times.Once);
    }

    [Fact]
    public async Task Agregar_FallaSegundoArchivo_ConservaExistentesYProgramaLaSubidaParcial()
    {
        var anterior = Existente();
        _storage.SetupSequence(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://imagenes.test/parcial.png").ThrowsAsync(new ArgumentException("contenido"));
        await Assert.ThrowsAsync<ArgumentException>(() => Caso.AgregarImagenesAsync(_restaurante.Id, _restaurante.DuenoId!.Value,
            TipoImagenRestaurante.Interior, [Archivo(), Archivo()], default));
        Assert.Same(anterior, Assert.Single(_restaurante.Imagenes));
        _repositorio.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _storage.Verify(s => s.ScheduleCleanupAsync("https://imagenes.test/parcial.png", It.IsAny<string>()), Times.Once);
    }

    [Theory]
    [InlineData("galeria")]
    [InlineData("logo")]
    [InlineData("destacada")]
    public async Task LimpiezaFallaDespuesDePersistir_NoRevierteNiProgramaEliminarLaFotoNueva(string tipo)
    {
        var anterior = Existente();
        _restaurante.LogoUrl = anterior.Url;
        _restaurante.ImagenUrl = anterior.Url;
        _storage.Setup(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new IOException("storage"));
        if (tipo == "galeria") await Caso.ModificarImagenAsync(_restaurante.Id, _restaurante.DuenoId!.Value, TipoImagenRestaurante.Interior, anterior.Id, Archivo(), default);
        else if (tipo == "logo") Assert.Equal("https://imagenes.test/nueva.png", await Caso.ActualizarLogoAsync(_restaurante.Id, _restaurante.DuenoId!.Value, Archivo(), false, default));
        else Assert.Equal("https://imagenes.test/nueva.png", await Caso.ActualizarImagenDestacadaAsync(_restaurante.Id, _restaurante.DuenoId!.Value, Archivo(), false, default));
        _storage.Verify(s => s.ScheduleCleanupAsync("https://imagenes.test/nueva.png", It.IsAny<string>()), Times.Never);
        _storage.Verify(s => s.ScheduleCleanupAsync(anterior.Url, It.IsAny<string>()), Times.Once);
    }
}
