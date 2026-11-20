using FluentAssertions;
using GustosApp.Application.Interfaces;
using GustosApp.Application.Services;
using GustosApp.Application.UseCases.RestauranteUseCases;
using GustosApp.Domain.Model;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GustosApp.Application.Tests
{
    public class RecomendacionIAUseCaseTest
    {
        [Fact]
        public async Task RestauranteDesactivadoNoConsultaGeminiNiEntregaExplicacionGuardada()
        {
            var usuario = CrearUsuario(gustos: ["Pizza"]);
            var restaurante = CrearRestaurante(gustos: ["Pizza"]);
            restaurante.CambiarDisponibilidad(true, 0, "Revisión", DateTime.UtcNow);
            var resultado = await CrearCasoDeUso().Handle(usuario, restaurante, default);
            Assert.Contains("no está disponible", resultado);
            _ia.Verify(s => s.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
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
                servicio => servicio.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()),
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
                servicio => servicio.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()),
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
                servicio => servicio.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task Handle_RestauranteSinMenuConGustos_UsaLosGustosGuardados()
        {
            string? promptEnviado = null;
            _ia.Setup(servicio => servicio.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, CancellationToken>((prompt, _) => promptEnviado = prompt)
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
                servicio => servicio.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Handle_CompatibilidadEstimada_UsaIASoloParaGustosYAgregaAdvertenciaPropia()
        {
            string? promptEnviado = null;
            _ia.Setup(servicio => servicio.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, CancellationToken>((prompt, _) => promptEnviado = prompt)
                .ReturnsAsync("Coincide con tu preferencia por comida picante.");
            var usuario = CrearUsuario(
                gustos: ["Picante"],
                restricciones: ["Sin gluten"],
                condiciones: []);
            var restaurante = CrearRestaurante(
                gustos: ["Picante"],
                restricciones: ["Sin gluten"]);
            restaurante.ConfirmarRestriccionesDeclaradas(0, DateTime.UtcNow);
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
                servicio => servicio.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Handle_SinCuidadosDeSalud_DevuelveLaExplicacionDeGustosSinAdvertencia()
        {
            _ia.Setup(servicio => servicio.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
            _ia.Setup(servicio => servicio.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("API key inválida: secreto-123"));
            var casoDeUso = CrearCasoDeUso();

            var resultado = await casoDeUso.Handle(
                CrearUsuario(gustos: ["Pizza"]),
                CrearRestaurante(gustos: ["Pizza"]),
                CancellationToken.None);

            resultado.Should().Contain("coinciden con tus gustos: Pizza");
            resultado.Should().NotContain("API key");
            resultado.Should().NotContain("secreto-123");
        }

        [Theory]
        [InlineData(60, 60)]
        [InlineData(999_999, 86_400)]
        public async Task Handle_ReutilizaHastaElTtlConfiguradoSinSuperarUnDia(int configurado, int esperado)
        {
            var reloj = new RelojExplicacionesPrueba(DateTimeOffset.UtcNow);
            var almacen = new AlmacenExplicacionesPrueba(reloj);
            var caso = CrearCasoDeUso(reloj, almacen, new GeminiSettings
            {
                ApiKey = "prueba", Model = "modelo", CacheExplicacionesTtlSegundos = configurado
            });
            var usuario = CrearUsuario(gustos: ["Pizza"]);
            var restaurante = CrearRestaurante(gustos: ["Pizza"]);
            _ia.SetupSequence(s => s.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Primera explicación.").ReturnsAsync("Segunda explicación.");

            (await caso.Handle(usuario, restaurante, default)).Should().Be("Primera explicación.");
            reloj.Avanzar(TimeSpan.FromSeconds(esperado - 1));
            (await caso.Handle(usuario, restaurante, default)).Should().Be("Primera explicación.");
            reloj.Avanzar(TimeSpan.FromSeconds(1));
            (await caso.Handle(usuario, restaurante, default)).Should().Be("Segunda explicación.");
            _ia.Verify(s => s.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task Handle_SeparaUsuariosYRestaurantesAunqueElPromptCoincida()
        {
            var reloj = TimeProvider.System;
            var almacen = new AlmacenExplicacionesPrueba(reloj);
            var caso = CrearCasoDeUso(reloj, almacen);
            var gusto = new Gusto { Id = Guid.NewGuid(), Nombre = "Pizza" };
            var usuario = new Usuario { Id = Guid.NewGuid(), Gustos = [gusto] };
            var otroUsuario = new Usuario { Id = Guid.NewGuid(), Gustos = [gusto] };
            var restaurante = CrearRestaurante(gustos: ["Pizza"]);
            var otroRestaurante = new Restaurante { Id = Guid.NewGuid(), Nombre = restaurante.Nombre, GustosQueSirve = restaurante.GustosQueSirve };
            _ia.SetupSequence(s => s.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Del primer usuario.").ReturnsAsync("Del segundo usuario.").ReturnsAsync("Del segundo lugar.");

            (await caso.Handle(usuario, restaurante, default)).Should().Be("Del primer usuario.");
            (await caso.Handle(otroUsuario, restaurante, default)).Should().Be("Del segundo usuario.");
            (await caso.Handle(usuario, otroRestaurante, default)).Should().Be("Del segundo lugar.");
            (await caso.Handle(usuario, restaurante, default)).Should().Be("Del primer usuario.");
        }

        [Theory]
        [InlineData("gustos")]
        [InlineData("reglas")]
        [InlineData("restricciones")]
        [InlineData("oferta")]
        [InlineData("declaraciones")]
        [InlineData("prompt")]
        [InlineData("modelo")]
        [InlineData("origen")]
        public async Task Handle_CambioRelevanteInvalidaSinExponerPreferenciasEnLaClave(string cambio)
        {
            var reloj = new RelojExplicacionesPrueba(DateTimeOffset.UtcNow);
            var almacen = new AlmacenExplicacionesPrueba(reloj);
            var configuracion = new GeminiSettings { ApiKey = "prueba", Model = "modelo-1" };
            var usuario = CrearUsuario(gustos: ["Pizza"], restricciones: ["Sin gluten"]);
            var restriccion = usuario.Restricciones.Single();
            var restaurante = CrearRestaurante(gustos: ["Pizza"]);
            restaurante.RestriccionesQueRespeta.Add(new Restriccion { Id = restriccion.Id, Nombre = "Opciones sin gluten" });
            restaurante.ConfirmarRestriccionesDeclaradas(0, reloj.GetUtcNow().UtcDateTime);
            var caso = CrearCasoDeUso(reloj, almacen, configuracion);
            _ia.SetupSequence(s => s.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Original.").ReturnsAsync("Actualizada.");
            (await caso.Handle(usuario, restaurante, default)).Should().StartWith("Original.");
            var claveOriginal = almacen.ClavesLeidas[0];

            switch (cambio)
            {
                case "gustos": usuario.Gustos.Add(new Gusto { Id = Guid.NewGuid(), Nombre = "Pasta" }); break;
                case "reglas": restriccion.TagsProhibidos.Add(new Tag { Nombre = "Avena" }); break;
                case "restricciones": usuario.Restricciones.Clear(); break;
                case "oferta": restaurante.GustosQueSirve.Add(new Gusto { Id = Guid.NewGuid(), Nombre = "Pasta" }); break;
                case "declaraciones":
                    restaurante.RestriccionesQueRespeta.Add(new Restriccion { Id = Guid.NewGuid(), Nombre = "Sin lactosa" });
                    break;
                case "prompt": restaurante.Nombre = "Nombre actualizado"; break;
                case "modelo": configuracion.Model = "modelo-2"; break;
                case "origen": restaurante.RegistrarVerificacionDatosCompatibilidad(GustosApp.Domain.Common.OrigenDatosCompatibilidadRestaurante.Restaurante, reloj.GetUtcNow().UtcDateTime); break;
            }

            (await caso.Handle(usuario, restaurante, default)).Should().StartWith("Actualizada.");
            almacen.ClavesLeidas.Last().Should().NotBe(claveOriginal);
            almacen.ClavesLeidas.Should().OnlyContain(clave => clave.Length == 64 && clave.All(char.IsAsciiHexDigit));
        }

        [Fact]
        public async Task Handle_ReordenarGustosYTagsMantieneLaEntrada()
        {
            var usuario = CrearUsuario(gustos: ["Pizza", "Pasta"], restricciones: ["Sin gluten"]);
            var restriccion = usuario.Restricciones.Single();
            restriccion.TagsProhibidos.Add(new Tag { Nombre = "Avena" });
            var restaurante = CrearRestaurante(gustos: ["Pasta", "Pizza"]);
            restaurante.RestriccionesQueRespeta = [restriccion];
            restaurante.ConfirmarRestriccionesDeclaradas(0, DateTime.UtcNow);
            var caso = CrearCasoDeUso();
            _ia.Setup(s => s.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("Coincidencias.");

            await caso.Handle(usuario, restaurante, default);
            usuario.Gustos = usuario.Gustos.Reverse().ToList();
            restaurante.GustosQueSirve = restaurante.GustosQueSirve.Reverse().ToList();
            restriccion.TagsProhibidos = restriccion.TagsProhibidos.Reverse().ToList();
            (await caso.Handle(usuario, restaurante, default)).Should().StartWith("Coincidencias.");
            _ia.Verify(s => s.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_DeclaracionAlCumplirCuatroMeses_ConservaExplicacionYCaché()
        {
            var fecha = new DateTimeOffset(2026, 1, 20, 12, 0, 0, TimeSpan.Zero);
            var reloj = new RelojExplicacionesPrueba(fecha.AddMonths(4).AddSeconds(-1));
            var almacen = new AlmacenExplicacionesPrueba(reloj);
            var usuario = CrearUsuario(gustos: ["Pizza"], restricciones: ["Sin gluten"]);
            var restaurante = CrearRestaurante(gustos: ["Pizza"]);
            restaurante.RestriccionesQueRespeta = usuario.Restricciones.ToList();
            restaurante.ConfirmarRestriccionesDeclaradas(0, fecha.UtcDateTime);
            var caso = CrearCasoDeUso(reloj, almacen);
            _ia.Setup(s => s.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() =>
            {
                reloj.Avanzar(TimeSpan.FromSeconds(2));
                return "Coincide con tus gustos.";
            });

            (await caso.Handle(usuario, restaurante, default)).Should().StartWith("Coincide con tus gustos.");
            var clave = almacen.ClavesLeidas.Distinct().Single();
            (await caso.Handle(usuario, restaurante, default)).Should().StartWith("Coincide con tus gustos.");
            almacen.ClavesLeidas.Should().OnlyContain(leida => leida == clave);
            _ia.Verify(s => s.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_OfertaAlCumplirCuatroMeses_ConservaExplicacion()
        {
            var fecha = new DateTimeOffset(2026, 1, 20, 12, 0, 0, TimeSpan.Zero);
            var reloj = new RelojExplicacionesPrueba(fecha.AddMonths(4).AddMinutes(-1));
            var usuario = CrearUsuario(gustos: ["Pizza"]);
            var restaurante = CrearRestaurante(gustos: ["Pizza"]);
            restaurante.ConfirmarOfertaVigente(0, fecha.UtcDateTime);
            var caso = CrearCasoDeUso(reloj);
            _ia.SetupSequence(s => s.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Oferta vigente.").ReturnsAsync("Oferta por revisar.");

            (await caso.Handle(usuario, restaurante, default)).Should().Be("Oferta vigente.");
            reloj.Avanzar(TimeSpan.FromMinutes(2));
            (await caso.Handle(usuario, restaurante, default)).Should().Be("Oferta vigente.");
            _ia.Verify(s => s.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_NuevaCondicionIncompatible_NoDevuelveExplicacionPrevia()
        {
            var almacen = new AlmacenExplicacionesPrueba(TimeProvider.System);
            var usuario = CrearUsuario(gustos: ["Pizza"]);
            var restaurante = CrearRestaurante(gustos: ["Pizza"], tags: ["Gluten"]);
            var caso = CrearCasoDeUso(almacen: almacen);
            _ia.Setup(s => s.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Coincide con tu gusto por la pizza.");
            (await caso.Handle(usuario, restaurante, default)).Should().Contain("tu gusto por la pizza");
            var lecturas = almacen.ClavesLeidas.Count;
            usuario.CondicionesMedicas.Add(new CondicionMedica
            {
                Id = Guid.NewGuid(), Nombre = "Enfermedad celíaca", TagsCriticos = [new Tag { Nombre = "Gluten" }]
            });

            (await caso.Handle(usuario, restaurante, default)).Should().Contain("incompatibilidad conocida");
            almacen.ClavesLeidas.Should().HaveCount(lecturas);
            _ia.Verify(s => s.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_RedisNoDisponible_UsaCoincidenciasSinConsultarGemini()
        {
            var almacen = new AlmacenExplicacionesPrueba(TimeProvider.System) { Caido = true };
            var caso = CrearCasoDeUso(almacen: almacen);
            var resultado = await caso.Handle(CrearUsuario(gustos: ["Pizza"]), CrearRestaurante(gustos: ["Pizza"]), default);

            resultado.Should().Contain("coinciden con tus gustos: Pizza");
            resultado.Should().NotContain("Redis");
            _ia.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Handle_FechasAusentesRecientesOAntiguas_NoAlteranLaClaveNiLaExplicacion()
        {
            var reloj = new RelojExplicacionesPrueba(DateTimeOffset.UtcNow);
            var almacen = new AlmacenExplicacionesPrueba(reloj);
            var usuario = CrearUsuario(gustos: ["Pizza"], restricciones: ["Sin gluten"]);
            var restaurante = CrearRestaurante(gustos: ["Pizza"]);
            restaurante.RestriccionesQueRespeta = usuario.Restricciones.ToList();
            var caso = CrearCasoDeUso(reloj, almacen);
            _ia.Setup(s => s.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Coincide con tus gustos.");

            foreach (int? meses in new int?[] { null, 0, -6, -120 })
            {
                DateTime? fecha = meses.HasValue ? reloj.GetUtcNow().UtcDateTime.AddMonths(meses.Value) : null;
                foreach (var campo in new[] {
                    nameof(Restaurante.FechaRevisionOfertaUtc), nameof(Restaurante.FechaRevisionRestriccionesUtc),
                    nameof(Restaurante.FechaObtencionDatosCompatibilidadUtc),
                    nameof(Restaurante.FechaUltimaVerificacionDatosCompatibilidadUtc) })
                    typeof(Restaurante).GetProperty(campo)!.SetValue(restaurante, fecha);
                (await caso.Handle(usuario, restaurante, default)).Should().StartWith("Coincide con tus gustos.");
            }

            almacen.ClavesLeidas.Distinct().Should().ContainSingle();
            _ia.Verify(s => s.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_CambiaOfertaDuranteGemini_DescartaTextoYUsaOfertaActual()
        {
            var usuario = CrearUsuario(gustos: ["Pizza", "Pasta"]);
            var restaurante = CrearRestaurante(gustos: ["Pizza"]);
            _ia.Setup(s => s.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => {
                    restaurante.GustosQueSirve = [new Gusto { Nombre = "Pasta" }];
                    return "La pizza coincide con tus gustos.";
                });

            var resultado = await CrearCasoDeUso().Handle(usuario, restaurante, default);

            resultado.Should().Contain("Pasta").And.NotContain("pizza").And.NotContain("Pizza");
        }

        [Fact]
        public async Task Handle_RetiranDeclaracionDuranteGemini_ConservaCompatibilidadDesconocida()
        {
            var usuario = CrearUsuario(gustos: ["Pizza"], restricciones: ["Sin gluten"]);
            var restaurante = CrearRestaurante(gustos: ["Pizza"]);
            restaurante.RestriccionesQueRespeta = usuario.Restricciones.ToList();
            _ia.Setup(s => s.GenerarRecomendacion(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => {
                    restaurante.RestriccionesQueRespeta.Clear();
                    return "Una explicación anterior.";
                });

            var resultado = await CrearCasoDeUso().Handle(usuario, restaurante, default);

            resultado.Should().Contain("No hay información suficiente").And.NotContain("explicación anterior");
        }

        private RecomendacionIAUseCase CrearCasoDeUso(TimeProvider? reloj = null,
            AlmacenExplicacionesPrueba? almacen = null, GeminiSettings? configuracion = null)
        {
            reloj ??= TimeProvider.System;
            almacen ??= new AlmacenExplicacionesPrueba(reloj);
            var opciones = Options.Create(configuracion ?? new GeminiSettings { ApiKey = "prueba", Model = "modelo-prueba" });
            return new RecomendacionIAUseCase(
                _ia.Object,
                new EvaluadorCompatibilidadRestaurante(reloj),
                new CacheExplicaciones(almacen, opciones, reloj, NullLogger<CacheExplicaciones>.Instance),
                opciones, reloj);
        }

        private static Usuario CrearUsuario(
            string[]? gustos = null,
            string[]? restricciones = null,
            string[]? condiciones = null)
        {
            return new Usuario
            {
                Id = Guid.NewGuid(),
                Gustos = (gustos ?? []).Select(nombre => new Gusto { Nombre = nombre }).ToList(),
                Restricciones = (restricciones ?? [])
                    .Select(nombre => new Restriccion
                    {
                        Nombre = nombre,
                        TagsProhibidos = nombre == "Sin gluten" ? [new Tag { Nombre = "Gluten" }] : []
                    })
                    .ToList(),
                CondicionesMedicas = (condiciones ?? [])
                    .Select(nombre => new CondicionMedica
                    {
                        Nombre = nombre,
                        TagsCriticos = nombre == "Enfermedad celíaca" ? [new Tag { Nombre = "Gluten" }] :
                            nombre == "Alergia al huevo" ? [new Tag { Nombre = "Huevos" }] : []
                    })
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
                Id = Guid.NewGuid(),
                Nombre = "Restaurante de prueba",
                GustosQueSirve = gustosRestaurante,
                RestriccionesQueRespeta = (restricciones ?? [])
                    .Select(nombre => new Restriccion { Nombre = nombre })
                    .ToList()
            };
        }
    }
}
