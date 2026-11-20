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
        Task<List<Model.RestauranteImagen>> AgregarImagenesAsync(Guid id, Guid usuarioId, TipoImagenRestaurante tipo, IList<ArchivoEntrada> archivos, CancellationToken ct);
        Task<List<Model.RestauranteImagen>> ModificarImagenAsync(Guid id, Guid usuarioId, TipoImagenRestaurante tipo, Guid imagenId, ArchivoEntrada? archivo, CancellationToken ct);
        Task<string?> ActualizarImagenDestacadaAsync(Guid id, Guid usuarioId, ArchivoEntrada? archivo, bool soloBorrar, CancellationToken ct);
        Task<string?> ActualizarLogoAsync(Guid id, Guid usuarioId, ArchivoEntrada? archivo, bool soloBorrar, CancellationToken ct);
        Task<List<string>> ActualizarImagenesColeccionAsync(Guid id, Guid usuarioId, TipoImagenRestaurante tipo, IList<ArchivoEntrada>? archivos, bool soloBorrar, CancellationToken ct);
    }
}
