using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GustosApp.Infraestructure.Services;

public sealed class MercadoPagoService(HttpClient http, GustosDbContext db, IConfiguration config,
    ILogger<MercadoPagoService> logger) : IPagoService
{
    private const string Producto = "premium_unico";
    private const string Endpoint = "https://api.mercadopago.com/v1/orders";
    private bool Produccion => config.GetValue<bool>("MercadoPago:Produccion");
    private string Entorno => Produccion ? "production" : "sandbox";
    private string? AccessToken => config[Produccion ? "MercadoPago:AccessToken" : "MercadoPago:AccessTokenPrueba"];
    private string? Vendedor => config[Produccion ? "MercadoPago:CollectorId" : "MercadoPago:CollectorIdPrueba"];
    private string? Firma => config[Produccion ? "MercadoPago:WebhookSecret" : "MercadoPago:WebhookSecretPrueba"];

    public CatalogoPremium ObtenerCatalogo()
    {
        var importe = LeerImporte(config["MercadoPago:Premium:Precio"]);
        decimal? precio = importe is > 0 && importe <= 9_999_999_999_999_999m && importe == decimal.Round(importe.Value, 2)
            ? importe : null;
        var moneda = config["MercadoPago:Premium:Moneda"] ?? "ARS";
        var habilitado = config.GetValue<bool>("MercadoPago:Premium:HabilitarCobros") && precio.HasValue && moneda == "ARS"
            && ProveedorConfigurado() && (!Produccion || !string.IsNullOrWhiteSpace(Firma))
            && new[] { "SuccessUrl", "FailureUrl", "PendingUrl" }.All(clave => EsRetornoValido(config[$"MercadoPago:{clave}"]));
        return new CatalogoPremium(Producto, precio, moneda, habilitado, ["Grupos sin límite", "Restaurantes favoritos sin límite"]);
    }

    public async Task<InicioCompraPremium> IniciarCompraAsync(string firebaseUid, CancellationToken ct = default)
    {
        var catalogo = ObtenerCatalogo();
        if (!catalogo.CobrosHabilitados || catalogo.Precio is null)
            throw new InvalidOperationException("Los pagos Premium todavía no están habilitados.");

        var usuarioId = await ObtenerUsuarioIdAsync(firebaseUid, ct);
        InicioCompraPremium? resultado = null;
        await ConBloqueoAsync(usuarioId, async token =>
        {
            var usuario = await db.Usuarios.SingleAsync(u => u.Id == usuarioId, token);
            if (usuario.EsPremium()) throw new InvalidOperationException("Tu cuenta ya tiene acceso Plus.");
            var compra = await ComprasActivas(usuarioId).Include(c => c.Usuario).Include(c => c.Pagos).SingleOrDefaultAsync(token);
            if (compra != null)
            {
                if (compra.EntornoEsperado != Entorno)
                    throw new PagoTemporalmenteNoDisponibleException("La compra pertenece a otro entorno.");
                if (compra.OrdenProveedorId == null)
                {
                    resultado = await CompletarOrdenAsync(compra, reutilizada: true, token);
                    return;
                }
                await ReconciliarCompraAsync(compra, token);
                if (compra.Estado is not (EstadoCompraPremium.Rechazada or EstadoCompraPremium.Cancelada) || usuario.EsPremium())
                {
                    resultado = Inicio(compra, reutilizada: true);
                    return;
                }
            }

            compra = new CompraPremium
            {
                Usuario = usuario,
                ImporteEsperado = catalogo.Precio.Value,
                MonedaEsperada = catalogo.Moneda,
                EntornoEsperado = Entorno,
                Estado = EstadoCompraPremium.CreandoProveedor,
                ExpiraUtc = DateTime.UtcNow.AddDays(1)
            };
            db.Add(compra);
            await db.SaveChangesAsync(token);
            resultado = await CompletarOrdenAsync(compra, reutilizada: false, token);
        }, ct);
        return resultado!;
    }

    public async Task<EstadoCompraPremiumResultado> ObtenerEstadoCompraAsync(string firebaseUid, Guid compraId, bool reconciliar, CancellationToken ct = default)
    {
        var usuarioId = await db.Set<CompraPremium>().Where(c => c.Id == compraId && c.Usuario.FirebaseUid == firebaseUid)
            .Select(c => (Guid?)c.UsuarioId).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Compra no encontrada.");
        EstadoCompraPremiumResultado? resultado = null;
        async Task LeerAsync(bool consultarProveedor, CancellationToken token)
        {
            var compra = await CargarCompraAsync(compraId, token);
            if (consultarProveedor) await ReconciliarCompraAsync(compra, token);
            resultado = CrearResultado(compra);
        }
        if (!reconciliar || !await IntentarBloqueoAsync(usuarioId, token => LeerAsync(true, token), ct))
            await LeerAsync(false, ct);
        return resultado!;
    }

    public async Task<ResultadoWebhookPago> ProcesarNotificacionOrdenAsync(string ordenId, CancellationToken ct = default)
    {
        if (!EsIdOrden(ordenId)) return ResultadoWebhookPago.Ignorado;
        var referencia = await db.Set<CompraPremium>().AsNoTracking()
            .Where(c => c.OrdenProveedorId == ordenId && c.EntornoEsperado == Entorno)
            .Select(c => new { c.Id, c.UsuarioId }).SingleOrDefaultAsync(ct);
        if (referencia == null) return ResultadoWebhookPago.Ignorado;
        try
        {
            var resultado = ResultadoWebhookPago.Ignorado;
            await ConBloqueoAsync(referencia.UsuarioId, async token =>
            {
                var compra = await CargarCompraAsync(referencia.Id, token);
                if (await ReconciliarCompraAsync(compra, token)) resultado = ResultadoWebhookPago.Procesado;
            }, ct);
            return resultado;
        }
        catch (Exception ex) when (ex is HttpRequestException or PagoTemporalmenteNoDisponibleException or DbUpdateException)
        {
            logger.LogWarning("No se pudo procesar la notificación de la compra {CompraId}. Tipo {Tipo}", referencia.Id, ex.GetType().Name);
            return ResultadoWebhookPago.FalloTransitorio;
        }
    }

    public async Task<bool> VerificarYProcesarPagosPendientesAsync(string firebaseUid, CancellationToken ct = default)
    {
        var usuarioId = await db.Usuarios.Where(u => u.FirebaseUid == firebaseUid).Select(u => (Guid?)u.Id).SingleOrDefaultAsync(ct);
        if (!usuarioId.HasValue) return false;
        if (ProveedorConfigurado() && await ComprasActivas(usuarioId.Value)
            .AnyAsync(c => c.EntornoEsperado == Entorno && c.OrdenProveedorId != null, ct))
        {
            await ConBloqueoAsync(usuarioId.Value, async token =>
            {
                var compra = await ComprasActivas(usuarioId.Value).Include(c => c.Usuario).Include(c => c.Pagos).SingleOrDefaultAsync(token);
                if (compra != null) await ReconciliarCompraAsync(compra, token);
            }, ct);
        }
        return await db.Usuarios.AsNoTracking().AnyAsync(u => u.Id == usuarioId && u.Plan == PlanUsuario.Plus, ct);
    }

    public bool ValidarFirmaWebhook(string dataId, string requestId, string signature)
    {
        if (string.IsNullOrWhiteSpace(Firma) || !EsIdOrden(dataId) || requestId.Length is 0 or > 150 ||
            !requestId.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') || signature.Length > 250) return false;
        string? ts = null, v1 = null;
        foreach (var parte in signature.Split(',', StringSplitOptions.TrimEntries))
        {
            if (parte.StartsWith("ts=", StringComparison.Ordinal) && ts == null) ts = parte[3..];
            else if (parte.StartsWith("v1=", StringComparison.Ordinal) && v1 == null) v1 = parte[3..];
            else return false;
        }
        if (v1?.Length != 64 || !v1.All(Uri.IsHexDigit) || ts?.Length is not (10 or 13) ||
            !long.TryParse(ts, NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp)) return false;
        var ahora = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (ts.Length == 10) timestamp *= 1000;
        if (Math.Abs(ahora - timestamp) > TimeSpan.FromMinutes(5).TotalMilliseconds) return false;
        var manifiesto = $"id:{dataId.ToLowerInvariant()};request-id:{requestId};ts:{ts};";
        var esperada = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Firma), Encoding.UTF8.GetBytes(manifiesto));
        return CryptographicOperations.FixedTimeEquals(esperada, Convert.FromHexString(v1));
    }

    public async Task<IReadOnlyList<CobroDuplicadoPendiente>> ObtenerCobrosDuplicadosAsync(CancellationToken ct = default) =>
        await db.Set<CompraPremium>().AsNoTracking().Where(c => c.PosibleCobroDuplicado)
            .OrderByDescending(c => c.ActualizadaUtc)
            .Select(c => new CobroDuplicadoPendiente(c.Id, c.UsuarioId, c.ActualizadaUtc, c.Pagos.Select(p => p.PagoProveedorId).ToList()))
            .ToListAsync(ct);

    private IQueryable<CompraPremium> ComprasActivas(Guid usuarioId) => db.Set<CompraPremium>()
        .Where(c => c.UsuarioId == usuarioId && c.Producto == Producto &&
            (c.Estado == EstadoCompraPremium.Pendiente || c.Estado == EstadoCompraPremium.CreandoProveedor ||
             c.Estado == EstadoCompraPremium.EsperandoPago || c.Estado == EstadoCompraPremium.ErrorTemporal));

    private Task<CompraPremium> CargarCompraAsync(Guid id, CancellationToken ct) => db.Set<CompraPremium>()
        .Include(c => c.Usuario).Include(c => c.Pagos).SingleAsync(c => c.Id == id, ct);

    private async Task<Guid> ObtenerUsuarioIdAsync(string firebaseUid, CancellationToken ct) =>
        await db.Usuarios.Where(u => u.FirebaseUid == firebaseUid).Select(u => (Guid?)u.Id).SingleOrDefaultAsync(ct)
        ?? throw new UnauthorizedAccessException("Usuario no encontrado.");

    private async Task ConBloqueoAsync(Guid usuarioId, Func<CancellationToken, Task> trabajo, CancellationToken ct)
    {
        if (!await IntentarBloqueoAsync(usuarioId, trabajo, ct))
            throw new PagoTemporalmenteNoDisponibleException("Ya se está actualizando una compra de tu cuenta.");
    }

    private Task<bool> IntentarBloqueoAsync(Guid usuarioId, Func<CancellationToken, Task> trabajo, CancellationToken ct) =>
        BloqueoExclusivoSql.IntentarAsync(db, $"gustos:compra-premium:{usuarioId:N}", trabajo, ct);

    private async Task<InicioCompraPremium> CompletarOrdenAsync(CompraPremium compra, bool reutilizada, CancellationToken ct)
    {
        JsonElement remoto;
        var recuperada = false;
        try
        {
            using var request = CrearRequest(HttpMethod.Post, Endpoint);
            request.Headers.Add("X-Idempotency-Key", compra.ClaveIdempotencia.ToString("D"));
            var importe = compra.ImporteEsperado.ToString("0.00", CultureInfo.InvariantCulture);
            request.Content = JsonContent.Create(new
            {
                type = "online", processing_mode = "manual", total_amount = importe,
                external_reference = compra.Id.ToString("D"), expiration_time = "P1D",
                items = new[] { new { external_code = Producto, title = "GustosApp Plus — pago único", quantity = 1, unit_price = importe } },
                config = new { online = new
                {
                    success_url = AgregarCompraId("SuccessUrl", compra.Id),
                    failure_url = AgregarCompraId("FailureUrl", compra.Id),
                    pending_url = AgregarCompraId("PendingUrl", compra.Id),
                auto_return = "approved",
                retries = new { allowed = false }
                } }
            });
            if (reutilizada && compra.ExpiraUtc <= DateTime.UtcNow)
            {
                remoto = await BuscarOrdenAsync(compra, ct);
                recuperada = true;
            }
            else
            {
                try { remoto = await EnviarAsync(request, ct); }
                catch (HttpRequestException ex) when (reutilizada && ex.StatusCode == HttpStatusCode.Conflict)
                {
                    remoto = await BuscarOrdenAsync(compra, ct);
                    recuperada = true;
                }
            }
            if (!OrdenCoincide(compra, remoto) || (!recuperada && !EsCheckoutValido(Campo(remoto, "checkout_url"), Campo(remoto, "id"))))
                throw new HttpRequestException("La respuesta de creación no coincide con la compra.");
        }
        catch (HttpRequestException ex)
        {
            compra.Estado = EstadoCompraPremium.ErrorTemporal;
            compra.UltimoErrorCodigo = ex.StatusCode.HasValue ? $"proveedor_http_{(int)ex.StatusCode.Value}" : "proveedor_no_confirmado";
            compra.ActualizadaUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Creación de orden no confirmada para {CompraId}. Código {Codigo}", compra.Id, compra.UltimoErrorCodigo);
            throw new PagoTemporalmenteNoDisponibleException("La compra quedó guardada para volver a intentar con la misma clave.");
        }

        compra.OrdenProveedorId = Campo(remoto, "id");
        var url = Campo(remoto, "checkout_url");
        compra.UrlCheckout = EsCheckoutValido(url, compra.OrdenProveedorId) ? url : null;
        compra.Estado = EstadoCompraPremium.EsperandoPago;
        compra.UltimoErrorCodigo = null;
        compra.ActualizadaUtc = DateTime.UtcNow;
        try
        {
            if (recuperada) await AplicarOrdenAsync(compra, remoto, ct);
            else await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            logger.LogWarning("No se pudo guardar el vínculo con la orden de la compra {CompraId}", compra.Id);
            throw new PagoTemporalmenteNoDisponibleException("La compra quedó guardada para recuperar el vínculo con Mercado Pago.");
        }
        return Inicio(compra, reutilizada);
    }

    private async Task<JsonElement> BuscarOrdenAsync(CompraPremium compra, CancellationToken ct)
    {
        static string Fecha(DateTime fecha) => Uri.EscapeDataString(DateTime.SpecifyKind(fecha, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture));
        var hasta = compra.CreadaUtc.AddDays(1) < DateTime.UtcNow ? compra.CreadaUtc.AddDays(1) : DateTime.UtcNow;
        using var busqueda = CrearRequest(HttpMethod.Get, $"{Endpoint}?begin_date={Fecha(compra.CreadaUtc.AddMinutes(-5))}&end_date={Fecha(hasta)}&external_reference={compra.Id:D}&limit=2");
        var respuesta = await EnviarAsync(busqueda, ct);
        var coincidencias = Elementos(respuesta, "data").Where(o => Campo(o, "external_reference") == compra.Id.ToString("D")).Take(2).ToArray();
        if (coincidencias.Length != 1 || !EsIdOrden(Campo(coincidencias[0], "id")))
            throw new HttpRequestException("Mercado Pago aún no confirmó una única orden para esta compra.");
        var id = Campo(coincidencias[0], "id")!;
        using var consulta = CrearRequest(HttpMethod.Get, $"{Endpoint}/{Uri.EscapeDataString(id)}");
        var orden = await EnviarAsync(consulta, ct);
        if (Campo(orden, "id") != id) throw new HttpRequestException("La orden recuperada no coincide con la consulta.");
        return orden;
    }

    private async Task<bool> ReconciliarCompraAsync(CompraPremium compra, CancellationToken ct)
    {
        if (!ProveedorConfigurado() || compra.EntornoEsperado != Entorno || !EsIdOrden(compra.OrdenProveedorId)) return false;
        using var request = CrearRequest(HttpMethod.Get, $"{Endpoint}/{Uri.EscapeDataString(compra.OrdenProveedorId!)}");
        var orden = await EnviarAsync(request, ct);
        if (!OrdenCoincide(compra, orden))
        {
            logger.LogWarning("La orden consultada no coincide con la compra {CompraId}", compra.Id);
            return false;
        }

        await AplicarOrdenAsync(compra, orden, ct);
        return true;
    }

    private async Task AplicarOrdenAsync(CompraPremium compra, JsonElement orden, CancellationToken ct)
    {
        var teniaPagoValido = compra.Pagos.Any(p => p.Estado is EstadoPagoProveedor.Aprobado or EstadoPagoProveedor.ReembolsoParcial);
        var estado = Campo(orden, "status");
        var detalle = Campo(orden, "status_detail");
        var liquidada = estado == "processed" && detalle == "accredited" && Importe(orden, "total_paid_amount") >= compra.ImporteEsperado;
        var transacciones = Propiedad(orden, "transactions");
        var fecha = DateTimeOffset.TryParse(Campo(orden, "last_updated_date"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var fechaRemota)
            ? fechaRemota.UtcDateTime : DateTime.UtcNow;
        foreach (var pago in Elementos(transacciones, "payments"))
        {
            var id = Campo(pago, "id");
            if (id is not { Length: > 3 and <= 100 } || !id.StartsWith("PAY", StringComparison.Ordinal) || !id.All(char.IsAsciiLetterOrDigit)
                || Importe(pago, "amount") != compra.ImporteEsperado) continue;
            var estadoPago = Campo(pago, "status");
            var detallePago = Campo(pago, "status_detail");
            var reembolsos = Elementos(transacciones, "refunds").Where(r => Campo(r, "transaction_id") == id && Campo(r, "status") == "processed")
                .Sum(r => Importe(r, "amount") ?? 0);
            var estadoLocal = (estadoPago, detallePago) switch
            {
                _ when reembolsos >= compra.ImporteEsperado => EstadoPagoProveedor.Reembolsado,
                _ when reembolsos > 0 => EstadoPagoProveedor.ReembolsoParcial,
                ("processed", "accredited") when liquidada && Importe(pago, "paid_amount") == compra.ImporteEsperado => EstadoPagoProveedor.Aprobado,
                ("refunded", _) => EstadoPagoProveedor.Reembolsado,
                ("charged_back", _) => EstadoPagoProveedor.Contracargo,
                ("processed", "partially_refunded") => EstadoPagoProveedor.ReembolsoParcial,
                ("failed", _) => EstadoPagoProveedor.Rechazado,
                ("canceled" or "cancelled" or "expired", _) => EstadoPagoProveedor.Cancelado,
                _ => EstadoPagoProveedor.Pendiente
            };
            var local = compra.Pagos.SingleOrDefault(p => p.PagoProveedorId == id);
            if (local != null && (local.FechaProveedorUtc > fecha ||
                (local.Estado is EstadoPagoProveedor.Reembolsado or EstadoPagoProveedor.Contracargo && estadoLocal != local.Estado) ||
                (local.Estado == EstadoPagoProveedor.Aprobado && estadoLocal is EstadoPagoProveedor.Pendiente or EstadoPagoProveedor.Rechazado or EstadoPagoProveedor.Cancelado))) continue;
            if (local == null)
            {
                local = new PagoPremiumProveedor { CompraPremiumId = compra.Id, PagoProveedorId = id };
                compra.Pagos.Add(local);
                db.Add(local);
            }
            local.Estado = estadoLocal;
            local.Importe = compra.ImporteEsperado;
            local.Moneda = compra.MonedaEsperada;
            local.EntornoProduccion = Produccion;
            local.CuentaReceptoraId = Vendedor;
            local.FechaProveedorUtc = fecha;
            local.UltimaConsultaUtc = DateTime.UtcNow;
            local.EstadoProveedor = estadoPago is { Length: <= 50 } ? estadoPago : null;
            if (reembolsos > 0) local.ImporteReembolsado = reembolsos;
        }

        var aprobados = compra.Pagos.Count(p => p.Estado == EstadoPagoProveedor.Aprobado);
        var aprobadosAnteriores = await db.Set<PagoPremiumProveedor>().AsNoTracking().CountAsync(p =>
            p.CompraPremium.UsuarioId == compra.UsuarioId && p.CompraPremiumId != compra.Id && p.Estado == EstadoPagoProveedor.Aprobado, ct);
        compra.PosibleCobroDuplicado |= aprobados > 0 && aprobados + aprobadosAnteriores > 1;
        var siguiente = aprobados > 0 ? EstadoCompraPremium.Aprobada : estado switch
        {
            "refunded" => EstadoCompraPremium.Reembolsada,
            "charged_back" => EstadoCompraPremium.Contracargo,
            "processed" when detalle == "partially_refunded" => EstadoCompraPremium.ReembolsoParcial,
            "failed" => EstadoCompraPremium.Rechazada,
            "canceled" or "cancelled" or "expired" => EstadoCompraPremium.Cancelada,
            _ => EstadoCompraPremium.EsperandoPago
        };
        // Una lectura pendiente tardía no reabre un intento terminal y no bloquea un intento nuevo.
        if (siguiente != EstadoCompraPremium.EsperandoPago || compra.Estado is EstadoCompraPremium.EsperandoPago or EstadoCompraPremium.ErrorTemporal)
            compra.Estado = siguiente;
        if (aprobados + aprobadosAnteriores > 0) compra.Usuario.Plan = PlanUsuario.Plus;
        else if (teniaPagoValido && compra.Estado is EstadoCompraPremium.Reembolsada or EstadoCompraPremium.Contracargo && !compra.Usuario.AccesoPlusLegado
            && !await db.Set<PagoPremiumProveedor>().AnyAsync(p => p.CompraPremium.UsuarioId == compra.UsuarioId &&
                p.CompraPremiumId != compra.Id && p.Estado == EstadoPagoProveedor.ReembolsoParcial, ct))
            compra.Usuario.Plan = PlanUsuario.Free;
        compra.ActualizadaUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private bool OrdenCoincide(CompraPremium compra, JsonElement orden)
    {
        var id = Campo(orden, "id");
        var live = Propiedad(orden, "live_mode");
        return EsIdOrden(id) && (compra.OrdenProveedorId == null || id == compra.OrdenProveedorId)
            && id!.StartsWith("ORDTST", StringComparison.Ordinal) == (compra.EntornoEsperado == "sandbox")
            && (live.ValueKind == JsonValueKind.Undefined || live.ValueKind == (Produccion ? JsonValueKind.True : JsonValueKind.False))
            && Campo(orden, "external_reference") == compra.Id.ToString("D")
            && Campo(orden, "currency") == compra.MonedaEsperada && Campo(orden, "country_code") == "ARG"
            && Campo(orden, "user_id") == Vendedor && Importe(orden, "total_amount") == compra.ImporteEsperado;
    }

    private static InicioCompraPremium Inicio(CompraPremium compra, bool reutilizada) => new(compra.Id, compra.Estado.ToString(),
        compra.Estado == EstadoCompraPremium.EsperandoPago && EsCheckoutValido(compra.UrlCheckout, compra.OrdenProveedorId)
            ? compra.UrlCheckout : null, reutilizada);

    private static EstadoCompraPremiumResultado CrearResultado(CompraPremium compra) => new(
        compra.Id, compra.Estado.ToString(), compra.Usuario.EsPremium(),
        compra.Estado is EstadoCompraPremium.Rechazada or EstadoCompraPremium.Cancelada,
        compra.Estado switch
        {
            EstadoCompraPremium.Aprobada => "Tu acceso Plus está activo.",
            EstadoCompraPremium.Rechazada => "El pago fue rechazado. Podés iniciar un nuevo intento.",
            EstadoCompraPremium.Cancelada => "La orden se canceló o venció. Podés iniciar un nuevo intento.",
            EstadoCompraPremium.Reembolsada => "Mercado Pago confirmó el reembolso.",
            EstadoCompraPremium.Contracargo => "El pago quedó en revisión por un contracargo.",
            EstadoCompraPremium.ErrorTemporal or EstadoCompraPremium.CreandoProveedor => "La compra quedó guardada. Podés retomar el inicio desde la opción de Plus.",
            EstadoCompraPremium.ReembolsoParcial => "El pago tiene un reembolso parcial y requiere revisión.",
            _ => "El pago todavía está pendiente de confirmación."
        });

    private HttpRequestMessage CrearRequest(HttpMethod method, string url) => new(method, url)
    { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", AccessToken) } };

    private async Task<JsonElement> EnviarAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(TimeSpan.FromSeconds(12));
            using var respuesta = await http.SendAsync(request, limite.Token);
            respuesta.EnsureSuccessStatusCode();
            using var json = await JsonDocument.ParseAsync(await respuesta.Content.ReadAsStreamAsync(limite.Token), cancellationToken: limite.Token);
            if (json.RootElement.ValueKind != JsonValueKind.Object) throw new HttpRequestException("Respuesta del proveedor sin objeto JSON.");
            return json.RootElement.Clone();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new HttpRequestException("Tiempo de espera del proveedor agotado."); }
        catch (JsonException) { throw new HttpRequestException("Respuesta del proveedor con formato inesperado."); }
    }

    private bool ProveedorConfigurado() => !string.IsNullOrWhiteSpace(AccessToken) && Vendedor is { Length: > 0 and <= 100 } && Vendedor.All(char.IsAsciiDigit);
    private static bool EsIdOrden(string? id) => id is { Length: > 3 and <= 100 } && id.StartsWith("ORD", StringComparison.OrdinalIgnoreCase) && id.All(char.IsAsciiLetterOrDigit);
    private static bool EsRetornoValido(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https"
        && !uri.IsLoopback && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0;
    private static bool EsCheckoutValido(string? url, string? id) => EsIdOrden(id) && url?.Length <= 1000
        && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0
        && uri.Host is "www.mercadopago.com.ar" or "www.mercadopago.com"
        && uri.Query.TrimStart('?').Split('&').Where(p => p.StartsWith("order_id=", StringComparison.Ordinal)).SequenceEqual([$"order_id={id}"]);
    private string AgregarCompraId(string clave, Guid id)
    {
        var url = config[$"MercadoPago:{clave}"]!;
        return $"{url}{(url.Contains('?') ? '&' : '?')}compraId={id:D}";
    }

    private static JsonElement Propiedad(JsonElement objeto, string clave) => objeto.ValueKind == JsonValueKind.Object && objeto.TryGetProperty(clave, out var valor) ? valor : default;
    private static string? Campo(JsonElement objeto, string clave) => Propiedad(objeto, clave) is var valor && valor.ValueKind is JsonValueKind.String or JsonValueKind.Number ? valor.ToString() : null;
    private static IEnumerable<JsonElement> Elementos(JsonElement objeto, string clave) => Propiedad(objeto, clave) is var valor && valor.ValueKind == JsonValueKind.Array ? valor.EnumerateArray() : [];
    private static decimal? Importe(JsonElement objeto, string clave) => LeerImporte(Campo(objeto, clave));
    private static decimal? LeerImporte(string? valor) => decimal.TryParse(valor, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var numero) ? numero : null;
}
