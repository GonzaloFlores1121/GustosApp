using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GustosApp.API.Controllers;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using GustosApp.Infraestructure;
using GustosApp.Infraestructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace GustosApp.API.Tests;

public class MercadoPagoServicePruebas
{
    [Fact]
    public void Catalogo_SinPrecioDefinitivo_NoHabilitaCobrosNiInventaImporte()
    {
        using var db = CrearDb();
        var service = CrearServicio(db, new Handler(_ => Json("{}")), incluirPrecio: false);
        var catalogo = service.ObtenerCatalogo();
        catalogo.CobrosHabilitados.Should().BeFalse();
        catalogo.Precio.Should().BeNull();
        catalogo.Beneficios.Should().Equal("Grupos sin límite", "Restaurantes favoritos sin límite");
    }

    [Fact]
    public async Task IniciarDosVeces_ReutilizaCompraYUnaSolaOrdenConMismaClavePersistida()
    {
        using var db = CrearDb();
        var usuario = CrearUsuario(); db.Add(usuario); await db.SaveChangesAsync();
        var requests = new List<HttpRequestMessage>();
        var handler = new Handler(request =>
        {
            requests.Add(request);
            return Json("{\"id\":\"ORD-1\",\"checkout_url\":\"https://checkout.test/1\"}");
        });
        var service = CrearServicio(db, handler);

        var first = await service.IniciarCompraAsync(usuario.FirebaseUid);
        var second = await service.IniciarCompraAsync(usuario.FirebaseUid);

        second.CompraId.Should().Be(first.CompraId);
        second.Reutilizada.Should().BeTrue();
        requests.Should().ContainSingle();
        requests[0].Headers.GetValues("X-Idempotency-Key").Single().Should().Be(
            (await db.ComprasPremium.SingleAsync()).ClaveIdempotencia.ToString("D"));
    }

    [Fact]
    public async Task PagoAprobadoCorrecto_ActivaUnaVez_NotificacionDuplicadaNoDuplicaRegistro()
    {
        using var db = CrearDb();
        var usuario = CrearUsuario(); db.Add(usuario); await db.SaveChangesAsync();
        var service = CrearServicio(db, new Handler(request => request.Method == HttpMethod.Post
            ? Json("{\"id\":\"ORD-1\",\"checkout_url\":\"https://checkout.test/1\"}")
            : Json(PagoJson(db.ComprasPremium.Single().Id, "approved", 1234m))));
        var compra = await service.IniciarCompraAsync(usuario.FirebaseUid);

        await service.ProcesarNotificacionPagoAsync("PAY-1");
        await service.ProcesarNotificacionPagoAsync("PAY-1");

        (await db.Usuarios.SingleAsync()).Plan.Should().Be(PlanUsuario.Plus);
        (await db.PagosPremiumProveedor.CountAsync()).Should().Be(1);
        (await db.ComprasPremium.FindAsync(compra.CompraId))!.Estado.Should().Be(EstadoCompraPremium.Aprobada);
    }

