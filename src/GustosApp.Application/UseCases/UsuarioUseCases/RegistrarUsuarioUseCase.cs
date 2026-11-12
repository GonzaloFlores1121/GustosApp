using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;

namespace GustosApp.Application.UseCases.UsuarioUseCases
{
    public sealed record ResultadoRegistroUsuario(Usuario Usuario, bool EsNuevo);

    public sealed class ConflictoRegistroUsuarioException(string codigo) : Exception(codigo)
    {
        public string Codigo { get; } = codigo;
    }

    public class RegistrarUsuarioUseCase
    {

        private readonly IUsuarioRepository _repo;

        public RegistrarUsuarioUseCase(IUsuarioRepository repo)
        {
            _repo = repo;
        }

        public async Task<Usuario> HandleAsync(string firebaseUid, Usuario request,
            CancellationToken ct = default)
            => (await HandleConEstadoAsync(firebaseUid, request, ct)).Usuario;

        public async Task<ResultadoRegistroUsuario> HandleConEstadoAsync(string firebaseUid, Usuario request,
            CancellationToken ct = default)
        {

            var existente = await _repo.GetByFirebaseUidAsync(firebaseUid, ct);

            if (existente != null)
            {
                return new ResultadoRegistroUsuario(existente, false);
            }

            var otroConEmail = await _repo.GetByEmailAsync(request.Email, ct);
            if (otroConEmail != null)
                throw new ConflictoRegistroUsuarioException("EMAIL_TAKEN");
            var otroConUsername = await _repo.GetByUsernameAsync(request.IdUsuario, ct);
            if (otroConUsername != null)
                throw new ConflictoRegistroUsuarioException("USERNAME_TAKEN");

            // Las fotos sólo se incorporan mediante una subida autenticada y validada.
            var usuario = new Usuario(firebaseUid, request.Email, request.Nombre, request.Apellido, request.IdUsuario, null);


            await _repo.AddAsync(usuario, ct);
            await _repo.SaveChangesAsync(ct);


            return new ResultadoRegistroUsuario(usuario, true);
        }

    }
}
