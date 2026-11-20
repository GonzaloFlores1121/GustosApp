using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using GustosApp.Application.Interfaces;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.UseCases.RestauranteUseCases.SolicitudRestauranteUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model.@enum;
using GustosApp.Domain.Model;
using Moq;

namespace GustosApp.Application.Tests
{
    public class RechazarSolicitudRestauranteUseCaseTests
    {
        private readonly Mock<ISolicitudRestauranteRepository> _solicitudes;
        private readonly Mock<IUsuarioRepository> _usuarios;
        private readonly Mock<IFileStorageService> _firebase;
        private readonly Mock<IFirebaseAuthService> _auth;
        private readonly Mock<IEmailService> _email;
        private readonly Mock<IEmailTemplateService> _templates;

        private readonly RechazarSolicitudRestauranteUseCase _useCase;

        public RechazarSolicitudRestauranteUseCaseTests()
        {
            _solicitudes = new Mock<ISolicitudRestauranteRepository>();
            _usuarios = new Mock<IUsuarioRepository>();
            _firebase = new Mock<IFileStorageService>();
            _auth = new Mock<IFirebaseAuthService>();
            _email = new Mock<IEmailService>();
            _templates = new Mock<IEmailTemplateService>();

            var procesar = new ProcesarResolucionSolicitudUseCase(_solicitudes.Object,
                Mock.Of<IRestauranteRepository>(), Mock.Of<IRestauranteMenuRepository>(), new BloqueoResolucionPruebas(),
                Mock.Of<IHttpDownloader>(), Mock.Of<IOcrService>(), Mock.Of<IMenuParser>(), _auth.Object, _email.Object, _templates.Object);
            _useCase = new RechazarSolicitudRestauranteUseCase(_solicitudes.Object, procesar);
        }

        // Helpers
        private Usuario FakeUsuario(Guid id)
            => new Usuario
            {
                Id = id,
                FirebaseUid = "uid123",
                Email = "test@test.com",
                Nombre = "Gonza",
                Rol = RolUsuario.Usuario
            };

        private SolicitudRestaurante FakeSolicitud(Guid id, Usuario user)
        {
            return new SolicitudRestaurante
            {
                Id = id,
                UsuarioId = user.Id,
                Usuario = user,
                Nombre = "MiResto",
                Direccion = "Calle 123",
                Estado = EstadoSolicitudRestaurante.Pendiente,
                Imagenes = new List<SolicitudRestauranteImagen>
            {
                new SolicitudRestauranteImagen { Url = "url1" },
                new SolicitudRestauranteImagen { Url = "url2" }
            }
            };
        }

       
        [Fact]
        public async Task HandleAsync_SolicitudNoExiste_DeberiaLanzarException()
        {
            // Arrange
            var id = Guid.NewGuid();
            _solicitudes.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((SolicitudRestaurante?)null);

            // Act
            Func<Task> act = () => _useCase.HandleAsync(id, "motivo", default);

            // Assert
            await act.Should().ThrowAsync<Exception>()
                .WithMessage("Solicitud no encontrada");
        }

        
        [Fact]
        public async Task HandleAsync_SolicitudNoPendiente_DeberiaLanzarInvalidOperationException()
        {
            var id = Guid.NewGuid();
            var user = FakeUsuario(Guid.NewGuid());
            var solicitud = FakeSolicitud(id, user);
            solicitud.Estado = EstadoSolicitudRestaurante.Aprobada;

            _solicitudes.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(solicitud);

            Func<Task> act = () => _useCase.HandleAsync(id, "motivo", default);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Solo se pueden rechazar solicitudes pendientes.");
        }

       

        [Fact]
        public async Task HandleAsync_MotivoVacio_DeberiaLanzarArgumentException()
        {
            var id = Guid.NewGuid();
            var user = FakeUsuario(Guid.NewGuid());
            var solicitud = FakeSolicitud(id, user);

            _solicitudes.Setup(r => r.GetByIdAsync(id, default))
                .ReturnsAsync(solicitud);

            Func<Task> act = () => _useCase.HandleAsync(id, "   ", default);

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("Debe especificar un motivo de rechazo.");
        }

    

