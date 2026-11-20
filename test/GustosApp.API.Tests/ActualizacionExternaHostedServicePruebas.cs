using System.Collections.Concurrent;
using FluentAssertions;
using GustosApp.Infraestructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace GustosApp.API.Tests;

public sealed class ActualizacionExternaHostedServicePruebas
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReleeHabilitadaEntreCiclos_YSeDetieneSinConsultaAlCancelar(bool fallaPrimerLote)
    {
        var habilitada = false;
        var configuracionInvalida = false;
        var options = new Mock<IOptionsMonitor<ActualizacionExternaRestaurantesOptions>>();
        options.SetupGet(o => o.CurrentValue).Returns(() => configuracionInvalida
            ? throw new OptionsValidationException("prueba", typeof(ActualizacionExternaRestaurantesOptions), ["Configuración de prueba inválida"])
            : new ActualizacionExternaRestaurantesOptions { Habilitada = habilitada, IntervaloMinutos = 1 });
        var actualizador = new Mock<IActualizadorExternoRestaurantes>();
        actualizador.Setup(a => a.ActualizarPendientesAsync(It.IsAny<CancellationToken>())).Returns(() => fallaPrimerLote
            ? Task.FromException<ResultadoLoteActualizacionExterna>(new InvalidOperationException("Fallo controlado"))
            : Task.FromResult(new ResultadoLoteActualizacionExterna(1, 1, 0, 0)));
        await using var provider = new ServiceCollection().AddSingleton(actualizador.Object).BuildServiceProvider();
        var reloj = new RelojManual();
        using var servicio = new ActualizacionExternaRestaurantesHostedService(provider.GetRequiredService<IServiceScopeFactory>(),
            options.Object, NullLogger<ActualizacionExternaRestaurantesHostedService>.Instance, reloj);
        await servicio.StartAsync(default);
        var primerTick = await reloj.SiguienteEsperaAsync();
        actualizador.VerifyNoOtherCalls();
        configuracionInvalida = true;
        primerTick.Disparar();
        var tickRecuperacion = await reloj.SiguienteEsperaAsync();
        actualizador.VerifyNoOtherCalls();
        configuracionInvalida = false;
        habilitada = true;
        tickRecuperacion.Disparar();
        var segundoTick = await reloj.SiguienteEsperaAsync();
        actualizador.Verify(a => a.ActualizarPendientesAsync(It.IsAny<CancellationToken>()), Times.Once);
        habilitada = false;
        segundoTick.Disparar();
        await reloj.SiguienteEsperaAsync();
        actualizador.Verify(a => a.ActualizarPendientesAsync(It.IsAny<CancellationToken>()), Times.Once);
        await servicio.StopAsync(default);
        servicio.ExecuteTask!.IsCompletedSuccessfully.Should().BeTrue();
    }

    private sealed class RelojManual : TimeProvider
    {
        private readonly ConcurrentQueue<TimerManual> _timers = new();
        private readonly SemaphoreSlim _creados = new(0);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            dueTime.Should().Be(TimeSpan.FromMinutes(1));
            var timer = new TimerManual(callback, state);
            _timers.Enqueue(timer);
            _creados.Release();
            return timer;
        }
        public async Task<TimerManual> SiguienteEsperaAsync()
        {
            (await _creados.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
            _timers.TryDequeue(out var timer).Should().BeTrue();
            return timer!;
        }
    }
    private sealed class TimerManual(TimerCallback callback, object? state) : ITimer
    {
        private bool _descartado;
        public void Disparar() { if (!_descartado) callback(state); }
        public bool Change(TimeSpan dueTime, TimeSpan period) => !_descartado;
        public void Dispose() => _descartado = true;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
