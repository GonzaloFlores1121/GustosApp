using System.Security.Claims;
using FluentAssertions;
using GustosApp.API.Hubs.GustosApp.API.Hubs;
using GustosApp.API.Hubs.Services;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.UseCases.GrupoUseCases.ChatGrupoUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace GustosApp.API.Tests;

public class ChatHubPruebas
{
    [Fact]
    public async Task JoinGroup_UsuarioAjeno_NoDejaSuscripcionResidual()
    {
        var grupoId = Guid.NewGuid();
        var grupos = new Mock<IGroupManager>();
        var contexto = CrearContexto("conexion-1", "externo");
        var grupoRepo = new Mock<IGrupoRepository>();
        grupoRepo.Setup(r => r.GetByIdAsync(grupoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Grupo("Privado", Guid.NewGuid()));
        var chatRepo = new Mock<IChatRepository>();
        var registro = new ChatConnectionRegistry();
        registro.RegistrarConexion("conexion-1", "externo");
        var hub = CrearHub(chatRepo, grupoRepo, registro);
        hub.Context = contexto.Object;
        hub.Groups = grupos.Object;

        Func<Task> accion = () => hub.JoinGroup(grupoId.ToString());

        await accion.Should().ThrowAsync<AccesoProhibidoException>();
        grupos.Verify(g => g.AddToGroupAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        registro.ObtenerConexiones("externo", grupoId).Should().BeEmpty();
    }

    [Fact]
    public void Registro_DosPestanas_CuentaUsuarioUnaVezYConservaLaOtraConexion()
    {
        var grupoId = Guid.NewGuid();
        var registro = new ChatConnectionRegistry();
        registro.RegistrarConexion("pestana-1", "uid");
        registro.RegistrarConexion("pestana-2", "uid");
        registro.RegistrarGrupo("pestana-1", grupoId);
        registro.RegistrarGrupo("pestana-2", grupoId);

        registro.ObtenerUsuarios(grupoId).Should().Equal("uid");
        registro.EliminarConexion("pestana-1");

        registro.ObtenerUsuarios(grupoId).Should().Equal("uid");
        registro.ObtenerConexiones("uid", grupoId).Should().Equal("pestana-2");
    }

    private static ChatHub CrearHub(
        Mock<IChatRepository> chatRepo,
        Mock<IGrupoRepository> grupoRepo,
        IChatConnectionRegistry registro)
    {
        var enviar = new EnviarMensajeGrupoUseCase(
            chatRepo.Object, Mock.Of<IUsuarioRepository>(), grupoRepo.Object);
        var obtener = new ObtenerChatGrupoUseCase(chatRepo.Object, grupoRepo.Object);
        return new ChatHub(enviar, obtener, registro);
    }

    private static Mock<HubCallerContext> CrearContexto(string conexionId, string uid)
    {
        var contexto = new Mock<HubCallerContext>();
        contexto.SetupGet(c => c.ConnectionId).Returns(conexionId);
        contexto.SetupGet(c => c.ConnectionAborted).Returns(CancellationToken.None);
        contexto.SetupGet(c => c.User).Returns(new ClaimsPrincipal(
            new ClaimsIdentity([new Claim("user_id", uid)], "pruebas")));
        return contexto;
    }
}

public class SignalRChatRealtimeServicePruebas
{
    [Fact]
    public async Task Expulsion_RevocaTodasLasConexionesDelUsuarioSoloEnEseGrupo()
    {
        var grupoId = Guid.NewGuid();
        var otroGrupoId = Guid.NewGuid();
        var registro = new ChatConnectionRegistry();
        registro.RegistrarConexion("movil", "expulsado");
        registro.RegistrarConexion("web", "expulsado");
        registro.RegistrarConexion("otro-grupo", "expulsado");
        registro.RegistrarGrupo("movil", grupoId);
        registro.RegistrarGrupo("web", grupoId);
        registro.RegistrarGrupo("otro-grupo", otroGrupoId);

        var proxy = new Mock<IClientProxy>();
        proxy.Setup(p => p.SendCoreAsync(
                It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var clientes = new Mock<IHubClients>();
        clientes.Setup(c => c.Clients(It.IsAny<IReadOnlyList<string>>())).Returns(proxy.Object);
        clientes.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
        var grupos = new Mock<IGroupManager>();
        grupos.Setup(g => g.RemoveFromGroupAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var contexto = new Mock<IHubContext<ChatHub>>();
        contexto.SetupGet(c => c.Clients).Returns(clientes.Object);
        contexto.SetupGet(c => c.Groups).Returns(grupos.Object);
        var servicio = new SignalRChatRealtimeService(contexto.Object, registro);

        await servicio.UsuarioExpulsadoDelGrupo(
            grupoId, Guid.NewGuid(), "expulsado", "Grupo");

        grupos.Verify(g => g.RemoveFromGroupAsync(
            "movil", $"chat:{grupoId:D}", It.IsAny<CancellationToken>()), Times.Once);
        grupos.Verify(g => g.RemoveFromGroupAsync(
            "web", $"chat:{grupoId:D}", It.IsAny<CancellationToken>()), Times.Once);
        grupos.Verify(g => g.RemoveFromGroupAsync(
            "otro-grupo", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        registro.ObtenerConexiones("expulsado", grupoId).Should().BeEmpty();
        registro.ObtenerConexiones("expulsado", otroGrupoId).Should().Equal("otro-grupo");
    }
}
