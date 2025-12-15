using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Domain.Common;

namespace GustosApp.Application.UseCases.GrupoUseCases
{
    public class ConstruirPreferenciasGrupoUseCase
    { 


        public async Task<UsuarioPreferencias> HandleAsync(string firebaseUID, Guid grupoId,
            CancellationToken ct)
        {
            // Grupo no selecciona filtro
           
            {
                return new UsuarioPreferencias
                {
                    Gustos = new List<string>(),
                    CondicionesMedicas = new List<string>(),
                    Restricciones = new List<string>()
                };
            }
            // Grupo selecciona filtro
          
        }
    }
}
