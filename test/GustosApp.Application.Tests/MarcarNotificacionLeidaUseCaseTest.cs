using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases;
using GustosApp.Application.UseCases.NotificacionUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GustosApp.Application.Tests
{
    public class MarcarNotificacionLeidaUseCaseTest
    {
        [Fact]
        public async Task MarcarNotificacionLeida()
        {
            var repoMock = new Mock<INotificacionRepository>();
            var usuariosMock = new Mock<IUsuarioRepository>();
            var notificacionId = Guid.NewGuid();
            var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = "uid" };
            var notificacion = new Notificacion { Id = notificacionId, UsuarioDestinoId = usuario.Id };

            usuariosMock.Setup(r => r.GetByFirebaseUidAsync("uid", It.IsAny<CancellationToken>()))
                .ReturnsAsync(usuario);
            repoMock.Setup(r => r.GetByIdAsync(notificacionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(notificacion);

           repoMock.Setup(r => r.MarcarComoLeidaAsync(notificacionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

            var useCaseLeida = new MarcarNotificacionLeidaUseCase(repoMock.Object, usuariosMock.Object);
            await useCaseLeida.HandleAsync("uid", notificacionId,CancellationToken.None);
           
            repoMock.Verify(r => r.MarcarComoLeidaAsync(notificacionId, It.IsAny<CancellationToken>()), Times.Once);

        }
    }
}
