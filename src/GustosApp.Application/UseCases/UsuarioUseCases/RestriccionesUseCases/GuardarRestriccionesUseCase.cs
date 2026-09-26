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

namespace GustosApp.Application.UseCases.UsuarioUseCases.RestriccionesUseCases
{
    public class GuardarRestriccionesUseCase
    {
        private readonly IUsuarioRepository _user;
        private readonly IRestriccionRepository _restricciones;
        private readonly ICacheService _cache;

        public GuardarRestriccionesUseCase(
            IUsuarioRepository user,
            IRestriccionRepository restricciones,
            ICacheService cache)
        {
            _user = user;
            _restricciones = restricciones;
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
                ? new List<Restriccion>()
                : await _restricciones.GetRestriccionesByIdsAsync(idsUnicos, ct);
            if (entidadesValidas.Count != idsUnicos.Count)
                throw new ArgumentException("Una o más restricciones no existen.");

            var actuales = usuario.Restricciones.Select(r => r.Id).ToHashSet();
            var nuevas = idsUnicos.ToHashSet();

            var paraAgregar = nuevas.Except(actuales).ToList();
            var paraQuitar = actuales.Except(nuevas).ToList();

            var huboCambios = paraAgregar.Count > 0 || paraQuitar.Count > 0;
            foreach (var r in usuario.Restricciones.Where(x => paraQuitar.Contains(x.Id)).ToList())
                usuario.Restricciones.Remove(r);

            foreach (var r in entidadesValidas.Where(x => paraAgregar.Contains(x.Id)))
                usuario.Restricciones.Add(r);

            var gustosRemovidos = usuario.ValidarCompatibilidad();
            if (huboCambios || gustosRemovidos.Count > 0)
                await _user.SaveChangesAsync(ct);

            //
            //  Guardar en Redis solo si es registro
            //
            if (modo == ModoPreferencias.Registro)
            {
                await _cache.SetAsync(
                    $"registro:{uid}:restricciones",
                        usuario.Restricciones.Select(r => r.Id).ToList(),
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
