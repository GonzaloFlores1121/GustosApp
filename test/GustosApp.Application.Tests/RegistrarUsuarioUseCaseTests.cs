using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GustosApp.Application.UseCases.UsuarioUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;

namespace GustosApp.Application.Tests
{
    public class RegistrarUsuarioUseCaseTests
    {

        private readonly Mock<IUsuarioRepository> _mockRepo;
        private readonly RegistrarUsuarioUseCase _useCase;

        public RegistrarUsuarioUseCaseTests()
        {
            _mockRepo = new Mock<IUsuarioRepository>();
            _useCase = new RegistrarUsuarioUseCase(_mockRepo.Object);
        }

        [Fact]
        public async Task HandleAsync_UsuarioNoExiste_CreaUsuario()
        {
            // Arrange
            var firebaseUid = "uid-test";
            var usuario = new Usuario
            {
                Nombre = "Juan",
                Apellido = "Perez",
                Email = "juan@test.com",
                IdUsuario = "id123",
                FotoPerfilUrl = "foto.jpg"
                
            };

            _mockRepo.Setup(r => r.GetByFirebaseUidAsync(firebaseUid, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Usuario)null);

            Usuario? usuarioCreado = null;
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()))
                .Callback<Usuario, CancellationToken>((u, ct) => usuarioCreado = u)
                .Returns(Task.CompletedTask);

            _mockRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var resp = await _useCase.HandleAsync(firebaseUid, usuario, CancellationToken.None);

            // Assert
            Assert.NotNull(resp);
            Assert.Equal(firebaseUid, resp.FirebaseUid);
            Assert.Equal(usuario.Email, resp.Email);
            Assert.Equal(usuario.Nombre, resp.Nombre);
            Assert.Equal(usuario.Apellido, resp.Apellido);
            Assert.Equal(usuario.IdUsuario, resp.IdUsuario);
            Assert.Null(resp.FotoPerfilUrl); // Una URL enviada por el cliente no se acepta como foto propia.
            Assert.NotNull(usuarioCreado);
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_UsuarioYaExiste_DevuelveUsuarioExistente()
        {
            // Arrange
            var firebaseUid = "uid-existente";
            var usuarioExistente = new Usuario(firebaseUid, "existente@test.com", "Ana", "Lopez", "id123", "foto2.jpg");

            var usuario = new Usuario
            {
                Nombre = "Ana",
                Apellido = "Lopez",
                Email = "existente@test.com",
                FotoPerfilUrl = "foto2.jpg"
            };

            _mockRepo.Setup(r => r.GetByFirebaseUidAsync(firebaseUid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(usuarioExistente);

            // Act
            var resp = await _useCase.HandleAsync(firebaseUid, usuario, CancellationToken.None);

            // Assert
            Assert.NotNull(resp);
            Assert.Equal(usuarioExistente.FirebaseUid, resp.FirebaseUid);
            Assert.Equal(usuarioExistente.Email, resp.Email);
            Assert.Equal(usuarioExistente.Nombre, resp.Nombre);
            Assert.Equal(usuarioExistente.Apellido, resp.Apellido);
            Assert.Equal(usuarioExistente.FotoPerfilUrl, resp.FotoPerfilUrl);
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ReintentoDespuesDeRespuestaPerdida_ConservaIdentidadYNoDuplica()
        {
            var usuario = new Usuario("uid", "correo@test.com", "Ana", "Prueba", "ana");
            _mockRepo.Setup(r => r.GetByFirebaseUidAsync("uid", It.IsAny<CancellationToken>()))
                .ReturnsAsync(usuario);

            var resultado = await _useCase.HandleConEstadoAsync("uid",
                new Usuario { Email = "correo@test.com", IdUsuario = "otro" });

            Assert.False(resultado.EsNuevo);
            Assert.Same(usuario, resultado.Usuario);
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task EmailDeOtroUid_NoAutorizaRegistro()
        {
            _mockRepo.Setup(r => r.GetByEmailAsync("correo@test.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Usuario { FirebaseUid = "otro" });

            var accion = () => _useCase.HandleConEstadoAsync("uid",
                new Usuario { Email = "correo@test.com", IdUsuario = "nuevo" });

            var error = await Assert.ThrowsAsync<ConflictoRegistroUsuarioException>(accion);
            Assert.Equal("EMAIL_TAKEN", error.Codigo);
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }


}
