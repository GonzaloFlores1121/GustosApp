using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using GustosApp.Infraestructure;
using GustosApp.Infraestructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GustosApp.API.Tests;

public class ConcurrenciaSolicitudesPruebas
{
    [Fact]
    public async Task DosLecturasPendientes_SoloUnaPuedeResolverLaSolicitud()
    {
        var opciones = new DbContextOptionsBuilder<GustosDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var primero = new GustosDbContext(opciones);
        await using var segundo = new GustosDbContext(opciones);
        var solicitud = new SolicitudRestaurante { Id = Guid.NewGuid(), Nombre = "Solicitud", Direccion = "Dirección", WebsiteUrl = "", Estado = EstadoSolicitudRestaurante.Pendiente };
        primero.SolicitudesRestaurantes.Add(solicitud);
        await primero.SaveChangesAsync();
        var lecturaAnterior = await segundo.SolicitudesRestaurantes.SingleAsync();
        solicitud.Estado = EstadoSolicitudRestaurante.Aprobada;
        await primero.SaveChangesAsync();
        lecturaAnterior.Estado = EstadoSolicitudRestaurante.Rechazada;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => segundo.SaveChangesAsync());
        segundo.ChangeTracker.Clear();
        Assert.Equal(EstadoSolicitudRestaurante.Aprobada, (await segundo.SolicitudesRestaurantes.SingleAsync()).Estado);
    }

    [Fact]
    public async Task DosReclamos_NoPuedenSobrescribirAlPropietarioGanador()
    {
        var opciones = new DbContextOptionsBuilder<GustosDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var primero = new GustosDbContext(opciones);
        await using var segundo = new GustosDbContext(opciones);
        var restaurante = new Restaurante { Id = Guid.NewGuid(), Nombre = "Ficha importada" };
        primero.Restaurantes.Add(restaurante);
        await primero.SaveChangesAsync();
        var lecturaAnterior = await segundo.Restaurantes.SingleAsync();
        var propietarioGanador = Guid.NewGuid();
        restaurante.DuenoId = propietarioGanador;
        await primero.SaveChangesAsync();
        lecturaAnterior.DuenoId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new RestauranteRepositoryEF(segundo).SaveChangesAsync());
        segundo.ChangeTracker.Clear();
        Assert.Equal(propietarioGanador, (await segundo.Restaurantes.SingleAsync()).DuenoId);
    }
}
