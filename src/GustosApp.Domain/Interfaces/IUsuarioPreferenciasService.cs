using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Domain.Common;

namespace GustosApp.Domain.Interfaces
{
    public interface IUsuarioPreferenciasService
    {
        Task<UsuarioPreferencias> ObtenerPreferencias(string firebaseUid, List<string>? gustosFiltro, CancellationToken ct);

    }
}
