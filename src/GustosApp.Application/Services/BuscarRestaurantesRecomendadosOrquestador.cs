using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.RestauranteUseCases;
using GustosApp.Application.UseCases.UsuarioUseCases;
using GustosApp.Domain.Common;
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

        public BuscarRestaurantesRecomendadosOrquestador(
            ConstruirPreferenciasUsuarioIndividualUseCase preferenciasSolo,
            ConstruirPreferenciasUsuarioConAmigoCase preferenciasConAmigo,
            IServicioRestaurantes buscarRestaurantes,
            SugerirGustosSobreUnRadioUseCase sugerir,
            ICacheService cache,
            RegistrarTop3IndividualRestaurantesUseCase registrarTop3)
        {
            _preferenciasSolo = preferenciasSolo;
            _preferenciasConAmigo = preferenciasConAmigo;
            _buscarRestaurantes = buscarRestaurantes;
            _sugerir = sugerir;
            _cache = cache;
            _registrarTop3 = registrarTop3;
        }


        public async Task<List<Restaurante>> HandleAsync(string firebaseUid, List<string>? gustos,
            string? amigoUsername, double? lat, double? lng, int? radius, int top, double rating,
            CancellationToken ct)
        {
            // 1️⃣ Construir preferencias
            var preferencias = amigoUsername != null
                ? await _preferenciasConAmigo.HandleAsync(firebaseUid, amigoUsername,gustos,ct)
                : await _preferenciasSolo.HandleAsync(firebaseUid, gustos,ct);

            // 2️⃣ Buscar restaurantes cercanos
            var candidatos = await _buscarRestaurantes.BuscarAsync(
                rating,
                lat,
                lng,
                radius,
                preferencias.Gustos,
                preferencias.Restricciones);

            if (candidatos == null || !candidatos.Any())
                throw new KeyNotFoundException("No se encontraron restaurantes para esa ubicación.");

            // 3️⃣ Guardar ubicación del usuario en cache
            await _cache.SetAsync(
                $"usuario:{firebaseUid}:location",
                new UserLocation(lat ?? 0, lng ?? 0, radius ?? 3000, DateTime.UtcNow),
                TimeSpan.FromMinutes(10));

            // 4️⃣ Algoritmo de recomendación
            var recomendados = await _sugerir.Handle(preferencias, candidatos, top, ct);

            if (!recomendados.Any())
                throw new KeyNotFoundException("No hay coincidencias con tus gustos en esa zona.");

            // 5️⃣ Registrar top3 individual
            var top3 = recomendados.Take(3).Select(r => r.Id).ToList();
            if (top3.Any())
                await _registrarTop3.HandleAsync(top3, ct);

            return recomendados;
        
    }
    }
}
