using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.API.Hubs.Services;
using GustosApp.API.Middleware;
using GustosApp.API.Templates.Email;
using GustosApp.Application.Interfaces;
using GustosApp.Application.Services;
using GustosApp.Application.UseCases.GrupoUseCases.ChatGrupoUseCases;
using GustosApp.Application.UseCases.RestauranteUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Infraestructure.Parsing;
using GustosApp.Infraestructure.Repositories;
using GustosApp.Infraestructure.Services;
using GustosApp.Infraestructure.Extrerno.GooglePlacesModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GustosApp.API.Extensiones
{
    public static class InfrastructureServiceRegistration
    {

        public static IServiceCollection AgregarInfraestructura(this IServiceCollection services,IConfiguration config)
        {
            // =====================
            //   Repositorios
            // =====================
            services.AddScoped<IHttpDownloader, HttpDownloader>();

            //reVISAR PROXIMAMENTE ESTE USECASE 
            services.AddScoped<IRecomendadorRestaurantes, SugerirGustosSobreUnRadioUseCase>();
            //reVISAR PROXIMAMENTE ESTE USECASE 
            services.AddScoped<IAuthorizationHandler, RegistroIncompletoHandler>();
            services.AddScoped<ICacheService, RedisCacheService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IUsuarioRepository, UsuarioRepositoryEF>();
            services.AddScoped<IRestriccionRepository, RestriccionRepositoryEF>();
            services.AddScoped<ICondicionMedicaRepository, CondicionMedicaRepositoryEF>();
            services.AddScoped<IGustoRepository, GustoRepositoryEF>();
            services.AddScoped<IGrupoRepository, GrupoRepositoryEF>();
            services.AddScoped<IMiembroGrupoRepository, MiembroGrupoRepositoryEF>();
            services.AddScoped<IInvitacionGrupoRepository, InvitacionGrupoRepositoryEF>();
            services.AddScoped<IGustosGrupoRepository, GustosGrupoRepositoryEF>();
            services.AddScoped<INotificacionRepository, NotificacionRepositoryEF>();
            services.AddScoped<INotificacionesVotacionService, NotificacionesVotacionService>();
            services.AddScoped<INotificacionRealtimeService, SignalRNotificacionRealtimeService>();
            services.AddScoped<IChatRealTimeService, SignalRChatRealtimeService>();
            services.AddScoped<ISolicitudAmistadRealtimeService, SignalRSolicitudAmistadRealtimeService>();

            //reVISAR PROXIMAMENTE ESTE USECASE 
            services.AddScoped<IEnviarMensajeGrupoUseCase, EnviarMensajeGrupoUseCase>();
            //reVISAR PROXIMAMENTE ESTE USECASE 

            services.AddScoped<IUsuariosActivosService, UsuariosActivosService>();
            services.AddScoped<IOpinionRestauranteRepository, OpinionRestauranteRepositoryEF>();
            services.AddScoped<IRestauranteEstadisticasRepository, RestauranteEstadisticasRepositoryEF>();
            services.AddScoped<IRestauranteRepository, RestauranteRepositoryEF>();
            services.AddScoped<IUsuarioRestauranteFavoritoRepository, UsuarioRestauranteFavoritoEF>();
            services.AddScoped<ISolicitudRestauranteRepository, SolicitudRestauranteRepositoryEF>();
            services.AddScoped<IRestauranteMenuRepository, RestauranteMenuRepositoryEF>();
            services.AddScoped<IFirebaseAuthService, FirebaseAuthService>();
            services.AddScoped<IEmailTemplateService, EmailTemplateService>();
            services.AddScoped<ISolicitudAmistadRepository, SolicitudAmistadRepositoryEF>();
            services.AddScoped<IVotacionRepository, VotacionRepository>();
            services.AddScoped<IChatRepository, ChatRepositoryEF>();
            services.AddScoped<IRestauranteRepository, RestauranteRepositoryEF>();
            services.AddScoped<IChatRepository, ChatRepositoryEF>();
            services.AddScoped<IMenuParser, SimpleMenuParser>();
            services.AddSingleton<IUserIdProvider, FirebaseUserIdProvider>();
            services.AddScoped<IServicioRestaurantes, ServicioRestaurantes>();
            services.AddScoped<ISolicitudAmistadRepository, SolicitudAmistadRepositoryEF>();
            services.AddScoped<IPagoService, MercadoPagoService>();
            services.AddScoped<IBuscarRestaurantesRecomendadosOrquestador, BuscarRestaurantesRecomendadosOrquestador>();
            services.AddScoped<IUsuarioPreferenciasService, UsuarioPreferenciasService>();
            services.AddHttpClient<IBuscadorRestaurantesExternos, BuscadorRestaurantesGooglePlaces>();



            return services;
        }
    }
}
