namespace GustosApp.Application.Interfaces;

/// <summary>Almacena explicaciones y coordina su generación entre instancias.</summary>
public interface IAlmacenExplicaciones
{
    /// <summary>Lee el texto y la presencia de una reserva en una sola operación.</summary>
    Task<(string? Texto, bool Reservada)> LeerAsync(string clave, CancellationToken ct);

    /// <summary>Adquiere la reserva si la clave está libre.</summary>
    Task<bool> ReservarAsync(string clave, string propietario, TimeSpan duracion, CancellationToken ct);

    /// <summary>Publica el texto solo mientras el propietario conserva la reserva.</summary>
    Task<bool> PublicarAsync(string clave, string propietario, string texto, TimeSpan ttl, CancellationToken ct);

    /// <summary>Libera la reserva únicamente si sigue perteneciendo al propietario.</summary>
    Task LiberarAsync(string clave, string propietario, CancellationToken ct);
}
