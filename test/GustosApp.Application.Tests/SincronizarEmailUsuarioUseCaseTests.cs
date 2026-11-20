using FluentAssertions;
using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.UsuarioUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;

namespace GustosApp.Application.Tests;

public sealed class SincronizarEmailUsuarioUseCaseTests
{
    private readonly Mock<IUsuarioRepository> _usuarios = new();
    private readonly Mock<IFirebaseAuthService> _firebase = new();

    [Fact]
    public async Task EmailConfirmado_ActualizaLaMismaCuentaYSusRelaciones()
    {
        var usuario = CrearUsuario();
        var id = usuario.Id;
        Preparar(usuario, "nuevo@test.com");

        var resultado = await CrearCaso().HandleAsync(usuario.FirebaseUid, default);

        resultado.Id.Should().Be(id);
        resultado.Email.Should().Be("nuevo@test.com");
        _usuarios.Verify(x => x.AddAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()), Times.Never);
        _usuarios.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReintentoConEmailSincronizado_NoEscribeNuevamente()
    {
        var usuario = CrearUsuario();
        Preparar(usuario, usuario.Email);

        await CrearCaso().HandleAsync(usuario.FirebaseUid, default);

        _usuarios.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ErrorSql_ConservaUidYPermiteReintento()
    {
        var usuario = CrearUsuario();
        Preparar(usuario, "nuevo@test.com");
        _usuarios.SetupSequence(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("SQL"))
            .Returns(Task.CompletedTask);
        var caso = CrearCaso();

        await FluentActions.Invoking(() => caso.HandleAsync(usuario.FirebaseUid, default))
            .Should().ThrowAsync<IOException>();
        usuario.Email.Should().Be("anterior@test.com");
        (await caso.HandleAsync(usuario.FirebaseUid, default)).Id.Should().Be(usuario.Id);
        usuario.Email.Should().Be("nuevo@test.com");
    }

    [Fact]
    public async Task EmailDeOtroUid_NoSeApropiaDeLaCuenta()
    {
        var usuario = CrearUsuario();
        Preparar(usuario, "ocupado@test.com");
        _usuarios.Setup(x => x.GetByEmailAsync("ocupado@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid(), FirebaseUid = "otro" });

        await FluentActions.Invoking(() => CrearCaso().HandleAsync(usuario.FirebaseUid, default))
            .Should().ThrowAsync<InvalidOperationException>();
        usuario.Email.Should().Be("anterior@test.com");
    }

    private void Preparar(Usuario usuario, string email)
    {
        _usuarios.Setup(x => x.GetByFirebaseUidAsync(usuario.FirebaseUid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _firebase.Setup(x => x.GetUserByUidAsync(usuario.FirebaseUid))
            .ReturnsAsync(new FirebaseUserInfo { Uid = usuario.FirebaseUid, Email = email });
    }

    private SincronizarEmailUsuarioUseCase CrearCaso() => new(_usuarios.Object, _firebase.Object);

    private static Usuario CrearUsuario() => new()
    {
        Id = Guid.NewGuid(), FirebaseUid = "uid", Email = "anterior@test.com",
    };
}
