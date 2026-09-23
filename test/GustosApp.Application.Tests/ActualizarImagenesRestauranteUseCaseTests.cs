using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.RestauranteUseCases;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using Moq;

namespace GustosApp.Application.Tests;

public sealed class ActualizarImagenesRestauranteUseCaseTests
{
    private readonly Mock<IRestauranteRepository> _restauranteRepositoryMock = new();
    private readonly Mock<IFileStorageService> _almacenamientoMock = new();
    private readonly ActualizarImagenesRestauranteUseCase _sut;
    private readonly Guid _usuarioId = Guid.NewGuid();

    public ActualizarImagenesRestauranteUseCaseTests()
    {
        _sut = new ActualizarImagenesRestauranteUseCase(
            _restauranteRepositoryMock.Object,
            _almacenamientoMock.Object);
    }

    [Theory]
    [InlineData("destacada")]
    [InlineData("logo")]
    [InlineData("coleccion")]
    public async Task ActualizarImagen_RestauranteDeOtroUsuario_LanzaAccesoProhibidoSinModificarArchivos(
        string operacion)
    {
        var restauranteId = Guid.NewGuid();
        var restaurante = new Restaurante
        {
            Id = restauranteId,
            DuenoId = Guid.NewGuid(),
            ImagenUrl = "https://archivos/imagen.jpg",
            LogoUrl = "https://archivos/logo.jpg"
        };
        restaurante.Imagenes.Add(new RestauranteImagen
        {
            RestauranteId = restauranteId,
            Tipo = TipoImagenRestaurante.Interior,
            Url = "https://archivos/interior.jpg"
        });

        _restauranteRepositoryMock
            .Setup(r => r.GetRestauranteConImagenesAsync(restauranteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurante);

        var accion = () => EjecutarOperacionAsync(operacion, restauranteId, _usuarioId);

        var excepcion = await Assert.ThrowsAsync<AccesoProhibidoException>(accion);

        Assert.Equal("No tenés permisos para actualizar las imágenes de este restaurante.", excepcion.Message);
        _almacenamientoMock.Verify(
            a => a.DeleteFileAsync(It.IsAny<string>()),
            Times.Never);
        _almacenamientoMock.Verify(
            a => a.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>()),
            Times.Never);
        _restauranteRepositoryMock.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("destacada")]
    [InlineData("logo")]
    [InlineData("coleccion")]
    public async Task ActualizarImagen_RestaurantePropio_PermiteLaOperacion(string operacion)
    {
        var restauranteId = Guid.NewGuid();
        var restaurante = new Restaurante
        {
            Id = restauranteId,
            DuenoId = _usuarioId
        };

        _restauranteRepositoryMock
            .Setup(r => r.GetRestauranteConImagenesAsync(restauranteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurante);

        await EjecutarOperacionAsync(operacion, restauranteId, _usuarioId);

        _restauranteRepositoryMock.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("destacada")]
    [InlineData("logo")]
    [InlineData("coleccion")]
    public async Task ActualizarImagen_ArchivoInvalido_ConservaLasImagenesExistentes(string operacion)
    {
        var restauranteId = Guid.NewGuid();
        var restaurante = new Restaurante
        {
            Id = restauranteId,
            DuenoId = _usuarioId,
            ImagenUrl = "https://archivos/imagen-anterior.jpg",
            LogoUrl = "https://archivos/logo-anterior.jpg"
        };
        restaurante.Imagenes.Add(new RestauranteImagen
        {
            RestauranteId = restauranteId,
            Tipo = TipoImagenRestaurante.Interior,
            Url = "https://archivos/interior-anterior.jpg"
        });
        _restauranteRepositoryMock
            .Setup(r => r.GetRestauranteConImagenesAsync(restauranteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurante);
        _almacenamientoMock
            .Setup(a => a.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ThrowsAsync(new ArgumentException("La imagen no es válida."));

        var accion = () => EjecutarReemplazoAsync(operacion, restauranteId);

        await Assert.ThrowsAsync<ArgumentException>(accion);
        Assert.Equal("https://archivos/imagen-anterior.jpg", restaurante.ImagenUrl);
        Assert.Equal("https://archivos/logo-anterior.jpg", restaurante.LogoUrl);
        Assert.Contains(restaurante.Imagenes, imagen => imagen.Url == "https://archivos/interior-anterior.jpg");
        _almacenamientoMock.Verify(a => a.DeleteFileAsync(It.IsAny<string>()), Times.Never);
        _restauranteRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private Task EjecutarOperacionAsync(string operacion, Guid restauranteId, Guid usuarioId)
    {
        return operacion switch
        {
            "destacada" => _sut.ActualizarImagenDestacadaAsync(
                restauranteId, usuarioId, null, soloBorrar: true, CancellationToken.None),
            "logo" => _sut.ActualizarLogoAsync(
                restauranteId, usuarioId, null, soloBorrar: true, CancellationToken.None),
            "coleccion" => _sut.ActualizarImagenesColeccionAsync(
                restauranteId,
                usuarioId,
                TipoImagenRestaurante.Interior,
                archivos: null,
                soloBorrar: true,
                CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(operacion), operacion, null)
        };
    }

    private Task EjecutarReemplazoAsync(string operacion, Guid restauranteId)
    {
        var archivo = new ArchivoEntrada(new MemoryStream([1, 2, 3]), "imagen.jpg");
        return operacion switch
        {
            "destacada" => _sut.ActualizarImagenDestacadaAsync(
                restauranteId, _usuarioId, archivo, soloBorrar: false, CancellationToken.None),
            "logo" => _sut.ActualizarLogoAsync(
                restauranteId, _usuarioId, archivo, soloBorrar: false, CancellationToken.None),
            "coleccion" => _sut.ActualizarImagenesColeccionAsync(
                restauranteId,
                _usuarioId,
                TipoImagenRestaurante.Interior,
                [archivo],
                soloBorrar: false,
                CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(operacion), operacion, null)
        };
    }
}
