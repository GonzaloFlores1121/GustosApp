namespace GustosApp.Application.Interfaces;

public interface IBloqueoResolucionSolicitud
{
    Task<bool> IntentarAsync(Guid solicitudId, Func<CancellationToken, Task> trabajo, CancellationToken ct);
}
