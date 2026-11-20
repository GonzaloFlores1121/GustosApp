using GustosApp.Application.Interfaces;

namespace GustosApp.Application.Tests;

internal sealed class BloqueoResolucionPruebas : IBloqueoResolucionSolicitud
{
    public bool Disponible { get; set; } = true;

    public async Task<bool> IntentarAsync(Guid solicitudId, Func<CancellationToken, Task> trabajo, CancellationToken ct)
    {
        if (!Disponible) return false;
        await trabajo(ct);
        return true;
    }
}
