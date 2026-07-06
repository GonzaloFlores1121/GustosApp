using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Application.Handlers;
using GustosApp.Application.Services;
using GustosApp.Application.UseCases.AmistadUseCases;
using GustosApp.Application.UseCases.GrupoUseCases.ChatGrupoUseCases;
using GustosApp.Application.UseCases.GrupoUseCases.InvitacionGrupoUseCases;
using GustosApp.Application.UseCases.GrupoUseCases;
using GustosApp.Application.UseCases.NotificacionUseCases;
using GustosApp.Application.UseCases.RestauranteUseCases.OpinionesRestaurantes;
using GustosApp.Application.UseCases.RestauranteUseCases.SolicitudRestauranteUseCases;
using GustosApp.Application.UseCases.RestauranteUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Application.UseCases.UsuarioUseCases.CondicionesMedicasUseCases;
using GustosApp.Application.UseCases.UsuarioUseCases.GustoUseCases;
using GustosApp.Application.UseCases.UsuarioUseCases.RestriccionesUseCases;
using GustosApp.Application.UseCases.UsuarioUseCases;
using GustosApp.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;
using GustosApp.Application.UseCases.VotacionUseCases;

namespace GustosApp.API.Extensiones
{
    public static class ApplicationServiceRegistration
    {
        public static IServiceCollection AgregarApplicationUseCases
            (this IServiceCollection services)
        {
            //Use cases y servicios de la capa Application 

           services.AddScoped<ObtenerUsuarioUseCase>();
            services.AddScoped<RegistrarUsuarioUseCase>();
            services.AddScoped<ObtenerCondicionesMedicasUseCase>();
            services.AddScoped<ObtenerGustosUseCase>();
            services.AddScoped<ObtenerRestriccionesUseCase>();
            services.AddScoped<CrearGrupoUseCase>();
            services.AddScoped<ConstruirPreferenciasGrupoUseCase>();
            services.AddScoped<ActualizarNombreGrupoUseCase>();
            services.AddScoped<InvitarUsuarioGrupoUseCase>();
            services.AddScoped<UnirseGrupoUseCase>();
            services.AddScoped<AbandonarGrupoUseCase>();
            services.AddScoped<ObtenerGruposUsuarioUseCase>();
            services.AddScoped<ObtenerInvitacionesUsuarioUseCase>();
            services.AddScoped<AceptarInvitacionGrupoUseCase>();
            services.AddScoped<GuardarCondicionesUseCase>();
            services.AddScoped<ObtenerGrupoDetalleUseCase>();
            services.AddScoped<GuardarGustosUseCase>();
            services.AddScoped<GuardarRestriccionesUseCase>();
            services.AddScoped<ObtenerGustosFiltradosUseCase>();
            services.AddScoped<ObtenerResumenRegistroUseCase>();
            services.AddScoped<FinalizarRegistroUseCase>();
            services.AddScoped<RemoverMiembroGrupoUseCase>();
            services.AddScoped<SugerirGustosSobreUnRadioUseCase>();
            services.AddScoped<CrearNotificacionUseCase>();
            services.AddScoped<ObtenerNotificacionesUsuarioUseCase>();
            services.AddScoped<ObtenerNotificacionUsuarioUseCase>();
            services.AddScoped<MarcarNotificacionLeidaUseCase>();
            services.AddScoped<ConstruirPreferenciasUsuarioIndividualUseCase>();
            services.AddScoped<ConstruirPreferenciasUsuarioConAmigoCase>();
            services.AddScoped<ActualizarValoracionRestauranteUseCase>();
            services.AddScoped<CrearSolicitudRestauranteUseCase>();
            services.AddScoped<AprobarSolicitudRestauranteUseCase>();
            services.AddScoped<ObtenerSolicitudRestaurantesPorIdUseCase>();
            services.AddScoped<ObtenerDatosRegistroRestauranteUseCase>();
            services.AddScoped<ObtenerSolicitudesPorTipoUseCase>();
            services.AddScoped<RechazarSolicitudRestauranteUseCase>();
            services.AddScoped<ActualizarValoracionRestauranteUseCase>();
            services.AddScoped<RecomendacionIAUseCase>();
            services.AddScoped<ActualizarPerfilUsuarioUseCase>();
            services.AddScoped<RechazarInvitacionAGrupoUseCase>();
            services.AddScoped<EnviarSolicitudAmistadUseCase>();
            services.AddScoped<ObtenerSolicitudesPendientesUseCase>();
            services.AddScoped<AceptarSolicitudUseCase>();
            services.AddScoped<RechazarSolicitudUseCase>();
            services.AddScoped<ObtenerAmigosUseCase>();
            services.AddScoped<EliminarAmigoUseCase>();
            services.AddScoped<EliminarGrupoUseCase>();
            services.AddScoped<ObtenerChatGrupoUseCase>();
            services.AddScoped<EnviarMensajeGrupoUseCase>();
            services.AddScoped<ActualizarGustosAGrupoUseCase>();
            services.AddScoped<ObtenerPreferenciasGruposUseCase>();
            services.AddScoped<EliminarGustosGrupoUseCase>();
            services.AddScoped<DesactivarMiembroDeGrupoUseCase>();
            services.AddScoped<IServicioPreferenciasGrupos, ServicioPreferenciasGrupos>();
            services.AddScoped<EliminarNotificacionUseCase>();
            services.AddScoped<ObtenerGustosPaginacionUseCase>();
            services.AddScoped<BuscarGustoPorCoincidenciaUseCase>();
            services.AddScoped<ObtenerGustosSeleccionadosPorUsuarioYParaFiltrarUseCase>();
            services.AddScoped<BuscarUsuariosUseCase>();
            services.AddScoped<ConfirmarAmistadEntreUsuarios>();
            services.AddScoped<VerificarSiMiembroEstaEnGrupoUseCase>();
            services.AddScoped<ObtenerRestaurantesAleatoriosGrupoUseCase>();
            services.AddScoped<ActivarMiembroDeGrupoUseCase>();
            services.AddScoped<EnviarRecomendacionesUsuariosActivosUseCase>();
            services.AddScoped<CrearOpinionRestauranteUseCase>();
            services.AddScoped<ObtenerValoracionUseCase>();
            services.AddScoped<BuscarRestaurantesUseCase>();
            services.AddScoped<AgregarUsuarioRestauranteFavoritoUseCase>();
            services.AddScoped<RegistrarTop3IndividualRestaurantesUseCase>();
            services.AddScoped<RegistrarTop3GrupoRestaurantesUseCase>();
            services.AddScoped<RegistrarVisitaPerfilRestauranteUseCase>();
            services.AddScoped<ObtenerMetricasRestauranteUseCase>();
            services.AddScoped<ActualizarRestauranteDashboardUseCase>();
            services.AddScoped<ObtenerRestaurantesFavoritosUseCase>();
            services.AddScoped<ObtenerRestauranteDetalleUseCase>();
            services.AddScoped<IniciarVotacionUseCase>();
            services.AddScoped<RegistrarVotoUseCase>();
            services.AddScoped<ObtenerResultadosVotacionUseCase>();
            services.AddScoped<CerrarVotacionUseCase>();
            services.AddScoped<SeleccionarGanadorRuletaUseCase>();
            services.AddScoped<IActualizarImagenesRestauranteUseCase, ActualizarImagenesRestauranteUseCase>();
            services.AddScoped<EliminarRestauranteUseCase>();
            services.AddScoped<ObtenerRestauranteIdPorPropietarioUseCase>();

            return services;
        }
    }
}
