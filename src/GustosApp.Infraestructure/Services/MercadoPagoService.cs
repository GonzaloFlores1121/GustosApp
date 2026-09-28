using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GustosApp.Domain.Interfaces;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GustosApp.Infraestructure.Services;

public sealed class MercadoPagoService : IPagoService
{
    private const string Producto = "premium_unico";
    private readonly HttpClient _http;
    private readonly GustosDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<MercadoPagoService> _logger;

    public MercadoPagoService(HttpClient http, GustosDbContext db, IConfiguration config, ILogger<MercadoPagoService> logger)
    {
        _http = http;
        _db = db;
        _config = config;
        _logger = logger;
    }

    public CatalogoPremium ObtenerCatalogo()
    {
        decimal? precio = decimal.TryParse(_config["MercadoPago:Premium:Precio"], NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed : null;
        var moneda = _config["MercadoPago:Premium:Moneda"] ?? "ARS";
        var habilitado = _config.GetValue<bool>("MercadoPago:Premium:HabilitarCobros") && precio.HasValue && ConfiguracionProveedorCompleta();
        return new CatalogoPremium(Producto, precio, moneda, habilitado,
            ["Grupos sin límite", "Restaurantes favoritos sin límite"]);
    }

    public async Task<InicioCompraPremium> IniciarCompraAsync(string firebaseUid, CancellationToken ct = default)
    {
        var catalogo = ObtenerCatalogo();
        if (!catalogo.CobrosHabilitados || catalogo.Precio is null)
            throw new InvalidOperationException("Los pagos Premium todavía no están habilitados.");

        var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.FirebaseUid == firebaseUid, ct)
            ?? throw new UnauthorizedAccessException("Usuario no encontrado.");
        if (usuario.EsPremium())
            throw new InvalidOperationException("Tu cuenta ya tiene acceso Plus.");

        var activa = await _db.Set<CompraPremium>()
            .Where(c => c.UsuarioId == usuario.Id && c.Producto == Producto &&
                (c.Estado == EstadoCompraPremium.Pendiente || c.Estado == EstadoCompraPremium.CreandoProveedor ||
                 c.Estado == EstadoCompraPremium.EsperandoPago || c.Estado == EstadoCompraPremium.ErrorTemporal))
            .OrderByDescending(c => c.CreadaUtc)
            .FirstOrDefaultAsync(ct);
        if (activa != null)
        {
            if (activa.ExpiraUtc <= DateTime.UtcNow && activa.Estado == EstadoCompraPremium.EsperandoPago)
            {
                activa.Estado = EstadoCompraPremium.Cancelada;
                activa.ActualizadaUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
            else if (activa.Estado == EstadoCompraPremium.ErrorTemporal && string.IsNullOrWhiteSpace(activa.OrdenProveedorId))
            {
                return await CompletarOrdenAsync(activa, usuario, true, ct);
            }
            else return new InicioCompraPremium(activa.Id, activa.Estado.ToString(), activa.UrlCheckout, true);
        }

        var compra = new CompraPremium
        {
            UsuarioId = usuario.Id,
            ImporteEsperado = catalogo.Precio.Value,
            MonedaEsperada = catalogo.Moneda,
            EntornoEsperado = EntornoConfigurado(),
            Estado = EstadoCompraPremium.CreandoProveedor,
            ExpiraUtc = DateTime.UtcNow.AddHours(24)
        };
        _db.Add(compra);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            _db.Entry(compra).State = EntityState.Detached;
            activa = await _db.Set<CompraPremium>().AsNoTracking()
                .Where(c => c.UsuarioId == usuario.Id && c.Producto == Producto &&
                    (c.Estado == EstadoCompraPremium.Pendiente || c.Estado == EstadoCompraPremium.CreandoProveedor ||
                     c.Estado == EstadoCompraPremium.EsperandoPago || c.Estado == EstadoCompraPremium.ErrorTemporal))
                .OrderByDescending(c => c.CreadaUtc).FirstAsync(ct);
            return new InicioCompraPremium(activa.Id, activa.Estado.ToString(), activa.UrlCheckout, true);
        }

        return await CompletarOrdenAsync(compra, usuario, false, ct);
    }

