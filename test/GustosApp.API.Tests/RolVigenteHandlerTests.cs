using System.Security.Claims;
using FluentAssertions;
using GustosApp.API.Middleware;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using GustosApp.API.Controllers;

namespace GustosApp.API.Tests;

public sealed class RolVigenteHandlerTests
{
    [Fact]
    public void MetricasDelRestaurante_ExigeRolDePropietarioVigente()
    {
        var atributo = typeof(RestaurantesController)
            .GetMethod(nameof(RestaurantesController.ObtenerMetricas))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Single(x => x.Policy is not null);

        atributo.Policy.Should().Be("DuenoRestaurante");
    }

    [Fact]
    public async Task RolRetirado_NoAutorizaAunqueElTokenConserveElClaimAdmin()
    {
        var (contexto, firebase, usuarios) = CrearContexto(RolUsuario.Usuario);
        var requisito = new RolVigenteRequirement(RolUsuario.Admin);
        var handler = new RolVigenteHandler(firebase.Object, usuarios.Object, NullLogger<RolVigenteHandler>.Instance);

        await handler.HandleAsync(contexto);

        contexto.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task TokenRevocado_NoAutorizaUnRolVigente()
    {
        var (contexto, firebase, usuarios) = CrearContexto(RolUsuario.Admin);
        firebase.Setup(x => x.VerifyIdTokenAsync("token-viejo", true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("revocado"));
        var handler = new RolVigenteHandler(firebase.Object, usuarios.Object, NullLogger<RolVigenteHandler>.Instance);

        await handler.HandleAsync(contexto);

        contexto.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task TokenYRolVigentes_AutorizanLaOperacion()
    {
        var (contexto, firebase, usuarios) = CrearContexto(RolUsuario.Admin);
        var handler = new RolVigenteHandler(firebase.Object, usuarios.Object, NullLogger<RolVigenteHandler>.Instance);

        await handler.HandleAsync(contexto);

        contexto.HasSucceeded.Should().BeTrue();
    }

    private static (AuthorizationHandlerContext Contexto, Mock<IFirebaseAuthService> Firebase, Mock<IUsuarioRepository> Usuarios)
        CrearContexto(RolUsuario rolActual)
    {
        var requisito = new RolVigenteRequirement(RolUsuario.Admin);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("user_id", "uid-1"),
            new Claim("rol", "Admin")
        ], "Bearer"));
        var http = new DefaultHttpContext();
        http.User = principal;
        http.Request.Headers.Authorization = "Bearer token-viejo";

        var firebase = new Mock<IFirebaseAuthService>();
        firebase.Setup(x => x.VerifyIdTokenAsync("token-viejo", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DecodedToken { Uid = "uid-1", Claims = [] });
        var usuarios = new Mock<IUsuarioRepository>();
        usuarios.Setup(x => x.GetByFirebaseUidAsync("uid-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Usuario { FirebaseUid = "uid-1", Rol = rolActual });

        return (new AuthorizationHandlerContext([requisito], principal, http), firebase, usuarios);
    }
}
