using GustosApp.Application.Common.Exceptions;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Application.Interfaces;

namespace GustosApp.Application.UseCases
{
    public class CrearGrupoUseCase
    {
        private readonly IGrupoRepository _grupoRepository;
        private readonly IMiembroGrupoRepository _miembroGrupoRepository;
        private readonly IUsuarioRepository _usuarioRepository;
        private IGustosGrupoRepository _gustosGrupoRepository;
        private readonly IApplicationTransaction _transaction;

        public CrearGrupoUseCase(IGrupoRepository grupoRepository,
            IMiembroGrupoRepository miembroGrupoRepository,
            IUsuarioRepository usuarioRepository,
            IGustosGrupoRepository gustosGrupoRepository,
            IApplicationTransaction? transaction = null)
        {
            _grupoRepository = grupoRepository;
            _miembroGrupoRepository = miembroGrupoRepository;
            _usuarioRepository = usuarioRepository;
            _gustosGrupoRepository = gustosGrupoRepository;
            _transaction = transaction ?? ImmediateApplicationTransaction.Instance;
        }

        public async Task<Grupo> HandleAsync(string firebaseUid, string NombreGrupo, string? DescripcionGrupo, CancellationToken cancellationToken = default)
        {
            // Obtener el usuario por Firebase UID  
            var usuario = await _usuarioRepository.GetByFirebaseUidAsync(firebaseUid, cancellationToken);
            if (usuario == null)
                throw new UnauthorizedAccessException("Usuario no encontrado");

            var grupo = await _transaction.ExecuteAsync(async transactionCt =>
            {
                await ValidarCupoAsync(usuario, transactionCt);
                var nuevoGrupo = new Grupo(NombreGrupo, usuario.Id, DescripcionGrupo);
                await _grupoRepository.CreateAsync(nuevoGrupo, transactionCt);
                var miembro = new MiembroGrupo(nuevoGrupo.Id, usuario.Id, true);
                await _miembroGrupoRepository.CreateAsync(miembro, transactionCt);
                await _gustosGrupoRepository.AgregarGustosAlGrupo(nuevoGrupo.Id, usuario.Gustos.ToList(), miembro.Id);
                return nuevoGrupo;
            }, cancellationToken);

            // Obtener el grupo completo con relaciones  
            var grupoCompleto = await _grupoRepository.GetByIdAsync(grupo.Id, cancellationToken);
            if (grupoCompleto == null)
                throw new InvalidOperationException("Error al crear el grupo");

            return grupoCompleto;
        }

        private async Task ValidarCupoAsync(Usuario usuario, CancellationToken ct)
        {
            if (usuario.EsPremium()) return;
            var cantidad = (await _grupoRepository.GetGruposByUsuarioIdAsync(usuario.Id, ct)).Count();
            if (cantidad >= 3) throw new LimiteGruposAlcanzadoException("Free", 3, cantidad);
        }
    }
}
