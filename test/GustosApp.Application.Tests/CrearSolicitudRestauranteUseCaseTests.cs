using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.RestauranteUseCases.SolicitudRestauranteUseCases;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model.@enum;
using GustosApp.Domain.Model;
using Moq;

namespace GustosApp.Application.Tests
{
    public class CrearSolicitudRestauranteUseCaseTests
    {
        private readonly Mock<ISolicitudRestauranteRepository> _solicitudesRepo;
        private readonly Mock<IGustoRepository> _gustosRepo;
        private readonly Mock<IRestriccionRepository> _restriccionesRepo;
        private readonly Mock<IUsuarioRepository> _usuariosRepo;
        private readonly Mock<IFirebaseAuthService> _firebase;
        private readonly Mock<IEmailService> _email;
        private readonly Mock<IEmailTemplateService> _templates;
        private readonly Mock<IFileStorageService> _firebaseStorage;

        private readonly CrearSolicitudRestauranteUseCase _useCase;

        public CrearSolicitudRestauranteUseCaseTests()
        {
            _solicitudesRepo = new Mock<ISolicitudRestauranteRepository>();
            _gustosRepo = new Mock<IGustoRepository>();
            _restriccionesRepo = new Mock<IRestriccionRepository>();
            _usuariosRepo = new Mock<IUsuarioRepository>();
            _firebase = new Mock<IFirebaseAuthService>();
            _email = new Mock<IEmailService>();
            _templates = new Mock<IEmailTemplateService>();
            _firebaseStorage = new Mock<IFileStorageService>();

            _useCase = new CrearSolicitudRestauranteUseCase(
                _solicitudesRepo.Object,
                _restriccionesRepo.Object,
                _gustosRepo.Object,
                _usuariosRepo.Object,
                _email.Object,
                _templates.Object,
                _firebaseStorage.Object
            );
        }

        private Usuario FakeUsuario(Guid id, RolUsuario rol = RolUsuario.Usuario)
            => new Usuario
            {
                Id = id,
                FirebaseUid = "firebase-uid",
                Email = "user@test.com",
                Nombre = "Gonza",
                Rol = rol
            };

        [Fact]
        public async Task FalloDePersistencia_EliminaLosArchivosSubidos()
        {
            var usuario = FakeUsuario(Guid.NewGuid());
            _usuariosRepo.Setup(r => r.GetByFirebaseUidAsync("uid", default)).ReturnsAsync(usuario);
            _gustosRepo.Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>(), default)).ReturnsAsync(new List<Gusto>());
            _restriccionesRepo.Setup(r => r.GetRestriccionesByIdsAsync(It.IsAny<List<Guid>>(), default)).ReturnsAsync(new List<Restriccion>());
            using var contenido = new MemoryStream(new byte[] { 1 });
            var archivo = new GustosApp.Domain.Common.ArchivoEntrada(contenido, "menu.jpg");
            _firebaseStorage.Setup(f => f.UploadFileAsync(contenido, "menu.jpg", "solicitudes")).ReturnsAsync("https://example.test/menu.jpg");
            _solicitudesRepo.Setup(r => r.AddAsync(It.IsAny<SolicitudRestaurante>(), default)).ThrowsAsync(new InvalidOperationException("Error SQL"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => _useCase.HandleAsync("uid", "Nombre", "Dirección", null, null, null,
                new List<Guid>(), new List<Guid>(), null, null, null, archivo, null, "", default));

            _firebaseStorage.Verify(f => f.DeleteFileAsync("https://example.test/menu.jpg"), Times.Once());
        }

        private List<Guid> FakeGuids(int count)
        {
            var list = new List<Guid>();
            for (int i = 0; i < count; i++)
                list.Add(Guid.NewGuid());
            return list;
        }

        // =========================================================
        // TESTS
        // =========================================================

