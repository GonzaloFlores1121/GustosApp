using System.Net;
using System.Net.Http.Json;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Infraestructure;
using GustosApp.Infraestructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GustosApp.API.Tests;

public sealed class DisponibilidadVotacionPruebas
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResultadoInactivoNoSeCierraNiResuelveConRuletaYSeConservanVotos(bool empate)
    {
        using var fabrica = AdministracionDisponibilidadPruebas.CrearFabrica();
        await AdministracionDisponibilidadPruebas.PrepararAdminAsync(fabrica);
        var datos = await SembrarAsync(fabrica, empate);
        await DesactivarAsync(fabrica, datos.RestauranteId);
        using var cliente = AdministracionDisponibilidadPruebas.Cliente(fabrica);
        Assert.Equal(HttpStatusCode.Conflict, (await cliente.PostAsJsonAsync($"/Votacion/{datos.VotacionId}/votar", new { restauranteId = datos.RestauranteId })).StatusCode);
        var accion = empate ? "seleccionar-ganador" : "cerrar";
        var response = await cliente.PostAsJsonAsync($"/Votacion/{datos.VotacionId}/{accion}", new { });
        // La ruta de cierre no acepta reemplazar un resultado por otra opción silenciosamente.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var lectura = fabrica.Services.CreateAsyncScope();
        var db = lectura.ServiceProvider.GetRequiredService<GustosDbContext>();
        var votacion = await db.Votaciones.Include(v => v.Votos).SingleAsync(v => v.Id == datos.VotacionId);
        Assert.Equal(EstadoVotacion.Activa, votacion.Estado);
        Assert.Null(votacion.RestauranteGanadorId);
        Assert.Equal(2, votacion.Votos.Count);
    }

    [Fact]
    public async Task RevalidaAlPersistirVotoGanadorYCandidaturaAunqueLaFichaCargadaEraActiva()
    {
        using var fabrica = AdministracionDisponibilidadPruebas.CrearFabrica();
        await AdministracionDisponibilidadPruebas.PrepararAdminAsync(fabrica);
        var datos = await SembrarAsync(fabrica, false);
        await using var anterior = fabrica.Services.CreateAsyncScope();
        var db = anterior.ServiceProvider.GetRequiredService<GustosDbContext>();
        var repo = new VotacionRepository(db);
        var cargada = (await repo.ObtenerPorIdConCandidatosAsync(datos.VotacionId))!;
        var fichaAnterior = cargada.RestaurantesCandidatos.Single(c => c.RestauranteId == datos.RestauranteId).Restaurante;
        Assert.False(fichaAnterior.Desactivado);
        await DesactivarAsync(fabrica, datos.RestauranteId);
        Assert.False(fichaAnterior.Desactivado);
        var voto = new VotoRestaurante(datos.VotacionId, datos.UsuarioId, datos.RestauranteId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.RegistrarVotoAsync(voto));
        cargada.CerrarVotacion(datos.RestauranteId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.ActualizarVotacionAsync(cargada));
        var nueva = new VotacionGrupo(datos.GrupoId);
        nueva.RestaurantesCandidatos.Add(new(nueva.Id, datos.RestauranteId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.CrearVotacionAsync(nueva));
        await using var actual = fabrica.Services.CreateAsyncScope();
        var final = actual.ServiceProvider.GetRequiredService<GustosDbContext>();
        Assert.Equal(EstadoVotacion.Activa, (await final.Votaciones.FindAsync(datos.VotacionId))!.Estado);
        Assert.Equal(2, await final.Votos.CountAsync());
        Assert.Single(await final.Votaciones.ToListAsync());
        Assert.True((await final.Restaurantes.FindAsync(datos.RestauranteId))!.Desactivado);
    }

    [Fact]
    public async Task DesactivarNoReescribeGanadorConfirmadoNiImpideConsultarHistorial()
    {
        using var fabrica = AdministracionDisponibilidadPruebas.CrearFabrica();
        await AdministracionDisponibilidadPruebas.PrepararAdminAsync(fabrica);
        var datos = await SembrarAsync(fabrica, false);
        await using (var scope = fabrica.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
            var votacion = (await db.Votaciones.FindAsync(datos.VotacionId))!;
            votacion.CerrarVotacion(datos.RestauranteId);
            await new VotacionRepository(db).ActualizarVotacionAsync(votacion);
        }
        await DesactivarAsync(fabrica, datos.RestauranteId);
        await using var lectura = fabrica.Services.CreateAsyncScope();
        var final = lectura.ServiceProvider.GetRequiredService<GustosDbContext>();
        var historico = await new VotacionRepository(final).ObtenerHistorialVotacionesAsync(datos.GrupoId, 1, 20);
        var registrada = Assert.Single(historico.Votaciones);
        Assert.Equal(datos.RestauranteId, registrada.RestauranteGanadorId);
        Assert.Equal(EstadoVotacion.Cerrada, registrada.Estado);
        Assert.Equal(2, registrada.Votos.Count);
        using var cliente = AdministracionDisponibilidadPruebas.Cliente(fabrica);
        using var payload = System.Text.Json.JsonDocument.Parse(await cliente.GetStringAsync($"/Votacion/grupo/{datos.GrupoId}/historial"));
        var ganador = Assert.Single(payload.RootElement.GetProperty("votaciones").EnumerateArray()).GetProperty("ganador");
        Assert.Equal(datos.RestauranteId, ganador.GetProperty("restauranteId").GetGuid());
        Assert.True(ganador.GetProperty("desactivado").GetBoolean());
    }

    private static async Task DesactivarAsync(FabricaFiltrosMapa fabrica, Guid id)
    {
        await using var scope = fabrica.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAdministracionRestaurantes>()
            .CambiarDisponibilidadAsync(id, new(true, 0, "Prueba de baja"), FabricaFiltrosMapa.Uid, default);
    }

    private static async Task<Datos> SembrarAsync(FabricaFiltrosMapa fabrica, bool empate)
    {
        await using var scope = fabrica.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
        var admin = await db.Usuarios.SingleAsync(u => u.FirebaseUid == FabricaFiltrosMapa.Uid);
        var otro = await db.Usuarios.SingleAsync(u => u.FirebaseUid == "amiga-filtros");
        var grupo = new Grupo("Grupo disponibilidad", admin.Id);
        grupo.Miembros.Add(new MiembroGrupo(grupo.Id, admin.Id) { ParticipaEnRecomendacion = true });
        grupo.Miembros.Add(new MiembroGrupo(grupo.Id, otro.Id) { ParticipaEnRecomendacion = true });
        var fichas = await db.Restaurantes.Take(2).ToListAsync();
        var votacion = new VotacionGrupo(grupo.Id) { Grupo = grupo };
        foreach (var ficha in fichas) votacion.RestaurantesCandidatos.Add(new(votacion.Id, ficha.Id));
        votacion.Participantes.Add(new(votacion.Id, admin.Id));
        votacion.Participantes.Add(new(votacion.Id, otro.Id));
        votacion.Votos.Add(new(votacion.Id, admin.Id, fichas[0].Id));
        votacion.Votos.Add(new(votacion.Id, otro.Id, fichas[empate ? 1 : 0].Id));
        db.Add(votacion);
        await db.SaveChangesAsync();
        return new(grupo.Id, votacion.Id, fichas[0].Id, admin.Id);
    }
    private sealed record Datos(Guid GrupoId, Guid VotacionId, Guid RestauranteId, Guid UsuarioId);
}
