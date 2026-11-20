using GustosApp.Application.Interfaces;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace GustosApp.Infraestructure.Services;

public sealed class AdministracionRestaurantes(GustosDbContext db, IApplicationTransaction transaccion,
    TimeProvider reloj) : IAdministracionRestaurantes
{
    public async Task<PaginaAdministracionRestaurantes> BuscarAsync(string? texto, bool? desactivado,
        int pagina, int tamanoPagina, CancellationToken ct)
    {
        if (pagina is < 1 or > 1000000 || tamanoPagina is < 1 or > 100 || texto?.Length > 160)
            throw new ArgumentException("Revisá los filtros y la página solicitada.");
        var consulta = db.Restaurantes.AsNoTracking();
        if (desactivado.HasValue) consulta = consulta.Where(r => r.Desactivado == desactivado.Value);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var termino = texto.Trim();
            if (Guid.TryParse(termino, out var id)) consulta = consulta.Where(r => r.Id == id);
            else consulta = consulta.Where(r => r.Nombre.Contains(termino) || r.Direccion.Contains(termino) || r.PlaceId == termino);
        }
        var total = await consulta.CountAsync(ct);
        var elementos = await consulta.OrderBy(r => r.Nombre).ThenBy(r => r.Id)
            .Skip(checked((pagina - 1) * tamanoPagina)).Take(tamanoPagina)
            .Select(r => new RestauranteAdministracion(r.Id, r.Nombre, r.Direccion, r.PlaceId, r.DuenoId,
                r.Desactivado, r.VersionDisponibilidad, r.MotivoDisponibilidad, r.FechaCambioDisponibilidadUtc))
            .ToListAsync(ct);
        return new(elementos, total, pagina, tamanoPagina);
    }

    public Task<RestauranteAdministracion> CambiarDisponibilidadAsync(Guid id, CambioDisponibilidadRestaurante cambio,
        string actorUid, CancellationToken ct) => EjecutarAsync(async token =>
    {
        await ExigirAdminAsync(actorUid, token);
        var restaurante = await db.Restaurantes.SingleOrDefaultAsync(r => r.Id == id, token)
            ?? throw new KeyNotFoundException("Restaurante no encontrado.");
        await db.Entry(restaurante).ReloadAsync(token);
        // Nunca guardar un grafo desconectado: solo cambian los campos de disponibilidad.
        if (restaurante.CambiarDisponibilidad(cambio.Desactivado, cambio.VersionEsperada, cambio.Motivo, reloj.GetUtcNow().UtcDateTime))
        {
            db.RegistrosAdministracionRestaurantes.Add(new() { RestauranteId = id, ActorUid = actorUid,
                Accion = cambio.Desactivado ? "Desactivar" : "Reactivar", Motivo = cambio.Motivo.Trim(), FechaUtc = reloj.GetUtcNow().UtcDateTime });
            await db.SaveChangesAsync(token);
        }
        return new RestauranteAdministracion(restaurante.Id, restaurante.Nombre, restaurante.Direccion,
            restaurante.PlaceId, restaurante.DuenoId, restaurante.Desactivado, restaurante.VersionDisponibilidad,
            restaurante.MotivoDisponibilidad, restaurante.FechaCambioDisponibilidadUtc);
    }, ct);

    public async Task CambiarArchivoAsync(Guid id, CambioArchivoSolicitud cambio, string actorUid, CancellationToken ct)
        => await EjecutarAsync(async token =>
        {
            await ExigirAdminAsync(actorUid, token);
            var solicitud = await db.SolicitudesRestaurantes.SingleOrDefaultAsync(s => s.Id == id, token)
                ?? throw new KeyNotFoundException("Solicitud no encontrada.");
            await db.Entry(solicitud).ReloadAsync(token);
            if (solicitud.CambiarArchivo(cambio.Archivada, cambio.VersionEsperada, reloj.GetUtcNow().UtcDateTime))
            {
                db.RegistrosAdministracionRestaurantes.Add(new() { SolicitudId = id, ActorUid = actorUid,
                    Accion = cambio.Archivada ? "Archivar" : "Restaurar", FechaUtc = reloj.GetUtcNow().UtcDateTime });
                await db.SaveChangesAsync(token);
            }
            return true;
        }, ct);

    private async Task ExigirAdminAsync(string uid, CancellationToken ct)
    {
        if (!await db.Usuarios.AnyAsync(u => u.FirebaseUid == uid && u.Activo && u.Rol == RolUsuario.Admin, ct))
            throw new AccesoProhibidoException("Solo un administrador puede realizar esta acción.");
    }

    private async Task<T> EjecutarAsync<T>(Func<CancellationToken, Task<T>> accion, CancellationToken ct)
    {
        try { return await transaccion.ExecuteAsync(accion, ct); }
        catch (DbUpdateConcurrencyException)
        { throw new InvalidOperationException("Los datos cambiaron. Recargá la pantalla antes de continuar."); }
        catch (Exception ex) when (ex is SqlException { Number: 1205 } || ex.InnerException is SqlException { Number: 1205 })
        { throw new InvalidOperationException("Otra operación cambió estos datos. Recargá la pantalla y volvé a intentar."); }
    }
}
