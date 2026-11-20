using FluentAssertions;
using GustosApp.API.Tests.Infraestructura;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Infraestructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GustosApp.API.Tests;

public sealed class FavoritosYVisitadosPersistenciaPruebas
{
    [Fact]
    public async Task MarcarVisitado_DosVeces_ConservaUnaSolaRelacion()
    {
        await using var contexto = DbContextEnMemoria.Crear(nameof(MarcarVisitado_DosVeces_ConservaUnaSolaRelacion));
        var repositorio = new UsuarioRepositoryEF(contexto);
        var usuarioId = Guid.NewGuid();
        var restauranteId = Guid.NewGuid();

        await repositorio.MarcarRestauranteComoVisitadoAsync(usuarioId, restauranteId, default);
        await repositorio.MarcarRestauranteComoVisitadoAsync(usuarioId, restauranteId, default);

        (await contexto.UsuarioRestauranteVisitados.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AgregarFavorito_RepetidoEsIdempotenteYUnoNuevoRespetaElLimite()
    {
        await using var contexto = DbContextEnMemoria.Crear(nameof(AgregarFavorito_RepetidoEsIdempotenteYUnoNuevoRespetaElLimite));
        var repositorio = new UsuarioRestauranteFavoritoEF(contexto);
        var usuarioId = Guid.NewGuid();
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        contexto.UsuarioRestauranteFavoritos.AddRange(ids.Select(id => new UsuarioRestauranteFavorito
        {
            UsuarioId = usuarioId,
            RestauranteId = id,
            FechaAgregado = DateTime.UtcNow
        }));
        await contexto.SaveChangesAsync();

        var repetido = await repositorio.AgregarSiCorrespondeAsync(usuarioId, ids[0], 3, DateTime.UtcNow, default);
        var excedido = await repositorio.AgregarSiCorrespondeAsync(usuarioId, Guid.NewGuid(), 3, DateTime.UtcNow, default);

        repetido.Should().Be(ResultadoAgregarFavorito.YaExistia);
        excedido.Should().Be(ResultadoAgregarFavorito.LimiteAlcanzado);
        (await contexto.UsuarioRestauranteFavoritos.CountAsync()).Should().Be(3);
    }

    [Fact]
    public void ModeloVisitados_DeclaraUnicidadPorUsuarioYOrigen()
    {
        using var contexto = DbContextEnMemoria.Crear(nameof(ModeloVisitados_DeclaraUnicidadPorUsuarioYOrigen));
        var indexes = contexto.Model.FindEntityType(typeof(UsuarioRestauranteVisitado))!.GetIndexes();

        indexes.Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "UsuarioId", "RestauranteId" }));
        indexes.Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "UsuarioId", "PlaceId" }));
    }
}
