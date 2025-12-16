using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
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
            _usuarioRepoMock= new Mock<IUsuarioRepository>();

            _useCase = new ConstruirPreferenciasUsuarioIndividualUseCase(
                _usuarioRepoMock.Object
            );
        }
        private Usuario CrearUsuarioPrueba()
        {
            return new Usuario
            {
                FirebaseUid = "test_uid",
                Gustos = new List<Gusto>
            {
                new Gusto { Nombre = "Pizza" },
                new Gusto { Nombre = "Sushi" }
                }
            };
            
             
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
        public async Task HandleAsync_UsuarioSeleccionaGustosDelFiltro()
        {
            // Arrange
            var usuario= CrearUsuarioPrueba();
            var gustosFiltro = new List<Gusto>
            {
                new Gusto { Nombre = "Ramen" },
                new Gusto { Nombre = "Asado" }
            };

            _usuarioRepoMock
                .Setup(repo => repo.GetByFirebaseUidAsync(usuario.FirebaseUid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(usuario);


            var act = async () => await _useCase.HandleAsync(usuario.FirebaseUid,default);

            await act.Should().ThrowAsync<UnauthorizedAccessException>()
                 .WithMessage("Usuario no encontrado o no registrado.");

        }


        [Fact]
        public async Task HandleAsync_UsuarioNoSeleccionaGustosDelFiltroYusaSusGustos()
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
    }
}
