using FluentAssertions;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.RestauranteUseCases.OpinionesRestaurantes;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;

namespace GustosApp.Application.Tests;

public class GestionarOpinionRestauranteUseCasesTests
{
    private readonly Mock<IFileStorageService> _archivos = new();
    private readonly Mock<IOpinionRestauranteRepository> _opiniones = new();
    private readonly Mock<IUsuarioRepository> _usuarios = new();
    private readonly Usuario _autor = new() { Id = Guid.NewGuid(), FirebaseUid = "autor", Email = "autor@test.com", Nombre = "Autor" };

    [Fact]
    public async Task Actualizar_ConservaFotosYFechaDePublicacion_CuandoSoloCambiaTexto()
    {
        var foto = new OpinionFoto { Id = Guid.NewGuid(), Url = "foto-vieja" };
        var fechaCreacion = DateTime.UtcNow.AddDays(-10);
        var opinion = NuevaOpinion(_autor.Id, foto, fechaCreacion);
        ConfigurarAutor(opinion);

        var sut = new ActualizarOpinionRestauranteUseCase(_archivos.Object, _opiniones.Object, _usuarios.Object);
        var fechaVisita = DateTime.UtcNow.AddDays(-2);
        await sut.HandleAsync("autor", opinion.Id, 4.5, "Texto actualizado", "Nuevo título",
            new[] { foto.Id }, Array.Empty<FileUpload>(), "pareja", fechaVisita, default);

        opinion.Fotos.Should().ContainSingle().Which.Url.Should().Be("foto-vieja");
        opinion.FechaCreacion.Should().Be(fechaCreacion);
        opinion.FechaVisita.Should().Be(fechaVisita);
        opinion.Valoracion.Should().Be(4.5);
        _opiniones.Verify(r => r.ActualizarAsync(opinion, default), Times.Once);
        _archivos.Verify(a => a.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Actualizar_RechazaUsuarioQueNoEsAutor()
    {
        var opinion = NuevaOpinion(Guid.NewGuid(), new OpinionFoto { Id = Guid.NewGuid(), Url = "foto" }, DateTime.UtcNow);
        ConfigurarAutor(opinion);
        var sut = new ActualizarOpinionRestauranteUseCase(_archivos.Object, _opiniones.Object, _usuarios.Object);

        var act = () => sut.HandleAsync("autor", opinion.Id, 5, "Texto válido", "Título",
            opinion.Fotos.Select(f => f.Id).ToArray(), Array.Empty<FileUpload>(), "solo", DateTime.UtcNow, default);

        await act.Should().ThrowAsync<AccesoProhibidoException>();
        _opiniones.Verify(r => r.ActualizarAsync(It.IsAny<OpinionRestaurante>(), default), Times.Never);
    }

    [Fact]
    public async Task Actualizar_RechazaOpinionImportada()
    {
        var opinion = NuevaOpinion(_autor.Id, new OpinionFoto { Id = Guid.NewGuid(), Url = "foto" }, DateTime.UtcNow);
        opinion.EsImportada = true;
        ConfigurarAutor(opinion);
        var sut = new ActualizarOpinionRestauranteUseCase(_archivos.Object, _opiniones.Object, _usuarios.Object);

        var act = () => sut.HandleAsync("autor", opinion.Id, 5, "Texto válido", "Título",
            opinion.Fotos.Select(f => f.Id).ToArray(), Array.Empty<FileUpload>(), "solo", DateTime.UtcNow, default);

        await act.Should().ThrowAsync<AccesoProhibidoException>();
    }

    [Fact]
    public async Task Actualizar_SiFallaCarga_NoQuitaFotosExistentes()
    {
        var foto = new OpinionFoto { Id = Guid.NewGuid(), Url = "foto-vieja" };
        var opinion = NuevaOpinion(_autor.Id, foto, DateTime.UtcNow);
        ConfigurarAutor(opinion);
        _archivos.Setup(a => a.UploadFileAsync(It.IsAny<Stream>(), "nueva.jpg", "opiniones"))
            .ThrowsAsync(new IOException("falló storage"));
        var sut = new ActualizarOpinionRestauranteUseCase(_archivos.Object, _opiniones.Object, _usuarios.Object);

        var act = () => sut.HandleAsync("autor", opinion.Id, 4, "Texto válido", "Título",
            Array.Empty<Guid>(), new[] { Archivo("nueva.jpg") }, "familia", DateTime.UtcNow, default);

        await act.Should().ThrowAsync<IOException>();
        opinion.Fotos.Should().ContainSingle().Which.Should().BeSameAs(foto);
        _opiniones.Verify(r => r.ActualizarAsync(It.IsAny<OpinionRestaurante>(), default), Times.Never);
    }

    [Fact]
    public async Task Eliminar_ComoAutor_EliminaYPermiteUnaPublicacionPosterior()
    {
        var opinion = NuevaOpinion(_autor.Id, new OpinionFoto { Id = Guid.NewGuid(), Url = "foto" }, DateTime.UtcNow);
        ConfigurarAutor(opinion);
        _opiniones.Setup(r => r.FotoEstaReferenciadaAsync("foto", default)).ReturnsAsync(false);
        var sut = new EliminarOpinionRestauranteUseCase(_archivos.Object, _opiniones.Object, _usuarios.Object);

        await sut.HandleAsync("autor", opinion.Id, default);

        _opiniones.Verify(r => r.EliminarAsync(opinion, default), Times.Once);
        _archivos.Verify(a => a.DeleteFileAsync("foto"), Times.Once);
    }

    [Fact]
    public async Task Eliminar_NoBorraArchivoCompartido()
    {
        var opinion = NuevaOpinion(_autor.Id, new OpinionFoto { Id = Guid.NewGuid(), Url = "compartida" }, DateTime.UtcNow);
        ConfigurarAutor(opinion);
        _opiniones.Setup(r => r.FotoEstaReferenciadaAsync("compartida", default)).ReturnsAsync(true);
        var sut = new EliminarOpinionRestauranteUseCase(_archivos.Object, _opiniones.Object, _usuarios.Object);

        await sut.HandleAsync("autor", opinion.Id, default);

        _archivos.Verify(a => a.DeleteFileAsync(It.IsAny<string>()), Times.Never);
    }

    private void ConfigurarAutor(OpinionRestaurante opinion)
    {
        _usuarios.Setup(r => r.GetByFirebaseUidAsync("autor", default)).ReturnsAsync(_autor);
        _opiniones.Setup(r => r.ObtenerPorIdAsync(opinion.Id, default)).ReturnsAsync(opinion);
    }

    private static OpinionRestaurante NuevaOpinion(Guid usuarioId, OpinionFoto foto, DateTime fechaCreacion)
        => new()
        {
            Id = Guid.NewGuid(),
            RestauranteId = Guid.NewGuid(),
            UsuarioId = usuarioId,
            FechaCreacion = fechaCreacion,
            FechaVisita = fechaCreacion.AddDays(-1),
            Valoracion = 3,
            Fotos = new List<OpinionFoto> { foto }
        };

    private static FileUpload Archivo(string nombre)
        => new() { FileName = nombre, Content = new MemoryStream(new byte[] { 1, 2, 3 }) };
}
