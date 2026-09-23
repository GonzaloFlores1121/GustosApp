using AutoMapper;
using FluentAssertions;
using GustosApp.API.DTO;
using GustosApp.API.Mapping.RestaurantesMapper;
using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.RestauranteUseCases;
using GustosApp.Domain.Common;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using GustosApp.Infraestructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GustosApp.API.Tests;

public sealed class ImagenMenuRestaurantePersistenciaPruebas
{
    [Fact]
    public async Task ActualizarImagenMenu_PersisteYSeRecuperaEnDetalleTrasRecargar()
    {
        await using var contexto = Infraestructura.DbContextEnMemoria.Crear(Guid.NewGuid().ToString());
        var usuarioId = Guid.NewGuid();
        var restauranteId = Guid.NewGuid();
        var usuario = new Usuario(
            "firebase-menu",
            "menu@example.test",
            "Ana",
            "Pérez",
            "ana-menu")
        {
            Id = usuarioId
        };
        var restaurante = new Restaurante
        {
            Id = restauranteId,
            DuenoId = usuarioId,
            Dueno = usuario,
            PropietarioUid = usuario.FirebaseUid,
            Nombre = "Restaurante",
            NombreNormalizado = "restaurante",
            Direccion = "Dirección"
        };
        contexto.AddRange(usuario, restaurante);
        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();

        var almacenamiento = new Mock<IFileStorageService>();
        almacenamiento.Setup(servicio => servicio.UploadFileAsync(
                It.IsAny<Stream>(),
                "menu.jpg",
                "restaurantes"))
            .ReturnsAsync("https://imagenes.test/menu-persistido.jpg");
        var repositorio = new RestauranteRepositoryEF(contexto);
        var casoDeUso = new ActualizarImagenesRestauranteUseCase(repositorio, almacenamiento.Object);
        var archivo = new ArchivoEntrada(new MemoryStream([1, 2, 3]), "menu.jpg");

        var urls = await casoDeUso.ActualizarImagenesColeccionAsync(
            restauranteId,
            usuarioId,
            TipoImagenRestaurante.Menu,
            [archivo],
            soloBorrar: false,
            CancellationToken.None);

        urls.Should().ContainSingle().Which.Should().Be("https://imagenes.test/menu-persistido.jpg");
        contexto.ChangeTracker.Clear();
        var recargado = await new RestauranteRepositoryEF(contexto)
            .GetRestauranteConImagenesAsync(restauranteId, CancellationToken.None);
        recargado.Should().NotBeNull();
        recargado!.Imagenes.Should().ContainSingle(imagen =>
            imagen.Tipo == TipoImagenRestaurante.Menu &&
            imagen.Url == "https://imagenes.test/menu-persistido.jpg");

        var configuracion = new MapperConfiguration(
            cfg => cfg.AddProfile<RestauranteDetalleProfile>(),
            NullLoggerFactory.Instance);
        var detalle = configuracion.CreateMapper().Map<RestauranteDetalleDto>(recargado);
        detalle.ImagenMenu.Should().Be("https://imagenes.test/menu-persistido.jpg");
    }
}
