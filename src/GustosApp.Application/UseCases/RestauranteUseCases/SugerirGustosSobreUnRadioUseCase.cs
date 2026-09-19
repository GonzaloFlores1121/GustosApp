
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Application.Services;
using Microsoft.Extensions.Logging;

namespace GustosApp.Application.UseCases.RestauranteUseCases
{
    public class SugerirGustosSobreUnRadioUseCase : IRecomendadorRestaurantes
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly IRestauranteRepository _restauranteRepository;
        private readonly EvaluadorCompatibilidadRestaurante _evaluadorCompatibilidad;

        private const double UMBRAL_MINIMO = 0.05;
        private const double UMBRAL_RELEVANCIA_RESENA = 0.60;
        private const double FACTOR_PENALIZACION = 0.15;

        private const double BONIFICADOR_POSITIVO = 1.02;
        private const double PENALIZACION_NEGATIVA = 0.95;

        public SugerirGustosSobreUnRadioUseCase(
            IEmbeddingService embeddingService,
            IRestauranteRepository restauranteRepository,
            EvaluadorCompatibilidadRestaurante evaluadorCompatibilidad)
        {
            _embeddingService = embeddingService;
            _restauranteRepository = restauranteRepository;
            _evaluadorCompatibilidad = evaluadorCompatibilidad;
        }


        public async Task<List<Restaurante>> Handle(
            UsuarioPreferencias usuario,
            List<Restaurante> restaurantesCercanos,
            int maxResultados = 10,
            CancellationToken ct = default)
        {
            if (ValidarEntrada(usuario, restaurantesCercanos))
                throw new KeyNotFoundException(
                    "No se encontraron restaurantes cercanos que coincidan con los gustos del usuario o el usuario no tiene gustos validos");

            float[] embeddingUsuario = ObtenerEmbeddingUsuario(usuario);

            if (embeddingUsuario ==null)
            {
                throw new KeyNotFoundException("usuario invalido");
            }

            List<ResultadoRecomendacion> resultados =
                CalcularSimilitudUsuarioRestaurante(usuario, restaurantesCercanos, embeddingUsuario);

            if (!resultados.Any())
                throw new KeyNotFoundException("No se obtuvo ningun restaurante en la zona para el usuario");

            List<Restaurante> restaurantesConResenas =
                await ConsultarRestaurantesConResenas(resultados);

            AjustarPuntuacionPorResenas(embeddingUsuario, resultados, restaurantesConResenas);

            return OrdenarResultados(maxResultados, resultados);
        }

        private void AjustarPuntuacionPorResenas(
            float[] embeddingUsuario,
            List<ResultadoRecomendacion> resultados,
            List<Restaurante> restaurantesConResenas)
        {
            foreach (var restConResenas in restaurantesConResenas)
            {
                var item = resultados.FirstOrDefault(x => x.Restaurante.Id == restConResenas.Id);
                if (item == null) continue;

                double ajuste = CalcularAjustePorResenas(restConResenas, embeddingUsuario);

                double nuevaPuntuacion = item.Puntuacion * ajuste;

                item.Restaurante.Score = nuevaPuntuacion;

                resultados.RemoveAll(x => x.Restaurante.Id == restConResenas.Id);
                resultados.Add(item with { Puntuacion = nuevaPuntuacion });
            }
        }

        private async Task<List<Restaurante>> ConsultarRestaurantesConResenas(
            List<ResultadoRecomendacion> resultados)
        {
            var ids = resultados.Select(r => r.Restaurante.Id).ToList();
            return await _restauranteRepository.obtenerRestauranteConResenias(ids);
        }

        private double CalcularAjustePorResenas(Restaurante rest, float[] embeddingUsuario)
        {
            if (rest.Reviews == null || !rest.Reviews.Any())
                return 1.0;

            var ajustes = new List<double>();

            foreach (var resena in rest.Reviews)
            {
                if (string.IsNullOrWhiteSpace(resena.Opinion))
                    continue;

                float[] embeddingResena = _embeddingService.GetEmbedding(resena.Opinion);

                if (embeddingResena == null) continue;

                double relevancia = CosineSimilarity.Coseno(embeddingUsuario, embeddingResena);

                if (relevancia < UMBRAL_RELEVANCIA_RESENA)
                    continue;

                if (resena.Valoracion >= 3)
                    ajustes.Add(BONIFICADOR_POSITIVO);
                else
                    ajustes.Add(PENALIZACION_NEGATIVA);
            }

            return ajustes.Any() ? ajustes.Average() : 1.0;
        }

