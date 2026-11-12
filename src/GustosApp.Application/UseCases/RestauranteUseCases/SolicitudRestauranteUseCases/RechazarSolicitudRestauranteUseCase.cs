using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model.@enum;

namespace GustosApp.Application.UseCases.RestauranteUseCases.SolicitudRestauranteUseCases;

public sealed class RechazarSolicitudRestauranteUseCase(
    ISolicitudRestauranteRepository solicitudes, ProcesarResolucionSolicitudUseCase procesar)
{
    public async Task HandleAsync(Guid solicitudId, string motivo, CancellationToken ct = default)
    {
        var solicitud = await solicitudes.GetByIdAsync(solicitudId, ct)
            ?? throw new KeyNotFoundException("Solicitud no encontrada");
        if (solicitud.Estado is not (EstadoSolicitudRestaurante.Pendiente or EstadoSolicitudRestaurante.Rechazada))
            throw new InvalidOperationException("Solo se pueden rechazar solicitudes pendientes.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("Debe especificar un motivo de rechazo.");

        if (solicitud.Estado == EstadoSolicitudRestaurante.Pendiente)
        {
            solicitud.Estado = EstadoSolicitudRestaurante.Rechazada;
            solicitud.MotivoRechazo = motivo;
            solicitud.CorreoResolucion = EstadoCorreoResolucion.Pendiente;
            await solicitudes.UpdateAsync(solicitud, ct);
        }
        await procesar.CompletarAsync(solicitud.Id, ct);
    }
}
