using AutoMapper;
using FluentAssertions;
using GustosApp.API.DTO;
using GustosApp.API.Mapping.RestaurantesMapper;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.RestauranteUseCases;
using GustosApp.Domain.Common;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using GustosApp.Infraestructure.Repositories;
using GustosApp.Infraestructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GustosApp.API.Tests;

public sealed class GaleriaRestaurantePersistenciaPruebas
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ImagenUnica_ReemplazarYEliminar_PersisteSinModificarLaOtraImagen(bool logo)
    {
        await using var db = Infraestructura.DbContextEnMemoria.Crear(Guid.NewGuid().ToString());
        var propietario = new Usuario("uid-unica", "unica@example.test", "Ana", "Prueba", "ana-unica");
        var restaurante = new Restaurante { Dueno = propietario, DuenoId = propietario.Id, Nombre = "Imagen única",
            NombreNormalizado = "imagen unica", Direccion = "Dirección", PlaceId = "google-conservado",
            ImagenUrl = "https://foto.test/portada.png", LogoUrl = "https://foto.test/logo.png" };
        db.AddRange(propietario, restaurante);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var storage = new Mock<IFileStorageService>();
        storage.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("https://foto.test/reemplazo.png");
        var caso = new ActualizarImagenesRestauranteUseCase(new RestauranteRepositoryEF(db), storage.Object);
        var archivo = new ArchivoEntrada(new MemoryStream([1, 2, 3]), "foto.png");
        if (logo) await caso.ActualizarLogoAsync(restaurante.Id, propietario.Id, archivo, false, default);
        else await caso.ActualizarImagenDestacadaAsync(restaurante.Id, propietario.Id, archivo, false, default);
        db.ChangeTracker.Clear();
        var recargado = await new RestauranteRepositoryEF(db).GetRestauranteConImagenesAsync(restaurante.Id, default);
        (logo ? recargado!.LogoUrl : recargado!.ImagenUrl).Should().Be("https://foto.test/reemplazo.png");
        (logo ? recargado.ImagenUrl : recargado.LogoUrl).Should().Be(logo ? "https://foto.test/portada.png" : "https://foto.test/logo.png");
        if (logo) await caso.ActualizarLogoAsync(restaurante.Id, propietario.Id, null, true, default);
        else await caso.ActualizarImagenDestacadaAsync(restaurante.Id, propietario.Id, null, true, default);
        db.ChangeTracker.Clear();
        var final = await new RestauranteRepositoryEF(db).GetRestauranteConImagenesAsync(restaurante.Id, default);
        (logo ? final!.LogoUrl : final!.ImagenUrl).Should().BeNull();
        (logo ? final.ImagenUrl : final.LogoUrl).Should().Be(logo ? "https://foto.test/portada.png" : "https://foto.test/logo.png");
        final.PlaceId.Should().Be("google-conservado");
    }

    [Theory]
    [InlineData(TipoImagenRestaurante.Interior)]
    [InlineData(TipoImagenRestaurante.Comida)]
    public async Task AgregarReemplazarEliminar_RecargaConIdsEstablesYRechazaUnaEdicionObsoleta(TipoImagenRestaurante tipo)
    {
        await using var db = Infraestructura.DbContextEnMemoria.Crear(Guid.NewGuid().ToString());
        var propietario = new Usuario("uid-galeria", "galeria@example.test", "Ana", "Prueba", "ana-galeria");
        var restaurante = new Restaurante { Dueno = propietario, DuenoId = propietario.Id, Nombre = "Galería",
            NombreNormalizado = "galeria", Direccion = "Dirección", PlaceId = "google-conservado", ImagenUrl = "https://foto.test/portada.png" };
        var anterior = new RestauranteImagen { RestauranteId = restaurante.Id, Tipo = tipo, Url = "https://foto.test/anterior.png", Orden = 0 };
        restaurante.Imagenes.Add(anterior);
        db.AddRange(propietario, restaurante);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var storage = new Mock<IFileStorageService>();
        storage.SetupSequence(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://foto.test/nueva.png").ReturnsAsync("https://foto.test/reemplazo.png");
        var caso = new ActualizarImagenesRestauranteUseCase(new RestauranteRepositoryEF(db), storage.Object, transaccion: new EfApplicationTransaction(db));
        var resultado = await caso.AgregarImagenesAsync(restaurante.Id, propietario.Id, tipo,
            [new ArchivoEntrada(new MemoryStream([1, 2, 3]), "foto.png")], default);
        resultado.Should().HaveCount(2);
        var agregada = resultado.Single(i => i.Id != anterior.Id);
        agregada.Id.Should().NotBeEmpty();
        db.ChangeTracker.Clear();
        var recargado = await new RestauranteRepositoryEF(db).GetRestauranteConImagenesAsync(restaurante.Id, default);
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<RestauranteDetalleProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var dto = mapper.Map<RestauranteDetalleDto>(recargado);
        dto.ImagenesGaleria.Select(i => i.Id).Should().BeEquivalentTo(new[] { anterior.Id, agregada.Id });
        dto.EsDeLaApp.Should().BeFalse();
        recargado!.PlaceId.Should().Be("google-conservado");
        recargado.ImagenUrl.Should().Be("https://foto.test/portada.png");
        var reemplazadas = await caso.ModificarImagenAsync(restaurante.Id, propietario.Id, tipo, agregada.Id,
            new ArchivoEntrada(new MemoryStream([1, 2, 3]), "reemplazo.png"), default);
        var reemplazo = reemplazadas.Single(i => i.Id != anterior.Id);
        reemplazo.Id.Should().NotBe(agregada.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => caso.ModificarImagenAsync(restaurante.Id, propietario.Id, tipo, agregada.Id, null, default));
        db.ChangeTracker.Clear();
        await caso.ModificarImagenAsync(restaurante.Id, propietario.Id, tipo, reemplazo.Id, null, default);
        db.ChangeTracker.Clear();
        var final = await new RestauranteRepositoryEF(db).GetRestauranteConImagenesAsync(restaurante.Id, default);
        final!.Imagenes.Should().ContainSingle(i => i.Id == anterior.Id && i.Url == anterior.Url);
        storage.Verify(s => s.DeleteFileAsync(anterior.Url, It.IsAny<string>()), Times.Never);
    }
}
