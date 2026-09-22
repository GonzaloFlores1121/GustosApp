using System.Net;
using System.Net.Http.Json;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using GustosApp.Infraestructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace GustosApp.API.Tests;

public class ReclamoRestauranteApiPruebas
{
    private static MultipartFormDataContent Formulario()
    {
        var datos = new MultipartFormDataContent();
        datos.Add(new StringContent("Ana Pérez"), "NombreSolicitante");
        datos.Add(new StringContent("Propietaria"), "RelacionRestaurante");
        datos.Add(new StringContent("1122334455"), "TelefonoContacto");
        datos.Add(new StringContent("true"), "DeclaraAutorizacion");
        datos.Add(new ByteArrayContent("%PDF-1.4 prueba"u8.ToArray()), "Comprobante", "prueba.pdf");
        return datos;
    }
    [Fact]
    public async Task ReclamarPorHttp_PersisteVinculoYReutilizaSolicitudPendiente()
    {
        await using var baseFabrica = new FabricaApiGustosAppVotacion();
        await using var fabrica = baseFabrica.WithWebHostBuilder(builder => builder.ConfigureTestServices(servicios =>
        {
            servicios.RemoveAll<IFirebaseAuthService>();
            servicios.AddSingleton(Mock.Of<IFirebaseAuthService>());
            servicios.RemoveAll<IEmailService>();
            servicios.AddSingleton(Mock.Of<IEmailService>());
            servicios.RemoveAll<IFileStorageService>();
            servicios.AddSingleton(Mock.Of<IFileStorageService>());
        }));
        using var cliente = fabrica.CreateClient();
        Guid restauranteId;
        using (var scope = fabrica.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.Usuarios.Add(new Usuario("usuario-pruebas-integracion", "usuario@example.test", "Usuario", "Prueba", "usuario", null)
            { Rol = RolUsuario.Usuario, RegistroInicialCompleto = true });
            var restaurante = new Restaurante { Id = Guid.NewGuid(), Nombre = "Importado", PlaceId = "place-importado" };
            restauranteId = restaurante.Id;
            db.Restaurantes.Add(restaurante);
            await db.SaveChangesAsync();
        }

        var ruta = $"/api/Restaurantes/{restauranteId}/reclamo";
        var sinDatos = await cliente.PostAsync(ruta, new MultipartFormDataContent());
        Assert.Equal(HttpStatusCode.BadRequest, sinDatos.StatusCode);
        var respuesta = await cliente.PostAsync(ruta, Formulario());
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var solicitudId = await respuesta.Content.ReadFromJsonAsync<Guid>();
        var repetida = await cliente.PostAsync(ruta, Formulario());
        Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);
        Assert.Equal(solicitudId, await repetida.Content.ReadFromJsonAsync<Guid>());
        var estadoRespuesta = await cliente.GetAsync("/api/Restaurantes/mi-solicitud");
        Assert.Equal(HttpStatusCode.OK, estadoRespuesta.StatusCode);
        var estado = await estadoRespuesta.Content.ReadFromJsonAsync<MiSolicitudRespuesta>();
        Assert.NotNull(estado);
        Assert.Equal(solicitudId, estado.Id);
        Assert.Equal("Pendiente", estado.Estado);
        Assert.Equal("Reclamo", estado.Tipo);
        Assert.Equal("Importado", estado.NombreRestaurante);
        using var verificacion = fabrica.Services.CreateScope();
        var contexto = verificacion.ServiceProvider.GetRequiredService<GustosDbContext>();
        var solicitud = await contexto.SolicitudesRestaurantes.SingleAsync();
        Assert.Equal(restauranteId, solicitud.RestauranteExistenteId);
        Assert.Equal("Ana Pérez", solicitud.NombreSolicitante);
        Assert.Equal("%PDF-1.4 prueba"u8.ToArray(), solicitud.ComprobanteReclamo);
        var descarga = await cliente.GetAsync($"/api/solicitudes-restaurantes/{solicitudId}/comprobante");
        Assert.Equal(HttpStatusCode.OK, descarga.StatusCode);
        Assert.Equal(solicitud.ComprobanteReclamo, await descarga.Content.ReadAsByteArrayAsync());
        Assert.Equal("no-store", descarga.Headers.CacheControl?.ToString());
        Assert.Equal("attachment", descarga.Content.Headers.ContentDisposition?.DispositionType);
        var otroUsuario = new Usuario("otro-uid", "otro@example.test", "Otro", "Usuario", "otro", null);
        contexto.Usuarios.Add(otroUsuario);
        var ajena = new SolicitudRestaurante { Id = Guid.NewGuid(), UsuarioId = otroUsuario.Id, Usuario = otroUsuario, Nombre = "Ajeno", Direccion = "Calle", WebsiteUrl = "", TipoComprobante = "application/pdf", ComprobanteReclamo = "%PDF-privado"u8.ToArray() };
        contexto.SolicitudesRestaurantes.Add(ajena);
        await contexto.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/solicitudes-restaurantes/{ajena.Id}/comprobante")).StatusCode);
        Assert.Equal(EstadoSolicitudRestaurante.Pendiente, solicitud.Estado);
        Assert.Null((await contexto.Restaurantes.SingleAsync(r => r.Id == restauranteId)).DuenoId);
        Assert.Equal(RolUsuario.Usuario, (await contexto.Usuarios.SingleAsync(u => u.FirebaseUid == "usuario-pruebas-integracion")).Rol);
    }

    private sealed record MiSolicitudRespuesta(Guid Id, string Estado, string Tipo, string NombreRestaurante);
}
