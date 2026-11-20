using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using GustosApp.Infraestructure;
using GustosApp.Infraestructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace GustosApp.API.Tests;

public sealed class AdministracionDisponibilidadPruebas
{
    internal static FabricaFiltrosMapa CrearFabrica()
    {
        var firebase = new Mock<IFirebaseAuthService>();
        firebase.Setup(f => f.VerifyIdTokenAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DecodedToken { Uid = FabricaFiltrosMapa.Uid });
        return new FabricaFiltrosMapa(firebase: firebase.Object);
    }

    internal static async Task PrepararAdminAsync(FabricaFiltrosMapa fabrica)
    {
        await fabrica.PrepararAsync();
        await using var scope = fabrica.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
        (await db.Usuarios.SingleAsync(u => u.FirebaseUid == FabricaFiltrosMapa.Uid)).Rol = RolUsuario.Admin;
        await db.SaveChangesAsync();
    }

    internal static HttpClient Cliente(FabricaFiltrosMapa fabrica)
    {
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "token-disponibilidad-pruebas");
        return cliente;
    }

    [Theory]
    [InlineData(RolUsuario.Usuario)]
    [InlineData(RolUsuario.DuenoRestaurante)]
    public async Task NoAdministradorNoPuedeConsultarCambiarDisponibilidadNiArchivar(RolUsuario rol)
    {
        using var fabrica = CrearFabrica();
        await fabrica.PrepararAsync();
        await using (var scope = fabrica.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
            (await db.Usuarios.SingleAsync(u => u.FirebaseUid == FabricaFiltrosMapa.Uid)).Rol = rol;
            await db.SaveChangesAsync();
        }
        using var cliente = Cliente(fabrica);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/Admin/restaurantes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PutAsJsonAsync($"/Admin/restaurantes/{Guid.NewGuid()}/disponibilidad", new CambioDisponibilidadRestaurante(true, 0, "Motivo"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PutAsJsonAsync($"/Admin/solicitudes/{Guid.NewGuid()}/archivo", new CambioArchivoSolicitud(true, 0))).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await cliente.DeleteAsync($"/api/Restaurantes/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task DesactivarRepetirYReactivarConservaRelacionesRegistraActorYBloqueaVersionVieja()
    {
        using var fabrica = CrearFabrica();
        await PrepararAdminAsync(fabrica);
        Guid id, propietarioId, usuarioId;
        await using (var scope = fabrica.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
            var restaurante = await db.Restaurantes.Include(r => r.GustosQueSirve).SingleAsync(r => r.Nombre == "Pizza local");
            id = restaurante.Id;
            var propietario = await db.Usuarios.SingleAsync(u => u.FirebaseUid == "amiga-filtros");
            propietario.Rol = RolUsuario.DuenoRestaurante;
            propietarioId = propietario.Id;
            usuarioId = (await db.Usuarios.SingleAsync(u => u.FirebaseUid == FabricaFiltrosMapa.Uid)).Id;
            restaurante.DuenoId = propietario.Id;
            restaurante.PropietarioUid = propietario.FirebaseUid;
            restaurante.MenuProcesado = true;
            restaurante.Menu = new RestauranteMenu { RestauranteId = id, Json = "{\"publicado\":true}" };
            restaurante.Imagenes.Add(new RestauranteImagen { RestauranteId = id, Url = "/foto-conservada.jpg" });
            restaurante.Reviews.Add(new OpinionRestaurante { Id = Guid.NewGuid(), RestauranteId = id, UsuarioId = usuarioId, Valoracion = 5, Opinion = "Conservar" });
            db.Add(restaurante.Menu);
            db.AddRange(restaurante.Imagenes);
            db.AddRange(restaurante.Reviews);
            db.Add(new UsuarioRestauranteFavorito { UsuarioId = usuarioId, RestauranteId = id });
            db.Add(new UsuarioRestauranteVisitado { Id = Guid.NewGuid(), UsuarioId = usuarioId, RestauranteId = id, Nombre = restaurante.Nombre });
            await db.SaveChangesAsync();
        }
        using var cliente = Cliente(fabrica);
        var ruta = $"/Admin/restaurantes/{id}/disponibilidad";
        var cambio = new CambioDisponibilidadRestaurante(true, 0, "Verificación administrativa");
        (await cliente.PutAsJsonAsync(ruta, cambio)).EnsureSuccessStatusCode();
        (await cliente.PutAsJsonAsync(ruta, cambio)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Gone, (await cliente.DeleteAsync($"/api/Restaurantes/{id}")).StatusCode);
        using var detalle = JsonDocument.Parse(await cliente.GetStringAsync($"/api/Restaurantes/{id}"));
        Assert.True(detalle.RootElement.GetProperty("desactivado").GetBoolean());
        using var favoritos = JsonDocument.Parse(await cliente.GetStringAsync("/PerfilUsuario/favoritos"));
        Assert.True(Assert.Single(favoritos.RootElement.EnumerateArray()).GetProperty("desactivado").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await cliente.GetAsync($"/api/Recomendador/{id}/recomendacion")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await cliente.PutAsJsonAsync(ruta, new CambioDisponibilidadRestaurante(false, 0, "Antigua"))).StatusCode);
        (await cliente.PutAsJsonAsync(ruta, new CambioDisponibilidadRestaurante(false, 1, "Resuelto"))).EnsureSuccessStatusCode();
        await using var lectura = fabrica.Services.CreateAsyncScope();
        var final = lectura.ServiceProvider.GetRequiredService<GustosDbContext>();
        var ficha = await final.Restaurantes.Include(r => r.Menu).Include(r => r.Imagenes).Include(r => r.GustosQueSirve).Include(r => r.Reviews).SingleAsync(r => r.Id == id);
        Assert.False(ficha.Desactivado);
        Assert.Equal(propietarioId, ficha.DuenoId);
        Assert.Equal("amiga-filtros", ficha.PropietarioUid);
        Assert.Equal(RolUsuario.DuenoRestaurante, (await final.Usuarios.FindAsync(propietarioId))!.Rol);
        Assert.Single(ficha.Imagenes);
        Assert.Single(ficha.GustosQueSirve);
        Assert.Single(ficha.Reviews);
        Assert.True(ficha.MenuProcesado);
        Assert.Equal("{\"publicado\":true}", ficha.Menu!.Json);
        Assert.Equal(1, await final.UsuarioRestauranteFavoritos.CountAsync(f => f.RestauranteId == id));
        Assert.Equal(1, await final.UsuarioRestauranteVisitados.CountAsync(f => f.RestauranteId == id));
        var registros = await final.RegistrosAdministracionRestaurantes.OrderBy(r => r.FechaUtc).ToListAsync();
        Assert.Equal(new[] { "Desactivar", "Reactivar" }, registros.Select(r => r.Accion));
        Assert.All(registros, r => { Assert.Equal(FabricaFiltrosMapa.Uid, r.ActorUid); Assert.NotEqual(default, r.FechaUtc); });
    }

    [Theory]
    [InlineData(EstadoSolicitudRestaurante.Pendiente, HttpStatusCode.Conflict)]
    [InlineData(EstadoSolicitudRestaurante.Aprobada, HttpStatusCode.Conflict)]
    [InlineData(EstadoSolicitudRestaurante.Rechazada, HttpStatusCode.NoContent)]
    public async Task ArchivoSoloOrganizaRechazadasYPuedeRestaurarse(EstadoSolicitudRestaurante estado, HttpStatusCode esperado)
    {
        using var fabrica = CrearFabrica();
        await PrepararAdminAsync(fabrica);
        var id = Guid.NewGuid();
        await using (var scope = fabrica.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
            db.Add(new SolicitudRestaurante { Id = id, UsuarioId = (await db.Usuarios.FirstAsync()).Id,
                Nombre = "Solicitud", Direccion = "Calle", WebsiteUrl = "", Estado = estado, MotivoRechazo = "Comprobante insuficiente" });
            await db.SaveChangesAsync();
        }
        using var cliente = Cliente(fabrica);
        var ruta = $"/Admin/solicitudes/{id}/archivo";
        Assert.Equal(esperado, (await cliente.PutAsJsonAsync(ruta, new CambioArchivoSolicitud(true, 0))).StatusCode);
        if (estado != EstadoSolicitudRestaurante.Rechazada) return;
        (await cliente.PutAsJsonAsync(ruta, new CambioArchivoSolicitud(true, 0))).EnsureSuccessStatusCode();
        using var panel = JsonDocument.Parse(await cliente.GetStringAsync("/Admin/solicitudes?tipo=Todas"));
        Assert.Empty(panel.RootElement.EnumerateArray());
        using var archivo = JsonDocument.Parse(await cliente.GetStringAsync("/Admin/solicitudes?tipo=Todas&archivadas=true"));
        Assert.True(Assert.Single(archivo.RootElement.EnumerateArray()).GetProperty("archivada").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await cliente.PutAsJsonAsync(ruta, new CambioArchivoSolicitud(false, 0))).StatusCode);
        (await cliente.PutAsJsonAsync(ruta, new CambioArchivoSolicitud(false, 1))).EnsureSuccessStatusCode();
        await using var fin = fabrica.Services.CreateAsyncScope();
        var final = fin.ServiceProvider.GetRequiredService<GustosDbContext>();
        var solicitud = await final.SolicitudesRestaurantes.SingleAsync(s => s.Id == id);
        Assert.False(solicitud.Archivada);
        Assert.Equal(EstadoSolicitudRestaurante.Rechazada, solicitud.Estado);
        Assert.Equal("Comprobante insuficiente", solicitud.MotivoRechazo);
        Assert.Equal(2, await final.RegistrosAdministracionRestaurantes.CountAsync());
        Assert.Equal(7, await final.Restaurantes.CountAsync());
    }

    [Fact]
    public async Task FiltraInactivosAntesDeLimitesYBuscadorAdminPaginaTodosLosOrigenes()
    {
        using var fabrica = CrearFabrica();
        await PrepararAdminAsync(fabrica);
        await using (var scope = fabrica.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
            var pizza = await db.Gustos.SingleAsync(g => g.Nombre == "Pizza");
            foreach (var ficha in await db.Restaurantes.ToListAsync()) ficha.CambiarDisponibilidad(true, 0, "Revisión", DateTime.UtcNow);
            for (var i = 0; i < 225; i++)
            {
                var ficha = new Restaurante { Id = Guid.NewGuid(), Nombre = "AAAA " + i.ToString("D3"), NombreNormalizado = "aaaa", PlaceId = "baja-" + i, Direccion = "Prueba", Rating = 5, GustosQueSirve = [pizza] };
                ficha.CambiarDisponibilidad(true, 0, "Revisión", DateTime.UtcNow);
                db.Add(ficha);
            }
            db.Add(new Restaurante { Id = Guid.NewGuid(), Nombre = "ZZZZ activo", NombreNormalizado = "zzzz activo", PlaceId = "activo", Direccion = "Prueba", Rating = 4.5, GustosQueSirve = [pizza] });
            await db.SaveChangesAsync();
            var repo = new RestauranteRepositoryEF(db);
            Assert.Single(await repo.GetNearbyAsync(0, 0, 3000));
            Assert.Single(await repo.BuscarPorPrefijo("ZZZZ"));
            Assert.Empty(await repo.BuscarPorPrefijo("AAAA"));
        }
        using var cliente = Cliente(fabrica);
        foreach (var amigo in new[] { "", "&amigoUsername=amigafiltros" })
        {
            using var json = JsonDocument.Parse(await cliente.GetStringAsync("/api/Restaurantes?gustos=Pizza&top=1&near.lat=0&near.lng=0&rating=4" + amigo));
            Assert.Equal("ZZZZ activo", Assert.Single(json.RootElement.GetProperty("recomendaciones").EnumerateArray()).GetProperty("nombre").GetString());
        }
        var primera = await cliente.GetFromJsonAsync<PaginaAdministracionRestaurantes>("/Admin/restaurantes?tamanoPagina=100");
        var segunda = await cliente.GetFromJsonAsync<PaginaAdministracionRestaurantes>("/Admin/restaurantes?tamanoPagina=100&pagina=2");
        Assert.Equal(233, primera!.Total);
        Assert.Equal(100, primera.Elementos.Count);
        Assert.DoesNotContain(segunda!.Elementos, r => primera.Elementos.Any(p => p.Id == r.Id));
        var soloActivos = await cliente.GetFromJsonAsync<PaginaAdministracionRestaurantes>("/Admin/restaurantes?desactivado=false");
        Assert.Equal("ZZZZ activo", Assert.Single(soloActivos!.Elementos).Nombre);
        var inactivos = await cliente.GetFromJsonAsync<PaginaAdministracionRestaurantes>("/Admin/restaurantes?desactivado=true&texto=AAAA");
        Assert.Equal(225, inactivos!.Total);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.GetAsync("/Admin/restaurantes?pagina=2147483647")).StatusCode);
    }
}
