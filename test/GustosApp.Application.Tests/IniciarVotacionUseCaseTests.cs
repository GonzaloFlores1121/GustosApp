using System;
using System.Threading;
using System.Threading.Tasks;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Interfaces;
using GustosApp.Application.Services;
using GustosApp.Application.UseCases.VotacionUseCases;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;
using Xunit;

namespace GustosApp.Application.Tests
{
    public class IniciarVotacionUseCaseTests
    {
        private readonly Mock<IVotacionRepository> _mockVotacionRepository;
        private readonly Mock<IGrupoRepository> _mockGrupoRepository;
        private readonly Mock<IUsuarioRepository> _mockUsuarioRepository;
        private readonly Mock<INotificacionesVotacionService> _mockNotificaciones;
        private readonly Mock<IRestauranteRepository> _mockRestauranteRepository;
        private readonly Mock<IConstruirPreferenciasGrupoUseCase> _mockPreferenciasGrupo;
        private readonly IniciarVotacionUseCase _useCase;

        public IniciarVotacionUseCaseTests()
        {
            _mockVotacionRepository = new Mock<IVotacionRepository>();
            _mockGrupoRepository = new Mock<IGrupoRepository>();
            _mockUsuarioRepository = new Mock<IUsuarioRepository>();
            _mockNotificaciones = new Mock<INotificacionesVotacionService>();
            _mockRestauranteRepository = new Mock<IRestauranteRepository>();
            _mockPreferenciasGrupo = new Mock<IConstruirPreferenciasGrupoUseCase>();
            _mockRestauranteRepository
                .Setup(repository => repository.ObtenerPorIdsAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                    ids.Select(id => new Restaurante
                    {
                        Id = id,
                        GustosQueSirve = new List<Gusto> { new() { Nombre = "Pizza" } },
                        RestriccionesQueRespeta = new List<Restriccion>()
                    }).ToList());
            _mockPreferenciasGrupo
                .Setup(preferencias => preferencias.HandleAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UsuarioPreferencias { Gustos = new List<string> { "Pizza" } });
            _useCase = new IniciarVotacionUseCase(
                _mockVotacionRepository.Object,
                _mockGrupoRepository.Object,
                _mockUsuarioRepository.Object,
                _mockNotificaciones.Object,
                _mockRestauranteRepository.Object,
                _mockPreferenciasGrupo.Object,
                new EvaluadorCompatibilidadRestaurante()
                );
        }

      
        [Fact]
        public async Task HandleAsync_UsuarioNoEncontrado_LanzaUnauthorizedAccessException()
        {
            var firebaseUid = "firebase123";
            var grupoId = Guid.NewGuid();

            _mockUsuarioRepository
                .Setup(x => x.GetByFirebaseUidAsync(firebaseUid, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Usuario?)null);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _useCase.HandleAsync(firebaseUid, grupoId, "desc", new List<Guid> { Guid.NewGuid() }));
        }

        
        [Fact]
        public async Task HandleAsync_MiembroQueNoEsAdministrador_LanzaAccesoProhibido()
        {
            var firebaseUid = "firebase123";
            var grupoId = Guid.NewGuid();
            var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = firebaseUid };
            var grupo = new Grupo("Grupo", Guid.NewGuid()) { Id = grupoId };
            grupo.Miembros.Add(new MiembroGrupo(grupoId, usuario.Id) { ParticipaEnRecomendacion = true });

