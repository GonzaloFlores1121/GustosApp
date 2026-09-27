using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;

namespace GustosApp.Application.UseCases.RestauranteUseCases.OpinionesRestaurantes;

public sealed class ObtenerMiOpinionRestauranteUseCase
{
    private readonly IOpinionRestauranteRepository _opiniones;
    private readonly IUsuarioRepository _usuarios;

    public ObtenerMiOpinionRestauranteUseCase(IOpinionRestauranteRepository opiniones, IUsuarioRepository usuarios)
    {
        _opiniones = opiniones;
        _usuarios = usuarios;
    }

    public async Task<OpinionRestaurante?> HandleAsync(string firebaseUid, Guid restauranteId, CancellationToken ct)
    {
        var usuario = await _usuarios.GetByFirebaseUidAsync(firebaseUid, ct)
            ?? throw new UnauthorizedAccessException("Usuario no encontrado");

        return await _opiniones.ObtenerPorUsuarioYRestauranteAsync(usuario.Id, restauranteId, ct);
    }
}

public sealed class ActualizarOpinionRestauranteUseCase
{
    private readonly IFileStorageService _archivos;
    private readonly IOpinionRestauranteRepository _opiniones;
    private readonly IUsuarioRepository _usuarios;
    private readonly ActualizarValoracionRestauranteUseCase? _actualizarValoracion;

    public ActualizarOpinionRestauranteUseCase(
        IFileStorageService archivos,
        IOpinionRestauranteRepository opiniones,
        IUsuarioRepository usuarios,
        ActualizarValoracionRestauranteUseCase? actualizarValoracion = null)
    {
        _archivos = archivos;
        _opiniones = opiniones;
        _usuarios = usuarios;
        _actualizarValoracion = actualizarValoracion;
    }

    public async Task HandleAsync(
        string firebaseUid,
        Guid opinionId,
        double valoracion,
        string? opinion,
        string? titulo,
        IReadOnlyCollection<Guid>? fotosConservadasIds,
        IReadOnlyCollection<FileUpload>? imagenesNuevas,
        string? motivoVisita,
        DateTime fechaVisita,
        CancellationToken ct)
    {
        if (valoracion is < 1 or > 5)
            throw new ArgumentException("La valoración debe estar entre 1 y 5.");

        var usuario = await _usuarios.GetByFirebaseUidAsync(firebaseUid, ct)
            ?? throw new UnauthorizedAccessException("Usuario no encontrado");
        var existente = await _opiniones.ObtenerPorIdAsync(opinionId, ct)
            ?? throw new KeyNotFoundException("Reseña no encontrada.");

        if (existente.EsImportada || !existente.UsuarioId.HasValue)
            throw new AccesoProhibidoException("Las reseñas importadas no se pueden editar.");
        if (existente.UsuarioId != usuario.Id)
            throw new AccesoProhibidoException("Sólo el autor puede editar esta reseña.");

        var conservar = (fotosConservadasIds ?? Array.Empty<Guid>()).ToHashSet();
        if (conservar.Except(existente.Fotos.Select(f => f.Id)).Any())
            throw new ArgumentException("Una de las fotos seleccionadas no pertenece a la reseña.");

        var nuevas = imagenesNuevas?.ToList() ?? new List<FileUpload>();
        if (conservar.Count + nuevas.Count is < 1 or > 3)
            throw new ArgumentException("La reseña debe conservar entre 1 y 3 imágenes.");

        var fotosQuitadas = existente.Fotos.Where(f => !conservar.Contains(f.Id)).ToList();
        var urlsNuevas = new List<string>();
        try
        {
            foreach (var archivo in nuevas)
            {
                var url = await _archivos.UploadFileAsync(archivo.Content, archivo.FileName, "opiniones");
                urlsNuevas.Add(url);
                existente.Fotos.Add(new OpinionFoto { Id = Guid.NewGuid(), Url = url });
            }

            foreach (var foto in fotosQuitadas)
                existente.Fotos.Remove(foto);

            existente.Valoracion = valoracion;
            existente.Opinion = opinion;
            existente.Titulo = titulo;
            existente.MotivoVisita = motivoVisita;
            existente.FechaVisita = fechaVisita;

            await _opiniones.ActualizarAsync(existente, ct);
            if (_actualizarValoracion != null)
                await _actualizarValoracion.HandleAsync(existente.RestauranteId, ct);
        }
        catch
        {
            foreach (var url in urlsNuevas)
            {
                try { await _archivos.DeleteFileAsync(url); } catch { }
            }
            throw;
        }

        foreach (var foto in fotosQuitadas)
        {
            if (!await _opiniones.FotoEstaReferenciadaAsync(foto.Url, ct))
            {
                try { await _archivos.DeleteFileAsync(foto.Url); } catch { }
            }
        }
    }
}

public sealed class EliminarOpinionRestauranteUseCase
{
    private readonly IFileStorageService _archivos;
    private readonly IOpinionRestauranteRepository _opiniones;
    private readonly IUsuarioRepository _usuarios;
    private readonly ActualizarValoracionRestauranteUseCase? _actualizarValoracion;

    public EliminarOpinionRestauranteUseCase(
        IFileStorageService archivos,
        IOpinionRestauranteRepository opiniones,
        IUsuarioRepository usuarios,
        ActualizarValoracionRestauranteUseCase? actualizarValoracion = null)
    {
        _archivos = archivos;
        _opiniones = opiniones;
        _usuarios = usuarios;
        _actualizarValoracion = actualizarValoracion;
    }

    public async Task HandleAsync(string firebaseUid, Guid opinionId, CancellationToken ct)
    {
        var usuario = await _usuarios.GetByFirebaseUidAsync(firebaseUid, ct)
            ?? throw new UnauthorizedAccessException("Usuario no encontrado");
        var existente = await _opiniones.ObtenerPorIdAsync(opinionId, ct)
            ?? throw new KeyNotFoundException("Reseña no encontrada.");

        if (existente.EsImportada || !existente.UsuarioId.HasValue)
            throw new AccesoProhibidoException("Las reseñas importadas no se pueden eliminar.");
        if (existente.UsuarioId != usuario.Id)
            throw new AccesoProhibidoException("Sólo el autor puede eliminar esta reseña.");

        var urls = existente.Fotos.Select(f => f.Url).Distinct().ToList();
        await _opiniones.EliminarAsync(existente, ct);
        if (_actualizarValoracion != null)
            await _actualizarValoracion.HandleAsync(existente.RestauranteId, ct);

        foreach (var url in urls)
        {
            if (!await _opiniones.FotoEstaReferenciadaAsync(url, ct))
            {
                try { await _archivos.DeleteFileAsync(url); } catch { }
            }
        }
    }
}
