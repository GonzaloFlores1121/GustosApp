using System.Globalization;
using System.Text;
using GustosApp.Domain.Common;
using GustosApp.Domain.Model;

namespace GustosApp.Application.Services
{
    public class EvaluadorCompatibilidadRestaurante
    {
        private readonly TimeProvider _reloj;

        public EvaluadorCompatibilidadRestaurante(TimeProvider reloj)
        {
            _reloj = reloj;
        }

        public EvaluadorCompatibilidadRestaurante()
            : this(TimeProvider.System)
        {
        }

        private static readonly IReadOnlyDictionary<string, string[]> TagsPorRestriccion =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["sin gluten"] = ["gluten"],
                ["sin lactosa"] = ["lacteo"],
                ["sin azucar"] = ["azucar"],
                ["sin sal"] = ["sal"],
                ["sin mariscos"] = ["mariscos"],
                ["sin carne roja"] = ["carne roja"],
                ["sin frito"] = ["frito"],
                ["sin picante"] = ["picante"],
                ["sin cafeina"] = ["cafeina"],
                ["sin alcohol"] = ["alcohol"],
                ["sin soja"] = ["soja"],
                ["sin frutos secos"] = ["frutos secos"]
            };

        private static readonly IReadOnlyDictionary<string, ReglaCondicionMedica> ReglasPorCondicion =
            new Dictionary<string, ReglaCondicionMedica>(StringComparer.OrdinalIgnoreCase)
            {
                ["diabetes"] = new(["azucar"], "sin azucar"),
                ["hipertension"] = new(["sal"], "sin sal"),
                ["colesterol alto"] = new(["grasa"], null),
                ["gastritis"] = new(["picante"], "sin picante"),
                ["enfermedad celiaca"] = new(["gluten"], "sin gluten"),
                ["intolerancia a la lactosa"] = new(["lacteo"], "sin lactosa"),
                ["alergia a mariscos"] = new(["mariscos"], "sin mariscos"),
                ["alergia a frutos secos"] = new(["frutos secos"], "sin frutos secos"),
                ["alergia al huevo"] = new(["huevos"], null),
                ["sindrome del intestino irritable"] = new(["frito"], "sin frito"),
                ["gota"] = new(["carne roja"], "sin carne roja"),
                ["ansiedad sensibilidad a cafeina"] = new(["cafeina"], "sin cafeina"),
                ["vegetariano"] = new(["carne roja", "carne blanca", "pescado", "mariscos"], null),
                ["vegano"] = new(["carne roja", "carne blanca", "pescado", "mariscos", "huevos", "lacteo"], null)
            };

        public NivelCompatibilidadRestaurante Evaluar(
            UsuarioPreferencias preferencias,
            Restaurante restaurante)
        {
            var restricciones = preferencias.Restricciones
                .Where(nombre => !string.IsNullOrWhiteSpace(nombre))
                .Select(Normalizar)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var condiciones = preferencias.CondicionesMedicas
                .Where(nombre => !string.IsNullOrWhiteSpace(nombre))
                .Select(Normalizar)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (restricciones.Count == 0 && condiciones.Count == 0)
                return NivelCompatibilidadRestaurante.Estimada;

            var restriccionesRespetadas = restaurante.RestriccionesQueRespeta
                .Where(restriccion => !string.IsNullOrWhiteSpace(restriccion.Nombre))
                .Select(restriccion => Normalizar(restriccion.Nombre))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var tagsConocidos = restaurante.GustosQueSirve
                .SelectMany(gusto => gusto.Tags ?? [])
                .Where(tag => !string.IsNullOrWhiteSpace(tag.Nombre))
                .Select(tag => Normalizar(tag.Nombre))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var reglas = restricciones
                .Select(CrearReglaRestriccion)
                .Concat(condiciones.Select(CrearReglaCondicion))
                .ToList();

            var datosDesactualizados = restaurante.ObtenerEstadoActualDatosCompatibilidad(
                _reloj.GetUtcNow().UtcDateTime) ==
                EstadoDatosCompatibilidadRestaurante.Desactualizado;

            var resultados = reglas
                .Select(regla => EvaluarRegla(
                    regla,
                    restriccionesRespetadas,
                    tagsConocidos,
                    datosDesactualizados))
                .ToList();

            if (resultados.Contains(ResultadoReglaCompatibilidad.Incompatible))
                return NivelCompatibilidadRestaurante.Incompatible;

            return resultados.All(resultado => resultado == ResultadoReglaCompatibilidad.Compatible)
                ? NivelCompatibilidadRestaurante.Estimada
                : NivelCompatibilidadRestaurante.Desconocida;
        }

        private static ReglaCompatibilidad CrearReglaRestriccion(string restriccion)
        {
            TagsPorRestriccion.TryGetValue(restriccion, out var tags);
            return new ReglaCompatibilidad(tags ?? [], restriccion);
        }

        private static ReglaCompatibilidad CrearReglaCondicion(string condicion)
        {
            return ReglasPorCondicion.TryGetValue(condicion, out var regla)
                ? new ReglaCompatibilidad(regla.TagsProhibidos, regla.RestriccionQueConfirma)
                : new ReglaCompatibilidad([], null);
        }

        private static ResultadoReglaCompatibilidad EvaluarRegla(
            ReglaCompatibilidad regla,
            HashSet<string> restriccionesRespetadas,
            HashSet<string> tagsConocidos,
            bool datosDesactualizados)
        {
            if (regla.RestriccionQueConfirma != null &&
                restriccionesRespetadas.Contains(regla.RestriccionQueConfirma))
            {
                return datosDesactualizados
                    ? ResultadoReglaCompatibilidad.Desconocida
                    : ResultadoReglaCompatibilidad.Compatible;
            }

            return regla.TagsProhibidos.Any(tagsConocidos.Contains)
                ? ResultadoReglaCompatibilidad.Incompatible
                : ResultadoReglaCompatibilidad.Desconocida;
        }

        private static string Normalizar(string valor)
        {
            var descompuesto = valor.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sinDiacriticos = new string(descompuesto
                .Where(caracter => CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
                .ToArray());

            return sinDiacriticos
                .Replace("(", " ", StringComparison.Ordinal)
                .Replace(")", " ", StringComparison.Ordinal)
                .Replace("  ", " ", StringComparison.Ordinal)
                .Trim();
        }

        private sealed record ReglaCompatibilidad(
            IReadOnlyCollection<string> TagsProhibidos,
            string? RestriccionQueConfirma);

        private sealed record ReglaCondicionMedica(
            IReadOnlyCollection<string> TagsProhibidos,
            string? RestriccionQueConfirma);

        private enum ResultadoReglaCompatibilidad
        {
            Desconocida = 0,
            Compatible = 1,
            Incompatible = 2
        }
    }
}
