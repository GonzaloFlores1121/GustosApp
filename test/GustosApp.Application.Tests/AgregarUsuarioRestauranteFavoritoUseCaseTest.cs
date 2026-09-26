using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.UseCases.UsuarioUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using Moq;

namespace GustosApp.Application.Tests;

public class AgregarUsuarioRestauranteFavoritoUseCaseTest
{
    private static (AgregarUsuarioRestauranteFavoritoUseCase UseCase,
        Mock<IUsuarioRestauranteFavoritoRepository> Favoritos,
        Mock<IRestauranteRepository> Restaurantes) Crear(Usuario usuario, Restaurante? restaurante = null)
    {
        var usuarios = new Mock<IUsuarioRepository>();
        var favoritos = new Mock<IUsuarioRestauranteFavoritoRepository>();
        var restaurantes = new Mock<IRestauranteRepository>();
        usuarios.Setup(r => r.GetByFirebaseUidAsync(usuario.FirebaseUid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        restaurantes.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurante ?? new Restaurante { Id = Guid.NewGuid(), Nombre = "Restaurante" });

        return (new AgregarUsuarioRestauranteFavoritoUseCase(
            favoritos.Object, usuarios.Object, restaurantes.Object), favoritos, restaurantes);
    }

    [Fact]
    public async Task Agregar_UsaIdentidadAutenticadaYOperacionAtomica()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = "abc", Plan = PlanUsuario.Free };
        var restauranteId = Guid.NewGuid();
        var (useCase, favoritos, _) = Crear(usuario);
        favoritos.Setup(r => r.AgregarSiCorrespondeAsync(
                usuario.Id, restauranteId, 3, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoAgregarFavorito.Agregado);

        await useCase.HandleAsync("abc", restauranteId);

        favoritos.VerifyAll();
    }

    [Fact]
    public async Task Agregar_RepetidoEsIdempotenteAunqueElPlanEsteEnElLimite()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = "abc", Plan = PlanUsuario.Free };
        var restauranteId = Guid.NewGuid();
        var (useCase, favoritos, _) = Crear(usuario);
        favoritos.Setup(r => r.AgregarSiCorrespondeAsync(
                usuario.Id, restauranteId, 3, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoAgregarFavorito.YaExistia);

        await useCase.HandleAsync("abc", restauranteId);

        favoritos.Verify(r => r.CountByUsuarioAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Agregar_CuandoPlanFreeLlegaAlLimite_LanzaExcepcionTipada()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = "abc", Plan = PlanUsuario.Free };
        var (useCase, favoritos, _) = Crear(usuario);
        favoritos.Setup(r => r.AgregarSiCorrespondeAsync(
                usuario.Id, It.IsAny<Guid>(), 3, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoAgregarFavorito.LimiteAlcanzado);
        favoritos.Setup(r => r.CountByUsuarioAsync(usuario.Id, It.IsAny<CancellationToken>())).ReturnsAsync(3);

        await Assert.ThrowsAsync<LimiteFavoritosAlcanzadoException>(() =>
            useCase.HandleAsync("abc", Guid.NewGuid()));
    }

    [Fact]
    public async Task Agregar_RestauranteInexistente_NoEscribe()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = "abc" };
        var (useCase, favoritos, restaurantes) = Crear(usuario);
        restaurantes.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Restaurante?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => useCase.HandleAsync("abc", Guid.NewGuid()));

        favoritos.Verify(r => r.AgregarSiCorrespondeAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Agregar_SinUsuarioAutenticado_RechazaLaOperacion()
    {
        var usuarios = new Mock<IUsuarioRepository>();
        var favoritos = new Mock<IUsuarioRestauranteFavoritoRepository>();
        var restaurantes = new Mock<IRestauranteRepository>();
        usuarios.Setup(r => r.GetByFirebaseUidAsync("uid-ajeno", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Usuario?)null);
        var useCase = new AgregarUsuarioRestauranteFavoritoUseCase(
            favoritos.Object, usuarios.Object, restaurantes.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            useCase.HandleAsync("uid-ajeno", Guid.NewGuid()));

        favoritos.VerifyNoOtherCalls();
        restaurantes.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EliminarFavorito_EliminaSoloLaRelacionDelUsuarioAutenticado()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = "uid" };
        var restauranteId = Guid.NewGuid();
        var (useCase, favoritos, _) = Crear(usuario);
        favoritos.Setup(r => r.ExistsAsync(usuario.Id, restauranteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await useCase.HandleAsyncDelete("uid", restauranteId);

        favoritos.Verify(r => r.EliminarAsync(usuario.Id, restauranteId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
