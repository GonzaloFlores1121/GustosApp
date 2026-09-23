using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;

namespace GustosApp.Application.UseCases.RestauranteUseCases.SolicitudRestauranteUseCases;

public sealed class ObtenerMiSolicitudRestauranteUseCase(
    ISolicitudRestauranteRepository solicitudes,
    IUsuarioRepository usuarios)
{
    public async Task<SolicitudRestaurante?> HandleAsync(string firebaseUid, CancellationToken ct)
    {
        var usuario = await usuarios.GetByFirebaseUidAsync(firebaseUid, ct)
            ?? throw new KeyNotFoundException("Usuario no encontrado.");

        return await solicitudes.BuscarUltimaPorUsuarioAsync(usuario.Id, ct);
    }
}