        [Fact]
        public async Task HandleAsync_DeberiaLanzarException_SiUsuarioNoExiste()
        {
            // Arrange
            _usuariosRepo
                .Setup(r => r.GetByFirebaseUidAsync("uid", It.IsAny<CancellationToken>()))
                .ReturnsAsync((Usuario?)null);

            // Act
            Func<Task> act = () => _useCase.HandleAsync(
                firebaseUid: "uid",
                nombre: "Mi Resto",
                direccion: "Calle Falsa 123",
                latitudStr: "1.23",
                longitudStr: "4.56",
                horariosJson: "{}",
                gustosIds: FakeGuids(3),
                restriccionesIds: new List<Guid>(),
                imagenDestacada: null,
                imagenesInterior: null,
                imagenesComidas: null,
                imagenMenu: null,
                logo: null,
                websiteUrl: "https://web.com"
            );

            // Assert
            await act.Should().ThrowAsync<Exception>()
                .WithMessage("Usuario no encontrado");

            _solicitudesRepo.Verify(r => r.AddAsync(It.IsAny<SolicitudRestaurante>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_DeberiaLanzarException_SiUsuarioYaEsDueno()
        {
            // Arrange
            var user = FakeUsuario(Guid.NewGuid(), rol: RolUsuario.DuenoRestaurante);

            _usuariosRepo
                .Setup(r => r.GetByFirebaseUidAsync("uid", It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            // Act
            Func<Task> act = () => _useCase.HandleAsync(
                firebaseUid: "uid",
                nombre: "Mi Resto",
                direccion: "Calle Falsa 123",
                latitudStr: "1.23",
                longitudStr: "4.56",
                horariosJson: "{}",
                gustosIds: FakeGuids(3),
                restriccionesIds: new List<Guid>(),
                imagenDestacada: null,
                imagenesInterior: null,
                imagenesComidas: null,
                imagenMenu: null,
                logo: null,
                websiteUrl: "https://web.com"
            );

            // Assert
            await act.Should().ThrowAsync<Exception>()
                .WithMessage("Ya sos dueño de un restaurante.");

            _solicitudesRepo.Verify(r => r.AddAsync(It.IsAny<SolicitudRestaurante>(), It.IsAny<CancellationToken>()), Times.Never);
            _usuariosRepo.Verify(r => r.UpdateAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_NoCreaSegundaSolicitudMientrasHayUnaPendiente()
        {
            var user = FakeUsuario(Guid.NewGuid());
            _usuariosRepo.Setup(r => r.GetByFirebaseUidAsync("uid", default)).ReturnsAsync(user);
            _solicitudesRepo.Setup(r => r.BuscarPendientePorUsuarioAsync(user.Id, default))
                .ReturnsAsync(new SolicitudRestaurante { Id = Guid.NewGuid() });

            Func<Task> act = () => _useCase.HandleAsync("uid", "Mi Resto", "Calle 123", null, null, null,
                [], [], null, null, null, null, null, "", default);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Ya tenés una solicitud de restaurante pendiente.");
            user.Rol.Should().Be(RolUsuario.Usuario);
            _solicitudesRepo.Verify(r => r.AddAsync(It.IsAny<SolicitudRestaurante>(), default), Times.Never);
            _firebase.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task HandleAsync_DeberiaCrearSolicitudCorrectamente_CuandoDatosSonValidos()
        {
            // Arrange
            var user = FakeUsuario(Guid.NewGuid(), RolUsuario.Usuario);
            var gustosIds = FakeGuids(3);
            var restriccionesIds = FakeGuids(2);

            var gustosResult = new List<Gusto> { new Gusto { Id = gustosIds[0] } };
            var restriccionesResult = new List<Restriccion> { new Restriccion { Id = restriccionesIds[0] } };

            _usuariosRepo.Setup(r => r.GetByFirebaseUidAsync("uid", It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            _gustosRepo.Setup(r => r.GetByIdsAsync(gustosIds, It.IsAny<CancellationToken>()))
                .ReturnsAsync(gustosResult);

            _restriccionesRepo.Setup(r => r.GetRestriccionesByIdsAsync(restriccionesIds, It.IsAny<CancellationToken>()))
                .ReturnsAsync(restriccionesResult);

            SolicitudRestaurante? solicitudCapturada = null;

            _solicitudesRepo
                .Setup(r => r.AddAsync(It.IsAny<SolicitudRestaurante>(), It.IsAny<CancellationToken>()))
                .Callback<SolicitudRestaurante, CancellationToken>((s, _) => solicitudCapturada = s)
                .Returns(Task.CompletedTask);

            _usuariosRepo
                .Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            string? emailBody = null;
            Dictionary<string, string>? templateData = null;

            _templates
                .Setup(t => t.Render("SolicitudNueva.html", It.IsAny<Dictionary<string, string>>()))
                .Callback<string, Dictionary<string, string>>((_, data) => templateData = data)
                .Returns("HTML_BODY");

            _email
                .Setup(e => e.EnviarEmailAsync(
                    "gonzalomarcos551@gmail.com",
                    "Nueva solicitud de restaurante",
                    "HTML_BODY", CancellationToken.None))
                .Returns(Task.CompletedTask);

            // Act
            var resultId = await _useCase.HandleAsync(
                firebaseUid: "uid",
                nombre: "  Mi Resto  ",
                direccion: "  Calle Falsa 123  ",
                latitudStr: "1.23",
                longitudStr: "4.56",
                horariosJson: "{\"lunes\":\"10-20\"}",
                gustosIds: gustosIds,
                restriccionesIds: restriccionesIds,
                imagenDestacada: null,
                imagenesInterior: null,
                imagenesComidas: null,
                imagenMenu: null,
                logo: null,
                websiteUrl: "  https://web.com  "
            );

            // Assert: retorno
            solicitudCapturada.Should().NotBeNull();
            resultId.Should().Be(solicitudCapturada!.Id);

            // Assert: datos básicos de la solicitud
            solicitudCapturada.UsuarioId.Should().Be(user.Id);
            solicitudCapturada.Nombre.Should().Be("Mi Resto"); // Trim aplicado
            solicitudCapturada.Direccion.Should().Be("Calle Falsa 123");
            solicitudCapturada.Latitud.Should().Be(1.23);
            solicitudCapturada.Longitud.Should().Be(4.56);
            solicitudCapturada.HorariosJson.Should().Be("{\"lunes\":\"10-20\"}");
            solicitudCapturada.GustosIds.Should().BeEquivalentTo(gustosIds);
            solicitudCapturada.RestriccionesIds.Should().BeEquivalentTo(restriccionesIds);
            solicitudCapturada.Estado.Should().Be(EstadoSolicitudRestaurante.Pendiente);
            solicitudCapturada.WebsiteUrl.Should().Be("https://web.com");
            solicitudCapturada.FechaCreacion.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            // Gustos / Restricciones cargados
            solicitudCapturada.Gustos.Should().BeEquivalentTo(gustosResult);
            solicitudCapturada.Restricciones.Should().BeEquivalentTo(restriccionesResult);

            // La solicitud no reemplaza la identidad del usuario.
            user.Rol.Should().Be(RolUsuario.Usuario);

            // El rol y la solicitud se persisten juntos, en lugar de dos escrituras independientes.
            _solicitudesRepo.Verify(r => r.AddAsync(It.Is<SolicitudRestaurante>(s =>
                s.Usuario == user && s.Usuario.Rol == RolUsuario.Usuario), It.IsAny<CancellationToken>()), Times.Once);

            _firebase.VerifyNoOtherCalls();

            // Template + Email
            templateData.Should().NotBeNull();
            templateData!["USUARIO"].Should().Be(user.Email);
            templateData["NOMBRE"].Should().Be(solicitudCapturada.Nombre);
            templateData["DIRECCION"].Should().Be(solicitudCapturada.Direccion);
            templateData["LINK"].Should().Be("http://localhost:3000/admin");

            _email.Verify(e =>
                e.EnviarEmailAsync("gonzalomarcos551@gmail.com",
                                   "Nueva solicitud de restaurante",
                                   "HTML_BODY", CancellationToken.None),
                Times.Once);

            _solicitudesRepo.Verify(r =>
                r.AddAsync(It.IsAny<SolicitudRestaurante>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task HandleAsync_DeberiaPermitirRestriccionesNull_YNoFallar()
        {
            // Arrange
            var user = FakeUsuario(Guid.NewGuid(), RolUsuario.Usuario);
            var gustosIds = FakeGuids(3);
            List<Guid>? restriccionesIds = null;

            var gustosResult = new List<Gusto> { new Gusto { Id = gustosIds[0] } };

            _usuariosRepo.Setup(r => r.GetByFirebaseUidAsync("uid", It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            _gustosRepo.Setup(r => r.GetByIdsAsync(gustosIds, It.IsAny<CancellationToken>()))
                .ReturnsAsync(gustosResult);

            _restriccionesRepo.Setup(r =>
                r.GetRestriccionesByIdsAsync(null!, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Restriccion>());

            _solicitudesRepo.Setup(r =>
                r.AddAsync(It.IsAny<SolicitudRestaurante>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _usuariosRepo.Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _firebase.Setup(f => f.SetUserRoleAsync(user.FirebaseUid, It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            _templates.Setup(t => t.Render("SolicitudNueva.html", It.IsAny<Dictionary<string, string>>()))
                .Returns("HTML_BODY");

            _email.Setup(e => e.EnviarEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),CancellationToken.None))
                .Returns(Task.CompletedTask);

            // Act
            var act = async () => await _useCase.HandleAsync(
                firebaseUid: "uid",
                nombre: "Mi Resto",
                direccion: "Calle Falsa 123",
                latitudStr: "1.23",
                longitudStr: "4.56",
                horariosJson: null,
                gustosIds: gustosIds,
                restriccionesIds: restriccionesIds,
                imagenDestacada: null,
                imagenesInterior: null,
                imagenesComidas: null,
                imagenMenu: null,
                logo: null,
                websiteUrl: "https://web.com"
            );

            // Assert: no debería tirar excepción
            await act.Should().NotThrowAsync();

            _restriccionesRepo.Verify(r =>
                r.GetRestriccionesByIdsAsync(null!, It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
