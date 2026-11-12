using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GustosApp.API.Controllers;
using GustosApp.API.DTO;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using GustosApp.Infraestructure;
using GustosApp.Infraestructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GustosApp.API.Tests;

public class MercadoPagoServicePruebas
{
    private const string OrdenId = "ORDTSTPRUEBA1";
    private const string PagoId = "PAYPRUEBA1";
    private const string FirmaPrueba = "firma-sintetica-webhook";

    [Theory]
    [InlineData("Premium:Precio", null)]
    [InlineData("Premium:Precio", "1.234")]
    [InlineData("AccessTokenPrueba", null)]
    [InlineData("CollectorIdPrueba", null)]
    [InlineData("SuccessUrl", "https://localhost/pago/exito")]
    [InlineData("FailureUrl", null)]
    [InlineData("Premium:Moneda", "USD")]
    public void Catalogo_ConfiguracionIncompletaOInvalida_DeshabilitaCobros(string clave, string? valor)
    {
        using var db = CrearDb();
        var servicio = CrearServicio(db, Handler.SinPeticiones(), ajustes: c => c[clave] = valor);
        var catalogo = servicio.ObtenerCatalogo();
        Assert.False(catalogo.CobrosHabilitados);
        if (clave == "Premium:Precio") Assert.Null(catalogo.Precio);
        Assert.Equal(["Grupos sin límite", "Restaurantes favoritos sin límite"], catalogo.Beneficios);
    }

    [Fact]
    public async Task DosInicios_CreanUnaOrdenConFormatoCheckoutProYLaMismaCompra()
    {
        await using var db = CrearDb();
        var usuario = await SembrarUsuarioAsync(db);
        var posts = 0;
        var servicio = CrearServicio(db, new Handler(async (request, ct) =>
        {
            var compra = await db.ComprasPremium.SingleAsync(ct);
            if (request.Method == HttpMethod.Post)
            {
                posts++;
                Assert.Equal("/v1/orders", request.RequestUri!.AbsolutePath);
                Assert.Equal("credencial-de-prueba-sintetica", request.Headers.Authorization?.Parameter);
                Assert.Equal(compra.ClaveIdempotencia.ToString("D"), request.Headers.GetValues("X-Idempotency-Key").Single());
                var cuerpo = await request.Content!.ReadFromJsonAsync<JsonElement>(ct);
                Assert.Equal("manual", cuerpo.GetProperty("processing_mode").GetString());
                Assert.Equal("P1D", cuerpo.GetProperty("expiration_time").GetString());
                Assert.Equal("1234.00", cuerpo.GetProperty("items")[0].GetProperty("unit_price").GetString());
                Assert.False(cuerpo.TryGetProperty("transactions", out _));
                Assert.False(cuerpo.TryGetProperty("back_urls", out _));
                var retorno = cuerpo.GetProperty("config").GetProperty("online");
                Assert.EndsWith($"?origen=prueba&compraId={compra.Id:D}", retorno.GetProperty("success_url").GetString());
                Assert.Equal("approved", retorno.GetProperty("auto_return").GetString());
                Assert.False(retorno.GetProperty("retries").GetProperty("allowed").GetBoolean());
            }
            else Assert.Equal($"/v1/orders/{OrdenId}", request.RequestUri!.AbsolutePath);
            return Orden(compra.Id);
        }));

        var primera = await servicio.IniciarCompraAsync(usuario.FirebaseUid);
        var segunda = await servicio.IniciarCompraAsync(usuario.FirebaseUid);
        Assert.Equal(primera.CompraId, segunda.CompraId);
        Assert.True(segunda.Reutilizada);
        Assert.NotNull(segunda.UrlCheckout);
        Assert.Equal(1, posts);
    }

