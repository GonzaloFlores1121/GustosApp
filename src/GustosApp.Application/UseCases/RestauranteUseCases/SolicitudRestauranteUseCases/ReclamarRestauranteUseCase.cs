using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;

namespace GustosApp.Application.UseCases.RestauranteUseCases.SolicitudRestauranteUseCases;

public sealed class ReclamarRestauranteUseCase(
    ISolicitudRestauranteRepository solicitudes,
    IRestauranteRepository restaurantes,
    IUsuarioRepository usuarios,
    IFirebaseAuthService firebase)
{
    public async Task<Guid> HandleAsync(string firebaseUid, Guid restauranteId, CancellationToken ct)
    {
        var usuario = await usuarios.GetByFirebaseUidAsync(firebaseUid, ct)
            ?? throw new KeyNotFoundException("Usuario no encontrado.");
        var pendiente = await solicitudes.BuscarReclamoPendienteAsync(usuario.Id, restauranteId, ct);
        if (pendiente != null)
        {
            await firebase.SetUserRoleAsync(usuario.FirebaseUid, RolUsuario.PendienteRestaurante.ToString());
            return pendiente.Id;
        }
        if (usuario.Rol != RolUsuario.Usuario)
            throw new InvalidOperationException("Ya tenés una solicitud pendiente o ya sos dueño de un restaurante.");

        var restaurante = await restaurantes.GetRestauranteByIdAsync(restauranteId, ct)
            ?? throw new KeyNotFoundException("Restaurante no encontrado.");
        if (restaurante.DuenoId.HasValue || !string.IsNullOrWhiteSpace(restaurante.PropietarioUid))
            throw new InvalidOperationException("El restaurante ya tiene propietario.");

        var solicitud = new SolicitudRestaurante
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            Usuario = usuario,
            RestauranteExistenteId = restaurante.Id,
            Nombre = restaurante.Nombre,
            Direccion = restaurante.Direccion,
            Latitud = restaurante.Latitud,
            Longitud = restaurante.Longitud,
            WebsiteUrl = restaurante.WebUrl ?? string.Empty,
            HorariosJson = restaurante.HorariosJson,
            Estado = EstadoSolicitudRestaurante.Pendiente
        };
        // El usuario se obtiene con seguimiento: se guarda junto con la solicitud.
        usuario.Rol = RolUsuario.PendienteRestaurante;
        await solicitudes.AddAsync(solicitud, ct);
        await firebase.SetUserRoleAsync(usuario.FirebaseUid, RolUsuario.PendienteRestaurante.ToString());
        return solicitud.Id;
    }
}
