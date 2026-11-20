using System.Net;
using System.Text.Json;
using GustosApp.Domain.Common;
using GustosApp.Domain.Model;
using GustosApp.Infraestructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GustosApp.API.Tests;

public sealed class CaducidadRecomendacionesPruebas
{
    [Fact]
    public async Task FechasYAvanceDelReloj_NoCambianRecomendacionesIndividualesConAmigoOGrupo()
    {
        var reloj = new RelojPrueba();
        using var fabrica = new FabricaFiltrosMapa(reloj);
        await fabrica.PrepararAsync();
        Guid grupoId;
        await using (var scope = fabrica.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
            var usuario = await db.Usuarios.SingleAsync(u => u.FirebaseUid == FabricaFiltrosMapa.Uid);
            var amiga = await db.Usuarios.SingleAsync(u => u.FirebaseUid == "amiga-filtros");
            var declaracion = new Restriccion { Id = Guid.NewGuid(), Nombre = "Declaración de prueba" };
            db.Add(declaracion);
            usuario.Restricciones.Add(declaracion);
            amiga.Restricciones.Add(declaracion);
            var declarado = await db.Restaurantes.SingleAsync(r => r.Nombre == "Pizza local");
            declarado.RestriccionesQueRespeta.Add(declaracion);
            var grupo = new Grupo("Grupo invariancia", usuario.Id);
            grupoId = grupo.Id;
            var miembro = new MiembroGrupo(grupo.Id, usuario.Id, true);
            grupo.Miembros.Add(miembro);
            foreach (var gusto in await db.Gustos.Where(g => g.Nombre == "Pizza" || g.Nombre == "Pasta").ToListAsync())
                grupo.Gustos.Add(new GrupoGusto {
                    Id = Guid.NewGuid(), GrupoId = grupo.Id, MiembroId = miembro.Id, GustoId = gusto.Id
                });
            db.Add(grupo);
            await db.SaveChangesAsync();
        }
        using var cliente = fabrica.CreateClient();
        string[] rutas = [
            "/api/Restaurantes?near.lat=0&near.lng=0&radiusMeters=3000&top=200",
            "/api/Restaurantes?near.lat=0&near.lng=0&radiusMeters=3000&top=200&amigoUsername=amigafiltros",
            $"/Grupo/restaurantes/{grupoId}?near.lat=0&near.lng=0&radiusMeters=3000&top=200"
        ];
        var originales = new Dictionary<string, string>();
        // El contenido real permanece constante. Solo varían la fecha histórica y el reloj.
        foreach (int? meses in new int?[] { null, 0, -6, -120 })
        {
            await using (var scope = fabrica.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
                DateTime? fecha = meses.HasValue ? reloj.Fecha.UtcDateTime.AddMonths(meses.Value) : null;
                foreach (var restaurante in await db.Restaurantes.ToListAsync())
                {
                    var entrada = db.Entry(restaurante);
                    foreach (var campo in new[] {
                        nameof(Restaurante.FechaRevisionOfertaUtc), nameof(Restaurante.FechaRevisionRestriccionesUtc),
                        nameof(Restaurante.FechaObtencionDatosCompatibilidadUtc),
                        nameof(Restaurante.FechaUltimaVerificacionDatosCompatibilidadUtc) })
                        entrada.Property(campo).CurrentValue = fecha;
                }
                await db.SaveChangesAsync();
            }
            foreach (var ruta in rutas)
            {
                using var respuesta = await cliente.GetAsync(ruta);
                Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
                using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
                var resultados = json.RootElement.GetProperty("recomendaciones");
                Assert.NotEmpty(resultados.EnumerateArray());
                var datos = resultados.EnumerateArray().Select(r => new {
                    Id = r.GetProperty("id").GetGuid(),
                    Score = r.GetProperty("score").GetDouble(),
                    Nivel = r.GetProperty("nivelCompatibilidad").GetString(),
                    Advertencia = r.GetProperty("advertenciaCompatibilidad").GetString()
                }).ToArray();
                Assert.Contains(datos, r => r.Nivel == "Estimada");
                Assert.Contains(datos, r => r.Nivel == "Desconocida");
                var huella = JsonSerializer.Serialize(datos);
                if (originales.TryGetValue(ruta, out var original)) Assert.Equal(original, huella);
                else originales[ruta] = huella;
                Assert.DoesNotContain("fechaRevision", resultados.GetRawText());
                Assert.DoesNotContain("fechaObtencionDatosCompatibilidad", resultados.GetRawText());
                Assert.DoesNotContain("fechaUltimaVerificacionDatosCompatibilidad", resultados.GetRawText());
            }
            reloj.Fecha = reloj.Fecha.AddYears(10);
        }
    }

    [Theory]
    [InlineData("api/Restaurantes", "confirmar-oferta")]
    [InlineData("api/Restaurantes", "confirmar-restricciones")]
    [InlineData("api/admin/restaurantes/menu", "confirmar-oferta")]
    [InlineData("api/admin/restaurantes/menu", "confirmar-restricciones")]
    public async Task ReconfirmacionPeriodica_NoEstaDisponible(string ruta, string accion)
    {
        using var fabrica = new FabricaFiltrosMapa();
        await fabrica.PrepararAsync();
        using var cliente = fabrica.CreateClient();
        using var respuesta = await cliente.PostAsync($"/{ruta}/{Guid.NewGuid()}/{accion}", new StringContent("{}"));
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    private sealed class RelojPrueba : TimeProvider
    {
        public DateTimeOffset Fecha { get; set; } = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Fecha;
    }
}
