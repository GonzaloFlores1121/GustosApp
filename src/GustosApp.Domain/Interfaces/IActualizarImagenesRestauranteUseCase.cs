using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using GustosApp.Domain.Model.@enum;
using GustosApp.Domain.Common;

namespace GustosApp.Domain.Interfaces
{
    public interface IActualizarImagenesRestauranteUseCase
    {
        Task<string?> ActualizarImagenDestacadaAsync(Guid id, ArchivoEntrada? archivo, bool soloBorrar, CancellationToken ct);
        Task<string?> ActualizarLogoAsync(Guid id, ArchivoEntrada? archivo, bool soloBorrar, CancellationToken ct);
        Task<List<string>> ActualizarImagenesColeccionAsync(Guid id, TipoImagenRestaurante tipo, IList<ArchivoEntrada>? archivos, bool soloBorrar, CancellationToken ct);
    }
}
