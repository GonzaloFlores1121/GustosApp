using GustosApp.Application.Services;
using GustosApp.Infraestructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GustosApp.API.Tests;

public sealed class RedisExplicacionesFactAttribute : FactAttribute
{
    public RedisExplicacionesFactAttribute()
    {
        if (!int.TryParse(Environment.GetEnvironmentVariable("GUSTOS_REDIS_PRUEBAS_PUERTO"), out var puerto) ||
            puerto is < 1 or > 65_535)
            Skip = "Requiere un Redis local aislado en GUSTOS_REDIS_PRUEBAS_PUERTO.";
    }
}

public sealed class AlmacenExplicacionesRedisPruebas
{
    [RedisExplicacionesFact]
    public async Task DosCoordinadoresConRedisRealReusanLaMismaGeneracion()
    {
        var puerto = int.Parse(Environment.GetEnvironmentVariable("GUSTOS_REDIS_PRUEBAS_PUERTO")!);
        await using var almacenA = new AlmacenExplicacionesRedis($"127.0.0.1:{puerto}");
        await using var almacenB = new AlmacenExplicacionesRedis($"127.0.0.1:{puerto}");
        var opciones = Options.Create(new GeminiSettings { CacheExplicacionesTtlSegundos = 3 });
        var cacheA = new CacheExplicaciones(almacenA, opciones, TimeProvider.System, NullLogger<CacheExplicaciones>.Instance);
        var cacheB = new CacheExplicaciones(almacenB, opciones, TimeProvider.System, NullLogger<CacheExplicaciones>.Instance);
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var clave = Guid.NewGuid().ToString("N");
        await almacenA.LeerAsync(clave, limite.Token);
        await almacenB.LeerAsync(clave, limite.Token);
        var inicio = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var respuesta = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var generaciones = 0;
        Task<string?> Generar(CancellationToken ct)
        {
            Interlocked.Increment(ref generaciones);
            inicio.TrySetResult();
            return respuesta.Task.WaitAsync(ct);
        }

        var primero = cacheA.ObtenerAsync(clave, Generar, limite.Token);
        await inicio.Task.WaitAsync(limite.Token);
        var segundo = cacheB.ObtenerAsync(clave, Generar, limite.Token);
        respuesta.SetResult("Texto compartido.");

        Assert.Equal(["Texto compartido.", "Texto compartido."], await Task.WhenAll(primero, segundo));
        Assert.Equal(1, generaciones);
    }

    [RedisExplicacionesFact]
    public async Task DosConexionesRespetanAlPropietarioYLosTiemposDeExpiracion()
    {
        var puerto = int.Parse(Environment.GetEnvironmentVariable("GUSTOS_REDIS_PRUEBAS_PUERTO")!);
        await using var primero = new AlmacenExplicacionesRedis($"127.0.0.1:{puerto}");
        await using var segundo = new AlmacenExplicacionesRedis($"127.0.0.1:{puerto}");
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = limite.Token;
        var clave = Guid.NewGuid().ToString("N");
        var duracion = TimeSpan.FromSeconds(2);

        // Calentar ambas conexiones antes de medir la expiración de la reserva.
        Assert.Equal((null, false), await primero.LeerAsync(clave, ct));
        Assert.Equal((null, false), await segundo.LeerAsync(clave, ct));
        Assert.True(await primero.ReservarAsync(clave, "primero", duracion, ct));
        Assert.False(await segundo.ReservarAsync(clave, "segundo", duracion, ct));
        await segundo.LiberarAsync(clave, "segundo", ct);
        Assert.True((await primero.LeerAsync(clave, ct)).Reservada);

        while (!await segundo.ReservarAsync(clave, "segundo", duracion, ct))
            await Task.Delay(100, ct);
        await primero.LiberarAsync(clave, "primero", ct);
        Assert.False(await primero.PublicarAsync(clave, "primero", "obsoleta", duracion, ct));
        Assert.True(await segundo.PublicarAsync(clave, "segundo", "vigente", duracion, ct));
        Assert.Equal(("vigente", false), await primero.LeerAsync(clave, ct));

        while ((await segundo.LeerAsync(clave, ct)).Texto is not null)
            await Task.Delay(100, ct);
        Assert.Equal((null, false), await primero.LeerAsync(clave, ct));
    }
}
