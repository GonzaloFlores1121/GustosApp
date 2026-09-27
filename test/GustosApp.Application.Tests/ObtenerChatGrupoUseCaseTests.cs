using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.UseCases.GrupoUseCases.ChatGrupoUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;

namespace GustosApp.Application.Tests
{
    public class ObtenerChatGrupoUseCaseTests
    {
        private readonly Mock<IChatRepository> _chatRepo;
        private readonly Mock<IGrupoRepository> _grupoRepo;

        private readonly ObtenerChatGrupoUseCase _sut;

        public ObtenerChatGrupoUseCaseTests()
        {
            _chatRepo = new Mock<IChatRepository>();
            _grupoRepo = new Mock<IGrupoRepository>();

            _sut = new ObtenerChatGrupoUseCase(
                _chatRepo.Object,
                _grupoRepo.Object
            );
        }

        // Helpers para crear objetos
        private Usuario CrearUsuario(string firebaseUid)
        {
            return new Usuario
            {
                Id = Guid.NewGuid(),
                FirebaseUid = firebaseUid,
                Nombre = "Juan",
                Apellido = "Pérez"
            };
        }

        private MiembroGrupo CrearMiembro(Grupo grupo, Usuario usuario)
        {
            return new MiembroGrupo(grupo.Id, usuario.Id)
            {
                Usuario = usuario 
            };
        }

        private Grupo CrearGrupoConMiembro(string firebaseUid)
        {
            var user = CrearUsuario(firebaseUid);
            var grupo = new Grupo("Grupo Test", user.Id);
            var miembro = CrearMiembro(grupo, user);
            grupo.Miembros = new List<MiembroGrupo> { miembro };
            return grupo;
        }

    
        [Fact]
        public async Task HandleAsync_grupoNoEncontrado_lanzaArgumentException()
        {
            _grupoRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Grupo)null!);

            Func<Task> act = () => _sut.HandleAsync("uid123", Guid.NewGuid());

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("Grupo no encontrado");
        }

        [Fact]
        public async Task HandleAsync_UsuarioNoEsMiembro_LanzaAccesoProhibido()
        {
            // Grupo con un miembro distinto
            var grupo = CrearGrupoConMiembro("otroUid");

            _grupoRepo.Setup(r => r.GetByIdAsync(grupo.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(grupo);

            Func<Task> act = () => _sut.HandleAsync("uidNoMiembro", grupo.Id);

            await act.Should().ThrowAsync<AccesoProhibidoException>()
                .WithMessage("No pertenece a este grupo");

            _chatRepo.Verify(
                r => r.GetMessagesByGrupoIdAsync(
                    It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(),
                    It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleAsync_MiembroInactivo_LanzaAccesoProhibidoYNoDevuelveMensajes()
        {
            var grupo = CrearGrupoConMiembro("uidInactivo");
            grupo.Miembros.Single().AbandonarGrupo();

            _grupoRepo.Setup(r => r.GetByIdAsync(grupo.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(grupo);

            Func<Task> accion = () => _sut.HandleAsync("uidInactivo", grupo.Id);

            await accion.Should().ThrowAsync<AccesoProhibidoException>()
                .WithMessage("No pertenece a este grupo");
            _chatRepo.Verify(
                r => r.GetMessagesByGrupoIdAsync(
                    It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(),
                    It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

       
        [Fact]
        public async Task HandleAsync_usuarioEsMiembro_retornaMensajes()
        {
            var grupo = CrearGrupoConMiembro("uid123");

            var mensajes = new List<ChatMensaje>
        {
            new ChatMensaje
            {
                Id = Guid.NewGuid(),
                GrupoId = grupo.Id,
                Mensaje = "Hola",
                UsuarioNombre = "Juan Pérez"
            }
        };

            _grupoRepo.Setup(r => r.GetByIdAsync(grupo.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(grupo);

            _chatRepo.Setup(r => r.GetMessagesByGrupoIdAsync(
                    grupo.Id, null, null, 51, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mensajes);

            var result = await _sut.HandleAsync("uid123", grupo.Id);

            result.Should().NotBeNull();
            result.Mensajes.Should().HaveCount(1);
            result.Mensajes.First().Mensaje.Should().Be("Hola");
            result.HayMas.Should().BeFalse();
        }

        [Fact]
        public async Task HandleAsync_cincuentaYUnMensajes_devuelveUltimosCincuentaEIndicaQueHayMas()
        {
            var grupo = CrearGrupoConMiembro("uid123");
            var mensajes = Enumerable.Range(0, 51)
                .Select(index => new ChatMensaje
                {
                    Id = Guid.NewGuid(), GrupoId = grupo.Id,
                    Mensaje = index.ToString(), FechaEnvio = DateTime.UtcNow.AddMinutes(index)
                })
                .ToList();
            _grupoRepo.Setup(r => r.GetByIdAsync(grupo.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(grupo);
            _chatRepo.Setup(r => r.GetMessagesByGrupoIdAsync(
                    grupo.Id, null, null, 51, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mensajes);

            var result = await _sut.HandleAsync("uid123", grupo.Id);

            result.HayMas.Should().BeTrue();
            result.Mensajes.Should().HaveCount(50);
            result.Mensajes.First().Mensaje.Should().Be("1");
            result.Mensajes.Last().Mensaje.Should().Be("50");
        }

        [Fact]
        public async Task ObtenerPosterioresAsync_limitaCadaLoteSinSaltarElSiguienteCursor()
        {
            var grupo = CrearGrupoConMiembro("uid123");
            var cursorFecha = DateTime.UtcNow;
            var cursorId = Guid.NewGuid();
            var mensajes = Enumerable.Range(1, 51)
                .Select(index => new ChatMensaje
                {
                    Id = Guid.NewGuid(), GrupoId = grupo.Id,
                    Mensaje = index.ToString(), FechaEnvio = cursorFecha.AddSeconds(index)
                })
                .ToList();
            _grupoRepo.Setup(r => r.GetByIdAsync(grupo.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(grupo);
            _chatRepo.Setup(r => r.GetMessagesAfterAsync(
                    grupo.Id, cursorFecha, cursorId, 51, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mensajes);

            var result = await _sut.ObtenerPosterioresAsync(
                "uid123", grupo.Id, cursorFecha, cursorId);

            result.HayMas.Should().BeTrue();
            result.Mensajes.Should().HaveCount(50);
            result.Mensajes.Last().Mensaje.Should().Be("50");
        }
    }
    }
