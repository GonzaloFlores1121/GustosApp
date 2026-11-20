using FluentAssertions;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.RestauranteUseCases.OpinionesRestaurantes;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;

namespace GustosApp.Application.Tests;

public sealed class ObtenerHistorialOpinionesUsuarioUseCaseTests
{
    private readonly Mock<IUsuarioRepository> _usuarios = new();
    private readonly Mock<ISolicitudAmistadRepository> _amistades = new();
    private readonly Mock<IOpinionRestauranteRepository> _opiniones = new();

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task TitularOPerfilPublico_PuedeVerHistorial(bool esPrivado, bool mismoUsuario)
    {
        var solicitante = Usuario("solicitante", esPrivado: false);
        var objetivo = mismoUsuario ? solicitante : Usuario("objetivo", esPrivado);
        PrepararUsuarios(solicitante, objetivo);
        _opiniones.Setup(x => x.ObtenerPorUsuarioAsync(objetivo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var sut = CrearSut();

        var resultado = await sut.HandleAsync(solicitante.FirebaseUid, objetivo.Id, default);

        resultado.Should().BeEmpty();
    }

    [Fact]
    public async Task PerfilPrivado_AmigoAceptadoPuedeVerHistorial()
    {
        var solicitante = Usuario("solicitante", false);
        var objetivo = Usuario("objetivo", true);
        PrepararUsuarios(solicitante, objetivo);
        var amistad = new SolicitudAmistad(solicitante.Id, objetivo.Id);
        amistad.Aceptar();
        _amistades.Setup(x => x.GetAmistadEntreUsuariosAsync(solicitante.Id, objetivo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(amistad);
        _opiniones.Setup(x => x.ObtenerPorUsuarioAsync(objetivo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var resultado = await CrearSut().HandleAsync(solicitante.FirebaseUid, objetivo.Id, default);

        resultado.Should().BeEmpty();
    }

    [Theory]
    [InlineData("ninguna")]
    [InlineData("pendiente")]
    [InlineData("rechazada")]
    public async Task PerfilPrivado_SinAmistadAceptadaNoExponeHistorial(string estado)
    {
        var solicitante = Usuario("solicitante", false);
        var objetivo = Usuario("objetivo", true);
        PrepararUsuarios(solicitante, objetivo);
        SolicitudAmistad? amistad = null;
        if (estado != "ninguna")
        {
            amistad = new SolicitudAmistad(solicitante.Id, objetivo.Id);
            if (estado == "rechazada") amistad.Rechazar();
        }
        _amistades.Setup(x => x.GetAmistadEntreUsuariosAsync(solicitante.Id, objetivo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(amistad);

        var accion = () => CrearSut().HandleAsync(solicitante.FirebaseUid, objetivo.Id, default);

        await accion.Should().ThrowAsync<AccesoProhibidoException>();
        _opiniones.Verify(x => x.ObtenerPorUsuarioAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PerfilPrivado_MiembroDeGrupoSinAmistad_NoObtieneAccesoAlHistorial()
    {
        var solicitante = Usuario("miembro-grupo", false);
        var objetivo = Usuario("perfil-privado", true);
        PrepararUsuarios(solicitante, objetivo);
        _amistades.Setup(x => x.GetAmistadEntreUsuariosAsync(solicitante.Id, objetivo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SolicitudAmistad?)null);

        var accion = () => CrearSut().HandleAsync(solicitante.FirebaseUid, objetivo.Id, default);

        await accion.Should().ThrowAsync<AccesoProhibidoException>();
        _opiniones.Verify(x => x.ObtenerPorUsuarioAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SinSesionValida_NoConsultaElHistorial()
    {
        _usuarios.Setup(x => x.GetByFirebaseUidAsync("desconocido", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Usuario?)null);

        var accion = () => CrearSut().HandleAsync("desconocido", Guid.NewGuid(), default);

        await accion.Should().ThrowAsync<UnauthorizedAccessException>();
        _opiniones.Verify(x => x.ObtenerPorUsuarioAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private ObtenerHistorialOpinionesUsuarioUseCase CrearSut() =>
        new(_usuarios.Object, _amistades.Object, _opiniones.Object);

    private void PrepararUsuarios(Usuario solicitante, Usuario objetivo)
    {
        _usuarios.Setup(x => x.GetByFirebaseUidAsync(solicitante.FirebaseUid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitante);
        _usuarios.Setup(x => x.GetByIdAsync(objetivo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(objetivo);
    }

    private static Usuario Usuario(string uid, bool esPrivado) => new()
    {
        Id = Guid.NewGuid(), FirebaseUid = uid, EsPrivado = esPrivado,
        Email = $"{uid}@example.test", Nombre = uid, Apellido = "Prueba", IdUsuario = uid,
    };
}
