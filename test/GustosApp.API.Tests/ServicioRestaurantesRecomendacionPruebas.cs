using FluentAssertions;
using GustosApp.API.Tests.Infraestructura;
using GustosApp.Application.Interfaces;
using GustosApp.Application.Services;
using GustosApp.Application.UseCases.RestauranteUseCases;
using GustosApp.Application.UseCases.RestauranteUseCases.Importacion;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Infraestructure;
using GustosApp.Infraestructure.Repositories;
using GustosApp.Infraestructure.Services;
using Microsoft.Extensions.Configuration;
using Moq;

namespace GustosApp.API.Tests.Servicios
{
    public class ServicioRestaurantesRecomendacionPruebas
    {
        [Fact]
        public async Task BuscarAsync_IncluyeCandidatosSinCoincidenciaExactaParaElFallback()
        {
            await using var contexto = DbContextEnMemoria.Crear(
                nameof(BuscarAsync_IncluyeCandidatosSinCoincidenciaExactaParaElFallback));

            var gustoPizza = contexto.Gustos.Single(gusto => gusto.Nombre == "Pizza");
            var gustoSushi = contexto.Gustos.Single(gusto => gusto.Nombre == "Sushi");

            contexto.Restaurantes.AddRange(
                CrearRestaurante("Pizzería", gustoPizza),
                CrearRestaurante("Restaurante de sushi", gustoSushi));
            await contexto.SaveChangesAsync();

            var servicio = new ServicioRestaurantes(
                contexto,
                new ConfigurationBuilder().Build(),
                new HttpClient(),
                Mock.Of<IRestauranteRepository>());

            var resultado = await servicio.BuscarAsync(
                rating: 0,
                lat: null,
                lng: null,
                radioMetros: null,
                gustos: new List<string> { "Pizza" },
                restricciones: new List<string>());

            resultado.Should().HaveCount(2);
            resultado.Should().Contain(restaurante => restaurante.Nombre == "Restaurante de sushi");
            resultado.Single(restaurante => restaurante.Nombre == "Pizzería")
                .GustosQueSirve.Single()
                .Tags.Should().Contain(tag => tag.Nombre == "Gluten");
        }

        [Theory]
        [InlineData(false, false, 0, 2)]
        [InlineData(false, true, 0, 2)]
        [InlineData(true, false, 0, 2)]
        [InlineData(true, true, 0, 2)]
        [InlineData(false, false, 4, 1)]
        [InlineData(false, true, 4, 1)]
        [InlineData(true, false, 4, 1)]
        [InlineData(true, true, 4, 1)]
        public async Task Buscar_RespetaFiltroExplicitoSinExcluirNuevos(bool porPreferencias, bool conUbicacion, double minimo, int cantidad)
        {
            await using var contexto = DbContextEnMemoria.Crear(Guid.NewGuid().ToString());
            var gusto = contexto.Gustos.Single(g => g.Nombre == "Pizza");
            var nuevo = CrearRestaurante("Nuevo sin opiniones", gusto);
            nuevo.Rating = null;
            contexto.Restaurantes.AddRange(nuevo, CrearRestaurante("Con opiniones", gusto));
            await contexto.SaveChangesAsync();
            var servicio = new ServicioRestaurantes(contexto, new ConfigurationBuilder().Build(), new HttpClient(), Mock.Of<IRestauranteRepository>());
            double? lat = conUbicacion ? 0 : null;
            double? lng = conUbicacion ? 0 : null;
            int? radio = conUbicacion ? 1000 : null;
            var resultado = porPreferencias
                ? await servicio.BuscarAsync(minimo, lat, lng, radio, new List<string> { "Pizza" }, new List<string>())
                : await servicio.BuscarAsync(minimo, null, null, lat, lng, radio);
            resultado.Should().HaveCount(cantidad);
            if (minimo == 0) resultado.Should().Contain(r => r.Id == nuevo.Id && r.Rating == null);
            else resultado.Should().NotContain(r => r.Id == nuevo.Id);
        }

