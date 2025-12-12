using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;

namespace GustosApp.Application.UseCases.UsuarioUseCases
{
    public class ConstruirPreferenciasUsuarioConAmigoCase
    {
        private readonly IUsuarioRepository _usuarioRepo;
        private readonly IGustosGrupoRepository _gustosGrupoRepo;
   //      private readonly ConfirmarAmistadEntreUsuarios _confirmarAmistad;

        public ConstruirPreferenciasUsuarioConAmigoCase(IUsuarioRepository usuarioRepo,
            IGustosGrupoRepository gustosGrupoRepo)
        {
            _gustosGrupoRepo= gustosGrupoRepo;
            _usuarioRepo = usuarioRepo;

        }

       /* public async Task<UsuarioPreferencias> HandleAsync()
        {

        }
*/    }
}