        [Fact]
        public async Task HandleAsync_DeberiaRechazarSolicitud_YRealizarAccionesCompletas()
        {
            // Arrange
            var id = Guid.NewGuid();
            var user = FakeUsuario(Guid.NewGuid());
            var solicitud = FakeSolicitud(id, user);

            _solicitudes.Setup(r => r.GetByIdAsync(id, default))
                .ReturnsAsync(solicitud);

            _usuarios.Setup(u => u.UpdateAsync(user, default))
                .Returns(Task.CompletedTask);

            _usuarios.Setup(u => u.SaveChangesAsync(default))
                .Returns(Task.CompletedTask);

            _templates.Setup(t => t.Render("SolicitudRechazada.html", It.IsAny<Dictionary<string, string>>()))
                .Returns("HTML_BODY");

            _email.Setup(e =>
                e.EnviarEmailAsync(user.Email, "Tu solicitud fue rechazada", "HTML_BODY"
                , It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _useCase.HandleAsync(id, "Motivo de rechazo", default);

            // Assert → Estado
            solicitud.Estado.Should().Be(EstadoSolicitudRestaurante.Rechazada);
            solicitud.MotivoRechazo.Should().Be("Motivo de rechazo");

            // La solicitud nunca reemplazó el rol personal.
            solicitud.Usuario.Rol.Should().Be(RolUsuario.Usuario);

            _auth.VerifyNoOtherCalls();

            // El expediente conserva sus imágenes referenciadas.
            _firebase.Verify(f => f.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);

            // Email
            _email.Verify(e =>
                e.EnviarEmailAsync(user.Email, "Tu solicitud fue rechazada", "HTML_BODY", 
                It.IsAny<CancellationToken>()),
                Times.Once);

            // La solicitud y el usuario se guardan juntos antes de los efectos externos.
            _solicitudes.Verify(s => s.UpdateAsync(It.Is<SolicitudRestaurante>(s =>
                s.Usuario.Rol == RolUsuario.Usuario && s.Estado == EstadoSolicitudRestaurante.Rechazada), default), Times.Exactly(4));

            // Update solicitud
            _solicitudes.Verify(s => s.UpdateAsync(solicitud, default), Times.Exactly(4));
        }

        [Fact]
        public async Task FalloDeCorreo_ConservaRechazoYPermiteReintentarSinBorrarOtraVez()
        {
            var solicitud = FakeSolicitud(Guid.NewGuid(), FakeUsuario(Guid.NewGuid()));
            _solicitudes.Setup(r => r.GetByIdAsync(solicitud.Id, default)).ReturnsAsync(solicitud);
            _templates.Setup(t => t.Render("SolicitudRechazada.html", It.IsAny<Dictionary<string, string>>()))
                .Returns("HTML");
            _email.SetupSequence(e => e.EnviarEmailAsync(solicitud.Usuario.Email,
                    "Tu solicitud fue rechazada", "HTML", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CorreoNoIniciadoException())
                .Returns(Task.CompletedTask);

            await _useCase.HandleAsync(solicitud.Id, "Motivo", default);

            solicitud.Estado.Should().Be(EstadoSolicitudRestaurante.Rechazada);
            solicitud.ArchivosRechazoEliminados.Should().BeFalse();
            solicitud.CorreoRechazoEnviado.Should().BeFalse();

            await _useCase.HandleAsync(solicitud.Id, "Motivo", default);

            solicitud.CorreoRechazoEnviado.Should().BeTrue();
            _firebase.Verify(f => f.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _email.Verify(e => e.EnviarEmailAsync(solicitud.Usuario.Email,
                "Tu solicitud fue rechazada", "HTML", It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task CorreoIniciadoSinConfirmacion_NoSeReenviaAlRetomar()
        {
            var solicitud = FakeSolicitud(Guid.NewGuid(), FakeUsuario(Guid.NewGuid()));
            solicitud.Estado = EstadoSolicitudRestaurante.Rechazada;
            solicitud.CorreoResolucion = EstadoCorreoResolucion.Enviando;
            _solicitudes.Setup(r => r.GetByIdAsync(solicitud.Id, default)).ReturnsAsync(solicitud);

            await _useCase.HandleAsync(solicitud.Id, "Motivo", default);
            await _useCase.HandleAsync(solicitud.Id, "Motivo", default);

            solicitud.CorreoResolucion.Should().Be(EstadoCorreoResolucion.Incierto);
            solicitud.CorreoRechazoEnviado.Should().BeFalse();
            _email.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task FalloAmbiguoDeSmtp_NoSeReenviaAlRetomar()
        {
            var solicitud = FakeSolicitud(Guid.NewGuid(), FakeUsuario(Guid.NewGuid()));
            _solicitudes.Setup(r => r.GetByIdAsync(solicitud.Id, default)).ReturnsAsync(solicitud);
            _email.Setup(e => e.EnviarEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ResultadoCorreoInciertoException());

            await _useCase.HandleAsync(solicitud.Id, "Motivo", default);
            await _useCase.HandleAsync(solicitud.Id, "Motivo", default);

            solicitud.CorreoResolucion.Should().Be(EstadoCorreoResolucion.Incierto);
            _email.Verify(e => e.EnviarEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Rechazo_ConservaArchivosReferenciadosAunqueStorageFalle()
        {
            var solicitud = FakeSolicitud(Guid.NewGuid(), FakeUsuario(Guid.NewGuid()));
            _solicitudes.Setup(r => r.GetByIdAsync(solicitud.Id, default)).ReturnsAsync(solicitud);
            _firebase.SetupSequence(f => f.DeleteFileAsync("url1", $"solicitudes/{solicitud.Id:N}"))
                .ThrowsAsync(new InvalidOperationException("Storage caído"))
                .Returns(Task.CompletedTask);
            _firebase.Setup(f => f.DeleteFileAsync("url2", $"solicitudes/{solicitud.Id:N}")).Returns(Task.CompletedTask);
            _templates.Setup(t => t.Render("SolicitudRechazada.html", It.IsAny<Dictionary<string, string>>()))
                .Returns("HTML");
            _email.Setup(e => e.EnviarEmailAsync(It.IsAny<string>(), It.IsAny<string>(), "HTML", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _useCase.HandleAsync(solicitud.Id, "Motivo", default);

            solicitud.Estado.Should().Be(EstadoSolicitudRestaurante.Rechazada);
            solicitud.ArchivosRechazoEliminados.Should().BeFalse();

            await _useCase.HandleAsync(solicitud.Id, "Motivo", default);

            solicitud.ArchivosRechazoEliminados.Should().BeFalse();
            _firebase.Verify(f => f.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RechazoConcurrente_NoCambiaFirebaseNiBorraImagenes()
        {
            var solicitud = FakeSolicitud(Guid.NewGuid(), FakeUsuario(Guid.NewGuid()));
            _solicitudes.Setup(r => r.GetByIdAsync(solicitud.Id, default)).ReturnsAsync(solicitud);
            _solicitudes.Setup(r => r.UpdateAsync(solicitud, default)).ThrowsAsync(new InvalidOperationException("Conflicto"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => _useCase.HandleAsync(solicitud.Id, "Rechazo", default));
            _auth.VerifyNoOtherCalls();
            _firebase.VerifyNoOtherCalls();
            _email.VerifyNoOtherCalls();
        }
    }

    }