        [Fact]
        public async Task GuardarClasificacion_SinMenu_PersisteGustosQueLleganALaRecomendacion()
        {
            await using var contexto = DbContextEnMemoria.Crear(
                nameof(GuardarClasificacion_SinMenu_PersisteGustosQueLleganALaRecomendacion));
            var restaurante = CrearRestauranteImportado("Importado clasificado");
            contexto.Restaurantes.Add(restaurante);
            await contexto.SaveChangesAsync();
            var pizza = contexto.Gustos.Single(gusto => gusto.Nombre == "Pizza");
            var repositorioRestaurantes = new RestauranteRepositoryEF(contexto);
            var casoGuardar = new GuardarClasificacionRestauranteImportadoUseCase(
                repositorioRestaurantes,
                new GustoRepositoryEF(contexto),
                TimeProvider.System);

            await casoGuardar.HandleAsync(
                restaurante.Id,
                "Pizzería",
                [pizza.Id]);
            contexto.ChangeTracker.Clear();

            var servicio = CrearServicio(contexto);
            var candidatos = await servicio.BuscarAsync(
                0, null, null, null, ["Pizza"], []);
            var recomendador = new SugerirGustosSobreUnRadioUseCase(
                new EmbeddingConstante(),
                new RestauranteRepositoryEF(contexto),
                new EvaluadorCompatibilidadRestaurante());
            var resultado = await recomendador.Handle(
                new UsuarioPreferencias { Gustos = ["Pizza"] },
                candidatos);

            resultado.Should().ContainSingle();
            var recomendado = resultado.Single();
            recomendado.Id.Should().Be(restaurante.Id);
            recomendado.MenuProcesado.Should().BeFalse();
            recomendado.GustosQueSirve.Should().ContainSingle(gusto => gusto.Id == pizza.Id);
            recomendado.OrigenDatosCompatibilidad.Should().Be(
                OrigenDatosCompatibilidadRestaurante.Administracion);
        }

        [Fact]
        public async Task RestauranteSinMenuYSinGustos_EsCandidatoPeroNoSeRecomiendaPorDatosInsuficientes()
        {
            await using var contexto = DbContextEnMemoria.Crear(
                nameof(RestauranteSinMenuYSinGustos_EsCandidatoPeroNoSeRecomiendaPorDatosInsuficientes));
            var restaurante = CrearRestauranteImportado("Importado sin clasificar");
            contexto.Restaurantes.Add(restaurante);
            await contexto.SaveChangesAsync();
            contexto.ChangeTracker.Clear();

            var candidatos = await CrearServicio(contexto).BuscarAsync(
                0, null, null, null, ["Pizza"], []);
            var recomendador = new SugerirGustosSobreUnRadioUseCase(
                new EmbeddingConstante(),
                new RestauranteRepositoryEF(contexto),
                new EvaluadorCompatibilidadRestaurante());

            candidatos.Should().ContainSingle(item => item.Id == restaurante.Id);
            var resultado = await recomendador.Handle(
                new UsuarioPreferencias { Gustos = ["Pizza"] },
                candidatos);

            resultado.Should().BeEmpty();
        }

        [Fact]
        public async Task RestauranteSinMenuConIncompatibilidadConocida_NoSeRecomienda()
        {
            await using var contexto = DbContextEnMemoria.Crear(
                nameof(RestauranteSinMenuConIncompatibilidadConocida_NoSeRecomienda));
            var restaurante = CrearRestauranteImportado("Pizzería con gluten");
            contexto.Restaurantes.Add(restaurante);
            await contexto.SaveChangesAsync();
            var pizza = contexto.Gustos.Single(gusto => gusto.Nombre == "Pizza");
            await new GuardarClasificacionRestauranteImportadoUseCase(
                    new RestauranteRepositoryEF(contexto),
                    new GustoRepositoryEF(contexto),
                    TimeProvider.System)
                .HandleAsync(restaurante.Id, "Pizzería", [pizza.Id]);
            contexto.ChangeTracker.Clear();

            var candidatos = await CrearServicio(contexto).BuscarAsync(
                0, null, null, null, ["Pizza"], ["Sin gluten"]);
            var recomendador = new SugerirGustosSobreUnRadioUseCase(
                new EmbeddingConstante(),
                new RestauranteRepositoryEF(contexto),
                new EvaluadorCompatibilidadRestaurante());
            var resultado = await recomendador.Handle(
                new UsuarioPreferencias
                {
                    Gustos = ["Pizza"],
                    Restricciones = ["Sin gluten"]
                },
                candidatos);

            resultado.Should().BeEmpty();
        }

        private static ServicioRestaurantes CrearServicio(GustosDbContext contexto) => new(
            contexto,
            new ConfigurationBuilder().Build(),
            new HttpClient(),
            new RestauranteRepositoryEF(contexto));

        private static Restaurante CrearRestauranteImportado(string nombre) => new()
        {
            Id = Guid.NewGuid(),
            Nombre = nombre,
            NombreNormalizado = nombre.ToLowerInvariant(),
            Direccion = "Dirección de prueba",
            PlaceId = Guid.NewGuid().ToString("N"),
            PropietarioUid = string.Empty,
            Categoria = "Restaurante",
            Rating = 4,
            MenuProcesado = false
        };

        private static Restaurante CrearRestaurante(string nombre, Gusto gusto)
        {
            return new Restaurante
            {
                Id = Guid.NewGuid(),
                Nombre = nombre,
                NombreNormalizado = nombre.ToLowerInvariant(),
                Direccion = "Dirección de prueba",
                PlaceId = Guid.NewGuid().ToString(),
                Rating = 4,
                GustosQueSirve = new List<Gusto> { gusto }
            };
        }

        private sealed class EmbeddingConstante : IEmbeddingService
        {
            public float[] GetEmbedding(string text) => [1, 1, 1, 1];
        }
    }
}
