using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using System;
using System.Threading.Tasks;

namespace GustosApp.Application.UseCases.RestauranteUseCases
{
    public class EliminarRestauranteUseCase
    {
        private readonly IServicioRestaurantes _servicio;

        public EliminarRestauranteUseCase(IServicioRestaurantes servicio)
        {
            _servicio = servicio;
        }

        public async Task<bool> HandleAsync(Guid id, string uid, bool esAdmin)
        {
            return await _servicio.EliminarAsync(id, uid, esAdmin);
        }
    }
}
