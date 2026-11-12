using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GustosApp.Infraestructure.Services;

public sealed class ActualizacionExternaRestaurantesHostedService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<ActualizacionExternaRestaurantesOptions> options,
    ILogger<ActualizacionExternaRestaurantesHostedService> logger,
    TimeProvider? timeProvider = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromMinutes(new ActualizacionExternaRestaurantesOptions().IntervaloMinutos);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var configuracion = options.CurrentValue;
                intervalo = TimeSpan.FromMinutes(configuracion.IntervaloMinutos);
                if (configuracion.Habilitada)
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var actualizador = scope.ServiceProvider.GetRequiredService<IActualizadorExternoRestaurantes>();
                    var resultado = await actualizador.ActualizarPendientesAsync(stoppingToken);
                    logger.LogInformation("Actualización externa finalizada. Intentados: {Intentados}; exitosos: {Exitosos}; fallidos: {Fallidos}; omitidos: {Omitidos}",
                        resultado.Intentados, resultado.Exitosos, resultado.Fallidos, resultado.Omitidos);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError("Falló la ejecución periódica de actualización externa. Tipo: {TipoError}", ex.GetType().Name);
            }

            // Ante una recarga inválida, no ejecutar y reintentar con el último intervalo válido.
            try { await Task.Delay(intervalo, timeProvider ?? TimeProvider.System, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