            _mockUsuarioRepository
                .Setup(x => x.GetByFirebaseUidAsync(firebaseUid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(usuario);

            _mockGrupoRepository
                .Setup(x => x.GetByIdAsync(grupoId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(grupo);

            await Assert.ThrowsAsync<AccesoProhibidoException>(() =>
                _useCase.HandleAsync(firebaseUid, grupoId, "desc", new List<Guid> { Guid.NewGuid() }));

            _mockVotacionRepository.Verify(
                x => x.CrearVotacionAsync(It.IsAny<VotacionGrupo>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleAsync_GrupoNoExiste_LanzaNotFoundException()
        {
            var firebaseUid = "firebase123";
            var grupoId = Guid.NewGuid();
            var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = firebaseUid };

            _mockUsuarioRepository
                .Setup(x => x.GetByFirebaseUidAsync(firebaseUid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(usuario);

            _mockGrupoRepository
                .Setup(x => x.GetByIdAsync(grupoId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Grupo?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _useCase.HandleAsync(firebaseUid, grupoId, "desc", new List<Guid> { Guid.NewGuid() }));
        }

       
        [Fact]
        public async Task HandleAsync_VotacionActivaExistente_LanzaInvalidOperationException()
        {
            var firebaseUid = "firebase123";
            var grupoId = Guid.NewGuid();
            var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = firebaseUid };
            var grupo = CrearGrupoConParticipante(grupoId, usuario);

            var votacionActiva = new VotacionGrupo(grupoId);

            _mockUsuarioRepository
                .Setup(x => x.GetByFirebaseUidAsync(firebaseUid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(usuario);

            _mockGrupoRepository
                .Setup(x => x.GetByIdAsync(grupoId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(grupo);

            _mockVotacionRepository
                .Setup(x => x.ObtenerVotacionActivaAsync(grupoId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(votacionActiva);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _useCase.HandleAsync(firebaseUid, grupoId, null, new List<Guid> { Guid.NewGuid() }));

            Assert.Equal("Ya existe una votación activa en este grupo", ex.Message);
        }

      
        [Fact]
        public async Task HandleAsync_SinRestaurantesCandidatos_LanzaInvalidOperationException()
        {
            var firebaseUid = "firebase123";
            var grupoId = Guid.NewGuid();
            var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = firebaseUid };
            var grupo = CrearGrupoConParticipante(grupoId, usuario);

            _mockUsuarioRepository
                .Setup(x => x.GetByFirebaseUidAsync(firebaseUid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(usuario);

            _mockGrupoRepository
                .Setup(x => x.GetByIdAsync(grupoId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(grupo);

            _mockVotacionRepository
                .Setup(x => x.ObtenerVotacionActivaAsync(grupoId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((VotacionGrupo?)null);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _useCase.HandleAsync(firebaseUid, grupoId, "desc", new List<Guid>()));

            Assert.Equal("Debe seleccionar al menos dos restaurantes candidatos.", ex.Message);
        }

       
        [Fact]
        public async Task HandleAsync_Valido_CreaVotacionConDescripcion()
        {
            var firebaseUid = "firebase123";
            var grupoId = Guid.NewGuid();
            var descripcion = "Votación viernes";

            var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = firebaseUid };
            var candidatos = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            var grupo = CrearGrupoConParticipante(grupoId, usuario);

            _mockUsuarioRepository
                .Setup(x => x.GetByFirebaseUidAsync(firebaseUid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(usuario);

            _mockGrupoRepository
                .Setup(x => x.GetByIdAsync(grupoId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(grupo);

            _mockVotacionRepository
                .Setup(x => x.ObtenerVotacionActivaAsync(grupoId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((VotacionGrupo?)null);

            _mockVotacionRepository
                .Setup(x => x.CrearVotacionAsync(It.IsAny<VotacionGrupo>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((VotacionGrupo v, CancellationToken _) => v);

            var result = await _useCase.HandleAsync(firebaseUid, grupoId, descripcion, candidatos);

            Assert.NotNull(result);
            Assert.Equal(grupoId, result.GrupoId);
            Assert.Equal(descripcion, result.Descripcion);
            Assert.Equal(EstadoVotacion.Activa, result.Estado);
            Assert.Equal(candidatos.Count, result.RestaurantesCandidatos.Count);
            Assert.Equal(2, result.Participantes.Count);
        }

       
        [Fact]
        public async Task HandleAsync_SinDescripcion_CreaVotacion()
        {
            var firebaseUid = "firebase123";
            var grupoId = Guid.NewGuid();
            var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = firebaseUid };
            var candidatos = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            var grupo = CrearGrupoConParticipante(grupoId, usuario);

            _mockUsuarioRepository
                .Setup(x => x.GetByFirebaseUidAsync(firebaseUid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(usuario);

            _mockGrupoRepository
                .Setup(x => x.GetByIdAsync(grupoId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(grupo);

            _mockVotacionRepository
                .Setup(x => x.ObtenerVotacionActivaAsync(grupoId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((VotacionGrupo?)null);

            _mockVotacionRepository
                .Setup(x => x.CrearVotacionAsync(It.IsAny<VotacionGrupo>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((VotacionGrupo v, CancellationToken _) => v);

            var result = await _useCase.HandleAsync(firebaseUid, grupoId, null, candidatos);

            Assert.NotNull(result);
            Assert.Null(result.Descripcion);
            Assert.Equal(2, result.RestaurantesCandidatos.Count);
            Assert.Equal(2, result.Participantes.Count);
        }

        [Fact]
        public async Task HandleAsync_SinParticipantesActivosSeleccionados_LanzaInvalidOperationException()
        {
            var firebaseUid = "firebase123";
            var grupoId = Guid.NewGuid();
            var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = firebaseUid };
            var grupo = new Grupo("Grupo", usuario.Id) { Id = grupoId };
            grupo.Miembros.Add(new MiembroGrupo(grupoId, usuario.Id) { ParticipaEnRecomendacion = false });
            var miembroInactivoSeleccionado = new MiembroGrupo(grupoId, Guid.NewGuid())
            {
                ParticipaEnRecomendacion = true
            };
            miembroInactivoSeleccionado.AbandonarGrupo();
            grupo.Miembros.Add(miembroInactivoSeleccionado);

            _mockUsuarioRepository
                .Setup(x => x.GetByFirebaseUidAsync(firebaseUid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(usuario);

            _mockGrupoRepository
                .Setup(x => x.GetByIdAsync(grupoId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(grupo);

            _mockVotacionRepository
                .Setup(x => x.ObtenerVotacionActivaAsync(grupoId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((VotacionGrupo?)null);

            var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _useCase.HandleAsync(firebaseUid, grupoId, "desc", new List<Guid> { Guid.NewGuid(), Guid.NewGuid() }));

            Assert.Equal("Debe seleccionar al menos dos miembros activos para participar de la votación.", excepcion.Message);
            _mockVotacionRepository.Verify(
                x => x.CrearVotacionAsync(It.IsAny<VotacionGrupo>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleAsync_CandidatoInexistente_NoCreaLaVotacion()
        {
            var firebaseUid = "firebase123";
            var grupoId = Guid.NewGuid();
            var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = firebaseUid };
            var candidatos = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            var grupo = CrearGrupoConParticipante(grupoId, usuario);
            ConfigurarInicioValido(firebaseUid, grupoId, usuario, grupo);
            _mockRestauranteRepository
                .Setup(repository => repository.ObtenerPorIdsAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Restaurante> { new() { Id = candidatos[0] } });

            var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _useCase.HandleAsync(firebaseUid, grupoId, null, candidatos));

            Assert.Equal("Uno o más restaurantes candidatos ya no están disponibles.", excepcion.Message);
            _mockVotacionRepository.Verify(
                repository => repository.CrearVotacionAsync(
                    It.IsAny<VotacionGrupo>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleAsync_CandidatoConIncompatibilidadConocida_NoCreaLaVotacion()
        {
            var firebaseUid = "firebase123";
            var grupoId = Guid.NewGuid();
            var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = firebaseUid };
            var candidatos = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            var grupo = CrearGrupoConParticipante(grupoId, usuario);
            ConfigurarInicioValido(firebaseUid, grupoId, usuario, grupo);
            _mockPreferenciasGrupo
                .Setup(preferencias => preferencias.HandleAsync(
                    firebaseUid,
                    grupoId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UsuarioPreferencias
                {
                    Restricciones = new List<string> { "Sin gluten" }
                });
            _mockRestauranteRepository
                .Setup(repository => repository.ObtenerPorIdsAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(candidatos.Select(id => new Restaurante
                {
                    Id = id,
                    GustosQueSirve = new List<Gusto>
                    {
                        new()
                        {
                            Nombre = "Pizza",
                            Tags = new List<Tag> { new() { Nombre = "Gluten" } }
                        }
                    },
                    RestriccionesQueRespeta = new List<Restriccion>()
                }).ToList());

            var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _useCase.HandleAsync(firebaseUid, grupoId, null, candidatos));

            Assert.Contains("incompatibles", excepcion.Message);
            _mockVotacionRepository.Verify(
                repository => repository.CrearVotacionAsync(
                    It.IsAny<VotacionGrupo>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleAsync_CandidatoConDatosInsuficientes_PermiteUsarloComoOpcion()
        {
            var firebaseUid = "firebase123";
            var grupoId = Guid.NewGuid();
            var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = firebaseUid };
            var candidatos = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            var grupo = CrearGrupoConParticipante(grupoId, usuario);
            ConfigurarInicioValido(firebaseUid, grupoId, usuario, grupo);
            _mockPreferenciasGrupo
                .Setup(preferencias => preferencias.HandleAsync(
                    firebaseUid,
                    grupoId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UsuarioPreferencias
                {
                    Gustos = new List<string> { "Pizza" },
                    Restricciones = new List<string> { "Sin gluten" }
                });
            _mockRestauranteRepository
                .Setup(repository => repository.ObtenerPorIdsAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(candidatos.Select(id => new Restaurante
                {
                    Id = id,
                    GustosQueSirve = new List<Gusto>(),
                    RestriccionesQueRespeta = new List<Restriccion>()
                }).ToList());
            _mockVotacionRepository
                .Setup(repository => repository.CrearVotacionAsync(
                    It.IsAny<VotacionGrupo>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((VotacionGrupo votacion, CancellationToken _) => votacion);

            var resultado = await _useCase.HandleAsync(
                firebaseUid,
                grupoId,
                null,
                candidatos);

            Assert.Equal(2, resultado.RestaurantesCandidatos.Count);
        }

        private void ConfigurarInicioValido(
            string firebaseUid,
            Guid grupoId,
            Usuario usuario,
            Grupo grupo)
        {
            _mockUsuarioRepository
                .Setup(repository => repository.GetByFirebaseUidAsync(
                    firebaseUid,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(usuario);
            _mockGrupoRepository
                .Setup(repository => repository.GetByIdAsync(
                    grupoId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(grupo);
            _mockVotacionRepository
                .Setup(repository => repository.ObtenerVotacionActivaAsync(
                    grupoId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((VotacionGrupo?)null);
        }

        private static Grupo CrearGrupoConParticipante(Guid grupoId, Usuario administrador)
        {
            var grupo = new Grupo("Grupo", administrador.Id) { Id = grupoId };
            grupo.Miembros.Add(new MiembroGrupo(grupoId, administrador.Id) { ParticipaEnRecomendacion = true });
            grupo.Miembros.Add(new MiembroGrupo(grupoId, Guid.NewGuid()) { ParticipaEnRecomendacion = true });
            return grupo;
        }
    }
    }
