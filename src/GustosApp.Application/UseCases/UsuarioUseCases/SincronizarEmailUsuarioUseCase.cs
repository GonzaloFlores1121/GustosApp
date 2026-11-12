using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;

namespace GustosApp.Application.UseCases.UsuarioUseCases;

/// <summary>Reconcilia el correo persistido con la identidad confirmada por Firebase.</summary>
public sealed class SincronizarEmailUsuarioUseCase(
    IUsuarioRepository usuarios,
    IFirebaseAuthService firebase)
{
    public async Task<Usuario> HandleAsync(string firebaseUid, CancellationToken ct)
    {
        var usuario = await usuarios.GetByFirebaseUidAsync(firebaseUid, ct)
            ?? throw new UnauthorizedAccessException("Usuario autenticado no encontrado.");
        var identidad = await firebase.GetUserByUidAsync(firebaseUid);
        if (identidad?.Uid != firebaseUid || string.IsNullOrWhiteSpace(identidad.Email))
            throw new InvalidOperationException("No se pudo verificar el correo de la cuenta.");

        var emailConfirmado = identidad.Email.Trim();
        if (string.Equals(usuario.Email, emailConfirmado, StringComparison.OrdinalIgnoreCase))
            return usuario;

        var asociado = await usuarios.GetByEmailAsync(emailConfirmado, ct);
        if (asociado != null && asociado.Id != usuario.Id)
            throw new InvalidOperationException("El correo confirmado pertenece a otra cuenta.");

        var emailAnterior = usuario.Email;
        usuario.Email = emailConfirmado;
        try
        {
            await usuarios.SaveChangesAsync(ct);
        }
        catch
        {
            usuario.Email = emailAnterior;
            throw;
        }

        return usuario;
    }
}
