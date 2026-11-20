using GustosApp.Application.Interfaces;
using StackExchange.Redis;

namespace GustosApp.Infraestructure.Services;

/// <summary>Guarda textos y reservas en Redis con conexión diferida.</summary>
public sealed class AlmacenExplicacionesRedis(string conexion) : IAlmacenExplicaciones, IAsyncDisposable
{
    private const string PublicarSiSigueReservada = """
        if redis.call('GET', KEYS[2]) ~= ARGV[1] then return 0 end
        redis.call('SET', KEYS[1], ARGV[2], 'PX', ARGV[3])
        redis.call('DEL', KEYS[2])
        return 1
        """;

    private readonly Lazy<Task<ConnectionMultiplexer>> _conexion = new(() => ConectarAsync(conexion));

    /// <inheritdoc />
    public async Task<(string? Texto, bool Reservada)> LeerAsync(string clave, CancellationToken ct)
    {
        var db = await ObtenerBaseAsync(ct);
        var valores = await db.StringGetAsync([Texto(clave), Reserva(clave)]).WaitAsync(ct);
        return ((string?)valores[0], valores[1].HasValue);
    }

    /// <inheritdoc />
    public async Task<bool> ReservarAsync(string clave, string propietario, TimeSpan duracion, CancellationToken ct)
    {
        var db = await ObtenerBaseAsync(ct);
        return await db.LockTakeAsync(Reserva(clave), propietario, duracion).WaitAsync(ct);
    }

    /// <inheritdoc />
    public async Task<bool> PublicarAsync(string clave, string propietario, string texto, TimeSpan ttl, CancellationToken ct)
    {
        var db = await ObtenerBaseAsync(ct);
        var resultado = await db.ScriptEvaluateAsync(PublicarSiSigueReservada, [Texto(clave), Reserva(clave)],
            [propietario, texto, (long)ttl.TotalMilliseconds]).WaitAsync(ct);
        return (long)resultado == 1;
    }

    /// <inheritdoc />
    public async Task LiberarAsync(string clave, string propietario, CancellationToken ct)
    {
        var db = await ObtenerBaseAsync(ct);
        await db.LockReleaseAsync(Reserva(clave), propietario).WaitAsync(ct);
    }

    private async Task<IDatabase> ObtenerBaseAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var redis = await _conexion.Value.WaitAsync(ct);
        ct.ThrowIfCancellationRequested();
        return redis.GetDatabase();
    }

    private static Task<ConnectionMultiplexer> ConectarAsync(string conexion)
    {
        var opciones = ConfigurationOptions.Parse(conexion);
        opciones.AbortOnConnectFail = false;
        opciones.ConnectRetry = 0;
        opciones.ConnectTimeout = 1000;
        opciones.AsyncTimeout = 1000;
        opciones.BacklogPolicy = BacklogPolicy.FailFast;
        return ConnectionMultiplexer.ConnectAsync(opciones);
    }

    // Las dos claves comparten slot si Redis se ejecuta en cluster.
    private static RedisKey Texto(string clave) => $"explicacion-gemini:{{{clave}}}:texto";
    private static RedisKey Reserva(string clave) => $"explicacion-gemini:{{{clave}}}:reserva";

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!_conexion.IsValueCreated)
            return;

        try
        {
            var redis = await _conexion.Value;
            await redis.DisposeAsync();
        }
        catch (RedisException)
        {
            // El cierre también puede coincidir con una caída del servidor.
        }
    }
}
