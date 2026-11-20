using GustosApp.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GustosApp.Infraestructure.Services;

public interface IActualizadorExternoRestaurantes
{
    Task<ResultadoLoteActualizacionExterna> ActualizarPendientesAsync(CancellationToken ct);
}

public sealed record ResultadoLoteActualizacionExterna(int Intentados, int Exitosos, int Fallidos, int Omitidos);

public sealed class ActualizadorExternoRestaurantes(
    GustosDbContext db,
    IServicioRestaurantes servicioRestaurantes,
    IOptionsSnapshot<ActualizacionExternaRestaurantesOptions> options,
    ILogger<ActualizadorExternoRestaurantes> logger) : IActualizadorExternoRestaurantes
{
    public async Task<ResultadoLoteActualizacionExterna> ActualizarPendientesAsync(CancellationToken ct)
    {
        var configuracion = options.Value;
        if (!configuracion.Habilitada) return new(0, 0, 0, 0);
        var limiteAntiguedad = DateTime.UtcNow.AddMinutes(-configuracion.IntervaloMinutos);
        var ids = await db.Restaurantes
            .AsNoTracking()
            .Where(r => !string.IsNullOrWhiteSpace(r.PlaceId) && !r.DuenoId.HasValue &&
                string.IsNullOrWhiteSpace(r.PropietarioUid) &&
                (!r.FechaUltimoIntentoActualizacionExternaUtc.HasValue ||
                 r.FechaUltimoIntentoActualizacionExternaUtc < limiteAntiguedad))
            .OrderBy(r => r.FechaUltimoIntentoActualizacionExternaUtc)
            .ThenBy(r => r.Id)
            .Select(r => r.Id)
            .Take(configuracion.TamanoLote)
            .ToListAsync(ct);

        var intentados = 0;
        var exitosos = 0;
        var fallidos = 0;
        var omitidos = 0;
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            if (!options.Value.Habilitada) break;
            intentados++;
            try
            {
                var resultado = await servicioRestaurantes.ActualizarImportadoDesdeGooglePlacesAsync(id, ct);
                switch (resultado)
                {
                    case ResultadoActualizacionExternaRestaurante.Exitosa: exitosos++; break;
                    case ResultadoActualizacionExternaRestaurante.Fallida: fallidos++; break;
                    case ResultadoActualizacionExternaRestaurante.Omitida: omitidos++; break;
                    default: throw new InvalidOperationException("Resultado de actualización desconocido.");
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                fallidos++;
                logger.LogError("Falló la actualización externa de {RestauranteId}. Tipo: {TipoError}", id, ex.GetType().Name);
            }
        }

        return new(intentados, exitosos, fallidos, omitidos);
    }
}
