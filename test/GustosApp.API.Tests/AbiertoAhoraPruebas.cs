using System.Text.Json;
using GustosApp.Domain.Model;
using GustosApp.Infraestructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GustosApp.API.Tests;

public sealed class AbiertoAhoraPruebas
{
    [Theory]
    [InlineData("")]
    [InlineData("&amigoUsername=amigafiltros")]
    public async Task FiltraAntesDelTopEntreMasDeDoscientosCandidatosYConservaLosOtrosFiltros(string amigo)
    {
        var reloj = new Reloj();
        using var fabrica = new FabricaFiltrosMapa(reloj);
        await fabrica.PrepararAsync();
        Guid abiertoId;
        await using (var scope = fabrica.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
            db.RemoveRange(await db.Restaurantes.ToListAsync());
            var pizza = await db.Gustos.SingleAsync(g => g.Nombre == "Pizza");
            for (var i = 0; i < 225; i++)
                db.Add(Crear(i, pizza, """[{"dia":"Domingo","cerrado":true},{"dia":"Lunes","cerrado":true}]"""));
            db.Add(Crear(226, pizza, "{}"));
            db.Add(Crear(227, pizza, """[{"dia":"Lunes","desde":"25:00","hasta":"30:00"}]"""));
            var abierto = Crear(228, pizza, """[{"dia":"Domingo","cerrado":true},{"dia":"Lunes","desde":"11:00","hasta":"12:01"}]""");
            abiertoId = abierto.Id;
            db.Add(abierto);
            var usuario = await db.Usuarios.SingleAsync(u => u.FirebaseUid == FabricaFiltrosMapa.Uid);
            db.Add(new UsuarioRestauranteFavorito {
                UsuarioId = usuario.Id, RestauranteId = abierto.Id, FechaAgregado = reloj.Fecha.UtcDateTime
            });
            await db.SaveChangesAsync();
        }
        using var cliente = fabrica.CreateClient();
        var consulta = "/api/Restaurantes?near.lat=0&near.lng=0&radiusMeters=3000&gustos=Pizza&rating=4" + amigo;
        using var sinFiltro = JsonDocument.Parse(await cliente.GetStringAsync(consulta + "&top=200"));
        Assert.Equal(200, sinFiltro.RootElement.GetProperty("total").GetInt32());
        using var filtrado = JsonDocument.Parse(await cliente.GetStringAsync(consulta + "&top=1&abiertoAhora=true"));
        var elegido = Assert.Single(filtrado.RootElement.GetProperty("recomendaciones").EnumerateArray());
        Assert.Equal(abiertoId, elegido.GetProperty("id").GetGuid());
        Assert.NotEmpty(elegido.GetProperty("horariosJson").GetString()!);
        using var favoritos = JsonDocument.Parse(await cliente.GetStringAsync("/PerfilUsuario/favoritos"));
        Assert.Equal(elegido.GetProperty("horariosJson").GetString(),
            Assert.Single(favoritos.RootElement.EnumerateArray()).GetProperty("horariosJson").GetString());
        reloj.Fecha = reloj.Fecha.AddMinutes(1);
        using var cerrado = JsonDocument.Parse(await cliente.GetStringAsync(consulta + "&top=1&abiertoAhora=true"));
        Assert.Equal(0, cerrado.RootElement.GetProperty("total").GetInt32());
    }

    private static Restaurante Crear(int indice, Gusto gusto, string horarios) => new() {
        Id = Guid.NewGuid(), Nombre = $"Restaurante {indice:D3}", NombreNormalizado = $"restaurante {indice:D3}",
        PlaceId = $"horarios-{indice}", Direccion = "Dirección de prueba", Latitud = 0, Longitud = 0,
        Rating = 4.5, GustosQueSirve = [gusto], HorariosJson = horarios
    };

    private sealed class Reloj : TimeProvider
    {
        public DateTimeOffset Fecha { get; set; } = DateTimeOffset.Parse("2026-11-16T15:00:00Z");
        public override DateTimeOffset GetUtcNow() => Fecha;
    }
}
