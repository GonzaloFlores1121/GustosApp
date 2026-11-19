using FluentAssertions;
using GustosApp.Application.Services;
using GustosApp.Domain.Common;
using GustosApp.Domain.Model;

namespace GustosApp.Application.Tests
{
    public class EvaluadorCompatibilidadRestauranteTests
    {
        private readonly EvaluadorCompatibilidadRestaurante _evaluador = new();

        [Fact]
        public void Evaluar_CondicionCeliacaYPlatoConGluten_DevuelveIncompatible()
        {
            var preferencias = new UsuarioPreferencias
            {
                CondicionesMedicas = new List<string> { "Enfermedad celíaca" },
                ReglasCompatibilidad = [new(Guid.NewGuid(), false, ["gluten"])]
            };
            var restaurante = CrearRestauranteConTag("Gluten");

            var resultado = _evaluador.Evaluar(preferencias, restaurante);

            resultado.Should().Be(NivelCompatibilidadRestaurante.Incompatible);
        }

        [Fact]
        public void Evaluar_RestriccionRespetadaAunqueHayaPlatoConTag_DevuelveEstimada()
        {
            var preferencias = new UsuarioPreferencias
            {
                Restricciones = new List<string> { "Sin gluten" },
                ReglasCompatibilidad = [new(Guid.Empty, true, ["gluten"])]
            };
            var restaurante = CrearRestauranteConTag("Gluten");
            restaurante.RestriccionesQueRespeta.Add(new Restriccion { Nombre = "Sin gluten" });
            restaurante.ConfirmarRestriccionesDeclaradas(0, DateTime.UtcNow);

            var resultado = _evaluador.Evaluar(preferencias, restaurante);

            resultado.Should().Be(NivelCompatibilidadRestaurante.Estimada);
        }

        [Fact]
        public void Evaluar_SinDatosSuficientes_DevuelveDesconocida()
        {
            var preferencias = new UsuarioPreferencias
            {
                Restricciones = new List<string> { "Sin gluten" },
                ReglasCompatibilidad = [new(Guid.Empty, true, ["gluten"])]
            };
            var restaurante = new Restaurante
            {
                GustosQueSirve = new List<Gusto>(),
                RestriccionesQueRespeta = new List<Restriccion>()
            };

            var resultado = _evaluador.Evaluar(preferencias, restaurante);

            resultado.Should().Be(NivelCompatibilidadRestaurante.Desconocida);
        }

        [Fact]
        public void Evaluar_CondicionSinConfirmacionDirecta_DevuelveDesconocida()
        {
            var preferencias = new UsuarioPreferencias
            {
                CondicionesMedicas = new List<string> { "Alergia al huevo" },
                ReglasCompatibilidad = [new(Guid.NewGuid(), false, ["huevos"])]
            };
            var restaurante = new Restaurante
            {
                GustosQueSirve = new List<Gusto>(),
                RestriccionesQueRespeta = new List<Restriccion>()
            };

            var resultado = _evaluador.Evaluar(preferencias, restaurante);

            resultado.Should().Be(NivelCompatibilidadRestaurante.Desconocida);
        }

        [Fact]
        public void Evaluar_RestriccionDeclaradaHaceCuatroMeses_ConservaCompatibilidad()
        {
            var fechaActual = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
            var evaluador = new EvaluadorCompatibilidadRestaurante(
                new RelojFijo(fechaActual));
            var preferencias = new UsuarioPreferencias
            {
                Restricciones = new List<string> { "Sin gluten" },
                ReglasCompatibilidad = [new(Guid.Empty, true, ["gluten"])]
            };
            var restaurante = CrearRestauranteConTag("Gluten");
            restaurante.RestriccionesQueRespeta.Add(new Restriccion { Nombre = "Sin gluten" });
            restaurante.ConfirmarRestriccionesDeclaradas(0,
                fechaActual.UtcDateTime.AddMonths(-4));

            var resultado = evaluador.Evaluar(preferencias, restaurante);

            resultado.Should().Be(NivelCompatibilidadRestaurante.Estimada);
        }

        [Theory]
        [InlineData(2024, 1, 31, 2024, 5, 31)]
        [InlineData(2024, 10, 31, 2025, 2, 28)]
        public void Evaluar_DeclaracionConservaCompatibilidadAlCumplirCuatroMeses(
            int anio, int mes, int dia, int anioLimite, int mesLimite, int diaLimite)
        {
            var revision = new DateTime(anio, mes, dia, 12, 0, 0, DateTimeKind.Utc);
            var limite = new DateTime(anioLimite, mesLimite, diaLimite, 12, 0, 0, DateTimeKind.Utc);
            var reglaId = Guid.NewGuid();
            var preferencias = new UsuarioPreferencias
            {
                Restricciones = ["Sin gluten"],
                ReglasCompatibilidad = [new(reglaId, true, ["gluten"])]
            };
            var restaurante = CrearRestauranteConTag("Gluten");
            restaurante.RestriccionesQueRespeta.Add(new Restriccion
                { Id = reglaId, Nombre = "Sin gluten" });
            restaurante.ConfirmarRestriccionesDeclaradas(0, revision);

            new EvaluadorCompatibilidadRestaurante(new RelojFijo(
                new DateTimeOffset(limite.AddTicks(-1)))).Evaluar(preferencias, restaurante)
                .Should().Be(NivelCompatibilidadRestaurante.Estimada);
            new EvaluadorCompatibilidadRestaurante(new RelojFijo(
                new DateTimeOffset(limite))).Evaluar(preferencias, restaurante)
                .Should().Be(NivelCompatibilidadRestaurante.Estimada);
        }

