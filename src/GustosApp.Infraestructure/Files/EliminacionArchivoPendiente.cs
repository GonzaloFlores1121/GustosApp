namespace GustosApp.Infraestructure.Files;

public sealed class EliminacionArchivoPendiente
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Url { get; set; } = string.Empty;
    public string CarpetaEsperada { get; set; } = string.Empty;
    public DateTime CreadaUtc { get; set; } = DateTime.UtcNow;
    public DateTime ProximoIntentoUtc { get; set; } = DateTime.UtcNow;
    public int Intentos { get; set; }
    public string? UltimoError { get; set; }
}
