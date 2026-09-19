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
                CondicionesMedicas = new List<string> { "Enfermedad celíaca" }
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
                Restricciones = new List<string> { "Sin gluten" }
            };
            var restaurante = CrearRestauranteConTag("Gluten");
            restaurante.RestriccionesQueRespeta.Add(new Restriccion { Nombre = "Sin gluten" });

            var resultado = _evaluador.Evaluar(preferencias, restaurante);

            resultado.Should().Be(NivelCompatibilidadRestaurante.Estimada);
        }

        [Fact]
        public void Evaluar_SinDatosSuficientes_DevuelveDesconocida()
        {
            var preferencias = new UsuarioPreferencias
            {
                Restricciones = new List<string> { "Sin gluten" }
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
                CondicionesMedicas = new List<string> { "Alergia al huevo" }
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
        public void Evaluar_RestriccionVerificadaPeroDesactualizada_DevuelveDesconocida()
        {
            var fechaActual = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
            var evaluador = new EvaluadorCompatibilidadRestaurante(
                new RelojFijo(fechaActual));
            var preferencias = new UsuarioPreferencias
            {
                Restricciones = new List<string> { "Sin gluten" }
            };
            var restaurante = CrearRestauranteConTag("Gluten");
            restaurante.RestriccionesQueRespeta.Add(new Restriccion { Nombre = "Sin gluten" });
            restaurante.RegistrarVerificacionDatosCompatibilidad(
                OrigenDatosCompatibilidadRestaurante.Restaurante,
                fechaActual.UtcDateTime.AddDays(-181));

            var resultado = evaluador.Evaluar(preferencias, restaurante);

            resultado.Should().Be(NivelCompatibilidadRestaurante.Desconocida);
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
                }
            };
            var restaurante = CrearRestauranteConTag("Mariscos");
            restaurante.RestriccionesQueRespeta.Add(new Restriccion { Nombre = "Sin gluten" });

            var resultado = _evaluador.Evaluar(preferencias, restaurante);

            resultado.Should().Be(NivelCompatibilidadRestaurante.Incompatible);
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