    [Fact]
    public async Task PruebaLocalSinRetornos_EnviaOrdenSinRedireccionAutomatica()
    {
        await using var db = CrearDb();
        var usuario = await SembrarUsuarioAsync(db);
        using var handler = new Handler(async (request, ct) =>
        {
            var cuerpo = await request.Content!.ReadFromJsonAsync<JsonElement>(ct);
            var online = cuerpo.GetProperty("config").GetProperty("online");
            Assert.Equal("retries", Assert.Single(online.EnumerateObject()).Name);
            return Orden(await db.ComprasPremium.Select(c => c.Id).SingleAsync(ct));
        });
        void SinRetornos(Dictionary<string, string?> configuracion)
        {
            configuracion["SuccessUrl"] = configuracion["FailureUrl"] = configuracion["PendingUrl"] = "";
        }
        var servicio = CrearServicio(db, handler, SinRetornos);
        Assert.True(servicio.ObtenerCatalogo().CobrosHabilitados);
        Assert.NotNull((await servicio.IniciarCompraAsync(usuario.FirebaseUid)).UrlCheckout);
        var produccion = CrearServicio(db, Handler.SinPeticiones(), configuracion =>
        {
            SinRetornos(configuracion);
            configuracion["Produccion"] = "true";
            configuracion["WebhookSecret"] = "firma-sintetica-produccion";
        });
        Assert.False(produccion.ObtenerCatalogo().CobrosHabilitados);
    }

    [Theory]
    [InlineData("1234.00", "ARS", "123", false, false, "ORDTSTPRUEBA1", true)]
    [InlineData("99.00", "ARS", "123", false, false, "ORDTSTPRUEBA1", false)]
    [InlineData("1234.00", "USD", "123", false, false, "ORDTSTPRUEBA1", false)]
    [InlineData("1234.00", "ARS", "321", false, false, "ORDTSTPRUEBA1", false)]
    [InlineData("1234.00", "ARS", "123", true, false, "ORDTSTPRUEBA1", false)]
    [InlineData("1234.00", "ARS", "123", false, true, "ORDTSTPRUEBA1", false)]
    [InlineData("1234.00", "ARS", "123", false, false, "ORDTSTOTRA", false)]
    public async Task NotificacionRepetida_SoloConDatosVerificadosOtorgaPlusYRegistraUnPago(string monto,
        string moneda, string vendedor, bool produccion, bool referenciaAjena, string id, bool valida)
    {
        await using var db = CrearDb();
        var compra = await SembrarCompraAsync(db);
        var servicio = CrearServicio(db, new Handler(request =>
        {
            Assert.Equal($"/v1/orders/{OrdenId}", request.RequestUri!.AbsolutePath);
            return Orden(referenciaAjena ? Guid.NewGuid() : compra.Id, "processed", "accredited", id, monto, moneda, vendedor, produccion);
        }));

        var esperado = valida ? ResultadoWebhookPago.Procesado : ResultadoWebhookPago.Ignorado;
        Assert.Equal(esperado, await servicio.ProcesarNotificacionOrdenAsync(OrdenId));
        Assert.Equal(esperado, await servicio.ProcesarNotificacionOrdenAsync(OrdenId));
        Assert.Equal(valida ? PlanUsuario.Plus : PlanUsuario.Free, compra.Usuario.Plan);
        Assert.Equal(valida ? 1 : 0, await db.PagosPremiumProveedor.CountAsync());
    }

    [Theory]
    [InlineData("created", "created", "EsperandoPago", false)]
    [InlineData("processing", "in_process", "EsperandoPago", false)]
    [InlineData("failed", "bad_filled_card_data", "Rechazada", false)]
    [InlineData("canceled", "expired", "Cancelada", false)]
    [InlineData("processed", "accredited", "Aprobada", true)]
    public async Task ReconciliacionSinWebhook_UsaElEstadoActualDeOrders(string estado, string detalle, string esperado, bool plus)
    {
        await using var db = CrearDb();
        var compra = await SembrarCompraAsync(db);
        var servicio = CrearServicio(db, new Handler(_ => Orden(compra.Id, estado, detalle)));
        Assert.Equal(plus, await servicio.VerificarYProcesarPagosPendientesAsync(compra.Usuario.FirebaseUid));
        var resultado = await servicio.ObtenerEstadoCompraAsync(compra.Usuario.FirebaseUid, compra.Id, false);
        Assert.Equal(esperado, resultado.Estado);
        Assert.Equal(plus, resultado.AccesoPlus);
    }

