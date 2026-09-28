using GustosApp.Application.Common.Exceptions;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Application.Interfaces;

namespace GustosApp.Application.UseCases.GrupoUseCases
{
    public class UnirseGrupoUseCase
    {
        private readonly IGrupoRepository _grupoRepository;
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IMiembroGrupoRepository _miembroGrupoRepository;
        private IGustosGrupoRepository _gustosGrupoRepository;
        private readonly IApplicationTransaction _transaction;


        public UnirseGrupoUseCase(IGrupoRepository grupoRepository,
            IUsuarioRepository usuarioRepository,
            IMiembroGrupoRepository miembroGrupoRepository,
            IGustosGrupoRepository gustosGrupoRepository,
            IApplicationTransaction? transaction = null)
        {
            _grupoRepository = grupoRepository;
            _usuarioRepository = usuarioRepository;
            _miembroGrupoRepository = miembroGrupoRepository;
            _gustosGrupoRepository = gustosGrupoRepository;
            _transaction = transaction ?? ImmediateApplicationTransaction.Instance;
        }

        public async Task<Grupo> HandleAsync(string firebaseUid, string codigoInvitacion, CancellationToken cancellationToken = default)
        {
            // Obtener el usuario
            var usuario = await _usuarioRepository.GetByFirebaseUidAsync(firebaseUid, cancellationToken);
            if (usuario == null)
                throw new UnauthorizedAccessException("Usuario no encontrado");

            // Buscar el grupo por código de invitación
            var grupo = await _grupoRepository.GetByCodigoInvitacionAsync(codigoInvitacion, cancellationToken);
            if (grupo == null)
                throw new ArgumentException("Código de invitación inválido");

            // Verificar que el código no ha expirado
            if (!grupo.EsCodigoInvitacionValido(codigoInvitacion))
                throw new ArgumentException("El código de invitación ha expirado");

            // Verificar que el usuario no es ya miembro del grupo
            if (await _miembroGrupoRepository.UsuarioEsMiembroActivoAsync(grupo.Id, usuario.Id, cancellationToken))
                throw new ArgumentException("Ya eres miembro de este grupo");

            await _transaction.ExecuteAsync(async transactionCt =>
            {
                if (!usuario.EsPremium())
                {
                    var cantidad = (await _grupoRepository.GetGruposByUsuarioIdAsync(usuario.Id, transactionCt)).Count();
                    if (cantidad >= 3) throw new LimiteGruposAlcanzadoException("Free", 3, cantidad,
                        "Has alcanzado el límite de 3 grupos para usuarios gratuitos. Mejora a Premium para unirte a más grupos.");
                }
                var miembro = new MiembroGrupo(grupo.Id, usuario.Id, false);
                await _miembroGrupoRepository.CreateAsync(miembro, transactionCt);
                await _gustosGrupoRepository.AgregarGustosAlGrupo(grupo.Id, usuario.Gustos.ToList(), miembro.Id);
                return true;
            }, cancellationToken);

            // Obtener el grupo completo con relaciones
            var grupoCompleto = await _grupoRepository.GetByIdAsync(grupo.Id, cancellationToken);
            if (grupoCompleto == null)
                throw new InvalidOperationException("Error al unirse al grupo");

            return grupoCompleto;
        }
    }
}
