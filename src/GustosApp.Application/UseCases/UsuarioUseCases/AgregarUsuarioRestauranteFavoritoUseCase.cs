using GustosApp.Application.Common.Exceptions;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GustosApp.Application.UseCases.UsuarioUseCases
{
    public class AgregarUsuarioRestauranteFavoritoUseCase
    {
        private readonly IUsuarioRepository _repositorioUsuario;
        private readonly IUsuarioRestauranteFavoritoRepository _repositorioFavorito;
        private readonly IRestauranteRepository _repositorioRestaurante;

        public AgregarUsuarioRestauranteFavoritoUseCase(
            IUsuarioRestauranteFavoritoRepository repositorioFavorito,
            IUsuarioRepository repositorioUsuario,
            IRestauranteRepository repositorioRestaurante)
        {
            _repositorioUsuario = repositorioUsuario;
            _repositorioFavorito = repositorioFavorito;
            _repositorioRestaurante = repositorioRestaurante;
        }

        public async Task HandleAsync(string firebaseUid, Guid restauranteId, CancellationToken ct = default)
        {
            var usuario = await _repositorioUsuario.GetByFirebaseUidAsync(firebaseUid, ct);
            if (usuario == null)
                throw new UnauthorizedAccessException("Usuario no encontrado");

            if (await _repositorioRestaurante.GetByIdAsync(restauranteId, ct) is null)
                throw new KeyNotFoundException("Restaurante no encontrado.");

            var limite = usuario.Plan == PlanUsuario.Free ? 3 : (int?)null;
            var resultado = await _repositorioFavorito.AgregarSiCorrespondeAsync(
                usuario.Id,
                restauranteId,
                limite,
                DateTime.UtcNow,
                ct);

            if (resultado == ResultadoAgregarFavorito.LimiteAlcanzado)
            {
                throw new LimiteFavoritosAlcanzadoException(
                    tipoPlan: "Free",
                    limiteActual: 3,
                    favoritosActuales: await _repositorioFavorito.CountByUsuarioAsync(usuario.Id, ct));
            }
        }

        public async Task HandleAsyncDelete(string firebaseUid, Guid restauranteId, CancellationToken ct = default)
        {
            var usuario = await _repositorioUsuario.GetByFirebaseUidAsync(firebaseUid, ct);
            if (usuario == null)
                throw new UnauthorizedAccessException("Usuario no encontrado");

            if (await _repositorioRestaurante.GetByIdAsync(restauranteId, ct) is null)
                throw new KeyNotFoundException("Restaurante no encontrado.");

            var existe = await _repositorioFavorito.ExistsAsync(usuario.Id, restauranteId, ct);

            if (!existe)
            {
                return; 
            }

            await _repositorioFavorito.EliminarAsync(usuario.Id, restauranteId, ct);
        }
    }
}