    [Fact]
    public async Task WebhookConFalloTransitorio_ConservaLaCompraYAdmiteReintento()
    {
        await using var db = CrearDb();
        var compra = await SembrarCompraAsync(db);
        var peticiones = 0;
        var servicio = CrearServicio(db, new Handler(_ => ++peticiones == 1
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : Orden(compra.Id, "processed", "accredited")));
        Assert.Equal(ResultadoWebhookPago.FalloTransitorio, await servicio.ProcesarNotificacionOrdenAsync(OrdenId));
        Assert.Equal(EstadoCompraPremium.EsperandoPago, compra.Estado);
        Assert.Equal(PlanUsuario.Free, compra.Usuario.Plan);
        Assert.Equal(ResultadoWebhookPago.Procesado, await servicio.ProcesarNotificacionOrdenAsync(OrdenId));
        Assert.Equal(EstadoCompraPremium.Aprobada, compra.Estado);
    }

    [Theory]
    [InlineData("https://checkout.example.test/checkout?order_id=ORDTSTPRUEBA1")]
    [InlineData("https://www.mercadopago.com.ar/checkout/v1/redirect?order_id=ORDTSTOTRA")]
    public async Task InicioConEnlaceInconsistente_ConservaLaCompraSinRedirigir(string url)
    {
        await using var db = CrearDb();
        var usuario = await SembrarUsuarioAsync(db);
        var servicio = CrearServicio(db, new Handler(_ => Orden(db.ComprasPremium.Single().Id, checkout: url)));
        await Assert.ThrowsAsync<PagoTemporalmenteNoDisponibleException>(() => servicio.IniciarCompraAsync(usuario.FirebaseUid));
        var compra = await db.ComprasPremium.SingleAsync();
        Assert.Null(compra.UrlCheckout);
        Assert.Equal(EstadoCompraPremium.ErrorTemporal, compra.Estado);
    }

    [Fact]
    public async Task ReintentoTrasTimeoutYConflicto_RecuperaLaOrdenPorReferenciaSinNuevaClave()
    {
        await using var db = CrearDb();
        var usuario = await SembrarUsuarioAsync(db);
        var claves = new List<string>();
        var servicio = CrearServicio(db, new Handler(request =>
        {
            var compra = db.ComprasPremium.Single();
            if (request.Method == HttpMethod.Post)
            {
                claves.Add(request.Headers.GetValues("X-Idempotency-Key").Single());
                if (claves.Count == 1) throw new HttpRequestException("timeout simulado");
                return new HttpResponseMessage(HttpStatusCode.Conflict);
            }
            if (request.RequestUri!.AbsolutePath == "/v1/orders")
            {
                Assert.Contains($"external_reference={compra.Id:D}", request.RequestUri.Query);
                return Json(new { data = new[] { new { id = OrdenId, external_reference = compra.Id } } });
            }
            return Orden(compra.Id, "processed", "accredited");
        }));

        await Assert.ThrowsAsync<PagoTemporalmenteNoDisponibleException>(() => servicio.IniciarCompraAsync(usuario.FirebaseUid));
        var resultado = await servicio.IniciarCompraAsync(usuario.FirebaseUid);
        Assert.True(resultado.Reutilizada);
        Assert.Equal("Aprobada", resultado.Estado);
        Assert.Null(resultado.UrlCheckout);
        Assert.Equal(2, claves.Count);
        Assert.Single(claves.Distinct());
        Assert.Equal(1, await db.ComprasPremium.CountAsync());
    }

