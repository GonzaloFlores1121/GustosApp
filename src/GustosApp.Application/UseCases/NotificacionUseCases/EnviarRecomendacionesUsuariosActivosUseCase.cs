using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.RestauranteUseCases;
using GustosApp.Application.UseCases.UsuarioUseCases;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;

namespace GustosApp.Application.UseCases.NotificacionUseCases
{
    public class EnviarRecomendacionesUsuariosActivosUseCase 
    {
        private readonly IUsuariosActivosService _usuariosActivos;
        private readonly ICacheService _cache;
        private readonly IBuscarRestaurantesRecomendadosOrquestador _orquestador;   
        private readonly IEmailService _email;
        private readonly IUsuarioRepository _usuariosRepo;
        

        public EnviarRecomendacionesUsuariosActivosUseCase(
            IUsuariosActivosService usuariosActivos,
            ICacheService cache,
            IBuscarRestaurantesRecomendadosOrquestador orquestador,
            IEmailService email,
            IUsuarioRepository usuariosRepo)
        {
            _usuariosActivos = usuariosActivos;
            _cache = cache;
            _orquestador = orquestador;
            _email = email;
            _usuariosRepo = usuariosRepo;
        }


        //validar q sea moderador usuario rol
        public async Task<bool> HandleAsync(string firebaseUid, CancellationToken ct)
        {
            var activos = _usuariosActivos.GetUsuariosActivos();
            bool mandoNotif = false;

            foreach (var uid in activos)
            {
                var ubicacion = await _cache.GetAsync<UserLocation>($"usuario:{uid}:location");
                if (ubicacion == null)
                    continue;

                var usuario = await _usuariosRepo.GetByFirebaseUidAsync(uid, ct);
                if (usuario == null)
                    continue;

               var recomendaciones= await _orquestador.HandleAsync(
                    uid,
                    null,
                    null,
                    ubicacion.Lat,
                    ubicacion.Lng,
                    ubicacion.Radio,
                    1,
                    4.0,
                    ct
                );


                await _email.EnviarEmailAsync(
                    usuario.Email,
                    "Recomendación personalizada",
                    $"Según tus gustos y ubicación, te recomendamos: {recomendaciones[0].Nombre}"
                );

                mandoNotif = true;
            }

            return mandoNotif;
        }
    }

    }
