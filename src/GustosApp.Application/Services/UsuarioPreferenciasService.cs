using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;

namespace GustosApp.Application.Services
{
    public class UsuarioPreferenciasService : IUsuarioPreferenciasService
    {
        private readonly IUsuarioRepository _usuarioRepo;

        public UsuarioPreferenciasService(IUsuarioRepository usuarioRepo)
        {
            _usuarioRepo = usuarioRepo;
        }

        public async Task<UsuarioPreferencias> ObtenerPreferencias(string firebaseUid, List<string>? gustosFiltro, CancellationToken ct)
        {
            var usuario = await _usuarioRepo.GetByFirebaseUidAsync(firebaseUid, ct)
                ?? throw new UnauthorizedAccessException("Usuario no encontrado.");

            var gustos = gustosFiltro?.Any() == true
                ? gustosFiltro
                : usuario.Gustos.Select(g => g.Nombre).ToList();

            return new UsuarioPreferencias
            {
                Gustos = gustos,
                Restricciones = usuario.Restricciones.Select(r => r.Nombre).ToList(),
                CondicionesMedicas = usuario.CondicionesMedicas.Select(c => c.Nombre).ToList()
            };
        }
    }

}