    [Fact]
    public async Task ReembolsoTotal_NoQuitaPlusSiOtroPagoValidoLoSustenta()
    {
        await using var db = CrearDb();
        var compra = await SembrarCompraAsync(db);
        var servicio = CrearServicio(db, new Handler(_ => Orden(compra.Id, "processed", "accredited")));
        await servicio.ProcesarNotificacionOrdenAsync(OrdenId);
        var anterior = await SembrarCompraAsync(db, compra.Usuario, "ORDTSTPREVIA", EstadoCompraPremium.Aprobada);
        db.PagosPremiumProveedor.Add(new PagoPremiumProveedor { CompraPremium = anterior, PagoProveedorId = "PAYPREVIO", Estado = EstadoPagoProveedor.Aprobado });
        await db.SaveChangesAsync();

        var reembolso = CrearServicio(db, new Handler(_ => Orden(compra.Id, "refunded", "refunded")));
        await reembolso.ProcesarNotificacionOrdenAsync(OrdenId);
        Assert.Equal(PlanUsuario.Plus, compra.Usuario.Plan);
        Assert.Equal(EstadoCompraPremium.Reembolsada, compra.Estado);
    }

    [Fact]
    public async Task SegundoPagoAprobado_MarcaRevisionSinReembolsar()
    {
        await using var db = CrearDb();
        var compra = await SembrarCompraAsync(db);
        var anterior = await SembrarCompraAsync(db, compra.Usuario, "ORDTSTPREVIA", EstadoCompraPremium.Aprobada);
        db.PagosPremiumProveedor.Add(new PagoPremiumProveedor { CompraPremium = anterior, PagoProveedorId = "PAYPREVIO", Estado = EstadoPagoProveedor.Aprobado });
        await db.SaveChangesAsync();
        var servicio = CrearServicio(db, new Handler(_ => Orden(compra.Id, "processed", "accredited")));
        await servicio.ProcesarNotificacionOrdenAsync(OrdenId);
        Assert.Equal(2, await db.PagosPremiumProveedor.CountAsync());
        Assert.Equal(compra.Id, Assert.Single(await servicio.ObtenerCobrosDuplicadosAsync()).CompraId);
    }

    [Fact]
    public async Task Webhook_ValidaFirmaYRechazaUnIdDeCuerpoDistintoDelFirmado()
    {
        await using var db = CrearDb();
        var servicio = CrearServicio(db, Handler.SinPeticiones());
        var firma = Firmar(OrdenId);
        Assert.True(servicio.ValidarFirmaWebhook(OrdenId, "request-1", firma));
        Assert.False(servicio.ValidarFirmaWebhook(OrdenId, "request-1", firma + ",ts=123"));
        Assert.False(servicio.ValidarFirmaWebhook(OrdenId, "request-2", firma));
        Assert.False(servicio.ValidarFirmaWebhook(OrdenId, "request-1", Firmar(OrdenId, DateTimeOffset.UtcNow.AddMinutes(-10))));
        var controlador = new PagoController(servicio, Mock.Of<IUsuarioRepository>(), NullLogger<PagoController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        controlador.Request.QueryString = new QueryString($"?data.id={OrdenId}");
        controlador.Request.Headers["x-request-id"] = "request-1";
        controlador.Request.Headers["x-signature"] = firma;
        var aviso = new WebhookPagoRequest { Type = "order", Data = new WebhookPagoData { Id = "ORDTSTOTRA" } };
        Assert.IsType<BadRequestResult>(await controlador.WebhookPago(aviso, default));
        controlador.Request.Headers["x-signature"] = "invalida";
        Assert.IsType<UnauthorizedResult>(await controlador.WebhookPago(aviso, default));
    }

    [Fact]
    public async Task ConsultarCompraAjena_NoExponeSuCompraNiConsultaProveedor()
    {
        await using var db = CrearDb();
        var compra = await SembrarCompraAsync(db);
        var servicio = CrearServicio(db, Handler.SinPeticiones());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => servicio.ObtenerEstadoCompraAsync("otro-uid", compra.Id, true));
    }

