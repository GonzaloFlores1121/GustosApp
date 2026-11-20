namespace GustosApp.Domain.Model;

public enum EstadoCompraPremium { Pendiente, CreandoProveedor, EsperandoPago, Aprobada, Rechazada, Cancelada, Reembolsada, Contracargo, ErrorTemporal, ReembolsoParcial }
public enum EstadoPagoProveedor { Pendiente, Aprobado, Rechazado, Cancelado, Reembolsado, Contracargo, ReembolsoParcial, Desconocido }

public sealed class CompraPremium
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public string Producto { get; set; } = "premium_unico";
    public decimal ImporteEsperado { get; set; }
    public string MonedaEsperada { get; set; } = "ARS";
    public string EntornoEsperado { get; set; } = "sandbox";
    public EstadoCompraPremium Estado { get; set; } = EstadoCompraPremium.Pendiente;
    public Guid ClaveIdempotencia { get; set; } = Guid.NewGuid();
    public string? OrdenProveedorId { get; set; }
    public string? UrlCheckout { get; set; }
    public DateTime CreadaUtc { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadaUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiraUtc { get; set; }
    public string? UltimoErrorCodigo { get; set; }
    public bool PosibleCobroDuplicado { get; set; }
    public ICollection<PagoPremiumProveedor> Pagos { get; set; } = new List<PagoPremiumProveedor>();
}

public sealed class PagoPremiumProveedor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompraPremiumId { get; set; }
    public CompraPremium CompraPremium { get; set; } = null!;
    public string PagoProveedorId { get; set; } = string.Empty;
    public EstadoPagoProveedor Estado { get; set; }
    public decimal Importe { get; set; }
    public decimal? ImporteReembolsado { get; set; }
    public string Moneda { get; set; } = string.Empty;
    public bool EntornoProduccion { get; set; }
    public string? CuentaReceptoraId { get; set; }
    public DateTime FechaProveedorUtc { get; set; }
    public DateTime UltimaConsultaUtc { get; set; } = DateTime.UtcNow;
    public string? EstadoProveedor { get; set; }
}