        [Fact]
        public void Evaluar_UnaCondicionIncompatible_PrevaleceSobreOtraConfirmada()
        {
            var preferencias = new UsuarioPreferencias
            {
                CondicionesMedicas = new List<string>
                {
                    "Enfermedad celíaca",
                    "Alergia a mariscos"
                },
                ReglasCompatibilidad =
                [
                    new(Guid.NewGuid(), false, ["gluten"]),
                    new(Guid.NewGuid(), false, ["mariscos"])
                ]
            };
            var restaurante = CrearRestauranteConTag("Mariscos");
            restaurante.RestriccionesQueRespeta.Add(new Restriccion { Nombre = "Sin gluten" });

            var resultado = _evaluador.Evaluar(preferencias, restaurante);

            resultado.Should().Be(NivelCompatibilidadRestaurante.Incompatible);
        }

        [Fact]
        public void Evaluar_OfertaMixta_ConservaOpcionPotencialSinConfirmarla()
        {
            var preferencias = new UsuarioPreferencias
            {
                CondicionesMedicas = ["Vegetariano"],
                ReglasCompatibilidad = [new(Guid.NewGuid(), false, ["carne roja", "mariscos"])]
            };
            var restaurante = CrearRestauranteConTag("Carne roja");
            restaurante.GustosQueSirve.Add(new Gusto
            {
                Nombre = "Ensalada",
                Tags = [new Tag { Nombre = "Vegetal" }]
            });

            _evaluador.Evaluar(preferencias, restaurante)
                .Should().Be(NivelCompatibilidadRestaurante.Desconocida);
        }

        [Fact]
        public void Evaluar_OpcionesConConflictosDistintosParaDosMiembros_Excluye()
        {
            var preferencias = new UsuarioPreferencias
            {
                Restricciones = ["Sin gluten", "Sin mariscos"],
                ReglasCompatibilidad =
                [
                    new(Guid.NewGuid(), true, ["gluten"]),
                    new(Guid.NewGuid(), true, ["mariscos"])
                ]
            };
            var restaurante = CrearRestauranteConTag("Gluten");
            restaurante.GustosQueSirve.Add(new Gusto
            {
                Nombre = "Mariscos",
                Tags = [new Tag { Nombre = "Mariscos" }]
            });

            _evaluador.Evaluar(preferencias, restaurante)
                .Should().Be(NivelCompatibilidadRestaurante.Incompatible);
        }

        [Fact]
        public void Evaluar_DatosDeReglaAusentes_NoInfiereCompatibilidad()
        {
            var preferencias = new UsuarioPreferencias
            {
                Restricciones = ["Sin gluten"],
                ReglasCompatibilidad = [new(Guid.NewGuid(), true, [])]
            };
            var restaurante = new Restaurante
            {
                GustosQueSirve = [new Gusto { Nombre = "Pizza", Tags = [] }]
            };

            _evaluador.Evaluar(preferencias, restaurante)
                .Should().Be(NivelCompatibilidadRestaurante.Desconocida);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(-6)]
        [InlineData(-120)]
        public void Evaluar_FechasYRelojNoAlteranDeclaracionesNiCreanEvidencia(int? meses)
        {
            var fecha = new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
            var id = Guid.NewGuid();
            var preferencias = new UsuarioPreferencias {
                Restricciones = ["Sin gluten"],
                ReglasCompatibilidad = [new(id, true, ["gluten"])]
            };
            var restaurante = CrearRestauranteConTag("Gluten");
            restaurante.RestriccionesQueRespeta.Add(new Restriccion { Id = id, Nombre = "Sin gluten" });
            if (meses.HasValue) restaurante.ConfirmarRestriccionesDeclaradas(0, fecha.UtcDateTime.AddMonths(meses.Value));

            foreach (var ahora in new[] { fecha, fecha.AddYears(10) })
            {
                var evaluador = new EvaluadorCompatibilidadRestaurante(new RelojFijo(ahora));
                evaluador.Evaluar(preferencias, restaurante).Should().Be(NivelCompatibilidadRestaurante.Estimada);
                restaurante.RestriccionesQueRespeta.Clear();
                // La fecha histórica por sí sola nunca reemplaza una declaración eliminada.
                evaluador.Evaluar(preferencias, restaurante).Should().Be(NivelCompatibilidadRestaurante.Incompatible);
                restaurante.GustosQueSirve.Clear();
                evaluador.Evaluar(preferencias, restaurante).Should().Be(NivelCompatibilidadRestaurante.Desconocida);
                restaurante.GustosQueSirve = CrearRestauranteConTag("Gluten").GustosQueSirve;
                restaurante.RestriccionesQueRespeta.Add(new Restriccion { Id = id, Nombre = "Sin gluten" });
            }
        }

        private static Restaurante CrearRestauranteConTag(string nombreTag)
        {
            return new Restaurante
            {
                GustosQueSirve = new List<Gusto>
                {
                    new()
                    {
                        Nombre = "Plato",
                        Tags = new List<Tag> { new() { Nombre = nombreTag } }
                    }
                },
                RestriccionesQueRespeta = new List<Restriccion>()
            };
        }

        private sealed class RelojFijo(DateTimeOffset fechaActual) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => fechaActual;
        }
    }
}
