using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GustosApp.API.DTO;
using GustosApp.Domain.Model;
using GustosApp.Infraestructure;
using Microsoft.Extensions.DependencyInjection;

namespace GustosApp.API.Tests;

public sealed class FlujoVotacionPruebas
{
    private const string FirebaseUid = "usuario-pruebas-integracion";

    [Fact]
    public async Task FlujoCompleto_SeleccionarParticipantesIniciarYUltimoVotoCierraConGanador()
    {
        await using var fabrica = new FabricaApiGustosAppVotacion();
        var datos = await PrepararEscenarioAsync(fabrica, usuarioAutenticadoEsAdministrador: true, participa: false);
        using var cliente = fabrica.CreateClient();

        var respuestaSeleccion = await cliente.PutAsync(
            $"/Grupo/activarMiembro?grupoId={datos.GrupoId}&UsuarioId={datos.UsuarioId}",
            content: null);
        respuestaSeleccion.StatusCode.Should().Be(HttpStatusCode.OK);

        var respuestaInicio = await cliente.PostAsJsonAsync("/Votacion/iniciar", new IniciarVotacionRequest
        {
            GrupoId = datos.GrupoId,
            Descripcion = "Almuerzo de integración",
            RestaurantesCandidatos = [datos.RestauranteId, datos.OtroRestauranteId]
        });
        respuestaInicio.StatusCode.Should().Be(HttpStatusCode.OK);
        var votacion = await respuestaInicio.Content.ReadFromJsonAsync<VotacionResponse>();
        votacion.Should().NotBeNull();

        await using (var alcance = fabrica.Services.CreateAsyncScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<GustosDbContext>();
            contexto.Votos.Add(new VotoRestaurante(
                votacion!.Id,
                datos.OtroUsuarioId,
                datos.RestauranteId));
            await contexto.SaveChangesAsync();
        }

        var respuestaVoto = await cliente.PostAsJsonAsync($"/Votacion/{votacion!.Id}/votar", new RegistrarVotoRequest
        {
            RestauranteId = datos.RestauranteId,
            Comentario = "Mi elección"
        });
        respuestaVoto.StatusCode.Should().Be(HttpStatusCode.OK);

        var respuestaResultados = await cliente.GetAsync($"/Votacion/{votacion.Id}/resultados");
        respuestaResultados.StatusCode.Should().Be(HttpStatusCode.OK);
        var resultados = await respuestaResultados.Content.ReadFromJsonAsync<ResultadoVotacionResponse>();
        resultados.Should().NotBeNull();
        resultados!.TotalVotos.Should().Be(2);
        resultados.TodosVotaron.Should().BeTrue();
        resultados.GanadorId.Should().Be(datos.RestauranteId);
        resultados.Estado.Should().Be("Cerrada");
    }

    [Fact]
    public async Task ObtenerResultados_UsuarioFueraDelGrupo_DevuelveProhibido()
    {
        await using var fabrica = new FabricaApiGustosAppVotacion();
        var datos = await PrepararEscenarioAsync(fabrica, usuarioAutenticadoEsAdministrador: false, participa: false);
        Guid votacionId;

        await using (var alcance = fabrica.Services.CreateAsyncScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<GustosDbContext>();
            var votacion = new VotacionGrupo(datos.GrupoId);
            contexto.Votaciones.Add(votacion);
            await contexto.SaveChangesAsync();
            votacionId = votacion.Id;
        }

        using var cliente = fabrica.CreateClient();
        var respuesta = await cliente.GetAsync($"/Votacion/{votacionId}/resultados");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SeleccionarGanadorRuleta_UsuarioNoAdministrador_DevuelveProhibido()
    {
        await using var fabrica = new FabricaApiGustosAppVotacion();
        var datos = await PrepararEscenarioAsync(fabrica, usuarioAutenticadoEsAdministrador: false, participa: true);
        Guid votacionId;

        await using (var alcance = fabrica.Services.CreateAsyncScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<GustosDbContext>();
            var votacion = new VotacionGrupo(datos.GrupoId);
            contexto.Votaciones.Add(votacion);
            await contexto.SaveChangesAsync();
            votacionId = votacion.Id;
        }

        using var cliente = fabrica.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync(
            $"/Votacion/{votacionId}/seleccionar-ganador",
            new SeleccionarGanadorRequest { RestauranteGanadorId = datos.RestauranteId });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<DatosEscenario> PrepararEscenarioAsync(
        FabricaApiGustosAppVotacion fabrica,
        bool usuarioAutenticadoEsAdministrador,
        bool participa)
    {
        await using var alcance = fabrica.Services.CreateAsyncScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<GustosDbContext>();

        var usuario = new Usuario(FirebaseUid, "integracion@example.com", "Usuario", "Prueba", $"usuario-{Guid.NewGuid():N}");
        var administrador = usuarioAutenticadoEsAdministrador
            ? usuario
            : new Usuario($"admin-{Guid.NewGuid():N}", "admin@example.com", "Admin", "Prueba", $"admin-{Guid.NewGuid():N}");
        var grupo = new Grupo("Grupo de integración", administrador.Id);
        var restaurante = new Restaurante
        {
            Id = Guid.NewGuid(),
            Nombre = "Restaurante de prueba",
            NombreNormalizado = "restaurante de prueba",
            Direccion = "Calle de prueba 123",
            PlaceId = $"place-{Guid.NewGuid():N}"
        };
        var otroRestaurante = new Restaurante
        {
            Id = Guid.NewGuid(),
            Nombre = "Otro restaurante de prueba",
            NombreNormalizado = "otro restaurante de prueba",
            Direccion = "Otra calle 456",
            PlaceId = $"place-{Guid.NewGuid():N}"
        };
        var otroUsuario = new Usuario(
            $"participante-{Guid.NewGuid():N}",
            "participante@example.com",
            "Otro",
            "Participante",
            $"participante-{Guid.NewGuid():N}");

        contexto.Usuarios.Add(usuario);
        contexto.Usuarios.Add(otroUsuario);
        if (!usuarioAutenticadoEsAdministrador)
            contexto.Usuarios.Add(administrador);
        contexto.Grupos.Add(grupo);

        if (usuarioAutenticadoEsAdministrador || participa)
        {
            contexto.MiembrosGrupos.Add(new MiembroGrupo(grupo.Id, usuario.Id)
            {
                ParticipaEnRecomendacion = participa
            });
        }

        contexto.MiembrosGrupos.Add(new MiembroGrupo(grupo.Id, otroUsuario.Id)
        {
            ParticipaEnRecomendacion = true
        });

        contexto.Restaurantes.Add(restaurante);
        contexto.Restaurantes.Add(otroRestaurante);
        await contexto.SaveChangesAsync();

        return new DatosEscenario(
            usuario.Id,
            otroUsuario.Id,
            grupo.Id,
            restaurante.Id,
            otroRestaurante.Id);
    }

    private sealed record DatosEscenario(
        Guid UsuarioId,
        Guid OtroUsuarioId,
        Guid GrupoId,
        Guid RestauranteId,
        Guid OtroRestauranteId);
}
