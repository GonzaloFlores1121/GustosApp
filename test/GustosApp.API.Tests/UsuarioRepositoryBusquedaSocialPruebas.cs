using FluentAssertions;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using GustosApp.Infraestructure;
using GustosApp.Infraestructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GustosApp.API.Tests;

public sealed class UsuarioRepositoryBusquedaSocialPruebas
{
    [Fact]
    public async Task PropietarioActivo_EsEncontrableConYSinTexto()
    {
        var opciones = new DbContextOptionsBuilder<GustosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new GustosDbContext(opciones);
        var actual = CrearUsuario("actual", RolUsuario.Usuario);
        var propietario = CrearUsuario("dueno-bistro", RolUsuario.DuenoRestaurante);
        db.Usuarios.AddRange(actual, propietario);
        await db.SaveChangesAsync();
        var repositorio = new UsuarioRepositoryEF(db);

        var inicial = await repositorio.GetAllExceptAsync(actual.Id, 50, default);
        var buscado = await repositorio.BuscarPorUsernameAsync("bistro", actual.Id, default);

        inicial.Should().ContainSingle(x => x.Id == propietario.Id);
        buscado.Should().ContainSingle(x => x.Id == propietario.Id);
    }

    private static Usuario CrearUsuario(string username, RolUsuario rol) => new()
    {
        Id = Guid.NewGuid(), FirebaseUid = $"uid-{username}", Email = $"{username}@example.test",
        Nombre = username, Apellido = "Prueba", IdUsuario = username, Rol = rol, Activo = true,
    };
}
