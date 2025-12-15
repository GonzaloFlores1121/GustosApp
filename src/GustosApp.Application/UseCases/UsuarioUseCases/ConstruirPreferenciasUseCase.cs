using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Application.UseCases.AmistadUseCases;
using GustosApp.Application.UseCases.UsuarioUseCases.GustoUseCases;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;

namespace GustosApp.Application.UseCases.UsuarioUseCases
{
    public class ConstruirPreferenciasUsuarioIndividualUseCase 
    {
       // private readonly ObtenerUsuarioUseCase _obtenerUsuario;
      //  private readonly ObtenerGustosUseCase _obtenerGustosUser;
      //  private readonly ConfirmarAmistadEntreUsuarios _confirmarAmistad;
        private readonly IUsuarioRepository _usuarioRepo;
       

        public ConstruirPreferenciasUsuarioIndividualUseCase(
            //ObtenerUsuarioUseCase obtenerUsuario,
           // ObtenerGustosUseCase obtenerGustosUser,
            //ConfirmarAmistadEntreUsuarios confirmarAmistad,
            IUsuarioRepository usuarioRepo)
        {
           // _obtenerUsuario = obtenerUsuario;
            //_obtenerGustosUser = obtenerGustosUser;
           // _confirmarAmistad = confirmarAmistad;
            _usuarioRepo = usuarioRepo;
           
        }

        public async Task<UsuarioPreferencias> HandleAsync(
            string firebaseUid,
            List<string>? gustosDelFiltro,
            CancellationToken ct)
        {
            /*if (grupoId.HasValue)
            {
                var gustosGrupo = await _gustosGrupoRepo.ObtenerGustosDelGrupo(grupoId.Value);
                var obtenerPrefDeMiembrosValidos = _miembroGrupoRepository.obtenerMiembrosActivosConSusPreferenciasYCondiciones(grupoId.Value);
                return new UsuarioPreferencias { Gustos = gustosGrupo,
                    CondicionesMedicas= obtenerPrefDeMiembrosValidos.Result.CondicionesMedicas,
                    Restricciones= obtenerPrefDeMiembrosValidos.Result.Restricciones};
            }
            */

            var usuario = await _usuarioRepo.GetByFirebaseUidAsync(firebaseUid, ct);

            if (usuario == null)
                throw new UnauthorizedAccessException("Usuario no encontrado o no registrado.");

            // Usuario no selecciona filtro

            if(gustosDelFiltro == null || gustosDelFiltro.Count == 0)
            {
                return new UsuarioPreferencias
                {
                    Gustos = usuario.Gustos?.Select(g => g.Nombre).ToList() ?? new List<string>(),
                    Restricciones = usuario.Restricciones?.Select(r => r.Nombre).ToList() ?? new List<string>(),
                    CondicionesMedicas = usuario.CondicionesMedicas?.Select(c => c.Nombre).ToList() ?? new List<string>()
                };
            }
            //Selecciona filtro
            return new UsuarioPreferencias
            {
                //validar q los gustos del filtro  existan en db PROXIMAMENTE
                Gustos = gustosDelFiltro,
                Restricciones = usuario.Restricciones?.Select(r => r.Nombre).ToList() ?? new List<string>(),
                CondicionesMedicas = usuario.CondicionesMedicas?.Select(c => c.Nombre).ToList() ?? new List<string>()
            };
        }

            /*
            if (!string.IsNullOrWhiteSpace(amigoUsername))
            {
                //aca trae los gustos y restricciones 
                var amigo = await _usuarioRepo.GetByUsernameAsync(amigoUsername, ct);
                if (amigo != null)
                {
                    var usuarioActual = await _obtenerUsuario.HandleAsync(FirebaseUid: firebaseUid, ct: ct);

                    var amistad = await _confirmarAmistad
                        .HandleAsync(usuarioActual.Id , amigo.Id, ct);

                    if (amistad == null)
                        throw new UnauthorizedAccessException("No hay amistad entre los usuarios.");
                    {
                        var prefsAmigo = await _obtenerGustosUser
                            .HandleAsync(amigo.FirebaseUid, ct, null);

                        var gustosCombinados = prefsUser.Gustos
                            .Concat(prefsAmigo.Gustos)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        var restriccionesCombinadas = prefsUser.Restricciones
                            .Concat(prefsAmigo.Restricciones)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        var condicionesCombinadas = prefsUser.CondicionesMedicas
                            .Concat(prefsAmigo.CondicionesMedicas)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();


                        return new UsuarioPreferencias { Gustos = gustosCombinados,
                                                        Restricciones= restriccionesCombinadas,
                                                        CondicionesMedicas= condicionesCombinadas};
                    }
                }
            }
            */
           
        }
    }



