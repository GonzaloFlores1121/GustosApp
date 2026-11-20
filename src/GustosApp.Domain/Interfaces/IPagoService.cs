namespace GustosApp.Domain.Interfaces;

public sealed record CatalogoPremium(string Producto, decimal? Precio, string Moneda, bool CobrosHabilitados, IReadOnlyList<string> Beneficios);
public sealed record InicioCompraPremium(Guid CompraId, string Estado, string? UrlCheckout, bool Reutilizada);
public sealed record EstadoCompraPremiumResultado(Guid CompraId, string Estado, bool AccesoPlus, bool PuedeReintentar, string Mensaje);
public sealed record CobroDuplicadoPendiente(Guid CompraId, Guid UsuarioId, DateTime DetectadoUtc, IReadOnlyList<string> PagosProveedor);
public enum ResultadoWebhookPago { Procesado, Ignorado, FalloTransitorio }

public interface IPagoService
{
    CatalogoPremium ObtenerCatalogo();
    Task<InicioCompraPremium> IniciarCompraAsync(string firebaseUid, CancellationToken ct = default);
    Task<EstadoCompraPremiumResultado> ObtenerEstadoCompraAsync(string firebaseUid, Guid compraId, bool reconciliar, CancellationToken ct = default);
    Task<ResultadoWebhookPago> ProcesarNotificacionOrdenAsync(string ordenId, CancellationToken ct = default);
    Task<bool> VerificarYProcesarPagosPendientesAsync(string firebaseUid, CancellationToken ct = default);
    bool ValidarFirmaWebhook(string dataId, string requestId, string signature);
    Task<IReadOnlyList<CobroDuplicadoPendiente>> ObtenerCobrosDuplicadosAsync(CancellationToken ct = default);
}
