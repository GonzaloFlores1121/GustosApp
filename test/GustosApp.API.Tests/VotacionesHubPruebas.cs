using System.Security.Claims;
using FluentAssertions;
using GustosApp.API.Hubs;
using GustosApp.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace GustosApp.API.Tests;

public class VotacionesHubPruebas
{
    [Fact]
    public void Hub_RequiereAutenticacion()
    {
        typeof(VotacionesHub)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Should()
            .NotBeEmpty();
    }

    [Fact]
    public async Task OnConnectedAsync_MiembroActivo_SeUneAlCanalDelGrupo()
    {
        var grupoId = Guid.NewGuid();
        var repositorio = new Mock<IGrupoRepository>();
        var grupos = new Mock<IGroupManager>();
        repositorio
            .Setup(r => r.UsuarioEsMiembroAsync(grupoId, "firebase-uid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var (contexto, httpContexto) = CrearContexto(grupoId, "firebase-uid");
        var hub = new VotacionesHub(repositorio.Object)
        {
            Context = contexto.Object,
            Groups = grupos.Object
        };

        await hub.OnConnectedAsync();

        grupos.Verify(
            g => g.AddToGroupAsync("conexion-1", grupoId.ToString(), It.IsAny<CancellationToken>()),
            Times.Once);
        contexto.Verify(c => c.Abort(), Times.Never);
        httpContexto.Request.QueryString.Should().NotBeNull();
    }

    [Fact]
    public async Task OnConnectedAsync_UsuarioAjeno_AbortaYNoSeUneAlCanal()
    {
        var grupoId = Guid.NewGuid();
        var repositorio = new Mock<IGrupoRepository>();
        var grupos = new Mock<IGroupManager>();
        repositorio
            .Setup(r => r.UsuarioEsMiembroAsync(grupoId, "externo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var (contexto, _) = CrearContexto(grupoId, "externo");
        var hub = new VotacionesHub(repositorio.Object)
        {
            Context = contexto.Object,
            Groups = grupos.Object
        };

        await hub.OnConnectedAsync();

        contexto.Verify(c => c.Abort(), Times.Once);
        grupos.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static (Mock<HubCallerContext> Contexto, DefaultHttpContext HttpContexto) CrearContexto(
        Guid grupoId,
        string firebaseUid)
    {
        var httpContexto = new DefaultHttpContext();
        httpContexto.Request.QueryString = new QueryString($"?grupoId={grupoId}");
        var caracteristicas = new FeatureCollection();
        var caracteristicaHttp = new Mock<IHttpContextFeature>();
        caracteristicaHttp.SetupProperty(f => f.HttpContext, httpContexto);
        caracteristicas.Set(caracteristicaHttp.Object);

        var contexto = new Mock<HubCallerContext>();
        contexto.SetupGet(c => c.ConnectionId).Returns("conexion-1");
        contexto.SetupGet(c => c.ConnectionAborted).Returns(CancellationToken.None);
        contexto.SetupGet(c => c.Features).Returns(caracteristicas);
        contexto.SetupGet(c => c.User).Returns(new ClaimsPrincipal(
            new ClaimsIdentity([new Claim("user_id", firebaseUid)], "pruebas")));

        return (contexto, httpContexto);
    }
}
