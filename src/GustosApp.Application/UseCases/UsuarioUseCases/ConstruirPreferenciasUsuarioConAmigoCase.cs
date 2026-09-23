using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Application.UseCases.AmistadUseCases;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;

namespace GustosApp.Application.UseCases.UsuarioUseCases
{
    public class ConstruirPreferenciasUsuarioConAmigoCase
    {
        private readonly IUsuarioRepository _usuarioRepo;
        private readonly ISolicitudAmistadRepository _solicitudAmistadRepository;
        private readonly IUsuarioPreferenciasService _preferenciasService;


        public ConstruirPreferenciasUsuarioConAmigoCase(IUsuarioRepository usuarioRepo,
            ISolicitudAmistadRepository solicitudAmistadRepository, IUsuarioPreferenciasService preferenciasService)
        {
            _usuarioRepo = usuarioRepo;
            _solicitudAmistadRepository = solicitudAmistadRepository;
            _preferenciasService = preferenciasService;

        }


        public async Task<UsuarioPreferencias> HandleAsync(
       string firebaseUid,
       string amigoUsername,
       List<string>? gustos,
       CancellationToken ct)
        {
            var amigo = await _usuarioRepo.GetByUsernameAsync(amigoUsername, ct)
                ?? throw new KeyNotFoundException("El amigo no existe.");

            var usuarioActual = await _usuarioRepo.GetByFirebaseUidAsync(firebaseUid, ct)
                ?? throw new UnauthorizedAccessException("Usuario no encontrado.");

            var amistad = await _solicitudAmistadRepository
                .GetAmistadEntreUsuariosAsync(usuarioActual.Id, amigo.Id, ct);

            if (amistad == null)
                throw new UnauthorizedAccessException("No hay amistad entre los usuarios.");

         
            var prefsUser = await _preferenciasService.ObtenerPreferencias(firebaseUid, gustos, ct);
            var prefsAmigo = await _preferenciasService.ObtenerPreferencias(amigo.FirebaseUid, null, ct);

            return new UsuarioPreferencias
            {
                Gustos = prefsUser.Gustos
                    .Concat(prefsAmigo.Gustos)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                Restricciones = prefsUser.Restricciones
                    .Concat(prefsAmigo.Restricciones)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                CondicionesMedicas = prefsUser.CondicionesMedicas
                    .Concat(prefsAmigo.CondicionesMedicas)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }
    }
    }
