using FluentAssertions;
using GustosApp.API.Tests.Infraestructura;
using GustosApp.Domain.Model;
using GustosApp.Infraestructure.Repositories;

namespace GustosApp.API.Tests.Repositorios
{
    public class GustosGrupoRepositoryPruebas
    {
        [Fact]
        public async Task ObtenerGustosDelGrupo_ExcluyeGustosDeMiembrosQueNoParticipan()
        {
            await using var contexto = DbContextEnMemoria.Crear(
                nameof(ObtenerGustosDelGrupo_ExcluyeGustosDeMiembrosQueNoParticipan));

            var participante = CrearUsuario("participante");
            var noParticipante = CrearUsuario("no-participante");
            var grupo = new Grupo("Grupo de prueba", participante.Id)
            {
                Administrador = participante
            };
            var miembroParticipante = new MiembroGrupo(grupo.Id, participante.Id, true)
            {
                Grupo = grupo,
                Usuario = participante
            };
            var miembroNoParticipante = new MiembroGrupo(grupo.Id, noParticipante.Id)
            {
                Grupo = grupo,
                Usuario = noParticipante,
                ParticipaEnRecomendacion = false
            };
            var pizza = contexto.Gustos.Single(gusto => gusto.Nombre == "Pizza");
            var sushi = contexto.Gustos.Single(gusto => gusto.Nombre == "Sushi");

            contexto.Grupos.Add(grupo);
            contexto.MiembrosGrupos.AddRange(miembroParticipante, miembroNoParticipante);
            contexto.GrupoGustos.AddRange(
                CrearGrupoGusto(grupo, miembroParticipante, pizza),
                CrearGrupoGusto(grupo, miembroNoParticipante, sushi));
            await contexto.SaveChangesAsync();

            var repositorio = new GustosGrupoRepositoryEF(contexto);

            var resultado = await repositorio.ObtenerGustosDelGrupo(grupo.Id);

            resultado.Should().ContainSingle().Which.Should().Be("Pizza");
        }

        private static Usuario CrearUsuario(string identificador)
        {
            return new Usuario(
                $"firebase-{identificador}",
                $"{identificador}@prueba.com",
                "Nombre",
                "Apellido",
                identificador);
        }

        private static GrupoGusto CrearGrupoGusto(
            Grupo grupo,
            MiembroGrupo miembro,
            Gusto gusto)
        {
            return new GrupoGusto
            {
                Id = Guid.NewGuid(),
                GrupoId = grupo.Id,
                Grupo = grupo,
                MiembroId = miembro.Id,
                Miembro = miembro,
                GustoId = gusto.Id,
                Gusto = gusto
            };
        }
    }
}
