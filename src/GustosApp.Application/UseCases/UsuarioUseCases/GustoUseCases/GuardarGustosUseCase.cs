using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;

namespace GustosApp.Application.UseCases.UsuarioUseCases.GustoUseCases
{
    public class GuardarGustosUseCase
    {
        private readonly IUsuarioRepository _userRepository;
        private readonly IGustoRepository _gustoRepository;
        private readonly ICacheService _cache;

        public GuardarGustosUseCase(
            IUsuarioRepository userRepository,
            IGustoRepository gustoRepository,
            ICacheService cache)
        {
            _userRepository = userRepository;
            _gustoRepository = gustoRepository;
            _cache = cache;
        }

        public async Task<List<string>> HandleAsync(
            string uid,
            List<Guid> ids,
            ModoPreferencias modo,
            CancellationToken ct)
        {
            var usuario = await _userRepository.GetByFirebaseUidAsync(uid, ct)
                ?? throw new InvalidOperationException("Usuario no encontrado.");

            var idsUnicos = (ids ?? new List<Guid>()).Distinct().ToList();
            if (modo == ModoPreferencias.Registro && idsUnicos.Count < 3)
                throw new ArgumentException("Debe seleccionar al menos 3 gustos.");

            var gustosValidos = idsUnicos.Count == 0
                ? new List<Gusto>()
                : await _gustoRepository.GetByIdsAsync(idsUnicos, ct);
            if (gustosValidos.Count != idsUnicos.Count)
                throw new ArgumentException("Uno o más gustos no existen.");

            var actuales = usuario.Gustos.Select(g => g.Id).ToHashSet();
            var nuevas = idsUnicos.ToHashSet();

            var paraAgregar = nuevas.Except(actuales).ToList();
            var paraQuitar = actuales.Except(nuevas).ToList();
            var huboCambios = paraAgregar.Count > 0 || paraQuitar.Count > 0;

            foreach (var g in usuario.Gustos.Where(x => paraQuitar.Contains(x.Id)).ToList())
                usuario.Gustos.Remove(g);

            foreach (var g in gustosValidos.Where(x => paraAgregar.Contains(x.Id)))
                usuario.Gustos.Add(g);

            var conflictos = usuario.ValidarCompatibilidad();
            if (huboCambios || conflictos.Count > 0)
                await _userRepository.SaveChangesAsync(ct);

            if (modo == ModoPreferencias.Registro)
            {
                await _cache.SetAsync(
                    $"registro:{uid}:gustos",
                    usuario.Gustos.Select(g => g.Id).ToList(),
                    TimeSpan.FromHours(12)
                );
            }

            return conflictos;
        }
    }

}
