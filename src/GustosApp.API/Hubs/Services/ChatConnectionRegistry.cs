using System.Collections.Concurrent;

namespace GustosApp.API.Hubs.Services;

public interface IChatConnectionRegistry
{
    void RegistrarConexion(string connectionId, string firebaseUid);
    IReadOnlyCollection<Guid> EliminarConexion(string connectionId);
    void RegistrarGrupo(string connectionId, Guid grupoId);
    void QuitarGrupo(string connectionId, Guid grupoId);
    IReadOnlyList<string> ObtenerConexiones(string firebaseUid, Guid grupoId);
    IReadOnlyList<string> ObtenerUsuarios(Guid grupoId);
    Task<T> EnGrupoAsync<T>(Guid grupoId, Func<Task<T>> accion);
    Task EnGrupoAsync(Guid grupoId, Func<Task> accion);
}

public sealed class ChatConnectionRegistry : IChatConnectionRegistry
{
    private sealed record Conexion(string FirebaseUid)
    {
        public ConcurrentDictionary<Guid, byte> Grupos { get; } = new();
    }

    private readonly ConcurrentDictionary<string, Conexion> _conexiones = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _bloqueosPorGrupo = new();

    public void RegistrarConexion(string connectionId, string firebaseUid) =>
        _conexiones[connectionId] = new Conexion(firebaseUid);

    public IReadOnlyCollection<Guid> EliminarConexion(string connectionId) =>
        _conexiones.TryRemove(connectionId, out var conexion)
            ? conexion.Grupos.Keys.ToArray()
            : Array.Empty<Guid>();

    public void RegistrarGrupo(string connectionId, Guid grupoId)
    {
        if (_conexiones.TryGetValue(connectionId, out var conexion))
            conexion.Grupos[grupoId] = 0;
    }

    public void QuitarGrupo(string connectionId, Guid grupoId)
    {
        if (_conexiones.TryGetValue(connectionId, out var conexion))
            conexion.Grupos.TryRemove(grupoId, out _);
    }

    public IReadOnlyList<string> ObtenerConexiones(string firebaseUid, Guid grupoId) =>
        _conexiones
            .Where(item =>
                item.Value.FirebaseUid == firebaseUid &&
                item.Value.Grupos.ContainsKey(grupoId))
            .Select(item => item.Key)
            .ToList();

    public IReadOnlyList<string> ObtenerUsuarios(Guid grupoId) =>
        _conexiones.Values
            .Where(conexion => conexion.Grupos.ContainsKey(grupoId))
            .Select(conexion => conexion.FirebaseUid)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(uid => uid, StringComparer.Ordinal)
            .ToList();

    public async Task<T> EnGrupoAsync<T>(Guid grupoId, Func<Task<T>> accion)
    {
        var bloqueo = _bloqueosPorGrupo.GetOrAdd(grupoId, _ => new SemaphoreSlim(1, 1));
        await bloqueo.WaitAsync();
        try { return await accion(); }
        finally { bloqueo.Release(); }
    }

    public Task EnGrupoAsync(Guid grupoId, Func<Task> accion) =>
        EnGrupoAsync(grupoId, async () =>
        {
            await accion();
            return true;
        });
}
