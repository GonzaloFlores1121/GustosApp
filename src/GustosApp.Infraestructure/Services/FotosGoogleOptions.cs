using System.ComponentModel.DataAnnotations;

namespace GustosApp.Infraestructure.Services;

public sealed class FotosGoogleOptions
{
    public bool Habilitadas { get; set; } // Requiere habilitación explícita después de aplicar la migración.
    [Range(1, 15)] public int TimeoutSegundos { get; set; } = 8;
    [Range(1024, 5_000_000)] public int MaximoBytes { get; set; } = 5_000_000;
}

/// <summary>Límite agregado por proceso, adicional al límite autenticado del endpoint.</summary>
public sealed class PresupuestoFotosGoogle
{
    private readonly SemaphoreSlim _concurrencia = new(4);
    private readonly object _lock = new();
    private DateTime _ventana = DateTime.UtcNow;
    private int _consumidos;
    public bool Entrar()
    {
        lock (_lock)
        {
            if (DateTime.UtcNow - _ventana >= TimeSpan.FromMinutes(1)) { _ventana = DateTime.UtcNow; _consumidos = 0; }
            if (_consumidos >= 60 || !_concurrencia.Wait(0)) return false;
            _consumidos++;
            return true;
        }
    }
    public void Salir() => _concurrencia.Release();
}
