using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;

namespace GustosApp.Application.UseCases.RestauranteUseCases.OpinionesRestaurantes;

public sealed class ObtenerHistorialOpinionesUsuarioUseCase(
    IUsuarioRepository usuarios,
    ISolicitudAmistadRepository amistades,
    IOpinionRestauranteRepository opiniones)
{
    public async Task<List<OpinionRestaurante>> HandleAsync(
        string firebaseUidSolicitante,
        Guid usuarioObjetivoId,
        CancellationToken ct)
    {
        var solicitante = await usuarios.GetByFirebaseUidAsync(firebaseUidSolicitante, ct)
            ?? throw new UnauthorizedAccessException("Usuario autenticado no encontrado.");
        var objetivo = await usuarios.GetByIdAsync(usuarioObjetivoId, ct)
            ?? throw new KeyNotFoundException("Usuario no encontrado.");

        var puedeVer = solicitante.Id == objetivo.Id || !objetivo.EsPrivado;
        if (!puedeVer)
        {
            var amistad = await amistades.GetAmistadEntreUsuariosAsync(solicitante.Id, objetivo.Id, ct);
            puedeVer = amistad?.Estado == EstadoSolicitud.Aceptada;
        }

        if (!puedeVer)
            throw new AccesoProhibidoException("El historial de reseñas de este perfil es privado.");

        return await opiniones.ObtenerPorUsuarioAsync(usuarioObjetivoId, ct);
    }
}
