using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using GustosApp.Application.Tests.Builders;
using GustosApp.Application.UseCases.UsuarioUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;

namespace GustosApp.Application.Tests
{
    public class ConstruirPreferenciasUsuarioIndividualUseCaseTests
    {
        private readonly Mock<IUsuarioRepository> _usuarioRepoMock;
        private readonly ConstruirPreferenciasUsuarioIndividualUseCase _useCase;

        public ConstruirPreferenciasUsuarioIndividualUseCaseTests()
        {
            _usuarioRepoMock = new Mock<IUsuarioRepository>();

            _useCase = new ConstruirPreferenciasUsuarioIndividualUseCase(
                _usuarioRepoMock.Object
            );
        }

        [Fact]
        public async Task HandleAsync_UsuarioNoEncontrado_DeberiaLanzarUnauthorizedAccessException()
        {
            // Arrange
            string firebaseUid = "nonexistent_uid";
            _usuarioRepoMock
                .Setup(repo => repo.GetByFirebaseUidAsync(firebaseUid, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Usuario?)null);


            var act = async () => await _useCase.HandleAsync(firebaseUid, null, CancellationToken.None);

            await act.Should().ThrowAsync<UnauthorizedAccessException>()
                 .WithMessage("Usuario no encontrado o no registrado.");

        }

        [Fact]
        public async Task HandleAsync_DevuelvePreferenciasDelFiltro()
        {
            // Arrange
            var usuario = new UsuarioBuilder()
                .ConFirebaseUid("test-uid")
                .ConGustos("Pizza", "Hamburguesa")
                .Build();

            var gustosFiltro = new List<string> { "Ramen", "Asado" };

            _usuarioRepoMock
                .Setup(repo => repo.GetByFirebaseUidAsync(usuario.FirebaseUid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(usuario);

            // Act
            var result = await _useCase.HandleAsync(usuario.FirebaseUid, gustosFiltro);

            // Assert
            result.Should().NotBeNull();
            result.Gustos.Should().BeEquivalentTo(gustosFiltro);
            result.Restricciones.Should().BeEquivalentTo(usuario.Restricciones.Select(r => r.Nombre));
            result.CondicionesMedicas.Should().BeEquivalentTo(usuario.CondicionesMedicas.Select(c => c.Nombre));
        }


        [Fact]
        public async Task HandleAsync_SinFiltro_DevuelvePreferenciasDelUsuario()
        {
            // Arrange
            var usuario = new UsuarioBuilder()
                   .ConFirebaseUid("test-uid")
                   .ConGustos("Sushi", "Tacos")
                   .ConRestricciones("Sin gluten")
                   .ConCondiciones("Hipertensión")
                   .Build();

            _usuarioRepoMock
                .Setup(r => r.GetByFirebaseUidAsync(usuario.FirebaseUid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(usuario);

            // Act
            var result = await _useCase.HandleAsync(usuario.FirebaseUid, null);

            // Assert
            result.Gustos.Should().BeEquivalentTo(new List<string> { "Sushi", "Tacos" });
            result.Restricciones.Should().BeEquivalentTo(new List<string> { "Sin gluten" });
            result.CondicionesMedicas.Should().BeEquivalentTo(new List<string> { "Hipertensión" });
        }
    }
    }
