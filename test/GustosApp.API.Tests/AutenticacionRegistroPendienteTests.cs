using System.Security.Claims;
using FluentAssertions;
using GustosApp.API.Controllers;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GustosApp.API.Tests;

public sealed class AutenticacionRegistroPendienteTests
{
    [Fact]
    public async Task RefrescarPermisosSinFicha_NoCreaUsuarioProvisional()
    {
        var usuarios = new Mock<IUsuarioRepository>();
        var firebase = new Mock<IFirebaseAuthService>();
        usuarios.Setup(x => x.GetByFirebaseUidAsync("uid", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Usuario?)null);
        var controller = new AutenticacionController(firebase.Object, usuarios.Object,
            NullLogger<AutenticacionController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("user_id", "uid")], "test")),
                },
            },
        };

        var respuesta = await controller.RefreshClaims(default);

        respuesta.Should().BeOfType<ConflictObjectResult>();
        usuarios.Verify(x => x.AddAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()), Times.Never);
        firebase.Verify(x => x.SetUserRoleAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
