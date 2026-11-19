using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.RestauranteUseCases.Importacion;
using GustosApp.Domain.Common;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using GustosApp.Infraestructure;
using GustosApp.Infraestructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace GustosApp.API.Tests;

public sealed class SqlPendientesFactAttribute : FactAttribute
{
    public SqlPendientesFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("GUSTOS_EF_SQL_PENDIENTES") != "1")
            Skip = "Requiere SQL Server local para una base de prueba desechable.";
    }
}

public sealed class ColaRevisionPaginadaPruebas
{
    [Fact]
    public async Task Api_FiltraTodoElCatalogoYPaginaConTotalYMotivosCoherentes()
    {
        using var fabrica = CrearFabrica();
        await fabrica.PrepararAsync();
        await using (var scope = fabrica.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
            db.RemoveRange(await db.Restaurantes.ToListAsync());
            var admin = await db.Usuarios.SingleAsync(u => u.FirebaseUid == FabricaFiltrosMapa.Uid);
            admin.Rol = RolUsuario.Admin;
            await db.SaveChangesAsync();
            await SembrarAsync(db);
        }
        using var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "token-cola-prueba");
        var ids = new List<Guid>();
        for (var pagina = 1; pagina <= 7; pagina++)
        {
            var resultado = await cliente.GetFromJsonAsync<PaginaPendientesClasificacion>(
                $"/Admin/restaurantes/menu/pendientes?pagina={pagina}&tamanoPagina=20");
            Assert.NotNull(resultado);
            Assert.Equal(125, resultado.Total);
            Assert.Equal(7, resultado.TotalPaginas);
            Assert.Equal(pagina, resultado.Pagina);
            Assert.Equal(pagina == 7 ? 5 : 20, resultado.Elementos.Count);
            ids.AddRange(resultado.Elementos.Select(r => r.Id));
        }
        Assert.Equal(125, ids.Distinct().Count());
        foreach (var (filtro, motivo, total) in new[] {
            ("ErrorMenu", "Error al procesar menú", 7),
            ("SinMenuProcesado", "Sin menú procesado", 110),
            ("SinGustos", "Sin gustos", 3),
            ("ClasificacionAutomatica", "Clasificación automática", 5) })
        {
            var resultado = await cliente.GetFromJsonAsync<PaginaPendientesClasificacion>(
                $"/Admin/restaurantes/menu/pendientes?filtro={filtro}&tamanoPagina=100");
            Assert.NotNull(resultado);
            Assert.Equal(total, resultado.Total);
            Assert.All(resultado.Elementos, r => Assert.Contains(motivo, r.Motivos));
            if (filtro == "ErrorMenu")
                Assert.All(resultado.Elementos, r => Assert.DoesNotContain("Sin menú procesado", r.Motivos));
        }
        var repetida = await cliente.GetFromJsonAsync<PaginaPendientesClasificacion>(
            "/Admin/restaurantes/menu/pendientes?pagina=1&tamanoPagina=20");
        Assert.Equal(ids.Take(20), repetida!.Elementos.Select(r => r.Id));
        var fuera = await cliente.GetFromJsonAsync<PaginaPendientesClasificacion>(
            "/Admin/restaurantes/menu/pendientes?pagina=8&tamanoPagina=20");
        Assert.Empty(fuera!.Elementos);
        Assert.Equal(125, fuera.Total);
    }

    [Theory]
    [InlineData("pagina=0")]
    [InlineData("pagina=-1")]
    [InlineData("pagina=2147483647&tamanoPagina=100")]
    [InlineData("tamanoPagina=0")]
    [InlineData("tamanoPagina=101")]
    [InlineData("tamanoPagina=-1")]
    [InlineData("filtro=Inexistente")]
    [InlineData("filtro=999")]
    public async Task Api_RechazaLimitesYFiltrosInvalidos(string consulta)
    {
        using var fabrica = CrearFabrica();
        await fabrica.PrepararAsync();
        await using (var scope = fabrica.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
            (await db.Usuarios.SingleAsync(u => u.FirebaseUid == FabricaFiltrosMapa.Uid)).Rol = RolUsuario.Admin;
            await db.SaveChangesAsync();
        }
        using var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "token-cola-prueba");
        using var respuesta = await cliente.GetAsync("/Admin/restaurantes/menu/pendientes?" + consulta);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Api_UsuarioSinRolAdministrador_NoPuedeConsultarPaginasNiFiltros()
    {
        using var fabrica = CrearFabrica();
        await fabrica.PrepararAsync();
        using var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "token-cola-prueba");
        using var respuesta = await cliente.GetAsync("/Admin/restaurantes/menu/pendientes?pagina=2&filtro=SinGustos");
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [SqlPendientesFact]
    public async Task Sql_FiltrosGlobalesYPaginasConNombresDuplicados_SeEjecutanEnBaseAislada()
    {
        var nombre = $"GustosPendientesPruebas_{Guid.NewGuid():N}";
        var opciones = new DbContextOptionsBuilder<GustosDbContext>()
            .UseSqlServer($@"Server=lpc:localhost\SQLEXPRESS;Database={nombre};Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10;Pooling=False")
            .Options;
        await using var db = new GustosDbContext(opciones);
        if (await db.Database.CanConnectAsync()) throw new InvalidOperationException("La base de prueba ya existe.");
        var creada = false;
        try
        {
            creada = await db.Database.EnsureCreatedAsync();
            Assert.True(creada);
            await SembrarAsync(db);
            var repo = new RestauranteRepositoryEF(db);
            var ids = new List<Guid>();
            for (var pagina = 1; pagina <= 7; pagina++)
            {
                var resultado = await repo.ObtenerPendientesClasificacionAsync(new(pagina));
                Assert.Equal(125, resultado.Total);
                ids.AddRange(resultado.Elementos.Select(r => r.Id));
                var repetida = await repo.ObtenerPendientesClasificacionAsync(new(pagina));
                Assert.Equal(resultado.Elementos.Select(r => r.Id), repetida.Elementos.Select(r => r.Id));
            }
            Assert.Equal(125, ids.Distinct().Count());
            foreach (var (filtro, total) in new[] {
                (FiltroPendientesClasificacion.ErrorMenu, 7), (FiltroPendientesClasificacion.SinMenuProcesado, 110),
                (FiltroPendientesClasificacion.SinGustos, 3), (FiltroPendientesClasificacion.ClasificacionAutomatica, 5) })
            {
                var pagina = await repo.ObtenerPendientesClasificacionAsync(new(1, 100, filtro));
                Assert.Equal(total, pagina.Total);
                Assert.Equal(Math.Min(100, total), pagina.Elementos.Count);
            }
        }
        finally
        {
            if (creada) await db.Database.EnsureDeletedAsync();
        }
    }

    private static FabricaFiltrosMapa CrearFabrica()
    {
        var firebase = new Mock<IFirebaseAuthService>(MockBehavior.Strict);
        firebase.Setup(f => f.VerifyIdTokenAsync("token-cola-prueba", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DecodedToken { Uid = FabricaFiltrosMapa.Uid });
        return new FabricaFiltrosMapa(firebase: firebase.Object);
    }

    private static async Task SembrarAsync(GustosDbContext db)
    {
        var gusto = await db.Gustos.FirstAsync();
        var propietario = new Usuario("propietario-cola", "propietario@example.test", "Dueño", "Prueba", "propietario_cola");
        db.Add(propietario);
        Restaurante ficha(int indice) => new() {
            Id = Guid.NewGuid(), Nombre = "Duplicado", NombreNormalizado = "duplicado", PlaceId = $"cola-{indice}",
            Direccion = "Dirección de prueba", MenuProcesado = false, GustosQueSirve = [gusto]
        };
        for (var i = 0; i < 125; i++)
        {
            var restaurante = ficha(i);
            if (i < 7) restaurante.MenuError = "Menú ilegible";
            else if (i < 117) restaurante.MenuError = "   ";
            else if (i < 120) { restaurante.MenuProcesado = true; restaurante.GustosQueSirve = []; }
            else {
                restaurante.MenuProcesado = true;
                restaurante.RegistrarDatosCompatibilidadEstimados(OrigenDatosCompatibilidadRestaurante.GooglePlaces, DateTime.UtcNow);
            }
            db.Add(restaurante);
        }
        var revisado = ficha(125);
        revisado.MenuProcesado = true;
        revisado.MenuError = "";
        var conDueno = ficha(126);
        conDueno.Dueno = propietario;
        conDueno.DuenoId = propietario.Id;
        var conUid = ficha(127);
        conUid.PropietarioUid = "propietario-anterior";
        var uidConEspacios = ficha(128);
        uidConEspacios.PropietarioUid = " ";
        db.AddRange(revisado, conDueno, conUid, uidConEspacios);
        await db.SaveChangesAsync();
    }
}
