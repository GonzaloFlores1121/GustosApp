namespace GustosApp.Domain.Model;

public sealed class RegistroAdministracionRestaurante
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? RestauranteId { get; set; }
    public Guid? SolicitudId { get; set; }
    public string ActorUid { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
    public DateTime FechaUtc { get; set; }
}
