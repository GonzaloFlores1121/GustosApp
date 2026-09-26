using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Application.Interfaces;
using GustosApp.Application.Services;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;

namespace GustosApp.Application.UseCases.UsuarioUseCases.CondicionesMedicasUseCases
{
    public class GuardarCondicionesUseCase
    {
        private readonly IUsuarioRepository _user;
        private readonly ICondicionMedicaRepository _condiciones;
        private readonly ICacheService _cache;

        public GuardarCondicionesUseCase(
            IUsuarioRepository user,
            ICondicionMedicaRepository condiciones,
            ICacheService cache)
        {
            _user = user;
            _condiciones = condiciones;
            _cache = cache;
        }

        public async Task<List<string>> HandleAsync(
            string uid,
            List<Guid> ids,
            bool skip,
            ModoPreferencias modo,
            CancellationToken ct)
        {
            var usuario = await _user.GetByFirebaseUidAsync(uid, ct)
                ?? throw new InvalidOperationException("Usuario no encontrado.");

            var idsUnicos = skip ? new List<Guid>() : (ids ?? new List<Guid>()).Distinct().ToList();
            var entidadesValidas = idsUnicos.Count == 0
                ? new List<CondicionMedica>()
                : await _condiciones.GetByIdsAsync(idsUnicos, ct);
            if (entidadesValidas.Count != idsUnicos.Count)
                throw new ArgumentException("Una o más condiciones médicas no existen.");

            //
            // Determinar cambios
            //
            var actuales = usuario.CondicionesMedicas.Select(c => c.Id).ToHashSet();
            var nuevas = idsUnicos.ToHashSet();

            var paraAgregar = nuevas.Except(actuales).ToList();
            var paraQuitar = actuales.Except(nuevas).ToList();

            var huboCambios = paraAgregar.Count > 0 || paraQuitar.Count > 0;

            // Quitar condiciones
            foreach (var c in usuario.CondicionesMedicas.Where(x => paraQuitar.Contains(x.Id)).ToList())
                usuario.CondicionesMedicas.Remove(c);

            // Agregar condiciones
            foreach (var c in entidadesValidas.Where(x => paraAgregar.Contains(x.Id)))
                usuario.CondicionesMedicas.Add(c);

            var gustosRemovidos = usuario.ValidarCompatibilidad();
            if (huboCambios || gustosRemovidos.Count > 0)
                await _user.SaveChangesAsync(ct);

            //
            // 6Guardar en Redis SOLO si es registro
            //
            if (modo == ModoPreferencias.Registro)
            {
                await _cache.SetAsync(
                    $"registro:{uid}:condiciones",
                        usuario.CondicionesMedicas.Select(c => c.Id).ToList(),
                        TimeSpan.FromHours(12)
                    );
                await _cache.SetAsync(
                    $"registro:{uid}:gustos",
                    usuario.Gustos.Select(g => g.Id).ToList(),
                    TimeSpan.FromHours(12)
                );
            }

            return gustosRemovidos;
        }
    }

}

