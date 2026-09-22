using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GustosApp.Application.UseCases.RestauranteUseCases
{
    public class BuscarRestaurantesUseCase
    {
        public const int LongitudMaximaBusqueda = 100;
        private readonly IRestauranteRepository _repo;

        public BuscarRestaurantesUseCase(IRestauranteRepository repo)
        {
            _repo = repo;
        }

        /* public async Task<List<Restaurante>> HandleAsync(string texto, CancellationToken ct)
         {
             if (string.IsNullOrWhiteSpace(texto))
                 return new List<Restaurante>();

             var restaurantes = await _repo.BuscarPorTextoAsync(texto, ct);
             var restaurantes = await _repo.BuscarPorTextoAsync(texto, ct);
             return restaurantes;
         }*/

        public async Task<List<Restaurante>> HandleAsync(string texto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(texto)) return [];
            var textoNormalizado = texto.Trim().Normalize(NormalizationForm.FormC);
            if (textoNormalizado.Length > LongitudMaximaBusqueda || textoNormalizado.Any(char.IsControl))
                throw new ArgumentException($"La búsqueda admite hasta {LongitudMaximaBusqueda} caracteres.");

            var resultado = await _repo.BuscarPorPrefijo(textoNormalizado, ct);

            return resultado;
            // return await Task.FromResult(resultado);
        }
    }
    }

