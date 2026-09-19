using GustosApp.Application.Interfaces;
using GustosApp.Application.Services;
using GustosApp.Domain.Common;
using GustosApp.Domain.Model;

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

        public RecomendacionIAUseCase(
            IRecomendacionAIService ia,
            EvaluadorCompatibilidadRestaurante evaluadorCompatibilidad)
        {
            _ia = ia;
            _evaluadorCompatibilidad = evaluadorCompatibilidad;
        }

        public async Task<string> Handle(
            Usuario usuario,
            Restaurante restaurante,
            CancellationToken cancellationToken)
        {
            var preferencias = new UsuarioPreferencias
            {
                Gustos = usuario.Gustos.Select(g => g.Nombre).ToList(),
                Restricciones = usuario.Restricciones.Select(r => r.Nombre).ToList(),
                CondicionesMedicas = usuario.CondicionesMedicas.Select(c => c.Nombre).ToList()
            };

            var compatibilidad = _evaluadorCompatibilidad.Evaluar(preferencias, restaurante);

            if (compatibilidad == NivelCompatibilidadRestaurante.Incompatible)
                return MensajeIncompatible;

            if (compatibilidad == NivelCompatibilidadRestaurante.Desconocida)
                return MensajeCompatibilidadDesconocida;

            cancellationToken.ThrowIfCancellationRequested();

            var explicacion = await _ia.GenerarRecomendacion(
                ConstruirPromptDeGustos(usuario, restaurante));
            var tieneCuidadosDeSalud = preferencias.Restricciones.Count > 0 ||
                preferencias.CondicionesMedicas.Count > 0;

            return tieneCuidadosDeSalud
                ? $"{explicacion.Trim()} {AdvertenciaCompatibilidadOrientativa}"
                : explicacion.Trim();
        }

        private static string ConstruirPromptDeGustos(Usuario usuario, Restaurante restaurante)
        {
            var gustosUsuario = usuario.Gustos
                .Select(g => g.Nombre)
                .Where(nombre => !string.IsNullOrWhiteSpace(nombre))
                .ToList();
            var gustosRestaurante = restaurante.GustosQueSirve
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
