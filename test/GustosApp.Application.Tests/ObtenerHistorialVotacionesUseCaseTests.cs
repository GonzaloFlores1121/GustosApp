using FluentAssertions;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.UseCases.VotacionUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;

namespace GustosApp.Application.Tests
{
    public class ObtenerHistorialVotacionesUseCaseTests
    {
        private readonly Mock<IVotacionRepository> _votacionRepository = new();
        private readonly Mock<IUsuarioRepository> _usuarioRepository = new();
        private readonly Mock<IGrupoRepository> _grupoRepository = new();

        private ObtenerHistorialVotacionesUseCase CrearCasoDeUso() => new(
            _votacionRepository.Object,
            _usuarioRepository.Object,
            _grupoRepository.Object);

        [Fact]
        public async Task HandleAsync_AdministradorDevuelveHistorialPaginadoConGanador()
        {
            var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = "admin" };
            var grupo = new Grupo("Grupo", usuario.Id);
            var restaurante = new Restaurante
            {
                Id = Guid.NewGuid(),
                Nombre = "La Esquina",
                Direccion = "Calle 123",
                PlaceId = "place-1"
            };
            var votacion = new VotacionGrupo(grupo.Id, "Cena")
            {
                RestauranteGanador = restaurante
            };
            votacion.Participantes.Add(new VotacionParticipante(votacion.Id, usuario.Id));
            votacion.Votos.Add(new VotoRestaurante(votacion.Id, usuario.Id, restaurante.Id));
            votacion.CerrarVotacion(restaurante.Id);

            _usuarioRepository
                .Setup(r => r.GetByFirebaseUidAsync("admin", It.IsAny<CancellationToken>()))
                .ReturnsAsync(usuario);
            _grupoRepository
                .Setup(r => r.GetByIdAsync(grupo.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(grupo);
            _votacionRepository
                .Setup(r => r.ObtenerHistorialVotacionesAsync(
                    grupo.Id,
                    2,
                    5,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((new List<VotacionGrupo> { votacion }, 12));

            var resultado = await CrearCasoDeUso().HandleAsync("admin", grupo.Id, 2, 5);

            resultado.Pagina.Should().Be(2);
            resultado.Total.Should().Be(12);
            resultado.TotalPaginas.Should().Be(3);
            resultado.Votaciones.Should().ContainSingle();
            resultado.Votaciones[0].CantidadParticipantes.Should().Be(1);
            resultado.Votaciones[0].CantidadVotos.Should().Be(1);
            resultado.Votaciones[0].Ganador.Should().NotBeNull();
            resultado.Votaciones[0].Ganador!.Nombre.Should().Be("La Esquina");
        }

        [Fact]
        public async Task HandleAsync_MiembroActivoPuedeConsultarHistorial()
        {
            var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = "miembro" };
            var grupo = new Grupo("Grupo", Guid.NewGuid());
            grupo.Miembros.Add(new MiembroGrupo(grupo.Id, usuario.Id));

            _usuarioRepository
                .Setup(r => r.GetByFirebaseUidAsync("miembro", It.IsAny<CancellationToken>()))
                .ReturnsAsync(usuario);
            _grupoRepository
                .Setup(r => r.GetByIdAsync(grupo.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(grupo);
            _votacionRepository
                .Setup(r => r.ObtenerHistorialVotacionesAsync(
                    grupo.Id,
                    1,
                    10,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((new List<VotacionGrupo>(), 0));

            var resultado = await CrearCasoDeUso().HandleAsync("miembro", grupo.Id);

            resultado.Total.Should().Be(0);
            resultado.TotalPaginas.Should().Be(0);
        }

        [Fact]
        public async Task HandleAsync_UsuarioFueraDelGrupoLanzaAccesoProhibido()
        {
            var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = "externo" };
            var grupo = new Grupo("Grupo", Guid.NewGuid());

            _usuarioRepository
                .Setup(r => r.GetByFirebaseUidAsync("externo", It.IsAny<CancellationToken>()))
                .ReturnsAsync(usuario);
            _grupoRepository
                .Setup(r => r.GetByIdAsync(grupo.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(grupo);

            var accion = () => CrearCasoDeUso().HandleAsync("externo", grupo.Id);

            await accion.Should().ThrowAsync<AccesoProhibidoException>();
            _votacionRepository.Verify(
                r => r.ObtenerHistorialVotacionesAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Theory]
        [InlineData(0, 10)]
        [InlineData(1, 0)]
        [InlineData(1, 51)]
        public async Task HandleAsync_PaginacionInvalidaLanzaArgumentOutOfRangeException(
            int pagina,
            int tamanoPagina)
        {
            var accion = () => CrearCasoDeUso().HandleAsync(
                "usuario",
                Guid.NewGuid(),
                pagina,
                tamanoPagina);

            await accion.Should().ThrowAsync<ArgumentOutOfRangeException>();
        }
    }
}
