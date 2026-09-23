using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GustosApp.Infraestructure.Services;

public sealed class ActualizacionExternaRestaurantesHostedService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<ActualizacionExternaRestaurantesOptions> options,
    ILogger<ActualizacionExternaRestaurantesHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.CurrentValue.Habilitada)
        {
            logger.LogInformation("La actualización externa periódica de restaurantes está deshabilitada.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var actualizador = scope.ServiceProvider.GetRequiredService<IActualizadorExternoRestaurantes>();
                var procesados = await actualizador.ActualizarPendientesAsync(stoppingToken);
                logger.LogInformation("Actualización externa finalizada. Restaurantes procesados: {Cantidad}", procesados);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falló la ejecución periódica de actualización externa.");
            }

            var intervalo = TimeSpan.FromMinutes(options.CurrentValue.IntervaloMinutos);
            await Task.Delay(intervalo, stoppingToken);
        }
    }
}
