using GustosApp.Application.Interfaces;
using GustosApp.Application.Services;
using GustosApp.Domain.Common;
using GustosApp.Domain.Model;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text.Json;

namespace GustosApp.Application.UseCases.RestauranteUseCases
{
    public class RecomendacionIAUseCase(
        IRecomendacionAIService ia,
        EvaluadorCompatibilidadRestaurante evaluadorCompatibilidad,
        CacheExplicaciones cache,
        IOptions<GeminiSettings> configuracion,
        TimeProvider reloj)
    {
        private const string MensajeIncompatible =
            "Este restaurante presenta una incompatibilidad conocida con tus restricciones o condiciones médicas. No lo recomendamos para esta selección.";

        private const string MensajeCompatibilidadDesconocida =
            "No hay información suficiente para confirmar la compatibilidad con tus restricciones o condiciones médicas. Revisá el menú o consultá directamente al restaurante antes de elegir.";

        private const string AdvertenciaCompatibilidadOrientativa =
            "La compatibilidad es orientativa. Ante una restricción o condición médica, confirmá la preparación y la posible contaminación cruzada con el restaurante.";

        private const int VersionPrompt = 1;

        public async Task<string> Handle(
            Usuario usuario,
            Restaurante restaurante,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var preferencias = new UsuarioPreferencias
            {
                Gustos = usuario.Gustos.Select(g => g.Nombre).ToList(),
                Restricciones = usuario.Restricciones.Select(r => r.Nombre).ToList(),
                CondicionesMedicas = usuario.CondicionesMedicas.Select(c => c.Nombre).ToList(),
                ReglasCompatibilidad = ReglasCompatibilidadPreferencias.DesdeUsuario(usuario)
            };

            if (!restaurante.GustosQueSirve.Any(gusto =>
                    !string.IsNullOrWhiteSpace(gusto.Nombre)))
            {
                return MensajeCompatibilidadDesconocida;
            }

            var ahoraUtc = reloj.GetUtcNow().UtcDateTime;
            var compatibilidad = evaluadorCompatibilidad.Evaluar(preferencias, restaurante, ahoraUtc);

            if (compatibilidad == NivelCompatibilidadRestaurante.Incompatible)
                return MensajeIncompatible;

            if (compatibilidad == NivelCompatibilidadRestaurante.Desconocida)
                return MensajeCompatibilidadDesconocida;

            var opcionesPotenciales = evaluadorCompatibilidad
                .ObtenerOpcionesSinConflictoConocido(preferencias, restaurante);
            if (opcionesPotenciales.Count == 0)
                return MensajeCompatibilidadDesconocida;

            cancellationToken.ThrowIfCancellationRequested();

            string? explicacion = null;
            var opcionesGemini = configuracion.Value;
            if (opcionesGemini.Habilitada && usuario.Id != Guid.Empty && restaurante.Id != Guid.Empty)
            {
                var prompt = ConstruirPromptDeGustos(preferencias, restaurante, opcionesPotenciales);
                var clave = CrearHuella(usuario, restaurante, preferencias, opcionesGemini.Model, prompt, ahoraUtc);
                explicacion = await cache.ObtenerAsync(clave, ct => ia.GenerarRecomendacion(prompt, ct), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                // Una declaración puede vencerse mientras se espera a Redis o a Gemini.
                var ahoraEntrega = reloj.GetUtcNow().UtcDateTime;
                compatibilidad = evaluadorCompatibilidad.Evaluar(preferencias, restaurante, ahoraEntrega);
                if (compatibilidad == NivelCompatibilidadRestaurante.Incompatible)
                    return MensajeIncompatible;
                if (compatibilidad == NivelCompatibilidadRestaurante.Desconocida)
                    return MensajeCompatibilidadDesconocida;
                if (restaurante.ObtenerEstadoOferta(ahoraEntrega) != restaurante.ObtenerEstadoOferta(ahoraUtc) ||
                    restaurante.ObtenerEstadoRestricciones(ahoraEntrega) != restaurante.ObtenerEstadoRestricciones(ahoraUtc))
                    explicacion = null;
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(explicacion))
            {
                var coincidencias = usuario.Gustos.Select(g => g.Nombre)
                    .Intersect(opcionesPotenciales.Select(g => g.Nombre), StringComparer.OrdinalIgnoreCase)
                    .Where(nombre => !string.IsNullOrWhiteSpace(nombre)).ToArray();
                explicacion = coincidencias.Length > 0
                    ? $"Según la ficha, este restaurante ofrece opciones que coinciden con tus gustos: {string.Join(", ", coincidencias)}."
                    : "No pudimos generar la explicación personalizada en este momento. Podés seguir consultando la información del restaurante.";
            }
            var tieneCuidadosDeSalud = preferencias.Restricciones.Count > 0 ||
                preferencias.CondicionesMedicas.Count > 0;

            return tieneCuidadosDeSalud
                ? $"{explicacion.Trim()} {AdvertenciaCompatibilidadOrientativa}"
                : explicacion.Trim();
        }

        private static string CrearHuella(Usuario usuario, Restaurante restaurante,
            UsuarioPreferencias preferencias, string modelo, string prompt, DateTime ahoraUtc)
        {
            var datos = new
            {
                VersionPrompt,
                Modelo = modelo,
                Prompt = prompt,
                UsuarioId = usuario.Id,
                GustosUsuario = usuario.Gustos.Select(g => g.Id).Order(),
                RestriccionesUsuario = OrdenarNombres(preferencias.Restricciones),
                CondicionesUsuario = OrdenarNombres(preferencias.CondicionesMedicas),
                Reglas = preferencias.ReglasCompatibilidad.OrderBy(r => r.Id).ThenBy(r => r.EsRestriccion)
                    .Select(r => new { r.Id, r.EsRestriccion, Tags = OrdenarNombres(r.TagsCriticos) }),
                RestauranteId = restaurante.Id,
                Oferta = restaurante.GustosQueSirve.OrderBy(g => g.Id).ThenBy(g => g.Nombre, StringComparer.Ordinal)
                    .Select(g => new { g.Id, g.Nombre, Tags = OrdenarNombres(g.Tags.Select(t => t.NombreNormalizado)) }),
                Declaraciones = restaurante.RestriccionesQueRespeta.OrderBy(r => r.Id)
                    .Select(r => new { r.Id, r.Nombre, Vigencia = restaurante.ObtenerEstadoDeclaracion(r.Id, ahoraUtc) }),
                restaurante.VersionOferta,
                restaurante.VersionRestricciones,
                restaurante.FechaRevisionOfertaUtc,
                restaurante.FechaRevisionRestriccionesUtc,
                EstadoOferta = restaurante.ObtenerEstadoOferta(ahoraUtc),
                EstadoRestricciones = restaurante.ObtenerEstadoRestricciones(ahoraUtc)
            };
            return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(datos)));
        }

        private static string[] OrdenarNombres(IEnumerable<string> nombres) => nombres
            .Where(nombre => !string.IsNullOrWhiteSpace(nombre))
            .Order(StringComparer.Ordinal)
            .ToArray();

        private static string ConstruirPromptDeGustos(UsuarioPreferencias preferencias, Restaurante restaurante,
            IReadOnlyCollection<Gusto> opcionesPotenciales)
        {
            var gustosUsuario = OrdenarNombres(preferencias.Gustos);
            var gustosRestaurante = OrdenarNombres(opcionesPotenciales.Select(g => g.Nombre));
            var gustosCoincidentes = gustosUsuario
                .Intersect(gustosRestaurante, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return $@"Explicá brevemente por qué {restaurante.Nombre} puede coincidir con los gustos gastronómicos del usuario.

Gustos del usuario: {string.Join(", ", gustosUsuario)}
Propuesta del restaurante: {string.Join(", ", gustosRestaurante)}
Coincidencias: {string.Join(", ", gustosCoincidentes)}

Usá texto plano, tono claro y un máximo de tres oraciones. No hagas afirmaciones sobre salud, alergias, seguridad alimentaria ni compatibilidad médica.";
        }
    }
}
