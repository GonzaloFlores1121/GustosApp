using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Application.Interfaces;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.UseCases.RestauranteUseCases;
using GustosApp.Application.UseCases.UsuarioUseCases;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;

namespace GustosApp.Application.Services
{
    public class BuscarRestaurantesRecomendadosOrquestador : IBuscarRestaurantesRecomendadosOrquestador
    {
        private readonly ConstruirPreferenciasUsuarioIndividualUseCase _preferenciasSolo;
        private readonly ConstruirPreferenciasUsuarioConAmigoCase _preferenciasConAmigo;
        private readonly IServicioRestaurantes _buscarRestaurantes;
        private readonly SugerirGustosSobreUnRadioUseCase _sugerir;
        private readonly ICacheService _cache;
        private readonly RegistrarTop3IndividualRestaurantesUseCase _registrarTop3;
        private readonly IGustoRepository _gustosRepository;
        private readonly TimeProvider _reloj;

        public BuscarRestaurantesRecomendadosOrquestador(
            ConstruirPreferenciasUsuarioIndividualUseCase preferenciasSolo,
            ConstruirPreferenciasUsuarioConAmigoCase preferenciasConAmigo,
            IServicioRestaurantes buscarRestaurantes,
            SugerirGustosSobreUnRadioUseCase sugerir,
            ICacheService cache,
            RegistrarTop3IndividualRestaurantesUseCase registrarTop3,
            IGustoRepository gustosRepository,
            TimeProvider? reloj = null)
        {
            _preferenciasSolo = preferenciasSolo;
            _preferenciasConAmigo = preferenciasConAmigo;
            _buscarRestaurantes = buscarRestaurantes;
            _sugerir = sugerir;
            _cache = cache;
            _registrarTop3 = registrarTop3;
            _gustosRepository = gustosRepository;
            _reloj = reloj ?? TimeProvider.System;
        }


        public async Task<List<Restaurante>> HandleAsync(string firebaseUid, List<string>? gustos,
            string? amigoUsername, double? lat, double? lng, int? radius, int top, double rating,
            CancellationToken ct, bool abiertoAhora = false)
        {
            var gustosExplicitos = (gustos ?? new List<string>())
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Select(g => g.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (gustosExplicitos.Count > 0)
            {
                var catalogo = await _gustosRepository.GetAllAsync(ct);
                var nombresValidos = catalogo.Select(g => g.Nombre).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var invalidos = gustosExplicitos.Where(g => !nombresValidos.Contains(g)).ToList();
                if (invalidos.Count > 0)
                    throw new FiltroGustosInvalidoException(invalidos);
            }

            // El filtro explícito no reemplaza el perfil ni los gustos del amigo:
            // funciona como condición OR obligatoria sobre los candidatos.
            var preferencias = amigoUsername != null
                ? await _preferenciasConAmigo.HandleAsync(firebaseUid, amigoUsername, null, ct)
                : await _preferenciasSolo.HandleAsync(firebaseUid, null, ct);

            if ((preferencias.Gustos == null || preferencias.Gustos.Count == 0) && gustosExplicitos.Count > 0)
                preferencias.Gustos = gustosExplicitos;

            if (preferencias.Gustos == null || !preferencias.Gustos.Any())
            {
                throw new SinPreferenciasRecomendacionException();
            }

            // 2️⃣ Buscar restaurantes cercanos
            var candidatos = await _buscarRestaurantes.BuscarAsync(
                rating,
                lat,
                lng,
                radius,
                preferencias.Gustos,
                preferencias.Restricciones);

            if (candidatos == null || !candidatos.Any())
                return new List<Restaurante>();

            candidatos = candidatos.Where(r => !r.Desactivado).ToList();

            if (abiertoAhora)
            {
                var ahora = _reloj.GetUtcNow();
                candidatos = candidatos.Where(r =>
                    HorariosRestaurante.Evaluar(r.HorariosJson, ahora) == EstadoAperturaRestaurante.Abierto).ToList();
                if (candidatos.Count == 0) return new List<Restaurante>();
            }

            if (gustosExplicitos.Count > 0)
            {
                candidatos = candidatos
                    .Where(r => r.GustosQueSirve.Any(g =>
                        gustosExplicitos.Contains(g.Nombre, StringComparer.OrdinalIgnoreCase)))
                    .ToList();

                if (candidatos.Count == 0)
                    return new List<Restaurante>();
            }

            // 3️⃣ Guardar ubicación del usuario en cache solo si hay lat y lng
            if (lat.HasValue && lng.HasValue)
            {
                await _cache.SetAsync(
                    $"usuario:{firebaseUid}:location",
                    new UserLocation(lat.Value, lng.Value, radius ?? 3000, DateTime.UtcNow),
                    TimeSpan.FromMinutes(10));
            }

            // 4️⃣ Algoritmo de recomendación
            var recomendados = await _sugerir.Handle(preferencias, candidatos, top, ct);

            if (!recomendados.Any())
                return new List<Restaurante>();

            // 5️⃣ Registrar top3 individual
            var top3 = recomendados.Take(3).Select(r => r.Id).ToList();
            if (top3.Any())
                await _registrarTop3.HandleAsync(top3, ct);

            return recomendados;
        }
    }
}