        private List<ResultadoRecomendacion> CalcularSimilitudUsuarioRestaurante(
            UsuarioPreferencias usuario,
            List<Restaurante> restaurantesCercanos,
            float[] embeddingUsuario)
        {
            var resultados = new List<ResultadoRecomendacion>();

            foreach (var rest in restaurantesCercanos)
            {
                var compatibilidad = _evaluadorCompatibilidad.Evaluar(usuario, rest);
                if (compatibilidad == NivelCompatibilidadRestaurante.Incompatible)
                    continue;

                var embeddingRest = ObtenerEmbeddingRestaurante(rest);
                if (embeddingRest == null) continue;

                var similitud = CosineSimilarity.Coseno(embeddingUsuario, embeddingRest);

                double penalizacion = CalcularPenalizacion(usuario, rest);

                double puntuacionFinal = similitud * (1 - penalizacion);

                if (puntuacionFinal >= UMBRAL_MINIMO)
                {
                    rest.Score = puntuacionFinal;
                    rest.NivelCompatibilidad = compatibilidad;
                    resultados.Add(new ResultadoRecomendacion(rest, puntuacionFinal, compatibilidad));
                }
            }

            return resultados;
        }

        private static List<Restaurante> OrdenarResultados(
            int maxResultados,
            List<ResultadoRecomendacion> resultados)
        {
            return resultados
                .GroupBy(x => x.Restaurante.Id)
                .Select(g => g.First())
                .OrderByDescending(x => x.Compatibilidad)
                .ThenByDescending(x => x.Puntuacion)
                .Take(maxResultados)
                .Select(x => x.Restaurante)
                .ToList();
        }

        private static double CalcularPenalizacion(UsuarioPreferencias usuario, Restaurante rest)
        {
            int gustosFaltantes = usuario.Gustos.Count(g =>
                !rest.GustosQueSirve.Any(e =>
                    e.Nombre != null &&
                    e.Nombre.Contains(g, StringComparison.OrdinalIgnoreCase)));

            int restriccionesIncumplidas = usuario.Restricciones.Count(r =>
                !rest.RestriccionesQueRespeta.Any(e =>
                    e.Nombre != null &&
                    e.Nombre.Contains(r, StringComparison.OrdinalIgnoreCase)));

            double penalizacion =
                (gustosFaltantes + restriccionesIncumplidas)
                * FACTOR_PENALIZACION
                / (usuario.Gustos.Count + usuario.Restricciones.Count);

            return penalizacion;
        }

        private float[] ObtenerEmbeddingRestaurante(Restaurante rest)
        {
            var textoRestaurante = string.Join(" ",
                rest.GustosQueSirve.Select(g => g.Nombre)
                .Concat(rest.RestriccionesQueRespeta.Select(r => r.Nombre))
                .Where(s => !string.IsNullOrWhiteSpace(s))
            );

            if (string.IsNullOrWhiteSpace(textoRestaurante))
                return null;

            return _embeddingService.GetEmbedding(textoRestaurante);
        }

        private float[] ObtenerEmbeddingUsuario(UsuarioPreferencias usuario)
        {
            var textoUsuario = string.Join(" ",
                usuario.Gustos.Concat(usuario.Restricciones)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
            );

            return _embeddingService.GetEmbedding(textoUsuario);
        }

        private bool ValidarEntrada(UsuarioPreferencias usuario,
            List<Restaurante> restaurantesCercanos)
        {
            bool usuarioNoValido = usuario == null || usuario.Gustos == null || !usuario.Gustos.Any();
            bool restaurantesInvalidos = restaurantesCercanos == null || !restaurantesCercanos.Any();

            return usuarioNoValido || restaurantesInvalidos;
        }

        private sealed record ResultadoRecomendacion(
            Restaurante Restaurante,
            double Puntuacion,
            NivelCompatibilidadRestaurante Compatibilidad);
    }
}
