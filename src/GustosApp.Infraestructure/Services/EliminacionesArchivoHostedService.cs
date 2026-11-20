using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace GustosApp.Infraestructure.Services;

public sealed class EliminacionesArchivoHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EliminacionesArchivoHostedService> _logger;
    private readonly TimeSpan _intervalo;

    public EliminacionesArchivoHostedService(IServiceScopeFactory scopeFactory,
        IConfiguration config, ILogger<EliminacionesArchivoHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _intervalo = TimeSpan.FromMinutes(Math.Clamp(config.GetValue("FileCleanup:IntervalMinutes", 10), 1, 1440));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_intervalo);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<FirebaseStorageService>()
                    .ProcesarPendientesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudieron procesar los borrados de archivos pendientes.");
            }
        }
    }
}
