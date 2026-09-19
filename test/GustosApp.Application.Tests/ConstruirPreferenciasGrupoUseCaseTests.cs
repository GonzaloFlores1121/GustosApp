using FluentAssertions;
using GustosApp.Application.Tests.Builders;
using GustosApp.Application.UseCases.GrupoUseCases;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;
using Moq;

namespace GustosApp.Application.Tests
{
    public class ConstruirPreferenciasGrupoUseCaseTests
    {
        private readonly Mock<IGustosGrupoRepository> _gustosGrupoRepository = new();
        private readonly Mock<IMiembroGrupoRepository> _miembroGrupoRepository = new();
        private readonly Mock<IUsuarioRepository> _usuarioRepository = new();

        [Fact]
        public async Task HandleAsync_MiembroActivo_CombinaPreferenciasDeParticipantes()
        {
            var grupoId = Guid.NewGuid();
            var usuario = new UsuarioBuilder().ConFirebaseUid("usuario-1").Build();
            var useCase = CrearUseCase();

            _usuarioRepository
                .Setup(repository => repository.GetByFirebaseUidAsync(
                    usuario.FirebaseUid,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(usuario);
            _miembroGrupoRepository
                .Setup(repository => repository.UsuarioEsMiembroActivoAsync(
                    grupoId,
                    usuario.Id,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _gustosGrupoRepository
                .Setup(repository => repository.ObtenerGustosDelGrupo(grupoId))
                .ReturnsAsync(new List<string> { "Pizza", "pizza", "Sushi" });
            _miembroGrupoRepository
                .Setup(repository => repository.obtenerMiembrosActivosConSusPreferenciasYCondiciones(grupoId))
                .ReturnsAsync(new UsuarioPreferencias
                {
                    Restricciones = new List<string> { "Sin gluten", "sin gluten" },
                    CondicionesMedicas = new List<string> { "Hipertensión" }
                });

            var resultado = await useCase.HandleAsync(usuario.FirebaseUid, grupoId, CancellationToken.None);

            resultado.Gustos.Should().BeEquivalentTo("Pizza", "Sushi");
            resultado.Restricciones.Should().ContainSingle().Which.Should().Be("Sin gluten");
            resultado.CondicionesMedicas.Should().ContainSingle().Which.Should().Be("Hipertensión");
        }

        [Fact]
        public async Task HandleAsync_UsuarioNoEsMiembroActivo_LanzaUnauthorizedAccessException()
        {
            var grupoId = Guid.NewGuid();
            var usuario = new UsuarioBuilder().ConFirebaseUid("usuario-1").Build();
            var useCase = CrearUseCase();

            _usuarioRepository
                .Setup(repository => repository.GetByFirebaseUidAsync(
                    usuario.FirebaseUid,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(usuario);
            _miembroGrupoRepository
                .Setup(repository => repository.UsuarioEsMiembroActivoAsync(
                    grupoId,
                    usuario.Id,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var accion = () => useCase.HandleAsync(
                usuario.FirebaseUid,
                grupoId,
                CancellationToken.None);

            await accion.Should().ThrowAsync<UnauthorizedAccessException>()
                .WithMessage("El usuario no es miembro activo del grupo.");
        }

        private ConstruirPreferenciasGrupoUseCase CrearUseCase()
        {
            return new ConstruirPreferenciasGrupoUseCase(
                _gustosGrupoRepository.Object,
                _miembroGrupoRepository.Object,
                _usuarioRepository.Object);
        }
    }
}
