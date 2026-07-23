using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using System;
using System.Threading.Tasks;

namespace GustosApp.Application.UseCases.RestauranteUseCases
{
    public class ObtenerRestauranteIdPorPropietarioUseCase
    {
        private readonly IServicioRestaurantes _servicio;

        public ObtenerRestauranteIdPorPropietarioUseCase(IServicioRestaurantes servicio)
        {
            _servicio = servicio;
        }

        public async Task<Guid> HandleAsync(Guid propietarioId)
        {
            var restaurante = await _servicio.ObtenerPorPropietarioAsync(propietarioId);
            
            if (restaurante == null)
            {
                throw new Application.Common.Exceptions.NotFoundException($"No se encontró un restaurante para el propietario con id {propietarioId}");
            }

            return restaurante.Id;
        }
    }
}
