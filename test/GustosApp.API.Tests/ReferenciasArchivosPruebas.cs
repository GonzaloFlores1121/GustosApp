using GustosApp.Domain.Model;
using GustosApp.Infraestructure;
using GustosApp.Infraestructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GustosApp.API.Tests;

public sealed class ReferenciasArchivosPruebas
{
    [Fact]
    public async Task ImagenDeSolicitudAprobadaSigueProtegidaAunqueLaSolicitudCambieDeEstado()
    {
        await using var db = CrearContexto();
        var solicitudId = Guid.NewGuid();
        var url = "https://archivo/compartido.jpg";
        var solicitud = new SolicitudRestaurante
        {
            Id = solicitudId, UsuarioId = Guid.NewGuid(), Nombre = "Prueba",
            Direccion = "Calle", WebsiteUrl = "", Imagenes = new List<SolicitudRestauranteImagen>
            {
                new() { Id = Guid.NewGuid(), SolicitudId = solicitudId, Url = url }
            }
        };
        db.SolicitudesRestaurantes.Add(solicitud);
        await db.SaveChangesAsync();

        Assert.True(await FirebaseStorageService.ArchivoEstaReferenciadoAsync(db, url));
        Assert.False(await FirebaseStorageService.ArchivoEstaReferenciadoAsync(db, "https://archivo/otro.jpg"));
    }

    [Fact]
    public async Task ImagenDeMenuPublicadaSigueProtegidaTrasDescartarBorrador()
    {
        await using var db = CrearContexto();
        var menu = new RestauranteMenu { RestauranteId = Guid.NewGuid() };
        var token = Guid.NewGuid();
        menu.IniciarBorrador(token, "https://archivo/menu.jpg", DateTime.UtcNow);
        menu.CompletarBorrador(token, "{}", DateTime.UtcNow);
        menu.PublicarBorrador(token, "{}", DateTime.UtcNow);
        db.RestauranteMenus.Add(menu);
        await db.SaveChangesAsync();

        Assert.True(await FirebaseStorageService.ArchivoEstaReferenciadoAsync(db, "https://archivo/menu.jpg"));
    }

    private static GustosDbContext CrearContexto() => new(new DbContextOptionsBuilder<GustosDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
