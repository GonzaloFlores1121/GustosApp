using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.RestauranteUseCases.SolicitudRestauranteUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using Moq;

namespace GustosApp.Application.Tests;

public class ReclamarRestaurantePruebas
{
    private readonly Mock<ISolicitudRestauranteRepository> solicitudes = new();
    private readonly Mock<IRestauranteRepository> restaurantes = new();
    private readonly Mock<IUsuarioRepository> usuarios = new();
    private readonly Mock<IFirebaseAuthService> firebase = new();
    private readonly Usuario usuario = new() { Id = Guid.NewGuid(), FirebaseUid = "solicitante", Rol = RolUsuario.Usuario };
    private readonly Restaurante restaurante = new() { Id = Guid.NewGuid(), Nombre = "Importado", PlaceId = "google-id" };

    private static DatosReclamo DatosValidos() => new("Ana Pérez", "Propietaria", "1122334455", true, "%PDF-1.4 comprobante"u8.ToArray(), "prueba.pdf");

    [Theory]
    [InlineData("nombre")]
    [InlineData("relacion")]
    [InlineData("telefono")]
    [InlineData("autorizacion")]
    [InlineData("vacio")]
    [InlineData("grande")]
    [InlineData("formato")]
    public async Task Reclamar_DatosInvalidos_NoPersisteNiCambiaRol(string campo)
    {
        var datos = DatosValidos();
        datos = campo switch
        {
            "nombre" => datos with { NombreSolicitante = " " },
            "relacion" => datos with { RelacionRestaurante = " " },
            "telefono" => datos with { TelefonoContacto = "abc" },
            "autorizacion" => datos with { DeclaraAutorizacion = false },
            "vacio" => datos with { Comprobante = [] },
            "grande" => datos with { Comprobante = new byte[DatosReclamo.LimiteBytes + 1] },
            _ => datos with { Comprobante = "<html>archivo disfrazado</html>"u8.ToArray() }
        };
        await Assert.ThrowsAsync<ArgumentException>(() => CrearCaso().HandleAsync(usuario.FirebaseUid, restaurante.Id, default, datos));
        solicitudes.Verify(r => r.AddAsync(It.IsAny<SolicitudRestaurante>(), default), Times.Never);
        Assert.Equal(RolUsuario.Usuario, usuario.Rol);
        firebase.VerifyNoOtherCalls();
    }

    private ReclamarRestauranteUseCase CrearCaso()
    {
        usuarios.Setup(r => r.GetByFirebaseUidAsync(usuario.FirebaseUid, default)).ReturnsAsync(usuario);
        restaurantes.Setup(r => r.GetRestauranteByIdAsync(restaurante.Id, default)).ReturnsAsync(restaurante);
        return new(solicitudes.Object, restaurantes.Object, usuarios.Object);
    }

    [Fact]
    public async Task Reclamar_CreaSolicitudPendienteSinAsignarPropietario()
    {
        var caso = CrearCaso();
        var id = await caso.HandleAsync(usuario.FirebaseUid, restaurante.Id, default, DatosValidos());
        solicitudes.Verify(r => r.AddAsync(It.Is<SolicitudRestaurante>(s => s.Id == id
            && s.RestauranteExistenteId == restaurante.Id && s.UsuarioId == usuario.Id
            && s.Estado == EstadoSolicitudRestaurante.Pendiente && s.Nombre == restaurante.Nombre), default), Times.Once);
        Assert.Null(restaurante.DuenoId);
        Assert.Equal(RolUsuario.Usuario, usuario.Rol);
        firebase.VerifyNoOtherCalls();
        restaurantes.Verify(r => r.AddAsync(It.IsAny<Restaurante>(), default), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reclamar_FichaConPropietario_NoCreaSolicitud(bool usaDuenoId)
    {
        var caso = CrearCaso();
        restaurante.DuenoId = usaDuenoId ? Guid.NewGuid() : null;
        restaurante.PropietarioUid = usaDuenoId ? "" : "anterior";
        await Assert.ThrowsAsync<InvalidOperationException>(() => caso.HandleAsync(usuario.FirebaseUid, restaurante.Id, default, DatosValidos()));
        solicitudes.Verify(r => r.AddAsync(It.IsAny<SolicitudRestaurante>(), default), Times.Never);
        firebase.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Reclamar_DuenoNoCreaSolicitud()
    {
        var caso = CrearCaso();
        usuario.Rol = RolUsuario.DuenoRestaurante;
        await Assert.ThrowsAsync<InvalidOperationException>(() => caso.HandleAsync(usuario.FirebaseUid, restaurante.Id, default, DatosValidos()));
        solicitudes.Verify(r => r.AddAsync(It.IsAny<SolicitudRestaurante>(), default), Times.Never);
    }

    [Fact]
    public async Task Reclamar_ReintentoDevuelveSolicitudExistente()
    {
        var caso = CrearCaso();
        var pendiente = new SolicitudRestaurante { Id = Guid.NewGuid() };
        solicitudes.Setup(r => r.BuscarReclamoPendienteAsync(usuario.Id, restaurante.Id, default)).ReturnsAsync(pendiente);
        Assert.Equal(pendiente.Id, await caso.HandleAsync(usuario.FirebaseUid, restaurante.Id, default, DatosValidos()));
        solicitudes.Verify(r => r.AddAsync(It.IsAny<SolicitudRestaurante>(), default), Times.Never);
        firebase.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Reclamar_OtraSolicitudPendiente_NoCreaUnaSegunda()
    {
        solicitudes.Setup(r => r.BuscarPendientePorUsuarioAsync(usuario.Id, default))
            .ReturnsAsync(new SolicitudRestaurante { Id = Guid.NewGuid() });
        await Assert.ThrowsAsync<InvalidOperationException>(() => CrearCaso().HandleAsync(usuario.FirebaseUid, restaurante.Id, default, DatosValidos()));
        solicitudes.Verify(r => r.AddAsync(It.IsAny<SolicitudRestaurante>(), default), Times.Never);
        Assert.Equal(RolUsuario.Usuario, usuario.Rol);
    }

    [Fact]
    public async Task Reclamar_FichaInexistente_DevuelveNoEncontrado()
    {
        var caso = CrearCaso();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => caso.HandleAsync(usuario.FirebaseUid, Guid.NewGuid(), default, DatosValidos()));
        solicitudes.Verify(r => r.AddAsync(It.IsAny<SolicitudRestaurante>(), default), Times.Never);
    }
}