    public async Task<EstadoCompraPremiumResultado> ObtenerEstadoCompraAsync(string firebaseUid, Guid compraId, bool reconciliar, CancellationToken ct = default)
    {
        var compra = await _db.Set<CompraPremium>().Include(c => c.Usuario).Include(c => c.Pagos)
            .SingleOrDefaultAsync(c => c.Id == compraId && c.Usuario.FirebaseUid == firebaseUid, ct)
            ?? throw new KeyNotFoundException("Compra no encontrada.");
        if (reconciliar) await ReconciliarCompraAsync(compra, ct);
        return CrearResultado(compra);
    }

    public async Task<ResultadoWebhookPago> ProcesarNotificacionPagoAsync(string pagoId, CancellationToken ct = default)
    {
        try
        {
            var pago = await ConsultarPagoAsync(pagoId, ct);
            if (!Guid.TryParse(pago.ReferenciaExterna, out var compraId)) return ResultadoWebhookPago.Ignorado;
            var compra = await _db.Set<CompraPremium>().Include(c => c.Usuario).Include(c => c.Pagos)
                .SingleOrDefaultAsync(c => c.Id == compraId, ct);
            if (compra == null) return ResultadoWebhookPago.Ignorado;
            await AplicarPagoAsync(compra, pago, ct);
            return ResultadoWebhookPago.Procesado;
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == System.Net.HttpStatusCode.NotFound) return ResultadoWebhookPago.Ignorado;
            _logger.LogWarning(ex, "Fallo transitorio consultando el pago {PagoId}", pagoId);
            return ResultadoWebhookPago.FalloTransitorio;
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            if (await _db.Set<PagoPremiumProveedor>().AsNoTracking().AnyAsync(p => p.PagoProveedorId == pagoId, ct))
                return ResultadoWebhookPago.Procesado;
            throw;
        }
    }

    public async Task<bool> VerificarYProcesarPagosPendientesAsync(string firebaseUid, CancellationToken ct = default)
    {
        var compras = await _db.Set<CompraPremium>().Include(c => c.Usuario).Include(c => c.Pagos)
            .Where(c => c.Usuario.FirebaseUid == firebaseUid &&
                (c.Estado == EstadoCompraPremium.EsperandoPago || c.Estado == EstadoCompraPremium.ErrorTemporal))
            .ToListAsync(ct);
        foreach (var compra in compras) await ReconciliarCompraAsync(compra, ct);
        return await _db.Usuarios.AsNoTracking().AnyAsync(u => u.FirebaseUid == firebaseUid && u.Plan == PlanUsuario.Plus, ct);
    }

    public bool ValidarFirmaWebhook(string dataId, string requestId, string signature)
    {
        var secret = _config["MercadoPago:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(dataId) || string.IsNullOrWhiteSpace(requestId)) return false;
        var parts = signature.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0], p => p[1]);
        if (!parts.TryGetValue("ts", out var ts) || !parts.TryGetValue("v1", out var supplied)) return false;
        if (!long.TryParse(ts, out var timestamp)) return false;
        var signedAt = ts.Length > 10 ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp) : DateTimeOffset.FromUnixTimeSeconds(timestamp);
        if ((DateTimeOffset.UtcNow - signedAt).Duration() > TimeSpan.FromMinutes(5)) return false;
        var manifest = $"id:{dataId.ToLowerInvariant()};request-id:{requestId};ts:{ts};";
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(supplied.ToLowerInvariant()));
    }

    public async Task<IReadOnlyList<CobroDuplicadoPendiente>> ObtenerCobrosDuplicadosAsync(CancellationToken ct = default) =>
        await _db.Set<CompraPremium>().AsNoTracking().Where(c => c.PosibleCobroDuplicado)
            .OrderByDescending(c => c.ActualizadaUtc)
            .Select(c => new CobroDuplicadoPendiente(c.Id, c.UsuarioId, c.ActualizadaUtc,
                c.Pagos.Select(p => p.PagoProveedorId).ToList()))
            .ToListAsync(ct);

    private async Task ReconciliarCompraAsync(CompraPremium compra, CancellationToken ct)
    {
        var ids = compra.Pagos.Select(p => p.PagoProveedorId).ToList();
        if (ids.Count == 0 && !string.IsNullOrWhiteSpace(compra.OrdenProveedorId))
            ids = await ConsultarPagosDeOrdenAsync(compra.OrdenProveedorId, ct);
        foreach (var id in ids) await AplicarPagoAsync(compra, await ConsultarPagoAsync(id, ct), ct);
    }

    private async Task<InicioCompraPremium> CompletarOrdenAsync(CompraPremium compra, Usuario usuario, bool reutilizada, CancellationToken ct)
    {
        try
        {
            var remoto = await CrearOrdenProveedorAsync(compra, usuario, ct);
            compra.OrdenProveedorId = remoto.Id;
            compra.UrlCheckout = remoto.UrlCheckout;
            compra.Estado = EstadoCompraPremium.EsperandoPago;
            compra.UltimoErrorCodigo = null;
            compra.ActualizadaUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return new InicioCompraPremium(compra.Id, compra.Estado.ToString(), compra.UrlCheckout, reutilizada);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            compra.Estado = EstadoCompraPremium.ErrorTemporal;
            compra.UltimoErrorCodigo = "proveedor_no_confirmado";
            compra.ActualizadaUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(CancellationToken.None);
            _logger.LogWarning("Resultado incierto al crear la orden Premium {CompraId}", compra.Id);
            throw new PagoTemporalmenteNoDisponibleException("No pudimos confirmar la creación del pago. La operación quedó guardada para reconciliación.");
        }
    }

    private async Task AplicarPagoAsync(CompraPremium compra, PagoRemoto remoto, CancellationToken ct)
    {
        if (remoto.ReferenciaExterna != compra.Id.ToString("D") || remoto.Moneda != compra.MonedaEsperada || remoto.Importe != compra.ImporteEsperado ||
            remoto.Produccion != (compra.EntornoEsperado == "production") ||
            (!string.IsNullOrWhiteSpace(_config["MercadoPago:CollectorId"]) && remoto.CollectorId != _config["MercadoPago:CollectorId"]))
        {
            _logger.LogWarning("Pago {PagoId} no coincide con la compra {CompraId}", remoto.Id, compra.Id);
            return;
        }

        var local = compra.Pagos.SingleOrDefault(p => p.PagoProveedorId == remoto.Id);
        if (local?.Estado is EstadoPagoProveedor.Reembolsado or EstadoPagoProveedor.Contracargo && remoto.Estado == EstadoPagoProveedor.Aprobado)
        {
            _logger.LogInformation("Se ignoró una transición antigua de {Estado} a aprobado para {PagoId}", local.Estado, remoto.Id);
            return;
        }
        if (local == null)
        {
            local = new PagoPremiumProveedor { CompraPremiumId = compra.Id, PagoProveedorId = remoto.Id };
            compra.Pagos.Add(local);
            _db.Add(local);
        }
        local.Estado = remoto.Estado;
        local.Importe = remoto.Importe;
        local.ImporteReembolsado = remoto.ImporteReembolsado;
        local.Moneda = remoto.Moneda;
        local.EntornoProduccion = remoto.Produccion;
        local.CuentaReceptoraId = remoto.CollectorId;
        local.FechaProveedorUtc = remoto.FechaUtc;
        local.UltimaConsultaUtc = DateTime.UtcNow;
        local.EstadoProveedor = remoto.EstadoOriginal;

        compra.Estado = remoto.Estado switch
        {
            EstadoPagoProveedor.Aprobado => EstadoCompraPremium.Aprobada,
            EstadoPagoProveedor.Rechazado => EstadoCompraPremium.Rechazada,
            EstadoPagoProveedor.Cancelado => EstadoCompraPremium.Cancelada,
            EstadoPagoProveedor.Reembolsado => EstadoCompraPremium.Reembolsada,
            EstadoPagoProveedor.Contracargo => EstadoCompraPremium.Contracargo,
            EstadoPagoProveedor.ReembolsoParcial => EstadoCompraPremium.ReembolsoParcial,
            _ => EstadoCompraPremium.EsperandoPago
        };
        compra.ActualizadaUtc = DateTime.UtcNow;

        if (remoto.Estado == EstadoPagoProveedor.Aprobado)
        {
            var otroPago = await _db.Set<PagoPremiumProveedor>().AsNoTracking().AnyAsync(p =>
                p.CompraPremium.UsuarioId == compra.UsuarioId && p.Estado == EstadoPagoProveedor.Aprobado && p.PagoProveedorId != remoto.Id, ct);
            compra.PosibleCobroDuplicado = otroPago;
        }
        var tienePagoValido = remoto.Estado == EstadoPagoProveedor.Aprobado || await _db.Set<PagoPremiumProveedor>().AnyAsync(p =>
            p.CompraPremium.UsuarioId == compra.UsuarioId && p.PagoProveedorId != remoto.Id && p.Estado == EstadoPagoProveedor.Aprobado, ct);
        if (remoto.Estado != EstadoPagoProveedor.ReembolsoParcial)
            compra.Usuario.Plan = compra.Usuario.AccesoPlusLegado || tienePagoValido ? PlanUsuario.Plus : PlanUsuario.Free;
        await _db.SaveChangesAsync(ct);
    }

    private EstadoCompraPremiumResultado CrearResultado(CompraPremium compra) => new(
        compra.Id, compra.Estado.ToString(), compra.Usuario.EsPremium(),
        compra.Estado is EstadoCompraPremium.Rechazada or EstadoCompraPremium.Cancelada,
        compra.Estado switch
        {
            EstadoCompraPremium.Aprobada => "Tu acceso Plus está activo.",
            EstadoCompraPremium.Rechazada => "El pago fue rechazado. Podés iniciar un nuevo intento.",
            EstadoCompraPremium.Cancelada => "El pago fue cancelado. Podés iniciar un nuevo intento.",
            EstadoCompraPremium.ErrorTemporal => "No pudimos confirmar el estado todavía. Volvé a intentar en unos minutos.",
            EstadoCompraPremium.ReembolsoParcial => "El pago tiene un reembolso parcial y requiere revisión.",
            _ => "El pago todavía está pendiente de confirmación."
        });

    private async Task<OrdenRemota> CrearOrdenProveedorAsync(CompraPremium compra, Usuario usuario, CancellationToken ct)
    {
        using var request = CrearRequest(HttpMethod.Post, "https://api.mercadopago.com/v1/orders");
        request.Headers.Add("X-Idempotency-Key", compra.ClaveIdempotencia.ToString("D"));
        var body = new
        {
            type = "online", processing_mode = "manual", total_amount = compra.ImporteEsperado.ToString("0.00", CultureInfo.InvariantCulture),
            external_reference = compra.Id.ToString("D"),
            payer = new { email = usuario.Email },
            transactions = new { payments = new[] { new { amount = compra.ImporteEsperado.ToString("0.00", CultureInfo.InvariantCulture) } } },
            items = new[] { new { external_code = Producto, title = "GustosApp Plus — pago único", quantity = 1, unit_price = compra.ImporteEsperado.ToString("0.00", CultureInfo.InvariantCulture) } },
            notification_url = _config["MercadoPago:WebhookUrl"],
            back_urls = new
            {
                success = AgregarCompraId(_config["MercadoPago:SuccessUrl"], compra.Id),
                failure = AgregarCompraId(_config["MercadoPago:FailureUrl"], compra.Id),
                pending = AgregarCompraId(_config["MercadoPago:PendingUrl"], compra.Id)
            }
        };
        request.Content = JsonContent.Create(body);
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
        return new OrdenRemota(json.RootElement.GetProperty("id").GetString()!, json.RootElement.GetProperty("checkout_url").GetString()!);
    }

    private async Task<List<string>> ConsultarPagosDeOrdenAsync(string orderId, CancellationToken ct)
    {
        using var response = await _http.SendAsync(CrearRequest(HttpMethod.Get, $"https://api.mercadopago.com/v1/orders/{orderId}"), ct);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
        if (!json.RootElement.TryGetProperty("transactions", out var tx) || !tx.TryGetProperty("payments", out var payments)) return [];
        return payments.EnumerateArray().Select(p => p.GetProperty("id").ToString()).ToList();
    }

    private async Task<PagoRemoto> ConsultarPagoAsync(string id, CancellationToken ct)
    {
        using var response = await _http.SendAsync(CrearRequest(HttpMethod.Get, $"https://api.mercadopago.com/v1/payments/{id}"), ct);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
        var root = json.RootElement;
        var status = root.GetProperty("status").GetString() ?? "unknown";
        var refunded = root.TryGetProperty("transaction_details", out var details) && details.TryGetProperty("total_refunded", out var totalRefunded) ? totalRefunded.GetDecimal() : 0;
        var amount = root.GetProperty("transaction_amount").GetDecimal();
        var mapped = status switch
        {
            "approved" when refunded == 0 => EstadoPagoProveedor.Aprobado,
            "refunded" when refunded >= amount => EstadoPagoProveedor.Reembolsado,
            "charged_back" => EstadoPagoProveedor.Contracargo,
            "cancelled" => EstadoPagoProveedor.Cancelado,
            "rejected" => EstadoPagoProveedor.Rechazado,
            _ when refunded > 0 => EstadoPagoProveedor.ReembolsoParcial,
            "pending" or "in_process" or "authorized" => EstadoPagoProveedor.Pendiente,
            _ => EstadoPagoProveedor.Desconocido
        };
        return new PagoRemoto(id, root.GetProperty("external_reference").GetString(), amount,
            root.GetProperty("currency_id").GetString() ?? "", root.GetProperty("live_mode").GetBoolean(),
            root.TryGetProperty("collector_id", out var collector) ? collector.ToString() : null,
            mapped, status, refunded == 0 ? null : refunded,
            root.TryGetProperty("date_approved", out var date) && date.ValueKind == JsonValueKind.String && DateTime.TryParse(date.GetString(), out var parsed) ? parsed.ToUniversalTime() : DateTime.UtcNow);
    }

    private HttpRequestMessage CrearRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config["MercadoPago:AccessToken"]);
        return request;
    }

    private bool ConfiguracionProveedorCompleta() =>
        !string.IsNullOrWhiteSpace(_config["MercadoPago:AccessToken"]) &&
        !string.IsNullOrWhiteSpace(_config["MercadoPago:WebhookSecret"]) &&
        !string.IsNullOrWhiteSpace(_config["MercadoPago:CollectorId"]) &&
        !string.IsNullOrWhiteSpace(_config["MercadoPago:WebhookUrl"]) &&
        !string.IsNullOrWhiteSpace(_config["MercadoPago:SuccessUrl"]) &&
        !string.IsNullOrWhiteSpace(_config["MercadoPago:FailureUrl"]) &&
        !string.IsNullOrWhiteSpace(_config["MercadoPago:PendingUrl"]);
    private string EntornoConfigurado() => _config.GetValue<bool>("MercadoPago:Produccion") ? "production" : "sandbox";
    private static string? AgregarCompraId(string? url, Guid compraId) =>
        string.IsNullOrWhiteSpace(url) ? null : $"{url}{(url.Contains('?') ? '&' : '?')}compraId={compraId:D}";

    private sealed record OrdenRemota(string Id, string UrlCheckout);
    private sealed record PagoRemoto(string Id, string? ReferenciaExterna, decimal Importe, string Moneda, bool Produccion,
        string? CollectorId, EstadoPagoProveedor Estado, string EstadoOriginal, decimal? ImporteReembolsado, DateTime FechaUtc);
}
