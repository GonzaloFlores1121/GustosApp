using GustosApp.Application.Model;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GustosApp.Application.UseCases.RestauranteUseCases
{
    public class ObtenerRestaurantesAleatoriosGrupoUseCase
    {
        private readonly IGrupoRepository _grupoRepository;
        private readonly IBuscadorRestaurantesGrupo _buscadorRestaurantesGrupo;

        public ObtenerRestaurantesAleatoriosGrupoUseCase(
            IGrupoRepository grupoRepository,
            IBuscadorRestaurantesGrupo buscadorRestaurantesGrupo)
        {
            _grupoRepository = grupoRepository;
            _buscadorRestaurantesGrupo = buscadorRestaurantesGrupo;
        }

        public async Task<List<RestauranteAleatorioModel>> HandleAsync(
            string firebaseUid,
            Guid grupoId,
            ObtenerRestaurantesAleatoriosRequestModel request,
            CancellationToken ct)
        {
            // Verificar que el grupo existe
            var grupo = await _grupoRepository.GetByIdAsync(grupoId, ct);
            if (grupo == null)
            {
                throw new ArgumentException("El grupo no existe", nameof(grupoId));
            }

            var resultadoBusqueda = await _buscadorRestaurantesGrupo.BuscarAsync(
                firebaseUid,
                grupoId,
                request.Latitud,
                request.Longitud,
                request.RadioMetros,
                200,
                ct);

            var restaurantes = resultadoBusqueda.Restaurantes.ToList();

            // Seleccionar restaurantes aleatorios
            var cantidad = Math.Min(request.Cantidad, restaurantes.Count);
            var restaurantesAleatorios = restaurantes
                .OrderBy(_ => Random.Shared.Next())
                .Take(cantidad)
                .ToList();

            // Mapear a response
            var response = restaurantesAleatorios.Select(r => new RestauranteAleatorioModel
            {
                Id = r.Id,
                Nombre = r.Nombre,
                Direccion = r.Direccion,
                Latitud = r.Latitud,
                Longitud = r.Longitud,
                Rating = r.Rating,
                CantidadResenas = r.CantidadResenas,
                Categoria = r.Categoria,
                ImagenUrl = GustosApp.Domain.Common.ImagenRestaurantePublica.Url(r.Id, r.ImagenUrl),
                Valoracion = r.Valoracion,
                WebUrl = r.WebUrl,
                PlaceId = r.PlaceId,
                Gustos = r.GustosQueSirve?.Select(g => g.Nombre).ToList() ?? new List<string>(),
                Restricciones = r.RestriccionesQueRespeta?.Select(res => res.Nombre).ToList() ?? new List<string>()
            }).ToList();

            return response;
        }

    }
}
