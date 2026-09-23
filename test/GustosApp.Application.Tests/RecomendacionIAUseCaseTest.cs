using FluentAssertions;
using GustosApp.Application.Interfaces;
using GustosApp.Application.Services;
using GustosApp.Application.UseCases.RestauranteUseCases;
using GustosApp.Domain.Model;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;

namespace GustosApp.Application.Tests
{
    public class RecomendacionIAUseCaseTest
    {
        private readonly Mock<IRecomendacionAIService> _ia = new();

        [Fact]
        public async Task Handle_CondicionMedicaIncompatible_NoConsultaLaIA()
        {
            var usuario = CrearUsuario(
                condiciones: ["Enfermedad celíaca"]);
            var restaurante = CrearRestaurante(tags: ["Gluten"]);
            var casoDeUso = CrearCasoDeUso();

            var resultado = await casoDeUso.Handle(
                usuario,
                restaurante,
                CancellationToken.None);

            resultado.Should().Contain("incompatibilidad conocida");
            _ia.Verify(
                servicio => servicio.GenerarRecomendacion(It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task Handle_CompatibilidadDesconocida_NoConsultaLaIAYRecomiendaVerificar()
        {
            var usuario = CrearUsuario(
                condiciones: ["Alergia al huevo"]);
            var restaurante = CrearRestaurante();
            var casoDeUso = CrearCasoDeUso();

            var resultado = await casoDeUso.Handle(
                usuario,
                restaurante,
                CancellationToken.None);

            resultado.Should().Contain("No hay información suficiente");
            resultado.Should().Contain("consultá directamente al restaurante");
            _ia.Verify(
                servicio => servicio.GenerarRecomendacion(It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task Handle_RestauranteSinGustos_NoConsultaLaIAPorqueLosDatosSonInsuficientes()
        {
            var casoDeUso = CrearCasoDeUso();
            var restaurante = CrearRestaurante(gustos: []);

            var resultado = await casoDeUso.Handle(
                CrearUsuario(gustos: ["Pizza"]),
                restaurante,
                CancellationToken.None);

            resultado.Should().Contain("No hay información suficiente");
            _ia.Verify(
                servicio => servicio.GenerarRecomendacion(It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task Handle_RestauranteSinMenuConGustos_UsaLosGustosGuardados()
        {
            string? promptEnviado = null;
            _ia.Setup(servicio => servicio.GenerarRecomendacion(It.IsAny<string>()))
                .Callback<string>(prompt => promptEnviado = prompt)
                .ReturnsAsync("Coincide con tu gusto por la pizza.");
            var restaurante = CrearRestaurante(gustos: ["Pizza"]);
            restaurante.MenuProcesado = false;

            var resultado = await CrearCasoDeUso().Handle(
                CrearUsuario(gustos: ["Pizza"]),
                restaurante,
                CancellationToken.None);

            resultado.Should().Contain("pizza");
            promptEnviado.Should().Contain("Propuesta del restaurante: Pizza");
            _ia.Verify(
                servicio => servicio.GenerarRecomendacion(It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task Handle_CompatibilidadEstimada_UsaIASoloParaGustosYAgregaAdvertenciaPropia()
        {
            string? promptEnviado = null;
            _ia.Setup(servicio => servicio.GenerarRecomendacion(It.IsAny<string>()))
                .Callback<string>(prompt => promptEnviado = prompt)
                .ReturnsAsync("Coincide con tu preferencia por comida picante.");
            var usuario = CrearUsuario(
                gustos: ["Picante"],
                restricciones: ["Sin gluten"],
                condiciones: ["Enfermedad celíaca"]);
            var restaurante = CrearRestaurante(
                gustos: ["Picante"],
                restricciones: ["Sin gluten"]);
            var casoDeUso = CrearCasoDeUso();

            var resultado = await casoDeUso.Handle(
                usuario,
                restaurante,
                CancellationToken.None);

            resultado.Should().Contain("Coincide con tu preferencia");
            resultado.Should().Contain("compatibilidad es orientativa");
            promptEnviado.Should().Contain("Picante");
            promptEnviado.Should().NotContain("Sin gluten");
            promptEnviado.Should().NotContain("celíaca");
            _ia.Verify(
                servicio => servicio.GenerarRecomendacion(It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task Handle_SinCuidadosDeSalud_DevuelveLaExplicacionDeGustosSinAdvertencia()
        {
            _ia.Setup(servicio => servicio.GenerarRecomendacion(It.IsAny<string>()))
                .ReturnsAsync("  Coincide con tus gustos.  ");
            var usuario = CrearUsuario(gustos: ["Pizza"]);
            var restaurante = CrearRestaurante(gustos: ["Pizza"]);
            var casoDeUso = CrearCasoDeUso();

            var resultado = await casoDeUso.Handle(
                usuario,
                restaurante,
                CancellationToken.None);

            resultado.Should().Be("Coincide con tus gustos.");
        }

        [Fact]
        public async Task Handle_FalloDeGemini_DevuelveMensajeComprensibleSinDetalleTecnico()
        {
            _ia.Setup(servicio => servicio.GenerarRecomendacion(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("API key inválida: secreto-123"));
            var casoDeUso = CrearCasoDeUso();

            var resultado = await casoDeUso.Handle(
                CrearUsuario(gustos: ["Pizza"]),
                CrearRestaurante(gustos: ["Pizza"]),
                CancellationToken.None);

            resultado.Should().Contain("No pudimos generar la explicación personalizada");
            resultado.Should().NotContain("API key");
            resultado.Should().NotContain("secreto-123");
        }

        private RecomendacionIAUseCase CrearCasoDeUso()
        {
            return new RecomendacionIAUseCase(
                _ia.Object,
                new EvaluadorCompatibilidadRestaurante(),
                NullLogger<RecomendacionIAUseCase>.Instance);
        }

        private static Usuario CrearUsuario(
            string[]? gustos = null,
            string[]? restricciones = null,
            string[]? condiciones = null)
        {
            return new Usuario
            {
                Gustos = (gustos ?? []).Select(nombre => new Gusto { Nombre = nombre }).ToList(),
                Restricciones = (restricciones ?? [])
                    .Select(nombre => new Restriccion { Nombre = nombre })
                    .ToList(),
                CondicionesMedicas = (condiciones ?? [])
                    .Select(nombre => new CondicionMedica { Nombre = nombre })
                    .ToList()
            };
        }

        private static Restaurante CrearRestaurante(
            string[]? gustos = null,
            string[]? restricciones = null,
            string[]? tags = null)
        {
            var tagsDelPrimerGusto = (tags ?? [])
                .Select(nombre => new Tag { Nombre = nombre })
                .ToList();
            var gustosRestaurante = (gustos ?? ["Comida"])
                .Select((nombre, indice) => new Gusto
                {
                    Nombre = nombre,
                    Tags = indice == 0 ? tagsDelPrimerGusto : new List<Tag>()
                })
                .ToList();

            return new Restaurante
            {
                Nombre = "Restaurante de prueba",
                GustosQueSirve = gustosRestaurante,
                RestriccionesQueRespeta = (restricciones ?? [])
                    .Select(nombre => new Restriccion { Nombre = nombre })
                    .ToList()
            };
        }
    }
}