    [Fact]
    public async Task PagoConImporteIncorrecto_NoActivaAcceso()
    {
        using var db = CrearDb();
        var usuario = CrearUsuario(); db.Add(usuario); await db.SaveChangesAsync();
        var service = CrearServicio(db, new Handler(request => request.Method == HttpMethod.Post
            ? Json("{\"id\":\"ORD-1\",\"checkout_url\":\"https://checkout.test/1\"}")
            : Json(PagoJson(db.ComprasPremium.Single().Id, "approved", 99m))));
        await service.IniciarCompraAsync(usuario.FirebaseUid);

        await service.ProcesarNotificacionPagoAsync("PAY-X");

        (await db.Usuarios.SingleAsync()).Plan.Should().Be(PlanUsuario.Free);
        (await db.PagosPremiumProveedor.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task TimeoutCreandoOrden_ConservaOperacionInciertaYNoCreaOtraAlReintentar()
    {
        using var db = CrearDb();
        var usuario = CrearUsuario(); db.Add(usuario); await db.SaveChangesAsync();
        var calls = 0;
        var service = CrearServicio(db, new Handler(_ =>
        {
            calls++;
            if (calls == 1) throw new HttpRequestException("timeout");
            return Json("{\"id\":\"ORD-RECOVERED\",\"checkout_url\":\"https://checkout.test/recovered\"}");
        }));

        Func<Task> first = () => service.IniciarCompraAsync(usuario.FirebaseUid);
        await first.Should().ThrowAsync<PagoTemporalmenteNoDisponibleException>();
        var second = await service.IniciarCompraAsync(usuario.FirebaseUid);

        calls.Should().Be(2);
        second.Reutilizada.Should().BeTrue();
        second.Estado.Should().Be(EstadoCompraPremium.EsperandoPago.ToString());
        (await db.ComprasPremium.CountAsync()).Should().Be(1);
        (await db.ComprasPremium.SingleAsync()).ClaveIdempotencia.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ReembolsoTotal_NoQuitaPlusSiOtroPagoValidoLoSustenta()
    {
        using var db = CrearDb();
        var usuario = CrearUsuario(); usuario.Plan = PlanUsuario.Plus;
        var invalidada = new CompraPremium { Usuario = usuario, UsuarioId = usuario.Id, ImporteEsperado = 1234, Estado = EstadoCompraPremium.Aprobada };
        var vigente = new CompraPremium { Usuario = usuario, UsuarioId = usuario.Id, ImporteEsperado = 1234, Estado = EstadoCompraPremium.Aprobada };
        invalidada.Pagos.Add(new PagoPremiumProveedor { CompraPremium = invalidada, PagoProveedorId = "PAY-REFUND", Estado = EstadoPagoProveedor.Aprobado, Importe = 1234, Moneda = "ARS" });
        vigente.Pagos.Add(new PagoPremiumProveedor { CompraPremium = vigente, PagoProveedorId = "PAY-VALID", Estado = EstadoPagoProveedor.Aprobado, Importe = 1234, Moneda = "ARS" });
        db.AddRange(usuario, invalidada, vigente); await db.SaveChangesAsync();
        var service = CrearServicio(db, new Handler(_ => Json(PagoJson(invalidada.Id, "refunded", 1234m, 1234m))));

        await service.ProcesarNotificacionPagoAsync("PAY-REFUND");

        usuario.Plan.Should().Be(PlanUsuario.Plus);
        invalidada.Estado.Should().Be(EstadoCompraPremium.Reembolsada);
    }

    [Fact]
    public async Task DosPagosDistintosAprobados_ConservaEvidenciaYMarcaRevisionSinDevolver()
    {
        using var db = CrearDb();
        var usuario = CrearUsuario();
        var anterior = new CompraPremium { Usuario = usuario, UsuarioId = usuario.Id, ImporteEsperado = 1234, Estado = EstadoCompraPremium.Aprobada };
        anterior.Pagos.Add(new PagoPremiumProveedor { CompraPremium = anterior, PagoProveedorId = "PAY-OLD", Estado = EstadoPagoProveedor.Aprobado, Importe = 1234, Moneda = "ARS" });
        db.AddRange(usuario, anterior); await db.SaveChangesAsync();
        var service = CrearServicio(db, new Handler(request => request.Method == HttpMethod.Post
            ? Json("{\"id\":\"ORD-NEW\",\"checkout_url\":\"https://checkout.test/new\"}")
            : Json(PagoJson(db.ComprasPremium.OrderByDescending(c => c.CreadaUtc).First(c => c.Id != anterior.Id).Id, "approved", 1234m))));
        var current = await service.IniciarCompraAsync(usuario.FirebaseUid);

        await service.ProcesarNotificacionPagoAsync("PAY-NEW");
        var flagged = await service.ObtenerCobrosDuplicadosAsync();

        (await db.PagosPremiumProveedor.CountAsync()).Should().Be(2);
        flagged.Should().ContainSingle(c => c.CompraId == current.CompraId);
    }

    [Fact]
    public void FirmaWebhook_ValidaHmacYRechazaAlteraciones()
    {
        using var db = CrearDb();
        var service = CrearServicio(db, new Handler(_ => Json("{}")));
        const string dataId = "PAY-1", requestId = "request-1";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var manifest = $"id:{dataId.ToLowerInvariant()};request-id:{requestId};ts:{ts};";
        var hash = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes("webhook-secret"), Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant();

        service.ValidarFirmaWebhook(dataId, requestId, $"ts={ts},v1={hash}").Should().BeTrue();
        service.ValidarFirmaWebhook(dataId, requestId, $"ts={ts},v1={new string('0', 64)}").Should().BeFalse();
    }

    [Fact]
    public async Task ConsultarCompraAjena_NoExponeSuEstado()
    {
        using var db = CrearDb();
        var owner = CrearUsuario();
        var foreign = new Usuario("other", "other@test.com", "Otra", "Cuenta", "otra");
        var compra = new CompraPremium { Usuario = owner, UsuarioId = owner.Id, ImporteEsperado = 1234 };
        db.AddRange(owner, foreign, compra); await db.SaveChangesAsync();
        var service = CrearServicio(db, new Handler(_ => Json("{}")));

        Func<Task> action = () => service.ObtenerEstadoCompraAsync(foreign.FirebaseUid, compra.Id, false);

        await action.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public void Controlador_NoExponeAccesosDePruebaNiDiagnostico()
    {
        var methods = typeof(PagoController).GetMethods().Select(m => m.Name);
        methods.Should().NotContain(["ForzarPremiumDev", "CrearPagoTest", "Diagnostico"]);
    }

    private static MercadoPagoService CrearServicio(GustosDbContext db, HttpMessageHandler handler, bool incluirPrecio = true)
    {
        var values = new Dictionary<string, string?>
        {
            ["MercadoPago:Premium:HabilitarCobros"] = "true",
            ["MercadoPago:Premium:Moneda"] = "ARS",
            ["MercadoPago:AccessToken"] = "TEST-token",
            ["MercadoPago:WebhookSecret"] = "webhook-secret",
            ["MercadoPago:CollectorId"] = "123",
            ["MercadoPago:WebhookUrl"] = "https://api.test/webhook",
            ["MercadoPago:SuccessUrl"] = "https://web.test/pago/exito",
            ["MercadoPago:FailureUrl"] = "https://web.test/pago/fallo",
            ["MercadoPago:PendingUrl"] = "https://web.test/pago/pendiente",
            ["MercadoPago:Produccion"] = "false"
        };
        if (incluirPrecio) values["MercadoPago:Premium:Precio"] = "1234.00";
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new MercadoPagoService(new HttpClient(handler), db, config, NullLogger<MercadoPagoService>.Instance);
    }

    private static GustosDbContext CrearDb() => new(new DbContextOptionsBuilder<GustosDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static Usuario CrearUsuario() => new("uid", "user@test.com", "Ana", "Prueba", "ana");
    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private static string PagoJson(Guid compraId, string status, decimal amount, decimal refunded = 0) => JsonSerializer.Serialize(new
    {
        id = 1, status, external_reference = compraId.ToString("D"), transaction_amount = amount,
        currency_id = "ARS", live_mode = false, collector_id = 123,
        date_approved = "2026-09-28T00:00:00Z", transaction_details = new { total_refunded = refunded }
    });

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(response(request));
    }
}
