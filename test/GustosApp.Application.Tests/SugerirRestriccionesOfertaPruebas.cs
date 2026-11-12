using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Services;
using GustosApp.Application.UseCases.RestauranteUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;

namespace GustosApp.Application.Tests;

public class SugerirRestriccionesOfertaPruebas
{
    private readonly SugeridorRestriccionesOferta _sugeridor = new();

    [Fact]
    public void GustoConMarcaExplicita_SugiereDeclaracionJustificada()
    {
        var restriccion = Restriccion("Sin gluten", "Gluten");
        var gusto = Gusto("Pizza sin gluten");

        var sugerencia = Assert.Single(_sugeridor.Sugerir([gusto], null, [restriccion], []));

        Assert.Equal(restriccion.Id, sugerencia.Id);
        Assert.Equal("Gusto seleccionado", sugerencia.TipoFuente);
        Assert.Equal(gusto.Nombre, sugerencia.Fuente);
        Assert.Equal(gusto.Id, sugerencia.FuenteGustoId);
        Assert.Contains("Revisá ingredientes", sugerencia.Motivo);
    }

    [Theory]
    [InlineData("Pizza")]
    [InlineData("Ensalada verde")]
    [InlineData("Pizza sin gluten opcional")]
    [InlineData("No tenemos pizza sin gluten")]
    [InlineData("Pizza estilo sin gluten")]
    [InlineData("Pizza sin gluten y con trigo")]
    public void NombreGenericoONegado_NoSugiere(string nombre)
    {
        Assert.Empty(_sugeridor.Sugerir([Gusto(nombre)], null,
            [Restriccion("Sin gluten", "Gluten")], []));
    }

    [Fact]
    public void TagsDeConflicto_NoSeConviertenEnPruebaDeAusencia()
    {
        var gusto = Gusto("Pizza sin gluten");
        gusto.Tags.Add(new Tag { Nombre = "Gluten" });

        Assert.Empty(_sugeridor.Sugerir([gusto], null,
            [Restriccion("Sin gluten", "Gluten")], []));
    }

    [Fact]
    public void MenuPublicado_UsaSoloNombreExplicitoYEvitaDuplicadosYGuardadas()
    {
        var restriccion = Restriccion("Sin lactosa", "Lácteo");
        const string json = """{"categorias":[{"items":[{"nombre":"Helado sin lactosa","descripcion":"Sin gluten"},{"nombre":"Postre libre de lactosa"}]}]}""";

        var sugerencia = Assert.Single(_sugeridor.Sugerir([], json, [restriccion], []));
        Assert.Equal("Menú publicado", sugerencia.TipoFuente);
        Assert.Null(sugerencia.FuenteGustoId);
        Assert.Equal("Helado sin lactosa", sugerencia.Fuente);
        Assert.Empty(_sugeridor.Sugerir([], json, [restriccion], [restriccion.Id]));
    }

    [Fact]
    public void CatalogoInsuficienteYJsonInvalido_NoInventanSugerencias()
    {
        Assert.Empty(_sugeridor.Sugerir([Gusto("Pizza sin gluten")], "{", [], []));
        Assert.Empty(_sugeridor.Sugerir([], "{", [Restriccion("Sin gluten", "Gluten")], []));
    }

    [Fact]
    public async Task CasoDeUso_NoUsaBorradorNiModificaVigenciaOPersistencia()
    {
        var propietario = Guid.NewGuid();
        var gusto = Gusto("Pizza");
        var restriccion = Restriccion("Sin gluten", "Gluten");
        var restaurante = new Restaurante { Id = Guid.NewGuid(), DuenoId = propietario,
            GustosQueSirve = [gusto], MenuProcesado = true };
        var menu = new RestauranteMenu { RestauranteId = restaurante.Id,
            Json = """{"categorias":[{"items":[{"nombre":"Pizza sin gluten"}]}]}""" };
        menu.IniciarBorrador(Guid.NewGuid(), "imagen", DateTime.UtcNow);
        menu.CompletarBorrador(menu.BorradorToken!.Value,
            """{"categorias":[{"items":[{"nombre":"Helado sin lactosa"}]}]}""", DateTime.UtcNow);
        var (caso, restaurantes, _, _, menus) = CrearCaso(restaurante, [gusto], [restriccion], menu);

        var resultado = await caso.HandleAsync(restaurante.Id, propietario, [gusto.Id]);

        Assert.Single(resultado.Sugerencias);
        Assert.Equal("Menú publicado", resultado.Sugerencias[0].TipoFuente);
        Assert.Null(restaurante.FechaRevisionRestriccionesUtc);
        Assert.Empty(restaurante.RestriccionesQueRespeta);
        restaurantes.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        menus.Verify(m => m.GetByRestauranteIdAsync(restaurante.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CasoDeUso_RechazaPropietarioAjenoEIdsInexistentes()
    {
        var propietario = Guid.NewGuid();
        var restaurante = new Restaurante { Id = Guid.NewGuid(), DuenoId = propietario };
        var (caso, _, _, _, _) = CrearCaso(restaurante, [], [], null);

        await Assert.ThrowsAsync<AccesoProhibidoException>(() =>
            caso.HandleAsync(restaurante.Id, Guid.NewGuid(), []));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            caso.HandleAsync(restaurante.Id, propietario, [Guid.NewGuid()]));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            caso.HandleAsync(restaurante.Id, propietario, [Guid.Empty]));
    }

    [Fact]
    public async Task CasoDeUso_IdDuplicadoNoDuplicaLaPista()
    {
        var propietario = Guid.NewGuid();
        var gusto = Gusto("Pizza sin gluten");
        var restaurante = new Restaurante { Id = Guid.NewGuid(), DuenoId = propietario };
        var (caso, _, _, _, _) = CrearCaso(restaurante, [gusto],
            [Restriccion("Sin gluten", "Gluten")], null);

        var resultado = await caso.HandleAsync(restaurante.Id, propietario,
            [gusto.Id, gusto.Id]);

        Assert.Single(resultado.Sugerencias);
    }

    private static Gusto Gusto(string nombre) => new() { Id = Guid.NewGuid(), Nombre = nombre };

    private static Restriccion Restriccion(string nombre, string tag)
        => new() { Id = Guid.NewGuid(), Nombre = nombre, TagsProhibidos = [new Tag { Nombre = tag }] };

    private static (SugerirRestriccionesOfertaUseCase Caso,
        Mock<IRestauranteRepository> Restaurantes, Mock<IGustoRepository> Gustos,
        Mock<IRestriccionRepository> Restricciones, Mock<IRestauranteMenuRepository> Menus) CrearCaso(
        Restaurante restaurante, List<Gusto> gustos, List<Restriccion> restricciones, RestauranteMenu? menu)
    {
        var restaurantes = new Mock<IRestauranteRepository>();
        var catalogoGustos = new Mock<IGustoRepository>();
        var catalogoRestricciones = new Mock<IRestriccionRepository>();
        var menus = new Mock<IRestauranteMenuRepository>();
        restaurantes.Setup(r => r.GetByIdAsync(restaurante.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurante);
        catalogoGustos.Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<Guid> ids, CancellationToken _) => gustos.Where(gusto => ids.Contains(gusto.Id)).ToList());
        catalogoRestricciones.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(restricciones);
        menus.Setup(m => m.GetByRestauranteIdAsync(restaurante.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(menu);
        return (new SugerirRestriccionesOfertaUseCase(restaurantes.Object, catalogoGustos.Object,
            catalogoRestricciones.Object, menus.Object, new SugeridorRestriccionesOferta()),
            restaurantes, catalogoGustos, catalogoRestricciones, menus);
    }
}
