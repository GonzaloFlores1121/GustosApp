using GustosApp.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GustosApp.Infraestructure.Services;

public interface IActualizadorExternoRestaurantes
{
    Task<int> ActualizarPendientesAsync(CancellationToken ct);
}

public sealed class ActualizadorExternoRestaurantes(
    GustosDbContext db,
    IServicioRestaurantes servicioRestaurantes,
    IOptionsSnapshot<ActualizacionExternaRestaurantesOptions> options,
    ILogger<ActualizadorExternoRestaurantes> logger) : IActualizadorExternoRestaurantes
{
    public async Task<int> ActualizarPendientesAsync(CancellationToken ct)
    {
        var configuracion = options.Value;
        var limiteAntiguedad = DateTime.UtcNow.AddMinutes(-configuracion.IntervaloMinutos);
        var placeIds = await db.Restaurantes
            .AsNoTracking()
            .Where(r => !string.IsNullOrWhiteSpace(r.PlaceId) &&
                (!r.FechaUltimoIntentoActualizacionExternaUtc.HasValue ||
                 r.FechaUltimoIntentoActualizacionExternaUtc < limiteAntiguedad))
            .OrderBy(r => r.FechaUltimoIntentoActualizacionExternaUtc)
            .ThenBy(r => r.Id)
            .Select(r => r.PlaceId)
            .Take(configuracion.TamanoLote)
            .ToListAsync(ct);

        var procesados = 0;
        foreach (var placeId in placeIds)
        {
            try
            {
                await servicioRestaurantes.ObtenerResenasDesdeGooglePlaces(placeId, ct);
                procesados++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falló la actualización externa del restaurante {PlaceId}", placeId);
            }
        }

        return procesados;
    }
}
