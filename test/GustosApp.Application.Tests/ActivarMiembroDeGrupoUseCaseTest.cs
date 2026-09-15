using GustosApp.Application.UseCases.GrupoUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Domain.Model;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GustosApp.Application.Tests
{
    public class ActivarMiembroDeGrupoUseCaseTest
    {

        [Fact]
        public async Task Handle_UsuarioSolicitanteNoExiste_ThrowsUnauthorized()
        {
            var mockGrupoRepo = new Mock<IGrupoRepository>();
            var mockUsuarioRepo = new Mock<IUsuarioRepository>();
            var mockMiembroRepo = new Mock<IMiembroGrupoRepository>();
            var mockVotacionRepo = new Mock<IVotacionRepository>();
            var useCase = new ActivarMiembroDeGrupoUseCase(
                mockGrupoRepo.Object,
                mockUsuarioRepo.Object,
                mockMiembroRepo.Object,
                mockVotacionRepo.Object
            );

            mockUsuarioRepo
                .Setup(r => r.GetByFirebaseUidAsync("uid", It.IsAny<CancellationToken>()))
                .ReturnsAsync((Usuario)null);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                useCase.Handle(Guid.NewGuid(), Guid.NewGuid(), "uid")
            );
        }
        [Fact]
        public async Task Handle_UsuarioAActivarNoExiste_LanzaArgumentException()
        {
            var mockGrupoRepo = new Mock<IGrupoRepository>();
            var mockUsuarioRepo = new Mock<IUsuarioRepository>();
            var mockMiembroRepo = new Mock<IMiembroGrupoRepository>();
            var mockVotacionRepo = new Mock<IVotacionRepository>();
            var useCase = new ActivarMiembroDeGrupoUseCase(
                mockGrupoRepo.Object,
                mockUsuarioRepo.Object,
                mockMiembroRepo.Object,
                  mockVotacionRepo.Object
            );

            var solicitante = new Usuario { Id = Guid.NewGuid() };

            mockUsuarioRepo
                .Setup(r => r.GetByFirebaseUidAsync("uid", It.IsAny<CancellationToken>()))
                .ReturnsAsync(solicitante);

            mockUsuarioRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Usuario)null);

            var excepcion = await Assert.ThrowsAsync<ArgumentException>(() =>
                useCase.Handle(Guid.NewGuid(), Guid.NewGuid(), "uid")
            );

            Assert.Equal("usuarioId", excepcion.ParamName);
        }
        [Fact]
        public async Task Handle_GrupoNoExiste_ThrowsKeyNotFound()
        {
            var mockGrupoRepo = new Mock<IGrupoRepository>();
            var mockUsuarioRepo = new Mock<IUsuarioRepository>();
            var mockMiembroRepo = new Mock<IMiembroGrupoRepository>();
            var mockVotacionRepo = new Mock<IVotacionRepository>();
            var useCase = new ActivarMiembroDeGrupoUseCase(
                mockGrupoRepo.Object,
                mockUsuarioRepo.Object,
                mockMiembroRepo.Object,
                  mockVotacionRepo.Object
            );

            var solicitante = new Usuario { Id = Guid.NewGuid() };
            var objetivo = new Usuario { Id = Guid.NewGuid() };

            mockUsuarioRepo
                .Setup(r => r.GetByFirebaseUidAsync("uid", It.IsAny<CancellationToken>()))
                .ReturnsAsync(solicitante);

            mockUsuarioRepo
                .Setup(r => r.GetByIdAsync(objetivo.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(objetivo);

            mockGrupoRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Grupo)null);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                useCase.Handle(Guid.NewGuid(), objetivo.Id, "uid")
            );
        }
        [Fact]
        public async Task Handle_NoEsAdministradorAunqueSeaElMismoUsuario_LanzaAccesoProhibido()
        {
            var mockGrupoRepo = new Mock<IGrupoRepository>();
            var mockUsuarioRepo = new Mock<IUsuarioRepository>();
            var mockMiembroRepo = new Mock<IMiembroGrupoRepository>();
            var mockVotacionRepo = new Mock<IVotacionRepository>();
            var useCase = new ActivarMiembroDeGrupoUseCase(
                mockGrupoRepo.Object,
                mockUsuarioRepo.Object,
                mockMiembroRepo.Object,
                    mockVotacionRepo.Object
            );

            var solicitante = new Usuario { Id = Guid.NewGuid() };
            var objetivo = solicitante;

            var grupo = new Grupo("Test Grupo", Guid.NewGuid()); // ✔️ instancia válida

            mockUsuarioRepo
                .Setup(r => r.GetByFirebaseUidAsync("uid", It.IsAny<CancellationToken>()))
                .ReturnsAsync(solicitante);

            mockUsuarioRepo
                .Setup(r => r.GetByIdAsync(objetivo.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(objetivo);

            mockGrupoRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(grupo);

            mockGrupoRepo
                .Setup(r => r.UsuarioEsAdministradorAsync(It.IsAny<Guid>(), solicitante.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            await Assert.ThrowsAsync<AccesoProhibidoException>(() =>
                useCase.Handle(Guid.NewGuid(), objetivo.Id, "uid")
            );

            mockMiembroRepo.Verify(
                r => r.ActivarMiembro(It.IsAny<Guid>(), It.IsAny<Guid>()),
                Times.Never);
        }

        [Fact]
        public async Task Handle_MiembroNoExiste_ThrowsInvalidOperation()
        {
            var mockGrupoRepo = new Mock<IGrupoRepository>();
            var mockUsuarioRepo = new Mock<IUsuarioRepository>();
            var mockMiembroRepo = new Mock<IMiembroGrupoRepository>();
            var mockVotacionRepo = new Mock<IVotacionRepository>();
            var useCase = new ActivarMiembroDeGrupoUseCase(
                mockGrupoRepo.Object,
                mockUsuarioRepo.Object,
                mockMiembroRepo.Object,
                    mockVotacionRepo.Object

            );

            var solicitante = new Usuario { Id = Guid.NewGuid() };
            var objetivo = new Usuario { Id = Guid.NewGuid() };

            mockUsuarioRepo
                .Setup(r => r.GetByFirebaseUidAsync("uid", It.IsAny<CancellationToken>()))
                .ReturnsAsync(solicitante);

            mockUsuarioRepo
                .Setup(r => r.GetByIdAsync(objetivo.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(objetivo);

            mockGrupoRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Grupo("grupo test", Guid.NewGuid()));

            mockGrupoRepo
                .Setup(r => r.UsuarioEsAdministradorAsync(It.IsAny<Guid>(), solicitante.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mockMiembroRepo
                .Setup(r =>
                    r.GetByGrupoYUsuarioAsync(It.IsAny<Guid>(), objetivo.IdUsuario, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync((MiembroGrupo)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                useCase.Handle(Guid.NewGuid(), objetivo.Id, "uid")
            );
        }

        [Fact]
        public async Task Handle_MiembroYaIncluidoEnRecomendacion_DevuelveTrue()
        {
            var mockGrupoRepo = new Mock<IGrupoRepository>();
            var mockUsuarioRepo = new Mock<IUsuarioRepository>();
            var mockMiembroRepo = new Mock<IMiembroGrupoRepository>();
            var mockVotacionRepo = new Mock<IVotacionRepository>();
            var useCase = new ActivarMiembroDeGrupoUseCase(
                mockGrupoRepo.Object,
                mockUsuarioRepo.Object,
                mockMiembroRepo.Object
                    , mockVotacionRepo.Object
            );

            var solicitante = new Usuario { Id = Guid.NewGuid() };
            var objetivo = new Usuario { Id = Guid.NewGuid() };

            mockUsuarioRepo.Setup(r => r.GetByFirebaseUidAsync("uid", It.IsAny<CancellationToken>()))
                           .ReturnsAsync(solicitante);

            mockUsuarioRepo.Setup(r => r.GetByIdAsync(objetivo.Id, It.IsAny<CancellationToken>()))
                           .ReturnsAsync(objetivo);

            mockGrupoRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(new Grupo("test", Guid.NewGuid()));

            mockGrupoRepo.Setup(r => r.UsuarioEsAdministradorAsync(It.IsAny<Guid>(), solicitante.Id, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(true);

            var miembro = new MiembroGrupo(Guid.NewGuid(), objetivo.Id);
            miembro.afectarRecomendacion = true; // ya participa de la recomendación

            mockMiembroRepo.Setup(r =>
                r.GetByGrupoYUsuarioAsync(It.IsAny<Guid>(), objetivo.IdUsuario, It.IsAny<CancellationToken>()))
                .ReturnsAsync(miembro);

            var result = await useCase.Handle(Guid.NewGuid(), objetivo.Id, "uid");

            Assert.True(result);
        }

        [Fact]
        public async Task Handle_MiembroExcluido_LoIncluyeEnRecomendacionYDevuelveTrue()
        {
            var mockGrupoRepo = new Mock<IGrupoRepository>();
            var mockUsuarioRepo = new Mock<IUsuarioRepository>();
            var mockMiembroRepo = new Mock<IMiembroGrupoRepository>();
            var mockVotacionRepo = new Mock<IVotacionRepository>();
            var useCase = new ActivarMiembroDeGrupoUseCase(
                mockGrupoRepo.Object,
                mockUsuarioRepo.Object,
                mockMiembroRepo.Object
                    , mockVotacionRepo.Object
            );

            var solicitante = new Usuario { Id = Guid.NewGuid() };
            var objetivo = new Usuario { Id = Guid.NewGuid() };

            mockUsuarioRepo.Setup(r => r.GetByFirebaseUidAsync("uid", It.IsAny<CancellationToken>()))
                           .ReturnsAsync(solicitante);

            mockUsuarioRepo.Setup(r => r.GetByIdAsync(objetivo.Id, It.IsAny<CancellationToken>()))
                           .ReturnsAsync(objetivo);

            mockGrupoRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(new Grupo("Test", Guid.NewGuid()));

            mockGrupoRepo.Setup(r => r.UsuarioEsAdministradorAsync(It.IsAny<Guid>(), solicitante.Id, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(true);

            // El miembro todavía no participa de la recomendación.
            mockMiembroRepo.Setup(r =>
                r.GetByGrupoYUsuarioAsync(It.IsAny<Guid>(), objetivo.IdUsuario, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MiembroGrupo(Guid.NewGuid(), objetivo.Id)
                {
                    afectarRecomendacion = false
                });

            mockMiembroRepo.Setup(r =>
                r.ActivarMiembro(It.IsAny<Guid>(), objetivo.Id))
                .ReturnsAsync(true);

            var result = await useCase.Handle(Guid.NewGuid(), objetivo.Id, "uid");

            Assert.True(result);

            mockMiembroRepo.Verify(r =>
                r.ActivarMiembro(It.IsAny<Guid>(), objetivo.Id),
                Times.Once);
        }


    }
}
