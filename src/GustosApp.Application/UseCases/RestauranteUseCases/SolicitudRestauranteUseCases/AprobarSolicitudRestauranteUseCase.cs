using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;

namespace GustosApp.Application.UseCases.RestauranteUseCases.SolicitudRestauranteUseCases;

public sealed class AprobarSolicitudRestauranteUseCase(
    ISolicitudRestauranteRepository _solicitudes, IRestauranteRepository _restaurantes,
    IRestriccionRepository _restricciones, IGustoRepository _gustos, ProcesarResolucionSolicitudUseCase procesar)
{
    public async Task<Restaurante> HandleAsync(Guid solicitudId, CancellationToken ct)
    {
        var solicitud = await _solicitudes.GetByIdAsync(solicitudId, ct)
            ?? throw new KeyNotFoundException("Solicitud no encontrada");

        if (solicitud.Estado == EstadoSolicitudRestaurante.Aprobada)
        {
            var aprobado = solicitud.RestauranteAprobadoId is Guid id
                ? await _restaurantes.GetRestauranteConImagenesAsync(id, ct) : null;
            if (aprobado == null)
                throw new InvalidOperationException("La solicitud ya fue aprobada y no tiene un restaurante vinculado disponible.");
            await procesar.CompletarAsync(solicitud.Id, ct);
            return aprobado;
        }

        if (solicitud.Estado != EstadoSolicitudRestaurante.Pendiente)
            throw new InvalidOperationException("Solo se pueden aprobar solicitudes pendientes.");
        if (solicitud.Usuario.Rol == RolUsuario.DuenoRestaurante)
            throw new InvalidOperationException("El solicitante ya tiene un restaurante afiliado.");

        var restaurante = solicitud.RestauranteExistenteId.HasValue
            ? await VincularRestauranteAsync(solicitud, ct)
            : await CrearRestauranteDesdeSolicitud(solicitud, ct);
        solicitud.Usuario.Rol = RolUsuario.DuenoRestaurante;
        solicitud.Estado = EstadoSolicitudRestaurante.Aprobada;
        solicitud.RestauranteAprobadoId = restaurante.Id;
        solicitud.CorreoResolucion = EstadoCorreoResolucion.Pendiente;
        if (!solicitud.RestauranteExistenteId.HasValue && solicitud.Imagenes.Any(i => i.Tipo == TipoImagenSolicitud.Menu))
        {
            solicitud.MenuResolucion = EstadoMenuResolucion.Pendiente;
            solicitud.TokenMenuResolucion = Guid.NewGuid();
        }

        // El mismo commit confirma ficha, propietario, resolución e intenciones posteriores.
        await _restaurantes.SaveChangesAsync(ct);
        await procesar.CompletarAsync(solicitud.Id, ct);
        return restaurante;
    }

    public async Task ReprocesarMenuAsync(Guid solicitudId, CancellationToken ct)
    {
        var solicitud = await _solicitudes.GetByIdAsync(solicitudId, ct)
            ?? throw new KeyNotFoundException("Solicitud no encontrada");
        if (solicitud.Estado != EstadoSolicitudRestaurante.Aprobada || !solicitud.RestauranteAprobadoId.HasValue)
            throw new InvalidOperationException("La solicitud debe estar aprobada y vinculada a un restaurante.");
        if (solicitud.MenuResolucion == EstadoMenuResolucion.NoProgramado)
            throw new InvalidOperationException("No hay un análisis recuperable para esta solicitud.");
        if (!await procesar.CompletarAsync(solicitud.Id, ct, soloMenu: true))
            throw new InvalidOperationException("No se pudo completar el análisis ahora. Consultá el estado e intentá más tarde.");
    }

    private async Task<Restaurante> VincularRestauranteAsync(SolicitudRestaurante solicitud, CancellationToken ct)
    {
        var restaurante = await _restaurantes.GetRestauranteConImagenesAsync(solicitud.RestauranteExistenteId!.Value, ct)
            ?? throw new KeyNotFoundException("Restaurante no encontrado");
        if (restaurante.DuenoId.HasValue || !string.IsNullOrWhiteSpace(restaurante.PropietarioUid))
            throw new InvalidOperationException("El restaurante ya tiene propietario.");
        AsignarPropietario(restaurante, solicitud);
        return restaurante;
    }

    private static void AsignarPropietario(Restaurante restaurante, SolicitudRestaurante solicitud)
    {
        if (string.IsNullOrWhiteSpace(solicitud.Usuario.FirebaseUid))
            throw new InvalidOperationException("El solicitante no tiene una identidad de Firebase válida.");

        restaurante.DuenoId = solicitud.UsuarioId;
        restaurante.Dueno = solicitud.Usuario;
        restaurante.PropietarioUid = solicitud.Usuario.FirebaseUid;
        restaurante.ActualizadoUtc = DateTime.UtcNow;
    }

    private async Task<Restaurante> CrearRestauranteDesdeSolicitud(
    SolicitudRestaurante solicitud,
    CancellationToken ct)
    {
        var restaurante = new Restaurante
        {
            Id = Guid.NewGuid(),
            Nombre = solicitud.Nombre,
            NombreNormalizado = solicitud.Nombre.ToLower().Trim(),
            Direccion = solicitud.Direccion,
            Latitud = solicitud.Latitud ?? 0,
            Longitud = solicitud.Longitud ?? 0,
            PrimaryType = "Restaurante",
            TypesJson = "",
            HorariosJson = solicitud.HorariosJson ?? "{}",
            CreadoUtc = DateTime.UtcNow,
            ActualizadoUtc = DateTime.UtcNow,
            WebUrl = solicitud.WebsiteUrl,
            Rating = null,
        };

        AsignarPropietario(restaurante, solicitud);

        // Relaciones
        restaurante.SetGustos(await _gustos.GetByIdsAsync(solicitud.GustosIds, ct));
        restaurante.SetRestricciones(await _restricciones.GetRestriccionesByIdsAsync(solicitud.RestriccionesIds, ct));

        // Imagen principal y logo
        restaurante.ImagenUrl = solicitud.Imagenes.FirstOrDefault(i => i.Tipo == TipoImagenSolicitud.Destacada)?.Url;
        restaurante.LogoUrl = solicitud.Imagenes.FirstOrDefault(i => i.Tipo == TipoImagenSolicitud.Logo)?.Url;
        restaurante.OrigenImagen = GustosApp.Domain.Common.OrigenImagenRestaurante.Propietario;
        restaurante.OrigenLogo = GustosApp.Domain.Common.OrigenImagenRestaurante.Propietario;

        // Img multiples
        restaurante.Imagenes = solicitud.Imagenes
            .Where(i => i.Tipo == TipoImagenSolicitud.Interior || i.Tipo == TipoImagenSolicitud.Comida)
            .Select(i => new RestauranteImagen
            {
                RestauranteId = restaurante.Id,
                Tipo = i.Tipo == TipoImagenSolicitud.Interior
                    ? TipoImagenRestaurante.Interior
                    : TipoImagenRestaurante.Comida,
                Url = i.Url,
                Origen = GustosApp.Domain.Common.OrigenImagenRestaurante.Propietario,
                FechaCreacionUtc = DateTime.UtcNow
            })
            .ToList();

        await _restaurantes.AddAsync(restaurante, ct);

        return restaurante;
    }
}
