using GustosApp.Application.Interfaces;
using GustosApp.Application.Services;
using GustosApp.Domain.Common;
using GustosApp.Domain.Model;
using Microsoft.Extensions.Logging;

namespace GustosApp.Application.UseCases.RestauranteUseCases
{
    public class RecomendacionIAUseCase
    {
        private const string MensajeIncompatible =
            "Este restaurante presenta una incompatibilidad conocida con tus restricciones o condiciones médicas. No lo recomendamos para esta selección.";

        private const string MensajeCompatibilidadDesconocida =
            "No hay información suficiente para confirmar la compatibilidad con tus restricciones o condiciones médicas. Revisá el menú o consultá directamente al restaurante antes de elegir.";

        private const string AdvertenciaCompatibilidadOrientativa =
            "La compatibilidad es orientativa. Ante una restricción o condición médica, confirmá la preparación y la posible contaminación cruzada con el restaurante.";

        private readonly IRecomendacionAIService _ia;
        private readonly EvaluadorCompatibilidadRestaurante _evaluadorCompatibilidad;
        private readonly ILogger<RecomendacionIAUseCase> _logger;

        public RecomendacionIAUseCase(
            IRecomendacionAIService ia,
            EvaluadorCompatibilidadRestaurante evaluadorCompatibilidad,
            ILogger<RecomendacionIAUseCase> logger)
        {
            _ia = ia;
            _evaluadorCompatibilidad = evaluadorCompatibilidad;
            _logger = logger;
        }

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

            var compatibilidad = _evaluadorCompatibilidad.Evaluar(preferencias, restaurante);

            if (compatibilidad == NivelCompatibilidadRestaurante.Incompatible)
                return MensajeIncompatible;

            if (compatibilidad == NivelCompatibilidadRestaurante.Desconocida)
                return MensajeCompatibilidadDesconocida;

            var opcionesPotenciales = _evaluadorCompatibilidad
                .ObtenerOpcionesSinConflictoConocido(preferencias, restaurante);
            if (opcionesPotenciales.Count == 0)
                return MensajeCompatibilidadDesconocida;

            cancellationToken.ThrowIfCancellationRequested();

            string? explicacion = null;
            try
            {
                explicacion = await _ia.GenerarRecomendacion(
                    ConstruirPromptDeGustos(usuario, restaurante, opcionesPotenciales), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("No se pudo generar la explicación del restaurante {RestauranteId}. Tipo: {Tipo}",
                    restaurante.Id, ex.GetType().Name);
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

        private static string ConstruirPromptDeGustos(Usuario usuario, Restaurante restaurante,
            IReadOnlyCollection<Gusto> opcionesPotenciales)
        {
            var gustosUsuario = usuario.Gustos
                .Select(g => g.Nombre)
                .Where(nombre => !string.IsNullOrWhiteSpace(nombre))
                .ToList();
            var gustosRestaurante = opcionesPotenciales
                .Select(g => g.Nombre)
                .Where(nombre => !string.IsNullOrWhiteSpace(nombre))
                .ToList();
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
