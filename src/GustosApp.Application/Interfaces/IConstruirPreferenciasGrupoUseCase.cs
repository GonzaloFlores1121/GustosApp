using GustosApp.Domain.Common;

namespace GustosApp.Application.Interfaces;

public interface IConstruirPreferenciasGrupoUseCase
{
    Task<UsuarioPreferencias> HandleAsync(
        string firebaseUid,
        Guid grupoId,
        CancellationToken cancellationToken = default);
}
