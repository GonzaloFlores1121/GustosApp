using GustosApp.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GustosApp.Application.Tests;

public sealed class CacheExplicacionesPruebas
{
    [Fact]
    public async Task DosInstanciasCompartenGeneracionAunqueSeCanceleElPrimerSolicitante()
    {
        var almacen = new AlmacenExplicacionesPrueba(TimeProvider.System);
        var primeraInstancia = CrearCache(almacen);
        var segundaInstancia = CrearCache(almacen);
        var inicio = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var salida = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancelacion = new CancellationTokenSource();

        var primera = primeraInstancia.ObtenerAsync("ficha", token =>
        {
            inicio.SetResult(token);
            return salida.Task.WaitAsync(token);
        }, cancelacion.Token);
        var tokenGemini = await inicio.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var segunda = segundaInstancia.ObtenerAsync("ficha",
            _ => throw new InvalidOperationException("Otra instancia ya está generando."), default);
        cancelacion.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => primera);
        Assert.False(tokenGemini.IsCancellationRequested);
        salida.SetResult("Explicación compartida.");
        Assert.Equal("Explicación compartida.", await segunda.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(("Explicación compartida.", false), await almacen.LeerAsync("ficha", default));
    }

    [Fact]
    public async Task CancelarUnaEsperaNoCancelaLaGeneracionNiLaReserva()
    {
        var almacen = new AlmacenExplicacionesPrueba(TimeProvider.System);
        var cache = CrearCache(almacen);
        var salida = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancelacion = new CancellationTokenSource();

        var generando = cache.ObtenerAsync("ficha", token => salida.Task.WaitAsync(token), default);
        var esperando = cache.ObtenerAsync("ficha", _ => Task.FromResult<string?>("Duplicada."), cancelacion.Token);
        cancelacion.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => esperando);
        Assert.True((await almacen.LeerAsync("ficha", default)).Reservada);
        salida.SetResult("Explicación.");
        Assert.Equal("Explicación.", await generando.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("excepcion")]
    public async Task RespuestaFallidaONulaLiberaLaReservaYNoSeReutiliza(string? respuesta)
    {
        var almacen = new AlmacenExplicacionesPrueba(TimeProvider.System);
        var cache = CrearCache(almacen);
        var salida = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var primera = cache.ObtenerAsync("ficha", _ => salida.Task, default);
        var duplicadas = 0;
        var esperando = cache.ObtenerAsync("ficha", _ =>
        {
            Interlocked.Increment(ref duplicadas);
            return Task.FromResult<string?>("Duplicada.");
        }, default);
        if (respuesta == "excepcion")
            salida.SetException(new InvalidOperationException("Error de prueba."));
        else
            salida.SetResult(respuesta);

        Assert.Null(await primera);
        Assert.Null(await esperando);
        Assert.Equal(0, duplicadas);
        Assert.Equal((null, false), await almacen.LeerAsync("ficha", default));
        Assert.Equal("Explicación nueva.", await cache.ObtenerAsync("ficha", _ => Task.FromResult<string?>("Explicación nueva."), default));
    }

    private static CacheExplicaciones CrearCache(AlmacenExplicacionesPrueba almacen) =>
        new(almacen, Options.Create(new GeminiSettings()), TimeProvider.System, NullLogger<CacheExplicaciones>.Instance);
}
