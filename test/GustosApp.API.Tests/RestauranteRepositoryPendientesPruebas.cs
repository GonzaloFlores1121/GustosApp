using FluentAssertions;
using GustosApp.API.Tests.Infraestructura;
using GustosApp.Domain.Common;
using GustosApp.Domain.Model;
using GustosApp.Infraestructure.Repositories;

namespace GustosApp.API.Tests;

public sealed class RestauranteRepositoryPendientesPruebas
{
    [Fact]
    public async Task ObtenerPendientes_DebeExcluirRevisadosYRestaurantesConPropietario()
    {
        await using var contexto = DbContextEnMemoria.Crear(nameof(ObtenerPendientes_DebeExcluirRevisadosYRestaurantesConPropietario));
        var pendiente = CrearRestaurante("Pendiente");
        var revisado = CrearRestaurante("Revisado");
        revisado.MenuProcesado = true;
        revisado.GustosQueSirve.Add(new Gusto { Id = Guid.NewGuid(), Nombre = "Pizza" });
        revisado.RegistrarDatosCompatibilidadEstimados(OrigenDatosCompatibilidadRestaurante.Administracion, DateTime.UtcNow);
        var conPropietario = CrearRestaurante("Con propietario");
        conPropietario.DuenoId = Guid.NewGuid();
        contexto.Restaurantes.AddRange(pendiente, revisado, conPropietario);
        await contexto.SaveChangesAsync();
        var repositorio = new RestauranteRepositoryEF(contexto);

        var resultado = await repositorio.ObtenerPendientesClasificacionAsync(100);

        resultado.Should().ContainSingle().Which.Id.Should().Be(pendiente.Id);
    }

    private static Restaurante CrearRestaurante(string nombre) => new()
    {
        Id = Guid.NewGuid(),
        Nombre = nombre,
        NombreNormalizado = nombre.ToLowerInvariant(),
        Direccion = "Calle 123",
        PlaceId = Guid.NewGuid().ToString("N"),
        PropietarioUid = string.Empty,
        CreadoUtc = DateTime.UtcNow,
        ActualizadoUtc = DateTime.UtcNow
    };
}