    [Fact]
    public void Controlador_NoExponeAccesosDePruebaNiDiagnostico()
    {
        var methods = typeof(PagoController).GetMethods().Select(m => m.Name);
        Assert.DoesNotContain(methods, m => m is "ForzarPremiumDev" or "CrearPagoTest" or "Diagnostico");
    }

    internal static MercadoPagoService CrearServicio(GustosDbContext db, HttpMessageHandler handler, Action<Dictionary<string, string?>>? ajustes = null)
    {
        var valores = new Dictionary<string, string?>
        {
            ["Premium:HabilitarCobros"] = "true", ["Premium:Precio"] = "1234.00",
            ["AccessTokenPrueba"] = "credencial-de-prueba-sintetica", ["CollectorIdPrueba"] = "123", ["WebhookSecretPrueba"] = FirmaPrueba,
            ["AccessToken"] = "credencial-produccion-sintetica", ["CollectorId"] = "999",
            ["SuccessUrl"] = "https://gusto.example.test/pago/exito?origen=prueba", ["FailureUrl"] = "https://gusto.example.test/pago/fallo",
            ["PendingUrl"] = "https://gusto.example.test/pago/pendiente"
        };
        ajustes?.Invoke(valores);
        var config = new ConfigurationBuilder().AddInMemoryCollection(valores.ToDictionary(kv => $"MercadoPago:{kv.Key}", kv => kv.Value)).Build();
        return new MercadoPagoService(new HttpClient(handler, disposeHandler: false), db, config, NullLogger<MercadoPagoService>.Instance);
    }

    private static GustosDbContext CrearDb() => new(new DbContextOptionsBuilder<GustosDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    internal static async Task<Usuario> SembrarUsuarioAsync(GustosDbContext db)
    {
        var id = Guid.NewGuid().ToString("N");
        var usuario = new Usuario(id, $"{id}@example.test", "Ana", "Prueba", id[..12]);
        db.Add(usuario);
        await db.SaveChangesAsync();
        return usuario;
    }

    private static async Task<CompraPremium> SembrarCompraAsync(GustosDbContext db, Usuario? usuario = null, string id = OrdenId,
        EstadoCompraPremium estado = EstadoCompraPremium.EsperandoPago)
    {
        usuario ??= await SembrarUsuarioAsync(db);
        var compra = new CompraPremium { Usuario = usuario, ImporteEsperado = 1234, OrdenProveedorId = id, Estado = estado };
        db.Add(compra);
        await db.SaveChangesAsync();
        return compra;
    }

    internal static HttpResponseMessage Orden(Guid compraId, string estado = "created", string detalle = "created", string id = OrdenId,
        string monto = "1234.00", string moneda = "ARS", string vendedor = "123", bool produccion = false, string? checkout = null) => Json(new
    {
        id, status = estado, status_detail = detalle, external_reference = compraId, total_amount = monto,
        total_paid_amount = estado == "processed" ? monto : "0.00", currency = moneda, country_code = "ARG", user_id = vendedor,
        live_mode = produccion, last_updated_date = DateTime.UtcNow,
        checkout_url = checkout ?? $"https://www.mercadopago.com.ar/checkout/v1/redirect?order_id={id}",
        transactions = new { payments = new[] { new { id = PagoId, amount = monto, paid_amount = monto, status = estado, status_detail = detalle } } }
    });

    internal static HttpResponseMessage Json<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    private static string Firmar(string id, DateTimeOffset? fecha = null)
    {
        var ts = (fecha ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds();
        var manifiesto = $"id:{id.ToLowerInvariant()};request-id:request-1;ts:{ts};";
        var hash = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(FirmaPrueba), Encoding.UTF8.GetBytes(manifiesto)));
        return $"ts={ts},v1={hash}";
    }

    internal sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        internal Handler(Func<HttpRequestMessage, HttpResponseMessage> responder) : this((r, _) => Task.FromResult(responder(r))) { }
        internal static Handler SinPeticiones() => new(_ => throw new InvalidOperationException("No se esperaba consultar al proveedor."));
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => responder(request, cancellationToken);
    }
}
