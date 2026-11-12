using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.RestauranteUseCases.SolicitudRestauranteUseCases;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using GustosApp.Infraestructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace GustosApp.API.Tests;

public sealed class CoincidenciasAltaApiPruebas
{
    [Fact]
    public async Task EnviarAlta_PersisteDeclaracionYDevuelveConflictoParaOtroTramite()
    {
        var storage = new Mock<IFileStorageService>();
        storage.Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://imagenes.example.test/foto.png");
        await using var baseFabrica = new FabricaApiGustosAppVotacion();
        await using var fabrica = baseFabrica.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IFileStorageService>();
            services.AddSingleton(storage.Object);
            services.RemoveAll<IEmailService>();
            services.AddSingleton(Mock.Of<IEmailService>());
        }));
        using var cliente = fabrica.CreateClient();
        using var scope = fabrica.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
        db.Usuarios.Add(new Usuario("usuario-pruebas-integracion", "usuario@example.test", "Usuario", "Prueba", "usuario", null)
        { Rol = RolUsuario.Usuario });
        var gusto = new Gusto { Id = Guid.NewGuid(), Nombre = "Pizza" };
        db.Gustos.Add(gusto);
        await db.SaveChangesAsync();

        MultipartFormDataContent Formulario(string nombre)
        {
            var datos = new MultipartFormDataContent();
            foreach (var (campo, valor) in new Dictionary<string, string>
            {
                ["Nombre"] = nombre, ["Direccion"] = "Córdoba 123", ["Lat"] = "-34.6", ["Lng"] = "-58.4",
                ["WebsiteUrl"] = "https://example.test", ["DeclaroOtroLocal"] = "true",
                ["GustosQueSirveIds"] = gusto.Id.ToString()
            })
                datos.Add(new StringContent(valor), campo);
            foreach (var campo in new[] { "ImagenDestacada", "ImagenesInterior", "ImagenesComidas", "ImagenMenu", "Logo" })
                datos.Add(new ByteArrayContent([1, 2, 3]), campo, "foto.png");
            return datos;
        }

        using var formulario = Formulario("Local alternativo");
        var respuesta = await cliente.PostAsync("/api/Restaurantes", formulario);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var id = await respuesta.Content.ReadFromJsonAsync<Guid>();
        var guardada = await db.SolicitudesRestaurantes.SingleAsync();
        Assert.Equal(id, guardada.Id);
        Assert.True(guardada.DeclaroOtroLocal);
        Assert.Null(guardada.RestauranteExistenteId);

        using var otroFormulario = Formulario("Otro trámite distinto");
        var conflicto = await cliente.PostAsync("/api/Restaurantes", otroFormulario);
        Assert.Equal(HttpStatusCode.Conflict, conflicto.StatusCode);
        Assert.Contains("SOLICITUD_PENDIENTE", await conflicto.Content.ReadAsStringAsync());
        storage.Verify(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(5));
    }

    [Fact]
    public async Task Busqueda_ExigeSesionYProyectaSoloDatosPublicosCruzandoMeridiano()
    {
        // Cargar el proveedor SQL antes de iniciar el host, como ocurre en una suite de integración completa.
        GC.KeepAlive(typeof(Microsoft.Data.SqlClient.SqlConnection).Assembly);
        const string ruta = "/api/Restaurantes/coincidencias-alta?nombre=Otro&direccion=Sur%2050&latitud=0&longitud=179.9999";
        await using var anonima = new FabricaApiGustosApp();
        using var sinSesion = anonima.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await sinSesion.GetAsync(ruta)).StatusCode);

        await using var fabrica = new FabricaApiGustosAppVotacion();
        using var cliente = fabrica.CreateClient();
        using var scope = fabrica.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
        var local = new Restaurante { Nombre = "Ficha existente", Direccion = "Avenida Sur 50",
            Latitud = 0, Longitud = -179.9999, PropietarioUid = "identidad-privada-prueba" };
        db.AddRange(local, new Restaurante { Nombre = "Sucursal distante", Direccion = local.Direccion, Latitud = 1, Longitud = local.Longitud });
        await db.SaveChangesAsync();

        var respuesta = await cliente.GetAsync(ruta);
        var json = await respuesta.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.DoesNotContain("identidad-privada-prueba", json);
        var candidatos = JsonSerializer.Deserialize<RevisionCoincidenciasAlta>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Candidatos;
        var candidato = Assert.Single(candidatos);
        Assert.Equal(local.Id, candidato.Id);
        Assert.True(candidato.Administrado);
    }

    [Fact]
    public async Task Aprobacion_ReevaluaPropiedadYPermiteAltaDespuesDeRevisarCoincidenciasActuales()
    {
        const string uidAdmin = "usuario-pruebas-integracion";
        var firebase = new Mock<IFirebaseAuthService>();
        firebase.Setup(s => s.VerifyIdTokenAsync("token-prueba", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DecodedToken { Uid = uidAdmin });
        var email = new Mock<IEmailService>();
        await using var baseFabrica = new FabricaApiGustosAppVotacion();
        await using var fabrica = baseFabrica.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IFirebaseAuthService>();
            services.AddSingleton(firebase.Object);
            services.RemoveAll<IEmailService>();
            services.AddSingleton(email.Object);
            services.RemoveAll<IOcrService>();
            services.AddSingleton(Mock.Of<IOcrService>());
        }));
        using var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "token-prueba");
        using var scope = fabrica.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
        var admin = new Usuario(uidAdmin, "admin@example.test", "Admin", "Prueba", "admin", null) { Rol = RolUsuario.Usuario };
        var solicitante = new Usuario("solicitante", "solicitante@example.test", "Usuario", "Prueba", "solicitante", null);
        var existente = new Restaurante { Nombre = "Local original", Direccion = "Córdoba 100", Latitud = -34.6, Longitud = -58.4 };
        var alta = new SolicitudRestaurante
        {
            Id = Guid.NewGuid(), Usuario = solicitante, Nombre = "Otro local del mismo edificio",
            Direccion = existente.Direccion, Latitud = existente.Latitud, Longitud = existente.Longitud,
            WebsiteUrl = "", DeclaroOtroLocal = true,
        };
        db.AddRange(admin, solicitante, existente, alta);
        await db.SaveChangesAsync();

        var detalle = $"/Admin/{alta.Id}";
        var aprobar = $"/Admin/solicitudes/aprobar/{alta.Id}";
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync(detalle)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsync($"/Admin/solicitudes/{alta.Id}/completar-resolucion", null)).StatusCode);
        admin.Rol = RolUsuario.Admin;
        await db.SaveChangesAsync();
        var revisionInicial = await RevisionAsync(cliente, detalle);
        Assert.False(Assert.Single(revisionInicial.Candidatos).Administrado);

        existente.PropietarioUid = "propietario-asignado-despues";
        await db.SaveChangesAsync();
        var desactualizada = await cliente.PostAsync($"{aprobar}?revision={revisionInicial.Revision}", null);
        Assert.Equal(HttpStatusCode.Conflict, desactualizada.StatusCode);
        Assert.Contains("COINCIDENCIAS_ACTUALIZADAS", await desactualizada.Content.ReadAsStringAsync());
        Assert.Equal(1, await db.Restaurantes.CountAsync());
        email.VerifyNoOtherCalls();

        var revisionVigente = await RevisionAsync(cliente, detalle);
        Assert.True(Assert.Single(revisionVigente.Candidatos).Administrado);
        var aprobada = await cliente.PostAsync($"{aprobar}?revision={revisionVigente.Revision}", null);
        Assert.Equal(HttpStatusCode.OK, aprobada.StatusCode);
        db.ChangeTracker.Clear();
        var guardada = await db.SolicitudesRestaurantes.SingleAsync(s => s.Id == alta.Id);
        Assert.Equal(EstadoSolicitudRestaurante.Aprobada, guardada.Estado);
        Assert.NotEqual(existente.Id, guardada.RestauranteAprobadoId);
        Assert.Equal("propietario-asignado-despues", (await db.Restaurantes.FindAsync(existente.Id))!.PropietarioUid);
    }

    private static async Task<RevisionCoincidenciasAlta> RevisionAsync(HttpClient cliente, string ruta)
    {
        var detalle = await cliente.GetFromJsonAsync<JsonElement>(ruta);
        Assert.True(detalle.GetProperty("declaroOtroLocal").GetBoolean());
        return detalle.GetProperty("coincidenciasAlta").Deserialize<RevisionCoincidenciasAlta>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
}
